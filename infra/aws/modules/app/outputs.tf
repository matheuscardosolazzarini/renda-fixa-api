output "alb_dns_name" {
  description = "DNS do ALB (HTTP 80)."
  value       = aws_lb.this.dns_name
}

output "cluster_name" {
  description = "Nome do cluster ECS."
  value       = aws_ecs_cluster.this.name
}

output "execution_role_arn" {
  description = "ARN da execution role usada pelas duas task definitions."
  value       = aws_ecs_task_definition.api.execution_role_arn
}

# Lido só pelos testes do raiz: a versão de escrita do secret precisa ser a do RDS (password_wo_version).
output "connection_string_version" {
  description = "secret_string_wo_version do secret da connection string."
  value       = aws_secretsmanager_secret_version.connection_string.secret_string_wo_version
}

# Lido só pelos testes do raiz: o agendado lê o prazo da tag expires_at deste cluster (D4), no endereço
# module.app.aws_ecs_cluster.this (scripts/destruir-se-vencido.sh).
output "cluster_expires_at" {
  description = "Tag expires_at do cluster ECS."
  value       = aws_ecs_cluster.this.tags["expires_at"]
}
