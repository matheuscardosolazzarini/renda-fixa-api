# Nenhum output com senha. Host, porta, banco e usuário são identificadores, não segredos; o
# módulo app (P3) os usa para montar a connection string junto com a senha efêmera.
output "address" {
  description = "Host do RDS."
  value       = aws_db_instance.this.address
}

output "port" {
  description = "Porta do RDS."
  value       = aws_db_instance.this.port
}

output "db_name" {
  description = "Nome do banco."
  value       = aws_db_instance.this.db_name
}

output "username" {
  description = "Usuário mestre."
  value       = aws_db_instance.this.username
}

# Lido só pelos testes do raiz: tem de ser igual a secret_string_wo_version do secret da connection string.
output "password_version" {
  description = "password_wo_version do RDS."
  value       = aws_db_instance.this.password_wo_version
}
