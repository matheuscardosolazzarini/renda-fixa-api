# Módulo database (P2): atributos do plano contra o provider mockado.
# A senha efêmera entra como variável do run; ela nunca aparece no plano nem em output.

mock_provider "aws" {}

variables {
  name              = "teste"
  subnet_ids        = ["subnet-aaa", "subnet-bbb"]
  security_group_id = "sg-rds"
  master_password   = "senha-de-teste-que-nunca-vai-para-o-state"
}

run "banco_tem_a_classe_o_motor_e_o_armazenamento_baratos" {
  command = plan

  module {
    source = "./modules/database"
  }

  assert {
    condition     = aws_db_instance.this.instance_class == "db.t4g.micro"
    error_message = "a classe do RDS deve ser db.t4g.micro"
  }

  assert {
    condition     = aws_db_instance.this.allocated_storage == 20
    error_message = "o armazenamento deve ser 20 GB"
  }

  assert {
    condition     = aws_db_instance.this.storage_type == "gp3"
    error_message = "o armazenamento deve ser gp3"
  }

  assert {
    condition     = aws_db_instance.this.engine == "postgres" && startswith(aws_db_instance.this.engine_version, "16")
    error_message = "o motor deve ser PostgreSQL 16"
  }
}

run "banco_e_single_az_privado_e_criptografado" {
  command = plan

  module {
    source = "./modules/database"
  }

  assert {
    condition     = aws_db_instance.this.multi_az == false
    error_message = "o RDS deve ser single-AZ"
  }

  assert {
    condition     = aws_db_instance.this.publicly_accessible == false
    error_message = "o RDS não pode ter acesso público"
  }

  assert {
    condition     = aws_db_instance.this.storage_encrypted == true
    error_message = "o armazenamento deve ser criptografado"
  }

  # kms_key_id é calculado: não dá para provar a ausência da chave de cliente antes do apply.
  # Quem a impede é a ausência de kms_key_id no módulo e a trava de custo (aws_kms_key reprovado).
}

run "banco_nao_deixa_residuo_ao_ser_destruido" {
  command = plan

  module {
    source = "./modules/database"
  }

  assert {
    condition     = aws_db_instance.this.skip_final_snapshot == true
    error_message = "skip_final_snapshot deve ser true"
  }

  assert {
    condition     = aws_db_instance.this.deletion_protection == false
    error_message = "deletion_protection deve ser false"
  }

  assert {
    condition     = aws_db_instance.this.backup_retention_period == 0
    error_message = "backup_retention_period deve ser 0"
  }

  assert {
    condition     = aws_db_instance.this.delete_automated_backups == true
    error_message = "delete_automated_backups deve ser true"
  }

  assert {
    condition     = aws_db_instance.this.monitoring_interval == 0
    error_message = "o monitoramento avançado deve ficar desligado"
  }

  assert {
    condition     = aws_db_instance.this.performance_insights_enabled == false
    error_message = "o Performance Insights deve ficar desligado"
  }

  assert {
    condition     = length(coalesce(aws_db_instance.this.enabled_cloudwatch_logs_exports, [])) == 0
    error_message = "o RDS não pode exportar logs para o CloudWatch"
  }
}

run "senha_e_de_escrita_unica_e_nao_ha_segredo_gerenciado" {
  command = plan

  module {
    source = "./modules/database"
  }

  assert {
    condition     = aws_db_instance.this.password_wo_version == 1
    error_message = "password_wo_version deve ser 1 (D6, opção B)"
  }

  assert {
    condition     = aws_db_instance.this.password == null
    error_message = "a senha em texto (password) não pode ser usada: iria para o state"
  }

  assert {
    condition     = aws_db_instance.this.manage_master_user_password == null
    error_message = "a opção A (segredo gerenciado pelo RDS) não deve estar ativa"
  }
}

run "banco_usa_o_sg_e_as_subnets_recebidos" {
  command = plan

  module {
    source = "./modules/database"
  }

  assert {
    condition     = aws_db_instance.this.vpc_security_group_ids == toset(["sg-rds"])
    error_message = "o RDS deve usar só o SG recebido"
  }

  assert {
    condition     = aws_db_subnet_group.this.subnet_ids == toset(["subnet-aaa", "subnet-bbb"])
    error_message = "o subnet group deve usar as duas subnets recebidas"
  }
}

run "uma_subnet_so_e_rejeitada_porque_o_rds_exige_duas_azs" {
  command = plan

  module {
    source = "./modules/database"
  }

  variables {
    subnet_ids = ["subnet-aaa"]
  }

  expect_failures = [var.subnet_ids]
}
