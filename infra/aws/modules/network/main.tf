# Só rede pública: sem NAT, sem subnet privada com saída, sem VPC endpoint (spec F11, "Rede").
# As tarefas usam assign_public_ip = true para puxar a imagem do GHCR; os SGs não aceitam nada
# da internet além do 80 do ALB, então o IP público da tarefa serve só para saída.

data "aws_availability_zones" "available" {
  state = "available"

  # Fora zonas locais e de borda, que não aceitam ALB nem RDS.
  filter {
    name   = "opt-in-status"
    values = ["opt-in-not-required"]
  }
}

resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name = var.name
  }
}

resource "aws_subnet" "public" {
  count = 2

  vpc_id                  = aws_vpc.this.id
  cidr_block              = cidrsubnet(var.vpc_cidr, 8, count.index)
  availability_zone       = data.aws_availability_zones.available.names[count.index]
  map_public_ip_on_launch = false

  tags = {
    Name = "${var.name}-public-${count.index}"
  }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = {
    Name = var.name
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.this.id
  }

  tags = {
    Name = "${var.name}-public"
  }
}

resource "aws_route_table_association" "public" {
  count = 2

  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

# Cadeia de security groups: internet -> ALB (80) -> tarefa (8080) -> RDS (5432).
# Os SGs do ALB e da tarefa nascem aqui para a cadeia inteira ficar num lugar só; o módulo app
# (P3) os consome. A tarefa de migração usa o mesmo SG da tarefa.
resource "aws_security_group" "alb" {
  name        = "${var.name}-alb"
  description = "ALB da API: HTTP da internet"
  vpc_id      = aws_vpc.this.id

  tags = {
    Name = "${var.name}-alb"
  }
}

resource "aws_security_group" "task" {
  name        = "${var.name}-task"
  description = "Tarefas da API e da migracao: 8080 so do ALB"
  vpc_id      = aws_vpc.this.id

  tags = {
    Name = "${var.name}-task"
  }
}

resource "aws_security_group" "rds" {
  name        = "${var.name}-rds"
  description = "RDS PostgreSQL: 5432 so da tarefa"
  vpc_id      = aws_vpc.this.id

  tags = {
    Name = "${var.name}-rds"
  }
}

resource "aws_vpc_security_group_ingress_rule" "alb_http" {
  security_group_id = aws_security_group.alb.id
  description       = "HTTP de qualquer origem"
  ip_protocol       = "tcp"
  from_port         = 80
  to_port           = 80
  cidr_ipv4         = "0.0.0.0/0"
}

resource "aws_vpc_security_group_ingress_rule" "task_http" {
  security_group_id            = aws_security_group.task.id
  description                  = "API, so do ALB"
  ip_protocol                  = "tcp"
  from_port                    = 8080
  to_port                      = 8080
  referenced_security_group_id = aws_security_group.alb.id
}

resource "aws_vpc_security_group_ingress_rule" "rds_postgres" {
  security_group_id            = aws_security_group.rds.id
  description                  = "PostgreSQL, so da tarefa"
  ip_protocol                  = "tcp"
  from_port                    = 5432
  to_port                      = 5432
  referenced_security_group_id = aws_security_group.task.id
}

# O provider remove a regra de saída padrão do SG, então toda saída é declarada.
# ALB: só até a tarefa, na porta da API (tráfego e health check).
resource "aws_vpc_security_group_egress_rule" "alb_to_task" {
  security_group_id            = aws_security_group.alb.id
  description                  = "ALB para a tarefa"
  ip_protocol                  = "tcp"
  from_port                    = 8080
  to_port                      = 8080
  referenced_security_group_id = aws_security_group.task.id
}

# Tarefa (API e migração, mesmo SG), sem NAT: só HTTPS para a internet e o PostgreSQL para o SG do RDS.
# O 443 cobre GHCR (pull da imagem, feito pela ENI da tarefa no Fargate 1.4), Secrets Manager, CloudWatch Logs,
# ECS e a API do SGS do Banco Central (https://api.bcb.gov.br/); o IP público da tarefa serve só para isso.
# DNS não precisa de regra: "Security groups cannot block DNS requests to or from the Route 53 Resolver,
# sometimes referred to as the 'VPC+2 IP address'" (guia da VPC, "Security group rules").
resource "aws_vpc_security_group_egress_rule" "task_https" {
  security_group_id = aws_security_group.task.id
  description       = "HTTPS da tarefa (GHCR, Secrets Manager, Logs, ECS, API do BC)"
  ip_protocol       = "tcp"
  from_port         = 443
  to_port           = 443
  cidr_ipv4         = "0.0.0.0/0"
}

resource "aws_vpc_security_group_egress_rule" "task_postgres" {
  security_group_id            = aws_security_group.task.id
  description                  = "PostgreSQL, so para o RDS"
  ip_protocol                  = "tcp"
  from_port                    = 5432
  to_port                      = 5432
  referenced_security_group_id = aws_security_group.rds.id
}

# O SG padrão da VPC nasce com entrada e saída livres entre os seus membros. Adotá-lo sem nenhuma regra as remove
# (o Terraform revoga as regras padrão ao adotar). Nada usa este SG. No destroy ele só sai do state: a VPC o leva.
resource "aws_default_security_group" "this" {
  vpc_id = aws_vpc.this.id

  tags = {
    Name = "${var.name}-default-sem-regras"
  }
}
