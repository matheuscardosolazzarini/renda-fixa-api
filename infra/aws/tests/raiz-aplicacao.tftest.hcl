# Raiz (P3): a ligação do módulo app com a rede e o banco, os outputs novos e a coerência entre a
# senha do RDS e a connection string do secret.

mock_provider "aws" {
  mock_data "aws_availability_zones" {
    defaults = {
      names = ["us-east-1a", "us-east-1b", "us-east-1c"]
    }
  }

  mock_data "aws_caller_identity" {
    defaults = {
      account_id = "123456789012"
    }
  }

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
  image_sha  = "0123456789abcdef0123456789abcdef01234567"
  expires_at = timeadd(timestamp(), "4h")
}

run "senha_do_rds_e_secret_da_connection_string_tem_a_mesma_versao_de_escrita" {
  command = apply

  # Se divergirem, o RDS reescreve a senha e o secret fica com a antiga: a connection string
  # vira inválida e a API não conecta (D6, opção B).
  assert {
    condition     = module.database.password_version == module.app.connection_string_version
    error_message = "password_wo_version do RDS deve ser igual a secret_string_wo_version do secret"
  }
}

run "outputs_novos_expoem_o_dns_do_alb_e_o_cluster" {
  command = apply

  assert {
    condition     = output.alb_dns_name == module.app.alb_dns_name
    error_message = "o output alb_dns_name deve ser o DNS do ALB do módulo app"
  }

  assert {
    condition     = output.cluster_name == module.app.cluster_name
    error_message = "o output cluster_name deve ser o cluster do módulo app"
  }
}

run "nenhum_output_do_raiz_e_sensivel" {
  command = apply

  assert {
    condition = alltrue([
      for v in [
        output.alb_dns_name, output.cluster_name, output.expires_at, output.image_sha,
        output.public_subnet_ids, output.task_security_group_id
      ] : !issensitive(v)
    ])
    error_message = "nenhum output do raiz pode ser sensível (nem existir um que precise ser)"
  }
}

run "a_execution_role_vem_do_bootstrap_pelo_nome_fixo" {
  command = apply

  assert {
    condition     = module.app.execution_role_arn == "arn:aws:iam::123456789012:role/renda-fixa-api-execution"
    error_message = "a execution role deve ser a do bootstrap (renda-fixa-api-execution) na conta atual"
  }
}

# O destroy do agendado (scripts/destruir-se-vencido.sh) lê o prazo da tag expires_at do cluster, no endereço
# module.app.aws_ecs_cluster.this, no prior_state do plano de destroy. Se o módulo app perder a tag vinda da
# variável da raiz, o script trataria todo ambiente como vencido (D4: a tag só existe no cluster). O terraform test
# só alcança outputs de módulo, não recursos dentro dele; por isso a tag sai pelo output cluster_expires_at.
# tests/agendado/rodar.sh confere que o endereço padrão do script existe nesta configuração.
run "o_cluster_leva_a_tag_expires_at_da_variavel_da_raiz" {
  command = apply

  assert {
    condition     = module.app.cluster_expires_at == var.expires_at
    error_message = "o cluster do módulo app deve levar tags[expires_at] = var.expires_at (o agendado lê o prazo daqui)"
  }
}
