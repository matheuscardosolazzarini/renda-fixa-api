locals {
  name = "renda-fixa-api"
}

# D6, opção B: a senha nasce efêmera, a cada execução, e não vai para o state nem para o plano.
# Sem pontuação: o RDS proíbe / @ " e espaço, e ; e = quebrariam a connection string do Npgsql.
# O mesmo valor efêmero alimenta o secret da connection string no P3 (secret_string_wo), na mesma
# execução, passado ao módulo por variável efêmera.
ephemeral "aws_secretsmanager_random_password" "db" {
  password_length     = 32
  exclude_punctuation = true
}

module "network" {
  source = "./modules/network"

  name = local.name
}

module "database" {
  source = "./modules/database"

  name              = local.name
  subnet_ids        = module.network.public_subnet_ids
  security_group_id = module.network.rds_security_group_id
  master_password   = ephemeral.aws_secretsmanager_random_password.db.random_password
}

# A execution role é criada pelo bootstrap (aws_iam_role fora da lista positiva da trava de custo).
# O ARN é montado com o id da conta e o nome fixo: sem variável nova, sem data aws_iam_role (que
# exigiria iam:GetRole na role do CI) e sem iam:* no Terraform. sts:GetCallerIdentity não pede
# permissão.
data "aws_caller_identity" "current" {}

module "app" {
  source = "./modules/app"

  name                   = local.name
  vpc_id                 = module.network.vpc_id
  subnet_ids             = module.network.public_subnet_ids
  alb_security_group_id  = module.network.alb_security_group_id
  task_security_group_id = module.network.task_security_group_id
  execution_role_arn     = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/${local.name}-execution"
  image_sha              = var.image_sha
  desired_count          = var.service_desired_count
  expires_at             = var.expires_at

  db_address  = module.database.address
  db_port     = module.database.port
  db_name     = module.database.db_name
  db_username = module.database.username
  db_password = ephemeral.aws_secretsmanager_random_password.db.random_password
}
