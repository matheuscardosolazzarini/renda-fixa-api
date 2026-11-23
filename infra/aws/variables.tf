variable "region" {
  description = "Região da AWS do ambiente efêmero."
  type        = string
  default     = "us-east-1"

  validation {
    condition     = can(regex("^[a-z]{2}(-[a-z]+)+-[0-9]+$", var.region))
    error_message = "region deve ter o formato de uma região da AWS, como us-east-1."
  }
}

variable "expires_at" {
  description = "Instante de expiração do ambiente (RFC 3339). Calculado pelo workflow, no máximo 12 h à frente."
  type        = string

  # Passado é aceito de propósito: o job destruir usa uma marcação antiga quando o state não tem o output.
  # try() evita um segundo erro, ilegível, quando o valor nem é RFC 3339 (o && do Terraform não faz curto-circuito).
  validation {
    condition = (
      can(timecmp(var.expires_at, plantimestamp())) &&
      try(timecmp(var.expires_at, timeadd(plantimestamp(), "12h")) <= 0, false)
    )
    error_message = "expires_at deve ser RFC 3339 (ex.: 2026-11-19T18:00:00Z) e no máximo 12 h à frente de agora."
  }
}

variable "image_sha" {
  description = "SHA do commit (40 hexadecimais minúsculos) das imagens da API e da migração."
  type        = string

  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.image_sha))
    error_message = "image_sha deve ter exatamente 40 hexadecimais minúsculos (nunca uma tag mutável como latest)."
  }
}

variable "service_desired_count" {
  description = "Tarefas da API: 0 durante a migração, 1 depois (D3)."
  type        = number
  default     = 0

  validation {
    condition     = var.service_desired_count == 0 || var.service_desired_count == 1
    error_message = "service_desired_count deve ser 0 ou 1: a importação de índices exige instância única."
  }
}
