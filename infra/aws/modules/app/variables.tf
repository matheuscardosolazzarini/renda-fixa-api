variable "name" {
  description = "Prefixo dos nomes: cluster, ALB, target group, famílias das task definitions e log group."
  type        = string
}

variable "vpc_id" {
  description = "VPC do target group."
  type        = string
}

variable "subnet_ids" {
  description = "Subnets públicas (duas, em AZs diferentes) do ALB e das tarefas."
  type        = list(string)

  validation {
    condition     = length(var.subnet_ids) >= 2
    error_message = "O ALB exige ao menos duas subnets, em AZs diferentes."
  }
}

variable "alb_security_group_id" {
  description = "SG do ALB (80 da internet)."
  type        = string
}

variable "task_security_group_id" {
  description = "SG das tarefas da API e da migração (8080 só do SG do ALB)."
  type        = string
}

variable "execution_role_arn" {
  description = "ARN da execution role criada pelo bootstrap (renda-fixa-api-execution). O Terraform não cria IAM."
  type        = string

  validation {
    condition     = can(regex("^arn:aws:iam::[0-9]{12}:role/.+$", var.execution_role_arn))
    error_message = "execution_role_arn deve ser o ARN de uma role do IAM."
  }
}

variable "image_sha" {
  description = "SHA do commit (40 hexadecimais) das imagens da API (<sha>) e da migração (<sha>-migrator)."
  type        = string

  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.image_sha))
    error_message = "image_sha deve ter exatamente 40 hexadecimais minúsculos (nunca uma tag mutável)."
  }
}

variable "desired_count" {
  description = "Tarefas da API: 0 durante a migração, 1 depois (D3)."
  type        = number

  validation {
    condition     = var.desired_count == 0 || var.desired_count == 1
    error_message = "desired_count deve ser 0 ou 1: a importação de índices exige instância única."
  }
}

variable "expires_at" {
  description = "Instante de expiração do ambiente (RFC 3339). Vai só para a tag do cluster (D4)."
  type        = string
}

variable "db_address" {
  description = "Host do RDS."
  type        = string
}

variable "db_port" {
  description = "Porta do RDS."
  type        = number
}

variable "db_name" {
  description = "Nome do banco."
  type        = string
}

variable "db_username" {
  description = "Usuário mestre do banco."
  type        = string
}

# Efêmera e sensível: o valor vem do recurso efêmero do raiz (D6, opção B) e só alimenta o
# argumento de escrita única do secret. Não vai para o state, para o plano nem para output.
variable "db_password" {
  description = "Senha do banco, efêmera. Vai só para secret_string_wo."
  type        = string
  ephemeral   = true
  sensitive   = true
}
