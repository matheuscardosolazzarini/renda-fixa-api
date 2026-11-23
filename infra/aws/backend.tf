# Configuração parcial: o bucket vem de -backend-config="bucket=$TF_STATE_BUCKET" (identificador,
# não segredo) e o restante de environments/aws.backend.hcl.
# O ciclo no Floci troca este bloco por um backend local via floci_override.tf.
terraform {
  backend "s3" {}
}
