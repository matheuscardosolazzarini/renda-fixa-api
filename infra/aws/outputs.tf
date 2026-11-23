output "expires_at" {
  description = "Instante de expiração do ambiente, lido pelo workflow agendado."
  value       = var.expires_at
}

output "image_sha" {
  description = "SHA das imagens implantadas."
  value       = var.image_sha
}

output "public_subnet_ids" {
  description = "Subnets públicas, usadas pela migração (run-task) e pelo ALB."
  value       = module.network.public_subnet_ids
}

output "task_security_group_id" {
  description = "SG da tarefa, usado pela migração (run-task)."
  value       = module.network.task_security_group_id
}

output "alb_dns_name" {
  description = "DNS do ALB (HTTP 80), usado pelo smoke e mostrado no resumo."
  value       = module.app.alb_dns_name
}

output "cluster_name" {
  description = "Nome do cluster ECS, usado pela migração (run-task) e pela espera do serviço."
  value       = module.app.cluster_name
}
