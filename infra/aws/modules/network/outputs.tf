output "vpc_id" {
  description = "Id da VPC."
  value       = aws_vpc.this.id
}

output "public_subnet_ids" {
  description = "Ids das duas subnets públicas, em AZs diferentes."
  value       = aws_subnet.public[*].id
}

output "alb_security_group_id" {
  description = "SG do ALB (80 da internet)."
  value       = aws_security_group.alb.id
}

output "task_security_group_id" {
  description = "SG das tarefas da API e da migração (8080 só do SG do ALB)."
  value       = aws_security_group.task.id
}

output "rds_security_group_id" {
  description = "SG do RDS (5432 só do SG da tarefa)."
  value       = aws_security_group.rds.id
}
