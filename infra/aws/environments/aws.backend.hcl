# Uso: terraform init -backend-config=environments/aws.backend.hcl -backend-config="bucket=$TF_STATE_BUCKET"
# Sem segredo: o bucket é passado à parte e a credencial vem do OIDC.
key          = "renda-fixa-api/aws.tfstate"
region       = "us-east-1"
encrypt      = true
use_lockfile = true
