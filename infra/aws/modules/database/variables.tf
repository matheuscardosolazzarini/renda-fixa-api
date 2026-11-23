variable "name" {
  description = "Identificador da instância e nome do subnet group."
  type        = string
}

variable "subnet_ids" {
  description = "Subnets do subnet group (duas, em AZs diferentes, exigência do RDS)."
  type        = list(string)

  validation {
    condition     = length(var.subnet_ids) >= 2
    error_message = "O subnet group do RDS exige ao menos duas subnets, em AZs diferentes."
  }
}

variable "security_group_id" {
  description = "SG do RDS (5432 só do SG da tarefa)."
  type        = string
}

variable "db_name" {
  description = "Nome do banco criado na instância."
  type        = string
  default     = "fixedincome"
}

variable "username" {
  description = "Usuário mestre."
  type        = string
  default     = "fixedincome"
}

# Efêmera e sensível: o valor vem do recurso efêmero do raiz (D6, opção B), só existe durante a
# execução e nunca vai para o state nem para o plano. Variável efêmera só pode alimentar
# argumento de escrita única (password_wo), outra variável ou output efêmero, recurso efêmero
# ou provider.
variable "master_password" {
  description = "Senha mestre do PostgreSQL, efêmera. Vai só para password_wo."
  type        = string
  ephemeral   = true
  sensitive   = true
}
