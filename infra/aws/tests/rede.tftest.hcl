# Módulo network (P2): atributos do plano contra o provider mockado.
# O mock_provider não chama a AWS: prova a configuração, não o comportamento da nuvem.
# Os runs usam o módulo diretamente (bloco module) porque asserts de run não enxergam recursos
# de módulos filhos.

mock_provider "aws" {
  # Com o provider mockado, a lista de AZs viria vazia; fixamos três para o plano.
  mock_data "aws_availability_zones" {
    defaults = {
      names = ["us-east-1a", "us-east-1b", "us-east-1c"]
    }
  }
}

variables {
  name = "teste"
}

run "ha_duas_subnets_publicas_em_azs_diferentes" {
  command = plan

  module {
    source = "./modules/network"
  }

  assert {
    condition     = length(aws_subnet.public) == 2
    error_message = "devem existir exatamente duas subnets públicas"
  }

  assert {
    condition     = length(toset([for s in aws_subnet.public : s.availability_zone])) == 2
    error_message = "as duas subnets devem ficar em AZs diferentes"
  }

  assert {
    condition     = alltrue([for s in aws_subnet.public : s.map_public_ip_on_launch == false])
    error_message = "subnets não devem atribuir IP público por padrão (a tarefa pede o seu)"
  }
}

run "so_ha_rede_publica_com_uma_rota_para_o_internet_gateway" {
  # Com o mock, os ids só existem depois do apply; é preciso para comparar o gateway da rota com o IGW.
  command = apply

  module {
    source = "./modules/network"
  }

  assert {
    condition     = length(aws_route_table_association.public) == 2
    error_message = "as duas subnets devem estar associadas à route table pública"
  }

  assert {
    condition     = alltrue([for a in aws_route_table_association.public : a.route_table_id == aws_route_table.public.id])
    error_message = "as duas subnets devem usar a route table pública"
  }

  assert {
    condition     = length(aws_route_table.public.route) == 1 && one(aws_route_table.public.route).cidr_block == "0.0.0.0/0"
    error_message = "a route table pública deve ter só a rota padrão"
  }

  assert {
    condition     = one(aws_route_table.public.route).gateway_id != null && one(aws_route_table.public.route).gateway_id == aws_internet_gateway.this.id
    error_message = "a rota 0.0.0.0/0 deve apontar para o internet gateway da VPC (nada de NAT, instância ou outro gateway)"
  }

  assert {
    condition     = aws_internet_gateway.this.vpc_id == aws_vpc.this.id && aws_route_table.public.vpc_id == aws_vpc.this.id
    error_message = "o internet gateway e a route table devem pertencer à VPC do módulo"
  }
}

run "alb_aceita_80_de_qualquer_origem" {
  command = plan

  module {
    source = "./modules/network"
  }

  assert {
    condition = (
      aws_vpc_security_group_ingress_rule.alb_http.from_port == 80 &&
      aws_vpc_security_group_ingress_rule.alb_http.to_port == 80 &&
      aws_vpc_security_group_ingress_rule.alb_http.ip_protocol == "tcp" &&
      aws_vpc_security_group_ingress_rule.alb_http.cidr_ipv4 == "0.0.0.0/0"
    )
    error_message = "o ALB deve aceitar 80/tcp de qualquer origem"
  }
}

run "tarefa_aceita_8080_e_rds_aceita_5432_sem_nenhum_cidr" {
  command = plan

  module {
    source = "./modules/network"
  }

  assert {
    condition = (
      aws_vpc_security_group_ingress_rule.task_http.from_port == 8080 &&
      aws_vpc_security_group_ingress_rule.task_http.to_port == 8080 &&
      aws_vpc_security_group_ingress_rule.task_http.ip_protocol == "tcp" &&
      aws_vpc_security_group_ingress_rule.task_http.cidr_ipv4 == null &&
      aws_vpc_security_group_ingress_rule.task_http.cidr_ipv6 == null
    )
    error_message = "a tarefa deve aceitar 8080/tcp e nenhum CIDR"
  }

  assert {
    condition = (
      aws_vpc_security_group_ingress_rule.rds_postgres.from_port == 5432 &&
      aws_vpc_security_group_ingress_rule.rds_postgres.to_port == 5432 &&
      aws_vpc_security_group_ingress_rule.rds_postgres.ip_protocol == "tcp" &&
      aws_vpc_security_group_ingress_rule.rds_postgres.cidr_ipv4 == null &&
      aws_vpc_security_group_ingress_rule.rds_postgres.cidr_ipv6 == null
    )
    error_message = "o RDS deve aceitar 5432/tcp e nenhum CIDR"
  }
}

run "cadeia_alb_tarefa_rds_aponta_sempre_para_o_sg_anterior" {
  # Com o mock, os ids só existem depois do apply.
  command = apply

  module {
    source = "./modules/network"
  }

  assert {
    condition = (
      aws_vpc_security_group_ingress_rule.alb_http.security_group_id == aws_security_group.alb.id &&
      aws_vpc_security_group_ingress_rule.task_http.security_group_id == aws_security_group.task.id &&
      aws_vpc_security_group_ingress_rule.rds_postgres.security_group_id == aws_security_group.rds.id
    )
    error_message = "cada regra de entrada pertence ao SG do seu elo da cadeia"
  }

  assert {
    condition     = aws_vpc_security_group_ingress_rule.task_http.referenced_security_group_id == aws_security_group.alb.id
    error_message = "a tarefa só aceita 8080 do SG do ALB"
  }

  assert {
    condition     = aws_vpc_security_group_ingress_rule.rds_postgres.referenced_security_group_id == aws_security_group.task.id
    error_message = "o RDS só aceita 5432 do SG da tarefa"
  }

  assert {
    condition     = aws_vpc_security_group_egress_rule.alb_to_task.referenced_security_group_id == aws_security_group.task.id
    error_message = "o ALB só sai para o SG da tarefa"
  }
}

run "outputs_expoem_os_tres_sgs_e_as_duas_subnets" {
  command = apply

  module {
    source = "./modules/network"
  }

  assert {
    condition     = length(output.public_subnet_ids) == 2
    error_message = "public_subnet_ids deve listar as duas subnets"
  }

  assert {
    condition = (
      output.alb_security_group_id == aws_security_group.alb.id &&
      output.task_security_group_id == aws_security_group.task.id &&
      output.rds_security_group_id == aws_security_group.rds.id
    )
    error_message = "os outputs de SG devem apontar para os SGs do módulo"
  }
}

# Saída da tarefa (e da migração, que usa o mesmo SG): só HTTPS para a internet (GHCR, Secrets Manager, CloudWatch
# Logs, ECS e a API do SGS do Banco Central, todos em 443) e o PostgreSQL só para o SG do RDS. O DNS da VPC
# (Route 53 Resolver, VPC+2) não precisa de regra: a documentação da AWS diz que security groups não o bloqueiam.
run "tarefa_sai_so_em_443_para_a_internet_e_em_5432_so_para_o_sg_do_rds" {
  command = apply

  module {
    source = "./modules/network"
  }

  assert {
    condition = (
      aws_vpc_security_group_egress_rule.task_https.security_group_id == aws_security_group.task.id &&
      aws_vpc_security_group_egress_rule.task_https.ip_protocol == "tcp" &&
      aws_vpc_security_group_egress_rule.task_https.from_port == 443 &&
      aws_vpc_security_group_egress_rule.task_https.to_port == 443 &&
      aws_vpc_security_group_egress_rule.task_https.cidr_ipv4 == "0.0.0.0/0"
    )
    error_message = "a tarefa deve sair em 443/tcp para 0.0.0.0/0"
  }

  assert {
    condition = (
      aws_vpc_security_group_egress_rule.task_postgres.security_group_id == aws_security_group.task.id &&
      aws_vpc_security_group_egress_rule.task_postgres.ip_protocol == "tcp" &&
      aws_vpc_security_group_egress_rule.task_postgres.from_port == 5432 &&
      aws_vpc_security_group_egress_rule.task_postgres.to_port == 5432 &&
      aws_vpc_security_group_egress_rule.task_postgres.referenced_security_group_id == aws_security_group.rds.id &&
      aws_vpc_security_group_egress_rule.task_postgres.cidr_ipv4 == null &&
      aws_vpc_security_group_egress_rule.task_postgres.cidr_ipv6 == null
    )
    error_message = "a tarefa deve sair em 5432/tcp só para o SG do RDS, sem CIDR"
  }

  assert {
    condition = alltrue([
      for r in [
        aws_vpc_security_group_egress_rule.alb_to_task,
        aws_vpc_security_group_egress_rule.task_https,
        aws_vpc_security_group_egress_rule.task_postgres
      ] : r.ip_protocol == "tcp"
    ])
    error_message = "nenhuma regra de saída pode usar o protocolo -1 (todos)"
  }
}

# O security group padrão da VPC nasce com entrada e saída livres entre os seus membros; adotá-lo sem nenhuma regra
# as remove (Checkov CKV2_AWS_12). Nada usa esse SG: ele sai junto com a VPC.
run "a_vpc_adota_o_security_group_padrao_sem_nenhuma_regra" {
  command = apply

  module {
    source = "./modules/network"
  }

  assert {
    condition     = aws_default_security_group.this.vpc_id == aws_vpc.this.id
    error_message = "o security group padrão adotado deve ser o da VPC do módulo"
  }

  assert {
    condition     = length(aws_default_security_group.this.ingress) == 0 && length(aws_default_security_group.this.egress) == 0
    error_message = "o security group padrão não pode ter regra de entrada nem de saída"
  }
}
