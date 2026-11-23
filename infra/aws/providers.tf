provider "aws" {
  region = var.region

  # expires_at fica de fora de propósito (D4): prorrogar o TTL não pode re-etiquetar todos os recursos.
  default_tags {
    tags = {
      project    = "renda-fixa-api"
      managed_by = "terraform"
    }
  }
}
