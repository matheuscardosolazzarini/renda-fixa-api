#!/usr/bin/env bash
# Para as tarefas avulsas de migração (família renda-fixa-migrator) que ainda estejam ativas no cluster.
# Uma tarefa solta fora do state impede o destroy do cluster e custa por hora; os jobs que destroem o
# ambiente (destruir, limpar, agendado) chamam este script antes do `terraform destroy`.
#
# Uso: scripts/parar-migracao.sh [CLUSTER]   (padrão: renda-fixa-api; região e credencial vêm do ambiente da CLI)
# Saída: 0 = nada ativo ou tudo parado (cluster inexistente também é 0); 1 = erro de chamada.
set -euo pipefail

export AWS_PAGER=""
cluster="${1:-renda-fixa-api}"
familia="renda-fixa-migrator"
erro_tmp="$(mktemp)"
trap 'rm -f "$erro_tmp"' EXIT

# Causa da falha da última chamada, lida de $erro_tmp. Rede, timeout e endpoint inalcançável não trazem código da
# AWS: sem esta mensagem o log mostraria só "ERRO ...:" e ninguém saberia o que investigar.
causa_da_falha() {
  local codigo
  codigo="$(sed -n 's/.*An error occurred (\([A-Za-z0-9._]*\)).*/\1/p' "$erro_tmp" | head -n 1)"
  if [[ "$codigo" == "NoRegion" ]]; then
    echo "região não definida (NoRegion): defina AWS_REGION ou AWS_DEFAULT_REGION"
  else
    echo "${codigo:-falha sem código da AWS (rede, credencial, timeout ou região)}"
  fi
}

if ! tarefas="$(aws ecs list-tasks --cluster "$cluster" --family "$familia" --desired-status RUNNING \
  --query 'taskArns[]' --output text 2> "$erro_tmp")"; then
  # Sem cluster não há tarefa: o ambiente já foi destruído (ou nunca existiu).
  if grep -q 'ClusterNotFoundException' "$erro_tmp"; then
    echo "Cluster $cluster não existe: nenhuma tarefa de migração a parar."
    exit 0
  fi
  echo "ERRO ecs list-tasks: $(causa_da_falha)" >&2
  exit 1
fi

if [[ -z "$tarefas" || "$tarefas" == "None" ]]; then
  echo "Nenhuma tarefa de migração ativa no cluster $cluster."
  exit 0
fi

falhas=0
for tarefa in $tarefas; do
  if aws ecs stop-task --cluster "$cluster" --task "$tarefa" --reason "infra-aws: limpeza da migração" > /dev/null 2> "$erro_tmp"; then
    echo "Tarefa de migração parada: $tarefa"
  else
    echo "ERRO ecs stop-task $tarefa: $(causa_da_falha)" >&2
    falhas=$((falhas + 1))
  fi
done

if [[ $falhas -gt 0 ]]; then
  exit 1
fi
