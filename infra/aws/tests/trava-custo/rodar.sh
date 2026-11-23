#!/usr/bin/env bash
# Roda a trava de custo (camada 6a) contra cada fixture desta pasta e compara com o esperado.
# Precisa de bash e jq (o script testado usa jq). Uso: tests/trava-custo/rodar.sh
#
# Regra de leitura dos casos:
#  - aprova-*: o script precisa sair com 0 e imprimir a aprovação;
#  - reprova-*: o script precisa sair com exatamente 1 e a mensagem precisa trazer o trecho esperado
#    (tipo, endereço ou atributo). Código 126/127 (script ausente) nunca conta como reprovação.
# Em todos os casos, o valor sentinela SEGREDO-CANARIO-123 não pode aparecer na saída.
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
script="${TRAVA_CUSTO:-$dir/../../scripts/trava-custo.sh}"

# arquivo | trecho que a mensagem de reprovação precisa conter (vazio nos casos de aprovação)
casos=(
  "aprova-plano-valido.json|"
  "aprova-segunda-fase-update.json|"
  "aprova-noop-dentro-dos-limites.json|"
  "aprova-delete-fora-da-lista.json|"
  "reprova-nat-gateway.json|aws_nat_gateway"
  "reprova-kms-key.json|aws_kms_key"
  "reprova-instance.json|aws_instance"
  "reprova-eip.json|aws_eip"
  "reprova-vpc-endpoint.json|aws_vpc_endpoint"
  "reprova-substituicao-fora-da-lista.json|aws_nat_gateway"
  "reprova-noop-fora-da-lista.json|aws_nat_gateway"
  "reprova-delete-ok-mas-outro-fora-da-lista.json|aws_kms_key"
  "reprova-multi-az.json|multi_az"
  "reprova-classe-diferente.json|instance_class"
  "reprova-storage-21.json|allocated_storage"
  "reprova-retencao-backup-7.json|backup_retention_period"
  "reprova-publicly-accessible.json|publicly_accessible"
  "reprova-max-allocated-storage.json|max_allocated_storage"
  "reprova-performance-insights.json|performance_insights_enabled"
  "reprova-atributo-ausente.json|instance_class"
  "reprova-desired-count-2.json|desired_count"
  "reprova-cpu-512.json|cpu"
  "reprova-memoria-1024.json|memory"
  "reprova-retencao-log-7.json|retention_in_days"
  "reprova-retencao-log-0-nunca-expira.json|retention_in_days"
  "reprova-nao-vaza-segredo.json|instance_class"
  "reprova-entrada-vazia.json|entrada inválida"
  "reprova-json-truncado.json|entrada inválida"
  "reprova-nao-e-json.json|entrada inválida"
  "reprova-sem-resource-changes.json|resource_changes"
  "reprova-resource-changes-vazio.json|resource_changes"
  "reprova-resource-changes-nulo.json|resource_changes"
  "reprova-json-nao-objeto.json|entrada inválida"
  "reprova-recurso-malformado.json|malformado"
  "reprova-actions-malformado.json|malformado"
  "reprova-dois-documentos.json|entrada inválida"
  "aprova-duas-task-definitions.json|"
  "aprova-rds-delete-nao-conta.json|"
  "aprova-rds-substituicao-conta-uma-vez.json|"
  "aprova-iops-no-incluso.json|"
  "aprova-secret-sem-replica.json|"
  "reprova-dois-rds.json|no máximo 1"
  "reprova-dois-lb.json|no máximo 1"
  "reprova-dois-servicos-ecs.json|no máximo 1"
  "reprova-tres-task-definitions.json|no máximo 2"
  "reprova-lb-network.json|load_balancer_type"
  "reprova-storage-io1.json|storage_type"
  "reprova-after-unknown-storage-type.json|storage_type"
  "reprova-iops-provisionado.json|iops"
  "reprova-secret-com-replica.json|replica"
  "reprova-monitoring-interval-60.json|monitoring_interval"
  "reprova-after-unknown-instance-class.json|instance_class"
  "reprova-after-unknown-com-valor-aparente.json|multi_az"
  "reprova-rds-deletion-protection.json|deletion_protection"
  "reprova-rds-sem-skip-final-snapshot.json|skip_final_snapshot"
  "reprova-rds-mantem-backups-automaticos.json|delete_automated_backups"
  "reprova-rds-exporta-logs.json|enabled_cloudwatch_logs_exports"
  "reprova-rds-sem-atributos-de-protecao.json|skip_final_snapshot"
  "reprova-alb-deletion-protection.json|enable_deletion_protection"
  "reprova-secret-janela-de-recuperacao.json|recovery_window_in_days"
  "reprova-cluster-container-insights-ligado.json|containerInsights"
  "reprova-cluster-sem-container-insights.json|containerInsights"
  "reprova-after-unknown-deletion-protection.json|deletion_protection"
  "reprova-after-unknown-logs-exportados.json|enabled_cloudwatch_logs_exports"
  "reprova-after-unknown-janela-de-recuperacao.json|recovery_window_in_days"
  "reprova-after-unknown-container-insights.json|containerInsights"
  "reprova-after-unknown-alb-deletion-protection.json|enable_deletion_protection"
  "aprova-default-security-group.json|"
  "reprova-dois-default-security-groups.json|no máximo 1"
)

falhas=0
printf '%-52s %-9s %-9s %s\n' CASO ESPERADO OBTIDO RESULTADO

for caso in "${casos[@]}"; do
  arquivo="${caso%%|*}"
  trecho="${caso#*|}"
  esperado="reprova"
  [[ "$arquivo" == aprova-* ]] && esperado="aprova"

  saida="$(bash "$script" < "$dir/$arquivo" 2>&1)"
  codigo=$?

  obtido="reprova"
  [[ $codigo -eq 0 ]] && obtido="aprova"
  motivo=""

  if [[ $codigo -eq 126 || $codigo -eq 127 ]]; then
    obtido="erro-exec"
    motivo="script não executável (código $codigo)"
  elif [[ "$esperado" == "aprova" && $codigo -ne 0 ]]; then
    motivo="deveria aprovar"
  elif [[ "$esperado" == "reprova" && $codigo -ne 1 ]]; then
    motivo="deveria sair com 1 (saiu com $codigo)"
  elif [[ "$esperado" == "reprova" && "$saida" != *"$trecho"* ]]; then
    motivo="mensagem sem o trecho '$trecho'"
  elif [[ "$saida" == *SEGREDO-CANARIO-123* ]]; then
    motivo="vazou o valor sentinela na saída"
  fi

  if [[ -n "$motivo" ]]; then
    printf '%-52s %-9s %-9s FALHOU: %s\n' "$arquivo" "$esperado" "$obtido" "$motivo"
    falhas=$((falhas + 1))
  else
    printf '%-52s %-9s %-9s ok\n' "$arquivo" "$esperado" "$obtido"
  fi
done

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de ${#casos[@]} casos falharam."
  exit 1
fi
echo "${#casos[@]} casos ok."
