variable "name" {
  description = "Prefixo dos nomes dos recursos de rede."
  type        = string
}

variable "vpc_cidr" {
  description = "CIDR da VPC. As duas subnets públicas são fatias /24 dele."
  type        = string
  default     = "10.20.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "vpc_cidr deve ser um CIDR IPv4 válido."
  }
}
