terraform {
  # >= 1.11 para argumentos de escrita única (password_wo, secret_string_wo) e use_lockfile no backend s3.
  required_version = "~> 1.11"

  required_providers {
    aws = {
      source = "hashicorp/aws"
      # Faixa menor: 6.73.x. O provider fixado precisa ter password_wo, secret_string_wo
      # e o recurso efêmero aws_secretsmanager_random_password (D6).
      version = "~> 6.73.0"
    }
  }
}
