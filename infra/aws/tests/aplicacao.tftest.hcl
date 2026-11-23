# Módulo app (P3): atributos do plano e do apply contra o provider mockado.
# O mock_provider não chama a AWS: prova a configuração, não o comportamento da nuvem.
# Os runs usam o módulo diretamente (bloco module) porque asserts de run não enxergam recursos
# de módulos filhos. Os que leem container_definitions rodam com apply: o ARN do secret só existe
# depois dele e, desconhecido, deixaria o JSON inteiro desconhecido.

mock_provider "aws" {
  # O provider valida o formato dos ARNs que o listener recebe; o mock geraria strings aleatórias.
  mock_resource "aws_lb" {
    defaults = {
      arn = "arn:aws:elasticloadbalancing:us-east-1:123456789012:loadbalancer/app/teste/0123456789abcdef"
    }
  }

  mock_resource "aws_lb_target_group" {
    defaults = {
      arn = "arn:aws:elasticloadbalancing:us-east-1:123456789012:targetgroup/teste/0123456789abcdef"
    }
  }
}

variables {
  name                   = "teste"
  vpc_id                 = "vpc-teste"
  subnet_ids             = ["subnet-aaa", "subnet-bbb"]
  alb_security_group_id  = "sg-alb"
  task_security_group_id = "sg-task"
  execution_role_arn     = "arn:aws:iam::123456789012:role/renda-fixa-api-execution"
  image_sha              = "0123456789abcdef0123456789abcdef01234567"
  desired_count          = 0
  expires_at             = "2026-11-19T18:00:00Z"
  db_address             = "banco.exemplo.us-east-1.rds.amazonaws.com"
  db_port                = 5432
  db_name                = "fixedincome"
  db_username            = "fixedincome"
  db_password            = "senha-de-teste-que-nunca-vai-para-o-state"
}

# ---- Imagens e SHA ----

run "api_e_migracao_usam_o_mesmo_sha_com_os_sufixos_certos" {
  command = apply

  module {
    source = "./modules/app"
  }

  assert {
    condition = (
      jsondecode(aws_ecs_task_definition.api.container_definitions)[0].image ==
      "ghcr.io/matheuscardosolazzarini/renda-fixa-api:0123456789abcdef0123456789abcdef01234567"
    )
    error_message = "a imagem da API deve terminar em :<sha>"
  }

  assert {
    condition = (
      jsondecode(aws_ecs_task_definition.migrator.container_definitions)[0].image ==
      "ghcr.io/matheuscardosolazzarini/renda-fixa-api:0123456789abcdef0123456789abcdef01234567-migrator"
    )
    error_message = "a imagem da migração deve terminar em :<sha>-migrator"
  }
}

run "nenhuma_imagem_usa_tag_mutavel" {
  command = apply

  module {
    source = "./modules/app"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      can(regex(":[0-9a-f]{40}(-migrator)?$", jsondecode(td.container_definitions)[0].image))
    ])
    error_message = "toda imagem deve ter como tag o SHA de 40 hexadecimais, nunca latest"
  }
}

# ---- Task definitions ----

run "task_definitions_sao_fargate_pequenas_e_amd64" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      td.cpu == "256" && td.memory == "512"
    ])
    error_message = "as duas task definitions devem ter 0,25 vCPU (256) e 0,5 GB (512)"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      td.network_mode == "awsvpc" && contains(td.requires_compatibilities, "FARGATE")
    ])
    error_message = "as duas task definitions devem ser Fargate com awsvpc"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      one(td.runtime_platform).cpu_architecture == "X86_64" && one(td.runtime_platform).operating_system_family == "LINUX"
    ])
    error_message = "as duas task definitions devem rodar em linux/amd64"
  }

  assert {
    condition     = aws_ecs_task_definition.api.family == "renda-fixa-api" && aws_ecs_task_definition.migrator.family == "renda-fixa-migrator"
    error_message = "as famílias devem ser exatamente renda-fixa-api e renda-fixa-migrator (a spec e o job limpar dependem desses nomes)"
  }
}

run "task_definitions_usam_a_mesma_execution_role_e_nenhuma_task_role" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      td.execution_role_arn == "arn:aws:iam::123456789012:role/renda-fixa-api-execution"
    ])
    error_message = "as duas task definitions devem usar a execution role recebida"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      td.task_role_arn == null
    ])
    error_message = "não pode haver task role: a API não chama serviço da AWS"
  }
}

run "task_definitions_compartilham_o_log_group_com_prefixos_diferentes" {
  command = apply

  module {
    source = "./modules/app"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      jsondecode(td.container_definitions)[0].logConfiguration.logDriver == "awslogs" &&
      jsondecode(td.container_definitions)[0].logConfiguration.options["awslogs-group"] == aws_cloudwatch_log_group.this.name
    ])
    error_message = "as duas devem logar no log group do módulo, referenciado pelo atributo do recurso"
  }

  assert {
    condition = (
      jsondecode(aws_ecs_task_definition.api.container_definitions)[0].logConfiguration.options["awslogs-stream-prefix"] !=
      jsondecode(aws_ecs_task_definition.migrator.container_definitions)[0].logConfiguration.options["awslogs-stream-prefix"]
    )
    error_message = "os prefixos de stream devem ser diferentes"
  }
}

run "connection_string_vem_do_secret_e_nunca_em_texto" {
  command = apply

  module {
    source = "./modules/app"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      length([
        for s in jsondecode(td.container_definitions)[0].secrets :
        s if s.name == "ConnectionStrings__DefaultConnection" && s.valueFrom == aws_secretsmanager_secret.connection_string.arn
      ]) == 1
    ])
    error_message = "ConnectionStrings__DefaultConnection deve vir do secret, com valueFrom igual ao ARN dele"
  }

  assert {
    condition = alltrue([
      for td in [aws_ecs_task_definition.api, aws_ecs_task_definition.migrator] :
      !strcontains(td.container_definitions, "senha-de-teste-que-nunca-vai-para-o-state") &&
      !strcontains(td.container_definitions, "Password=")
    ])
    error_message = "a task definition não pode carregar a senha nem a connection string"
  }
}

run "api_roda_em_producao_na_porta_8080" {
  command = apply

  module {
    source = "./modules/app"
  }

  assert {
    condition = length([
      for e in jsondecode(aws_ecs_task_definition.api.container_definitions)[0].environment :
      e if e.name == "ASPNETCORE_ENVIRONMENT" && e.value == "Production"
    ]) == 1
    error_message = "a API deve rodar com ASPNETCORE_ENVIRONMENT=Production"
  }

  assert {
    condition     = jsondecode(aws_ecs_task_definition.api.container_definitions)[0].portMappings[0].containerPort == 8080
    error_message = "a API deve expor a porta 8080"
  }
}

# ---- Logs ----

run "log_group_tem_retencao_de_3_dias" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition     = aws_cloudwatch_log_group.this.retention_in_days == 3
    error_message = "a retenção do log group deve ser 3 dias"
  }
}

# ---- Secret ----

run "secret_some_na_hora_e_a_connection_string_e_de_escrita_unica" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition     = aws_secretsmanager_secret.connection_string.name == "renda-fixa-api/connection-string"
    error_message = "o secret deve se chamar renda-fixa-api/connection-string (a execution role do bootstrap lê renda-fixa-api/*)"
  }

  assert {
    condition     = aws_secretsmanager_secret.connection_string.recovery_window_in_days == 0
    error_message = "recovery_window_in_days deve ser 0: sem janela de recuperação, nada sobra após o destroy"
  }

  assert {
    condition     = aws_secretsmanager_secret_version.connection_string.secret_string_wo_version == 1
    error_message = "secret_string_wo_version deve ser 1 (D6, opção B)"
  }

  assert {
    condition     = aws_secretsmanager_secret_version.connection_string.secret_string == null
    error_message = "secret_string em texto não pode ser usado: iria para o state"
  }
}

# ---- Cluster ----

run "cluster_tem_container_insights_desligado_e_a_tag_de_expiracao" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition = length([
      for s in aws_ecs_cluster.this.setting : s if s.name == "containerInsights" && s.value == "disabled"
    ]) == 1
    error_message = "Container Insights deve ficar desligado"
  }

  assert {
    condition     = aws_ecs_cluster.this.tags["expires_at"] == "2026-11-19T18:00:00Z"
    error_message = "o cluster deve levar a tag expires_at (D4: no cluster, não em default_tags)"
  }
}

# ---- Serviço ----

run "servico_segue_a_variavel_de_desired_count_zero" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition     = aws_ecs_service.this.desired_count == 0
    error_message = "desired_count deve seguir a variável (0 durante a migração)"
  }
}

run "servico_segue_a_variavel_de_desired_count_um" {
  command = plan

  module {
    source = "./modules/app"
  }

  variables {
    desired_count = 1
  }

  assert {
    condition     = aws_ecs_service.this.desired_count == 1
    error_message = "desired_count deve seguir a variável (1 depois da migração)"
  }
}

run "servico_tem_circuit_breaker_com_rollback_e_propaga_tags" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition = (
      one(aws_ecs_service.this.deployment_circuit_breaker).enable == true &&
      one(aws_ecs_service.this.deployment_circuit_breaker).rollback == true
    )
    error_message = "o serviço deve ter deployment_circuit_breaker com rollback"
  }

  assert {
    condition     = aws_ecs_service.this.propagate_tags == "SERVICE"
    error_message = "propagate_tags deve ser SERVICE"
  }

  assert {
    condition     = aws_ecs_service.this.launch_type == "FARGATE"
    error_message = "o serviço deve ser Fargate"
  }

  assert {
    condition     = aws_ecs_service.this.health_check_grace_period_seconds == 120
    error_message = "health_check_grace_period_seconds deve ser 120"
  }
}

run "servico_roda_nas_subnets_publicas_com_ip_publico_e_o_sg_da_tarefa" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition = (
      one(aws_ecs_service.this.network_configuration).assign_public_ip == true &&
      one(aws_ecs_service.this.network_configuration).subnets == toset(["subnet-aaa", "subnet-bbb"]) &&
      one(aws_ecs_service.this.network_configuration).security_groups == toset(["sg-task"])
    )
    error_message = "o serviço deve usar as duas subnets públicas, IP público e só o SG da tarefa"
  }
}

# ---- ALB ----

run "alb_e_publico_sem_protecao_contra_exclusao_nas_duas_subnets" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition     = aws_lb.this.enable_deletion_protection == false
    error_message = "enable_deletion_protection deve ser false"
  }

  assert {
    condition     = aws_lb.this.load_balancer_type == "application" && aws_lb.this.internal == false
    error_message = "deve ser um Application Load Balancer voltado para a internet"
  }

  assert {
    condition     = aws_lb.this.subnets == toset(["subnet-aaa", "subnet-bbb"]) && aws_lb.this.security_groups == toset(["sg-alb"])
    error_message = "o ALB deve usar as duas subnets e só o SG do ALB"
  }
  assert {
    condition     = aws_lb.this.drop_invalid_header_fields == true
    error_message = "drop_invalid_header_fields deve ser true (descarta cabeçalhos HTTP inválidos antes de chegarem à API)"
  }

}

run "target_group_e_ip_na_8080_com_health_check_e_drenagem_curta" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition = (
      aws_lb_target_group.this.target_type == "ip" &&
      aws_lb_target_group.this.port == 8080 &&
      aws_lb_target_group.this.protocol == "HTTP"
    )
    error_message = "o target group deve ser do tipo ip, HTTP na porta 8080"
  }

  assert {
    condition     = one(aws_lb_target_group.this.health_check).path == "/health"
    error_message = "o health check deve usar /health"
  }

  assert {
    condition     = aws_lb_target_group.this.deregistration_delay == "10"
    error_message = "deregistration_delay deve ser 10 (o padrão de 300 s alonga o destroy)"
  }
}

run "listener_e_http_na_80_e_encaminha_para_o_target_group" {
  command = plan

  module {
    source = "./modules/app"
  }

  assert {
    condition     = aws_lb_listener.http.port == 80 && aws_lb_listener.http.protocol == "HTTP"
    error_message = "o listener deve ser HTTP na porta 80"
  }

  assert {
    condition     = one(aws_lb_listener.http.default_action).type == "forward"
    error_message = "o listener deve encaminhar ao target group"
  }
}

# ---- Outputs e a ligação com o banco ----

run "outputs_do_modulo_nao_sao_sensiveis_e_nao_carregam_a_senha" {
  command = apply

  module {
    source = "./modules/app"
  }

  assert {
    condition = alltrue([
      for v in [output.alb_dns_name, output.cluster_name, output.connection_string_version] :
      !issensitive(v)
    ])
    error_message = "nenhum output do módulo pode ser sensível"
  }

  assert {
    condition = alltrue([
      for v in [output.alb_dns_name, output.cluster_name] :
      !strcontains(v, "senha-de-teste-que-nunca-vai-para-o-state")
    ])
    error_message = "nenhum output do módulo pode conter a senha"
  }

  assert {
    condition     = output.cluster_name == aws_ecs_cluster.this.name && output.alb_dns_name == aws_lb.this.dns_name
    error_message = "os outputs devem apontar para o cluster e o ALB do módulo"
  }
}
