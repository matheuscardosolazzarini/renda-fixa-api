#!/usr/bin/env bash
# Bootstrap da F11 (docs/SPEC-F11-EXECUCAO.md, "Bootstrap" e "Permissões da role do CI").
#
# Roda UMA vez, no AWS CloudShell da conta (usuário com permissão de administrador; o root com MFA, sem chave de
# acesso), e é idempotente: pode rodar de novo para corrigir um passo que falhou ou para atualizar as policies.
# Cria o que não cobra por hora e que o Terraform do ambiente efêmero não pode criar nem destruir:
#   - bucket do state: Block Public Access, SSE-S3, versionamento, expiração das versões antigas e TLS obrigatório;
#   - provedor OIDC do GitHub e a role renda-fixa-gha (sessão de 2 h; trust só do environment aws do repositório);
#   - a execution role renda-fixa-api-execution das tarefas ECS;
#   - as service-linked roles de ECS, Elastic Load Balancing e RDS, se faltarem;
#   - o budget mensal de US$ 5 (sem créditos) com alertas por e-mail em US$ 1, 3 e 5.
# Imprime só identificadores (bucket, ARNs, nome do budget): nenhum segredo existe aqui. Nada de chave de acesso.
#
# Os recursos do bootstrap NÃO levam a tag project=renda-fixa-api, que é a do ambiente efêmero e a que a verificação
# de zerado procura; usam stack=bootstrap e app=renda-fixa-api (contrato com scripts/verificar-zerado.sh).
#
# Uso:  bootstrap.sh --email voce@exemplo.com        (ou BUDGET_EMAIL=voce@exemplo.com bootstrap.sh)
#       bootstrap.sh --imprimir-policy-ci            (só imprime a policy da role do CI; não chama a AWS)
set -euo pipefail

export AWS_PAGER=""

# Tudo no mesmo lugar: a policy da role do CI limita as ações a esta região (aws:RequestedRegion).
REGION="us-east-1"
REPOSITORIO="matheuscardosolazzarini/renda-fixa-api"
AMBIENTE_GITHUB="aws"
APP="renda-fixa-api"
ROLE_CI="renda-fixa-gha"
ROLE_EXECUCAO="renda-fixa-api-execution"
FAMILIA_MIGRACAO="renda-fixa-migrator" # família da task definition da migração (modules/app)
BUDGET_NOME="renda-fixa-api-mensal"
BUDGET_LIMITE_USD="5"
OIDC_HOST="token.actions.githubusercontent.com"
LIMITE_POLICY_INLINE=10240 # caracteres, sem espaço: limite agregado das policies inline de uma role

die() {
  echo "ERRO $*" >&2
  exit 1
}

# ---- Policies (funções puras: só imprimem JSON, para poderem ser lidas e testadas sem a AWS) ----

politica_confianca_ci() { # conta
  cat << EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Federated": "arn:aws:iam::$1:oidc-provider/$OIDC_HOST" },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "$OIDC_HOST:aud": "sts.amazonaws.com",
          "$OIDC_HOST:sub": "repo:$REPOSITORIO:environment:$AMBIENTE_GITHUB"
        }
      }
    }
  ]
}
EOF
}

politica_confianca_execucao() { # conta
  # aws:SourceAccount e aws:SourceArn: prevenção de "confused deputy" recomendada para a role de tarefas ECS.
  cat << EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Service": "ecs-tasks.amazonaws.com" },
      "Action": "sts:AssumeRole",
      "Condition": {
        "StringEquals": { "aws:SourceAccount": "$1" },
        "ArnLike": { "aws:SourceArn": "arn:aws:ecs:$REGION:$1:*" }
      }
    }
  ]
}
EOF
}

politica_execucao() { # conta
  # Só escrever logs no log group do projeto e ler os secrets renda-fixa-api/* (opção B da D6).
  cat << EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "EscreverLogsDoProjeto",
      "Effect": "Allow",
      "Action": ["logs:CreateLogStream", "logs:PutLogEvents"],
      "Resource": "arn:aws:logs:$REGION:$1:log-group:/ecs/$APP:*"
    },
    {
      "Sid": "LerSecretsDoProjeto",
      "Effect": "Allow",
      "Action": "secretsmanager:GetSecretValue",
      "Resource": "arn:aws:secretsmanager:$REGION:$1:secret:$APP/*"
    }
  ]
}
EOF
}

politica_ci() { # conta bucket
  # Derivada do que o Terraform (provider aws 6.73.0) e os jobs de infra-aws.yml realmente chamam: as ações vêm de um
  # plan/apply/destroy contra o Floci com TF_LOG=DEBUG, somadas ao que o provider faz em update/tag e à CLI dos jobs.
  # Escrita regional limitada por aws:RequestedRegion; os Deny (camada 6b) valem em qualquer região.
  # RunTask e StopTask não usam o curinga: RunTask autoriza o ARN da task definition (task-definition/<família>:<revisão>)
  # e aceita a chave ecs:cluster (tipo ARN, daí ArnEquals); StopTask autoriza o ARN da tarefa (task/<cluster>/<id>).
  # Fonte: AWS Service Authorization Reference, "Actions, resources, and condition keys for Amazon Elastic Container
  # Service". O Terraform não chama RunTask nem StopTask: só o workflow (subir) e scripts/parar-migracao.sh, sempre
  # no cluster renda-fixa-api e na família renda-fixa-migrator.
  local conta="$1" bucket="$2" condicao_regiao
  condicao_regiao="\"Condition\": { \"StringEquals\": { \"aws:RequestedRegion\": \"$REGION\" } }"
  cat << EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "StateListarBucket",
      "Effect": "Allow",
      "Action": "s3:ListBucket",
      "Resource": "arn:aws:s3:::$bucket"
    },
    {
      "Sid": "StateObjetos",
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": [
        "arn:aws:s3:::$bucket/$APP/aws.tfstate",
        "arn:aws:s3:::$bucket/$APP/aws.tfstate.tflock"
      ]
    },
    {
      "Sid": "PassarSoAExecutionRole",
      "Effect": "Allow",
      "Action": "iam:PassRole",
      "Resource": "arn:aws:iam::$conta:role/$ROLE_EXECUCAO",
      "Condition": { "StringEquals": { "iam:PassedToService": "ecs-tasks.amazonaws.com" } }
    },
    {
      "Sid": "Ec2RedeEscrita",
      "Effect": "Allow",
      "Action": [
        "ec2:CreateVpc", "ec2:DeleteVpc", "ec2:ModifyVpcAttribute",
        "ec2:CreateSubnet", "ec2:DeleteSubnet", "ec2:ModifySubnetAttribute",
        "ec2:CreateInternetGateway", "ec2:DeleteInternetGateway", "ec2:AttachInternetGateway", "ec2:DetachInternetGateway",
        "ec2:CreateRouteTable", "ec2:DeleteRouteTable", "ec2:CreateRoute", "ec2:DeleteRoute", "ec2:ReplaceRoute",
        "ec2:AssociateRouteTable", "ec2:DisassociateRouteTable",
        "ec2:CreateSecurityGroup", "ec2:DeleteSecurityGroup",
        "ec2:AuthorizeSecurityGroupIngress", "ec2:AuthorizeSecurityGroupEgress",
        "ec2:RevokeSecurityGroupIngress", "ec2:RevokeSecurityGroupEgress", "ec2:ModifySecurityGroupRules",
        "ec2:UpdateSecurityGroupRuleDescriptionsIngress", "ec2:UpdateSecurityGroupRuleDescriptionsEgress",
        "ec2:CreateTags", "ec2:DeleteTags"
      ],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "Ec2LeituraEVerificacao",
      "Effect": "Allow",
      "Action": "ec2:Describe*",
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "ElbEscrita",
      "Effect": "Allow",
      "Action": [
        "elasticloadbalancing:CreateLoadBalancer", "elasticloadbalancing:DeleteLoadBalancer",
        "elasticloadbalancing:ModifyLoadBalancerAttributes", "elasticloadbalancing:SetSecurityGroups", "elasticloadbalancing:SetSubnets",
        "elasticloadbalancing:CreateTargetGroup", "elasticloadbalancing:DeleteTargetGroup",
        "elasticloadbalancing:ModifyTargetGroup", "elasticloadbalancing:ModifyTargetGroupAttributes",
        "elasticloadbalancing:CreateListener", "elasticloadbalancing:DeleteListener",
        "elasticloadbalancing:ModifyListener", "elasticloadbalancing:ModifyListenerAttributes",
        "elasticloadbalancing:AddTags", "elasticloadbalancing:RemoveTags"
      ],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "ElbLeituraEVerificacao",
      "Effect": "Allow",
      "Action": [
        "elasticloadbalancing:DescribeLoadBalancers", "elasticloadbalancing:DescribeLoadBalancerAttributes",
        "elasticloadbalancing:DescribeTargetGroups", "elasticloadbalancing:DescribeTargetGroupAttributes",
        "elasticloadbalancing:DescribeListeners", "elasticloadbalancing:DescribeListenerAttributes",
        "elasticloadbalancing:DescribeTags", "elasticloadbalancing:DescribeTargetHealth",
        "elasticloadbalancing:DescribeCapacityReservation"
      ],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "EcsEscrita",
      "Effect": "Allow",
      "Action": [
        "ecs:CreateCluster", "ecs:DeleteCluster", "ecs:UpdateCluster", "ecs:UpdateClusterSettings",
        "ecs:RegisterTaskDefinition", "ecs:DeregisterTaskDefinition",
        "ecs:CreateService", "ecs:UpdateService", "ecs:DeleteService",
        "ecs:TagResource", "ecs:UntagResource"
      ],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "EcsRunTaskSoDaMigracao",
      "Effect": "Allow",
      "Action": "ecs:RunTask",
      "Resource": "arn:aws:ecs:$REGION:$conta:task-definition/$FAMILIA_MIGRACAO:*",
      "Condition": { "ArnEquals": { "ecs:cluster": "arn:aws:ecs:$REGION:$conta:cluster/$APP" } }
    },
    {
      "Sid": "EcsStopTaskSoDoCluster",
      "Effect": "Allow",
      "Action": "ecs:StopTask",
      "Resource": "arn:aws:ecs:$REGION:$conta:task/$APP/*"
    },
    {
      "Sid": "EcsLeituraEVerificacao",
      "Effect": "Allow",
      "Action": [
        "ecs:DescribeClusters", "ecs:DescribeServices", "ecs:DescribeTaskDefinition", "ecs:DescribeTasks",
        "ecs:ListClusters", "ecs:ListServices", "ecs:ListTasks", "ecs:ListTagsForResource"
      ],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "RdsEscrita",
      "Effect": "Allow",
      "Action": [
        "rds:CreateDBInstance", "rds:DeleteDBInstance", "rds:ModifyDBInstance",
        "rds:CreateDBSubnetGroup", "rds:DeleteDBSubnetGroup", "rds:ModifyDBSubnetGroup",
        "rds:AddTagsToResource", "rds:RemoveTagsFromResource"
      ],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "RdsLeituraEVerificacao",
      "Effect": "Allow",
      "Action": [
        "rds:DescribeDBInstances", "rds:DescribeDBSubnetGroups", "rds:DescribeDBSnapshots",
        "rds:DescribeDBInstanceAutomatedBackups", "rds:ListTagsForResource"
      ],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "LogsDoProjeto",
      "Effect": "Allow",
      "Action": [
        "logs:CreateLogGroup", "logs:DeleteLogGroup", "logs:PutRetentionPolicy", "logs:DeleteRetentionPolicy",
        "logs:TagResource", "logs:UntagResource", "logs:ListTagsForResource",
        "logs:DescribeLogStreams", "logs:FilterLogEvents", "logs:GetLogEvents"
      ],
      "Resource": [
        "arn:aws:logs:$REGION:$conta:log-group:/ecs/$APP",
        "arn:aws:logs:$REGION:$conta:log-group:/ecs/$APP:*"
      ]
    },
    {
      "Sid": "LogsListarGrupos",
      "Effect": "Allow",
      "Action": "logs:DescribeLogGroups",
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "SecretsDoProjeto",
      "Effect": "Allow",
      "Action": [
        "secretsmanager:CreateSecret", "secretsmanager:DeleteSecret", "secretsmanager:UpdateSecret",
        "secretsmanager:DescribeSecret", "secretsmanager:GetResourcePolicy",
        "secretsmanager:PutSecretValue", "secretsmanager:GetSecretValue", "secretsmanager:ListSecretVersionIds",
        "secretsmanager:UpdateSecretVersionStage", "secretsmanager:TagResource", "secretsmanager:UntagResource"
      ],
      "Resource": "arn:aws:secretsmanager:$REGION:$conta:secret:$APP/*"
    },
    {
      "Sid": "SecretsSemRecursoEVerificacao",
      "Effect": "Allow",
      "Action": ["secretsmanager:GetRandomPassword", "secretsmanager:ListSecrets"],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "VerificacaoKmsEtags",
      "Effect": "Allow",
      "Action": ["kms:ListKeys", "kms:DescribeKey", "tag:GetResources"],
      "Resource": "*",
      $condicao_regiao
    },
    {
      "Sid": "NegarServicosCarosEmQualquerRegiao",
      "Effect": "Deny",
      "Action": [
        "ec2:CreateNatGateway", "ec2:AllocateAddress", "ec2:RunInstances", "ec2:CreateVpcEndpoint",
        "ec2:CreateTransitGateway", "kms:CreateKey", "route53:CreateHostedZone"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NegarRdsForaDeDbT4gMicro",
      "Effect": "Deny",
      "Action": [
        "rds:CreateDBInstance", "rds:ModifyDBInstance", "rds:RestoreDBInstanceFromDBSnapshot",
        "rds:RestoreDBInstanceFromS3", "rds:RestoreDBInstanceToPointInTime", "rds:CreateDBInstanceReadReplica"
      ],
      "Resource": "arn:aws:rds:*:*:db:*",
      "Condition": {
        "StringNotEquals": { "rds:DatabaseClass": "db.t4g.micro" },
        "Null": { "rds:DatabaseClass": "false" }
      }
    },
    {
      "Sid": "NegarRdsMultiAz",
      "Effect": "Deny",
      "Action": [
        "rds:CreateDBInstance", "rds:ModifyDBInstance", "rds:RestoreDBInstanceFromDBSnapshot",
        "rds:RestoreDBInstanceFromS3", "rds:RestoreDBInstanceToPointInTime", "rds:CreateDBInstanceReadReplica"
      ],
      "Resource": "arn:aws:rds:*:*:db:*",
      "Condition": { "Bool": { "rds:MultiAz": "true" } }
    }
  ]
}
EOF
}

politica_bucket_state() { # bucket
  cat << EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "NegarSemTls",
      "Effect": "Deny",
      "Principal": "*",
      "Action": "s3:*",
      "Resource": ["arn:aws:s3:::$1", "arn:aws:s3:::$1/*"],
      "Condition": { "Bool": { "aws:SecureTransport": "false" } }
    },
    {
      "Sid": "NegarTlsAntigo",
      "Effect": "Deny",
      "Principal": "*",
      "Action": "s3:*",
      "Resource": ["arn:aws:s3:::$1", "arn:aws:s3:::$1/*"],
      "Condition": { "NumericLessThan": { "s3:TlsVersion": "1.2" } }
    }
  ]
}
EOF
}

lifecycle_bucket_state() {
  # Versões antigas do state somem em 7 dias; o marcador de exclusão do .tflock (que o Terraform apaga a cada
  # operação) e uploads incompletos também, para o bucket não acumular lixo.
  cat << 'EOF'
{
  "Rules": [
    {
      "ID": "expirar-versoes-antigas-do-state",
      "Status": "Enabled",
      "Filter": {},
      "NoncurrentVersionExpiration": { "NoncurrentDays": 7 },
      "Expiration": { "ExpiredObjectDeleteMarker": true },
      "AbortIncompleteMultipartUpload": { "DaysAfterInitiation": 1 }
    }
  ]
}
EOF
}

orcamento() {
  cat << EOF
{
  "BudgetName": "$BUDGET_NOME",
  "BudgetLimit": { "Amount": "$BUDGET_LIMITE_USD", "Unit": "USD" },
  "TimeUnit": "MONTHLY",
  "BudgetType": "COST",
  "CostTypes": { "IncludeCredit": false }
}
EOF
}

notificacao() { # limite
  cat << EOF
{ "NotificationType": "ACTUAL", "ComparisonOperator": "GREATER_THAN", "Threshold": $1, "ThresholdType": "ABSOLUTE_VALUE" }
EOF
}

assinantes() { # email
  cat << EOF
[ { "SubscriptionType": "EMAIL", "Address": "$1" } ]
EOF
}

# ---- Passos ----

# Código de erro da AWS (ex.: NoSuchEntity) da última chamada que falhou; vazio se não houver.
codigo_erro() { # arquivo-de-stderr
  sed -n 's/.*An error occurred (\([A-Za-z0-9._]*\)).*/\1/p' "$1" | head -n 1
}

verificar_tamanho_policy() { # nome json
  local tamanho
  tamanho="$(printf '%s' "$2" | tr -d ' \n\t\r' | wc -c | tr -d ' ')"
  if (( tamanho > LIMITE_POLICY_INLINE )); then
    die "a policy $1 tem $tamanho caracteres (limite $LIMITE_POLICY_INLINE das policies inline de uma role)"
  fi
  echo "Policy $1: $tamanho de $LIMITE_POLICY_INLINE caracteres."
}

criar_bucket_state() { # conta bucket
  local conta="$1" bucket="$2" erro_tmp
  erro_tmp="$(mktemp)"
  if aws s3api head-bucket --bucket "$bucket" --expected-bucket-owner "$conta" > /dev/null 2> "$erro_tmp"; then
    echo "Bucket do state já existe: $bucket"
  else
    # us-east-1 não aceita LocationConstraint. BucketAlreadyOwnedByYou é sucesso; BucketAlreadyExists é nome tomado.
    if ! aws s3api create-bucket --bucket "$bucket" --region "$REGION" > /dev/null 2> "$erro_tmp"; then
      case "$(codigo_erro "$erro_tmp")" in
        BucketAlreadyOwnedByYou) ;;
        *) rm -f "$erro_tmp"; die "não foi possível criar o bucket $bucket (nome já usado por outra conta ou sem permissão)" ;;
      esac
    fi
    echo "Bucket do state criado: $bucket"
  fi
  rm -f "$erro_tmp"

  # --expected-bucket-owner em tudo: se o nome pertencesse a outra conta, nenhuma das chamadas abaixo passaria.
  local dono=(--bucket "$bucket" --expected-bucket-owner "$conta")
  aws s3api put-public-access-block "${dono[@]}" \
    --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true > /dev/null
  aws s3api put-bucket-ownership-controls "${dono[@]}" --ownership-controls 'Rules=[{ObjectOwnership=BucketOwnerEnforced}]' > /dev/null
  # SSE-S3 (AES256), não KMS de cliente: chave de cliente cobra e a trava de custo a proíbe.
  aws s3api put-bucket-encryption "${dono[@]}" \
    --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}' > /dev/null
  aws s3api put-bucket-versioning "${dono[@]}" --versioning-configuration Status=Enabled > /dev/null
  aws s3api put-bucket-lifecycle-configuration "${dono[@]}" --lifecycle-configuration "$(lifecycle_bucket_state)" > /dev/null
  aws s3api put-bucket-policy "${dono[@]}" --policy "$(politica_bucket_state "$bucket")" > /dev/null
  aws s3api put-bucket-tagging "${dono[@]}" --tagging "TagSet=[{Key=stack,Value=bootstrap},{Key=app,Value=$APP}]" > /dev/null
}

garantir_provedor_oidc() { # conta
  local conta="$1" arn clientes
  arn="arn:aws:iam::$conta:oidc-provider/$OIDC_HOST"
  if aws iam get-open-id-connect-provider --open-id-connect-provider-arn "$arn" > /dev/null 2>&1; then
    echo "Provedor OIDC do GitHub já existe."
    clientes="$(aws iam get-open-id-connect-provider --open-id-connect-provider-arn "$arn" --query 'ClientIDList' --output text)"
    if [[ " $clientes " != *" sts.amazonaws.com "* ]]; then
      aws iam add-client-id-to-open-id-connect-provider --open-id-connect-provider-arn "$arn" --client-id sts.amazonaws.com
    fi
  else
    # A lista de thumbprints é opcional: a AWS valida o certificado do GitHub com a própria lista de CAs confiáveis.
    aws iam create-open-id-connect-provider --url "https://$OIDC_HOST" --client-id-list sts.amazonaws.com \
      --tags "Key=stack,Value=bootstrap" "Key=app,Value=$APP" > /dev/null
    echo "Provedor OIDC do GitHub criado."
  fi
}

# Cria a role ou, se existir, atualiza a trust policy e a duração máxima da sessão.
garantir_role() { # nome trust-json duracao descricao
  local nome="$1" trust="$2" duracao="$3" descricao="$4" erro_tmp
  erro_tmp="$(mktemp)"
  if aws iam get-role --role-name "$nome" > /dev/null 2> "$erro_tmp"; then
    aws iam update-assume-role-policy --role-name "$nome" --policy-document "$trust"
    aws iam update-role --role-name "$nome" --max-session-duration "$duracao" --description "$descricao" > /dev/null
    echo "Role atualizada: $nome"
  elif [[ "$(codigo_erro "$erro_tmp")" == "NoSuchEntity" ]]; then
    aws iam create-role --role-name "$nome" --assume-role-policy-document "$trust" --max-session-duration "$duracao" \
      --description "$descricao" --tags "Key=stack,Value=bootstrap" "Key=app,Value=$APP" > /dev/null
    echo "Role criada: $nome"
  else
    rm -f "$erro_tmp"
    die "não foi possível consultar a role $nome"
  fi
  rm -f "$erro_tmp"
}

garantir_service_linked_role() { # servico nome-da-role
  local servico="$1" nome="$2" erro_tmp
  erro_tmp="$(mktemp)"
  if aws iam get-role --role-name "$nome" > /dev/null 2> "$erro_tmp"; then
    echo "Service-linked role já existe: $nome"
  elif [[ "$(codigo_erro "$erro_tmp")" == "NoSuchEntity" ]]; then
    aws iam create-service-linked-role --aws-service-name "$servico" > /dev/null
    echo "Service-linked role criada: $nome"
  else
    rm -f "$erro_tmp"
    die "não foi possível consultar a role $nome"
  fi
  rm -f "$erro_tmp"
}

garantir_orcamento() { # conta email
  local conta="$1" email="$2" limite
  if aws budgets describe-budget --account-id "$conta" --budget-name "$BUDGET_NOME" > /dev/null 2>&1; then
    aws budgets update-budget --account-id "$conta" --new-budget "$(orcamento)"
    echo "Budget atualizado: $BUDGET_NOME"
  else
    aws budgets create-budget --account-id "$conta" --budget "$(orcamento)"
    echo "Budget criado: $BUDGET_NOME"
  fi
  # Alertas recriados a cada execução: é o que atualiza o e-mail se ele mudou. A exclusão falha se o alerta ainda
  # não existe, e isso é esperado na primeira vez; a criação logo em seguida falha de verdade se algo estiver errado.
  for limite in 1 3 5; do
    aws budgets delete-notification --account-id "$conta" --budget-name "$BUDGET_NOME" \
      --notification "$(notificacao "$limite")" > /dev/null 2>&1 || true
    aws budgets create-notification --account-id "$conta" --budget-name "$BUDGET_NOME" \
      --notification "$(notificacao "$limite")" --subscribers "$(assinantes "$email")"
  done
  echo "Alertas por e-mail em US\$ 1, 3 e 5 (valor bruto, sem créditos)."
}

principal() {
  local email="${BUDGET_EMAIL:-}" conta bucket politica_ci politica_exec

  while (( $# > 0 )); do
    case "$1" in
      --email)
        (( $# >= 2 )) || die "--email exige um endereço"
        email="$2"
        shift 2
        ;;
      --imprimir-policy-ci)
        politica_ci "123456789012" "renda-fixa-api-tfstate-123456789012"
        return 0
        ;;
      *)
        die "argumento desconhecido: $1"
        ;;
    esac
  done

  # Validação estrita: o e-mail entra em JSON, então nada de aspas ou espaços.
  if [[ ! "$email" =~ ^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$ ]]; then
    die "informe o e-mail dos alertas do budget com --email ou BUDGET_EMAIL (ele não fica no script)"
  fi
  command -v aws > /dev/null 2>&1 || die "aws CLI não encontrada (rode no CloudShell)"

  export AWS_DEFAULT_REGION="$REGION" AWS_REGION="$REGION"
  conta="$(aws sts get-caller-identity --query Account --output text)"
  [[ "$conta" =~ ^[0-9]{12}$ ]] || die "não foi possível obter o id da conta"
  bucket="$APP-tfstate-$conta"

  politica_ci="$(politica_ci "$conta" "$bucket")"
  politica_exec="$(politica_execucao "$conta")"
  verificar_tamanho_policy "$ROLE_CI" "$politica_ci"
  verificar_tamanho_policy "$ROLE_EXECUCAO" "$politica_exec"

  criar_bucket_state "$conta" "$bucket"
  garantir_provedor_oidc "$conta"

  garantir_role "$ROLE_CI" "$(politica_confianca_ci "$conta")" 7200 "OIDC do GitHub Actions (renda-fixa-api, environment aws)"
  aws iam put-role-policy --role-name "$ROLE_CI" --policy-name "$ROLE_CI-ambiente-efemero" --policy-document "$politica_ci"
  # Qualquer policy gerenciada anexada à mão ampliaria a role fora desta lista enumerada.
  if [[ -n "$(aws iam list-attached-role-policies --role-name "$ROLE_CI" --query 'AttachedPolicies[].PolicyName' --output text)" ]]; then
    echo "AVISO a role $ROLE_CI tem policy gerenciada anexada: remova, a policy dela deve ser só a inline" >&2
  fi

  garantir_role "$ROLE_EXECUCAO" "$(politica_confianca_execucao "$conta")" 3600 "Execution role das tarefas ECS (renda-fixa-api)"
  aws iam put-role-policy --role-name "$ROLE_EXECUCAO" --policy-name "$ROLE_EXECUCAO-logs-e-secrets" --policy-document "$politica_exec"

  garantir_service_linked_role ecs.amazonaws.com AWSServiceRoleForECS
  garantir_service_linked_role elasticloadbalancing.amazonaws.com AWSServiceRoleForElasticLoadBalancing
  garantir_service_linked_role rds.amazonaws.com AWSServiceRoleForRDS

  garantir_orcamento "$conta" "$email"

  cat << EOF

Bootstrap concluído. Identificadores para o GitHub:
  AWS_ROLE_ARN    = arn:aws:iam::$conta:role/$ROLE_CI
  AWS_REGION      = $REGION
  TF_STATE_BUCKET = $bucket
Execution role das tarefas: arn:aws:iam::$conta:role/$ROLE_EXECUCAO
Budget: $BUDGET_NOME (US\$ $BUDGET_LIMITE_USD por mês, sem créditos)
Confirme que o e-mail dos alertas recebeu (e aceite) qualquer mensagem de inscrição do AWS Budgets.
EOF
}

# Permite `source bootstrap.sh` para chamar as funções de policy sem executar o bootstrap.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  principal "$@"
fi
