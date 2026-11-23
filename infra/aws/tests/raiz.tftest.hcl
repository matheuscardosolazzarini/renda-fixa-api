# Raiz (P2): a ligação dos módulos e os outputs novos. Nenhum output é sensível nem carrega senha.

mock_provider "aws" {
  mock_data "aws_availability_zones" {
    defaults = {
      names = ["us-east-1a", "us-east-1b", "us-east-1c"]
    }
  }

  # O módulo app (P3) monta o ARN da execution role com o id da conta e valida o formato dos ARNs.
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

run "outputs_novos_listam_subnets_e_sg_da_tarefa" {
  command = apply


  assert {
    condition     = length(output.public_subnet_ids) == 2
    error_message = "o output public_subnet_ids deve listar as duas subnets"
  }

  assert {
    condition     = output.task_security_group_id == module.network.task_security_group_id
    error_message = "o output task_security_group_id deve ser o SG da tarefa"
  }
}

run "o_raiz_chama_o_modulo_database" {
  command = apply


  assert {
    condition     = module.database.address != null && module.database.username == "fixedincome"
    error_message = "o módulo database deve ser chamado pelo raiz"
  }
}
