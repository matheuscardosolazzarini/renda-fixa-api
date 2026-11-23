locals {
  image_repository = "ghcr.io/matheuscardosolazzarini/renda-fixa-api"

  # Mesmo estilo Npgsql do appsettings e do docker-compose. Sem "SSL Mode": o RDS 16 exige TLS e o
  # padrão do Npgsql (Prefer) negocia. A senha não tem pontuação (ver o recurso efêmero do raiz),
  # então ; e = não quebram a string. Local derivado de valor efêmero é efêmero.
  connection_string = join(";", [
    "Host=${var.db_address}",
    "Port=${var.db_port}",
    "Database=${var.db_name}",
    "Username=${var.db_username}",
    "Password=${var.db_password}",
  ])

  # Os dois containers leem a connection string do secret, pela execution role.
  secrets = [
    {
      name      = "ConnectionStrings__DefaultConnection"
      valueFrom = aws_secretsmanager_secret.connection_string.arn
    }
  ]
}

data "aws_region" "current" {}

# ---- Logs ----

resource "aws_cloudwatch_log_group" "this" {
  name              = "/ecs/${var.name}"
  retention_in_days = 3 # menor retenção útil: nada de log fica depois da demo
}

# ---- Secret da connection string (D6, opção B) ----

resource "aws_secretsmanager_secret" "connection_string" {
  name                    = "renda-fixa-api/connection-string"
  description             = "Connection string do PostgreSQL da API"
  recovery_window_in_days = 0 # sem janela de recuperação: o nome volta a ficar livre no destroy
}

# Escrita única: a connection string não entra no state nem no plano. A versão fixa em 1 faz os
# applies seguintes (prorrogar o TTL) não reescreverem o secret, igual a password_wo_version no RDS.
resource "aws_secretsmanager_secret_version" "connection_string" {
  secret_id                = aws_secretsmanager_secret.connection_string.id
  secret_string_wo         = local.connection_string
  secret_string_wo_version = 1
}

# ---- Cluster e task definitions ----

resource "aws_ecs_cluster" "this" {
  name = var.name

  setting {
    name  = "containerInsights"
    value = "disabled"
  }

  # D4: a expiração fica só no cluster. Em default_tags, prorrogar re-etiquetaria todos os recursos.
  tags = {
    expires_at = var.expires_at
  }
}

resource "aws_ecs_task_definition" "api" {
  family                   = "renda-fixa-api" # nome fixo da spec, não derivado de var.name
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = var.execution_role_arn # sem task_role_arn: a API não chama a AWS

  runtime_platform {
    cpu_architecture        = "X86_64"
    operating_system_family = "LINUX"
  }

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = "${local.image_repository}:${var.image_sha}"
      essential = true
      portMappings = [
        {
          containerPort = 8080
          protocol      = "tcp"
        }
      ]
      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        }
      ]
      secrets = local.secrets
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.this.name
          "awslogs-region"        = data.aws_region.current.region
          "awslogs-stream-prefix" = "api"
        }
      }
    }
  ])
}

# Tarefa avulsa (run-task) que aplica as migrations antes de a API subir (D3). Fica no Terraform
# para ser destruída junto com o ambiente.
resource "aws_ecs_task_definition" "migrator" {
  family                   = "renda-fixa-migrator" # o job limpar do P6 filtra por esta família
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = var.execution_role_arn

  runtime_platform {
    cpu_architecture        = "X86_64"
    operating_system_family = "LINUX"
  }

  container_definitions = jsonencode([
    {
      name      = "migrator"
      image     = "${local.image_repository}:${var.image_sha}-migrator"
      essential = true
      secrets   = local.secrets
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.this.name
          "awslogs-region"        = data.aws_region.current.region
          "awslogs-stream-prefix" = "migrator"
        }
      }
    }
  ])
}

# ---- ALB ----

resource "aws_lb" "this" {
  name               = var.name
  load_balancer_type = "application"
  internal           = false
  subnets            = var.subnet_ids
  security_groups    = [var.alb_security_group_id]

  enable_deletion_protection = false

  # Descarta cabeçalhos HTTP inválidos antes de chegarem à API (Trivy AWS-0052, Checkov CKV_AWS_131). Sem custo.
  drop_invalid_header_fields = true
}

resource "aws_lb_target_group" "this" {
  name        = var.name
  target_type = "ip"
  protocol    = "HTTP"
  port        = 8080
  vpc_id      = var.vpc_id

  # O padrão de 300 s alongaria o destroy.
  deregistration_delay = 10

  health_check {
    path                = "/health"
    matcher             = "200"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.this.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.this.arn
  }
}

# ---- Serviço ----

resource "aws_ecs_service" "this" {
  name            = var.name
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  launch_type     = "FARGATE"
  desired_count   = var.desired_count
  propagate_tags  = "SERVICE"

  # A API não consulta o banco no /health, mas a partida em 0,25 vCPU inclui o pull da imagem do
  # GHCR sem NAT, a partida do .NET e a importação do histórico do BC (BackgroundService), que
  # disputa CPU com as primeiras respostas do health check. Nesse prazo uma resposta lenta não
  # derruba a tarefa; passado o prazo, o ALB e o circuit breaker voltam a valer.
  health_check_grace_period_seconds = 120

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  network_configuration {
    subnets          = var.subnet_ids
    security_groups  = [var.task_security_group_id]
    assign_public_ip = true # único caminho até o GHCR sem NAT; o SG não aceita nada da internet
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.this.arn
    container_name   = "api"
    container_port   = 8080
  }

  # O listener precisa existir antes de o serviço registrar o alvo; o secret, antes de a tarefa
  # tentar lê-lo.
  depends_on = [
    aws_lb_listener.http,
    aws_secretsmanager_secret_version.connection_string,
  ]
}
