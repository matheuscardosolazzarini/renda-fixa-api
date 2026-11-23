resource "aws_db_subnet_group" "this" {
  name       = var.name
  subnet_ids = var.subnet_ids

  tags = {
    Name = var.name
  }
}

resource "aws_db_instance" "this" {
  identifier = var.name

  engine         = "postgres"
  engine_version = "16"
  instance_class = "db.t4g.micro"

  allocated_storage = 20
  storage_type      = "gp3"
  storage_encrypted = true # sem kms_key_id: chave gerenciada pela AWS (aws/rds), nunca chave de cliente

  db_name  = var.db_name
  username = var.username

  # D6, opção B: a senha é escrita uma vez e não entra no state. A versão fixa em 1 faz os applies
  # seguintes (prorrogar o TTL) não reescreverem a senha, mesmo com o efêmero gerando valor novo.
  password_wo         = var.master_password
  password_wo_version = 1

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [var.security_group_id]
  publicly_accessible    = false
  multi_az               = false

  # Nada pode sobrar depois do destroy (premissa de custo da spec).
  skip_final_snapshot      = true
  deletion_protection      = false
  backup_retention_period  = 0
  delete_automated_backups = true

  monitoring_interval          = 0
  performance_insights_enabled = false

  tags = {
    Name = var.name
  }
}
