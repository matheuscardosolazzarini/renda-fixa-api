#!/usr/bin/env bash
# Gera infra/aws/floci_override.tf, que aponta o Terraform para o emulador Floci.
# Override file (https://developer.hashicorp.com/terraform/language/files/override):
#  - o bloco backend do override substitui o backend "s3" de backend.tf por um local;
#  - os argumentos do provider "aws" são mesclados aos de providers.tf (region e default_tags ficam).
# O arquivo está no .gitignore e nunca vai para a AWS real. Credenciais são fictícias.
#
# Uso: scripts/gerar-floci-override.sh [URL_DO_FLOCI]   (padrão: http://localhost:4566)
set -euo pipefail

endpoint="${1:-http://localhost:4566}"

if [[ ! "$endpoint" =~ ^https?://[A-Za-z0-9._:-]+$ ]]; then
  echo "URL do Floci inválida: $endpoint" >&2
  exit 1
fi

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
destino="$raiz/floci_override.tf"

cat > "$destino" <<EOF
# GERADO por scripts/gerar-floci-override.sh. Não editar, não versionar.
terraform {
  backend "local" {
    path = "floci.tfstate"
  }
}

provider "aws" {
  access_key = "test"
  secret_key = "test"

  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true
  skip_region_validation      = true
  s3_use_path_style           = true

  endpoints {
    ec2                      = "$endpoint"
    ecs                      = "$endpoint"
    elbv2                    = "$endpoint"
    rds                      = "$endpoint"
    secretsmanager           = "$endpoint"
    cloudwatchlogs           = "$endpoint"
    iam                      = "$endpoint"
    sts                      = "$endpoint"
    s3                       = "$endpoint"
    resourcegroupstaggingapi = "$endpoint"
  }
}
EOF

echo "Gerado: $destino (endpoint $endpoint)"
