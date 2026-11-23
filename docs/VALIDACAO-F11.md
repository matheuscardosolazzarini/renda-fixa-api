# Validação da F11 — Nuvem na AWS (parte local, P1 a P7)

Este arquivo tem duas verificações independentes da parte local da F11 e a conferência da rodada 3 de correção. A
**reverificação de 20/11/2026 (pós-QA)**, abaixo, é a vigente: o veredito, a tabela de critérios e as contagens dela
substituem os da verificação anterior. As contagens de `terraform test` e das suítes de shell estão atualizadas pela
seção "Rodada 3", logo a seguir. A **verificação de 19/11/2026** fica mais abaixo, inteira, como histórico, com marcas
`[SUBSTITUÍDO ...]` no que perdeu a validade.

# Correção de segurança (Trivy e Checkov): conferência do coordenador (23/11/2026)

O Matheus aprovou em 21/11/2026 três correções, cada uma com o teste escrito antes. O detalhe e a evidência sobre o
DNS da VPC estão em `docs/SPEC-F11-EXECUCAO.md`, seção "Correção de segurança (Trivy e Checkov)". As correções:
- **saída do SG das tarefas** (AWS-0104): só TCP 443 para `0.0.0.0/0` e TCP 5432 para o SG do RDS;
- **`drop_invalid_header_fields = true` no ALB** (AWS-0052, CKV_AWS_131);
- **`aws_default_security_group` sem regras** (CKV2_AWS_12).

**Conferência.** O coordenador rodou tudo sem passar pelo relatório do implementador, no mesmo ambiente da rodada
3: `qa11-runner:tmp`, cópia só leitura, Floci descartável, `CI=true` e Terraform 1.17.0.

| Verificação | Resultado |
|---|---|
| `terraform fmt -check -recursive`, `init -backend=false` | saída 0 |
| `terraform test` | **57 passed, 0 failed** (eram 55). Runs novos: a saída da tarefa só em 443 e 5432, o SG padrão sem regras e o ALB com `drop_invalid_header_fields` |
| Suítes de shell, todas com saída 0 e nenhum caso pulado | trava-custo 69, verificar-zerado 19, bloquear-recriacao 15, bootstrap 8, parar-migracao 11, agendado 34, lifecycle 19, workflow 9, readme 21, seguranca 9 (nova). **Total: 214** (eram 201) |
| `verificar-sem-prevent-destroy.sh` na árvore | `APROVADO` em 15 arquivos `.tf` |
| Ciclo do `validar` no Floci | `plan` 0 · trava `APROVADO 29 itens` (o SG padrão entrou na lista positiva) · bloqueio de recriação 0 · `apply` 29 criados (96 s) · verificação com o ambiente no ar: `REPROVADA (23 achado(s))` · `destroy` 29 destruídos (117 s) · artefatos D1 e D2 removidos · verificação depois: `APROVADA`, saída 0 · state com 0 recursos |
| Trivy 0.76.2, a mesma imagem do relatório do Matheus, sem rede e com `--skip-check-update` | O AWS-0052 sumiu. O AWS-0104 continua na regra `task_https` (443 para a internet), porque o Trivy aponta qualquer saída para `0.0.0.0/0`. Os outros 9 achados ficaram iguais. Os 10 restantes são riscos aceitos em `docs/QA-F11.md` |

- **Linters:** shellcheck, actionlint e zizmor passaram segundo o relatório do implementador. O YAML só ganhou
  `tests/seguranca` no `validar` e na lista do `shellcheck`. A conferência acima não repetiu os linters.
- **Critérios:** a tabela da reverificação não muda. O critério 1 fica reforçado pelos 57 runs e o 3 pelos 69 casos.
- **Pendente do P8**, registrado na spec: provar ao vivo que, com a saída restrita, a tarefa:
  - puxa a imagem do GHCR;
  - lê o secret no Secrets Manager;
  - escreve no CloudWatch Logs;
  - fala com o RDS na 5432 e com a API do SGS do Banco Central (critério 11);
  - roda a migração.

  O Floci não prova regra de security group.
- **Limpeza:** Floci, volume de cache e volume anônimo do Floci foram removidos. Os filhos `floci-aws-*` saíram com o
  `destroy`. `fixedincome-api` e `fixedincome-db` seguem no ar.

# Rodada 3 de correção: conferência do coordenador (20/11/2026)

O Matheus aprovou em 20/11/2026 uma terceira rodada curta, sem QA novo e com teste antes de cada correção. Ela
trouxe:
- a decisão sobre a tag futura demais;
- as sugestões das revisões e do QA;
- os riscos R-B1 (a trava de custo olha proteções de deleção, exportação de logs e Container Insights) e R-B3 (o
  texto do README sobre o alcance da verificação de zerado).

O detalhe está em `docs/SPEC-F11-EXECUCAO.md`, "Rodada 3 de correção".

**Conferência.** O coordenador rodou, sem passar pelo relatório do implementador, as suítes afetadas, o
`terraform test` e o ciclo do `validar` no Floci. O contêiner foi o `qa11-runner:tmp`, com o repositório montado só
leitura e uma cópia de `infra/`, `.github/` e `README.md`. O Floci era descartável, com `CI=true` e Terraform 1.17.0.

| Verificação | Resultado |
|---|---|
| `terraform fmt -check -recursive` | saída 0 |
| `terraform init -backend=false` e `validate` | `Success! The configuration is valid.` |
| `terraform test` | **55 passed, 0 failed** (era 54; o run novo é `o_cluster_leva_a_tag_expires_at_da_variavel_da_raiz`) |
| Suítes de shell, todas com saída 0 e nenhum caso pulado | trava-custo 67, verificar-zerado 19, bloquear-recriacao 15, bootstrap 6, parar-migracao 11, agendado 34, lifecycle 19, workflow 9, readme 21. **Total: 201** (era 173) |
| `verificar-sem-prevent-destroy.sh` na árvore | `APROVADO: nenhum prevent_destroy nem destroy = false em 15 arquivo(s) .tf.` |
| Ciclo do `validar` no Floci (os passos do YAML, e o passo de artefatos extraído do próprio YAML) | `plan` 0 · trava `APROVADO 27 itens` (já com as checagens novas do R-B1) · bloqueio de recriação 0 · `apply` 27 criados (93 s) · verificação com o ambiente no ar: `REPROVADA (22 achado(s))` · `destroy` 27 destruídos (117 s) · artefatos D1 e D2 removidos · verificação depois: `APROVADA`, saída 0 · state com 0 recursos |

- **O que não mudou nesta rodada:** nenhum YAML de workflow, nada de .NET, `Dockerfile`, `Dockerfile.migrator`,
  `ci.yml` ou `mutation.yml`. Por isso valem os resultados de 20/11/2026 da reverificação abaixo para estes pontos:
  - actionlint e zizmor (critério 8);
  - `dotnet build` com 0 warnings e `dotnet test` com 712 (critério 9);
  - o bundle (critério 5).
- **Shellcheck:** passou nos scripts e nos `rodar.sh` (relatório do implementador). A conferência acima não o
  repetiu.
- **Critérios:** a tabela da reverificação não muda. Os critérios 1 e 3 a 9 estão atendidos, com o 1, o 3 e o 4
  reforçados pelas contagens acima. O critério 2 continua dependendo do PR, e os critérios 10 a 16 do P8.
- **Limpeza:** Floci, runner, volume de cache e o volume anônimo do Floci foram removidos. Os filhos `floci-aws-*`
  saíram com o `destroy`. `fixedincome-api` e `fixedincome-db` seguem no ar (`/health` = 200).

# Reverificação de 20/11/2026 (pós-QA)

Cada afirmação desta seção vem de comando rodado ou arquivo lido nesta reverificação; nada veio de relatório de
implementador. Nenhum código foi alterado, nada foi commitado nem adicionado ao índice, e nada foi escrito na AWS
real nem no GitHub. O único arquivo alterado é este.

- Data: 2026-11-20
- Commit-base: `f141590` (HEAD da branch `f11-nuvem-aws`, o mesmo da verificação anterior). A árvore tem alterações
  não commitadas, esperado porque a fase ainda não foi commitada: 8 arquivos modificados (`.claude/commands/executar-fase.md`,
  `.github/dependabot.yml`, `.github/workflows/release.yml`, `.gitignore`, `CLAUDE.md`, `README.md`,
  `docs/ESPECIFICACAO.md`, `docs/ROADMAP.md`) e 7 entradas novas não rastreadas (`.github/workflows/infra-aws.yml`,
  `Dockerfile.migrator`, `docs/QA-F11-ROTEIRO.md`, `docs/QA-F11.md`, `docs/SPEC-F11-EXECUCAO.md`,
  `docs/VALIDACAO-F11.md`, `infra/`). `git diff --cached` vazio, `git stash list` vazio.
- Fontes: `docs/SPEC-F11-EXECUCAO.md` (critérios 1 a 16, "Decisões de implementação" e "Rodada 2 de correção"),
  `docs/QA-F11.md` (só para saber o que a rodada 2 corrigiu), `CLAUDE.md`, `.claude/agents/verificador-de-fase.md`.
- Ambiente: Windows 11, .NET SDK 8, Docker Desktop (Linux/WSL2). Terraform 1.17.0, jq 1.7.1, aws-cli 2.41.1 e
  bash 5.2 na imagem local `qa11-runner:tmp`, com o repositório montado só leitura e o trabalho numa cópia dentro do
  contêiner (`infra/aws` sem `.terraform/`, `.github/workflows` e `README.md`); provider `hashicorp/aws` 6.73.0
  baixado de novo num volume de cache descartável. Linters pelas imagens fixadas por digest (abaixo).
- Fora do alcance, e não verificado: P0 (conta AWS) e P8 (validação ao vivo), que são com o Matheus.

## Reverificação pós-QA: o que mudou desde a verificação anterior

Conferido por `find -newer docs/VALIDACAO-F11.md` (a verificação anterior foi gravada em 19/11 às 21:21) e por leitura.
Arquivos alterados depois dela, fora este documento: `.github/workflows/infra-aws.yml`, `.github/workflows/release.yml`,
`README.md`, `docs/SPEC-F11-EXECUCAO.md`, `docs/QA-F11.md`, `docs/QA-F11-ROTEIRO.md`,
`infra/aws/scripts/parar-migracao.sh` e os novos `infra/aws/scripts/{destruir-se-vencido.sh,verificar-sem-prevent-destroy.sh,expires-at-do-cluster.jq}`,
`infra/aws/tests/{parar-migracao,agendado,lifecycle,workflow,readme}/rodar.sh`.

**Não mudou nada em:** `src/`, `tests/` (.NET), `Dockerfile`, `Dockerfile.migrator`, `ci.yml`, `mutation.yml`,
`.csproj`, `.gitignore`, nenhum `.tf`, nenhum `*.tftest.hcl`, nenhum `*.tfvars`, `trava-custo.sh`,
`bloquear-recriacao.sh`, `verificar-zerado.sh`, `verificar-zerado-apos-destroy.sh`, `gerar-floci-override.sh`,
`bootstrap.sh` nem as fixtures da trava (todos com data anterior à verificação de 19/11).

| Mudança | O que é (lido nesta reverificação) |
|---|---|
| BUG-1 | `parar-migracao.sh` cita a causa quando a CLI falha sem código da AWS (`falha sem código da AWS (rede, credencial, timeout ou região)`) e tem mensagem própria para `NoRegion`. O passo que o chama tem `continue-on-error: true` nos jobs `destruir` (linha 679), `limpar` e no passo "Ambiente vencido - parar tarefas de migração" do `agendado` |
| BUG-3 | `scripts/verificar-sem-prevent-destroy.sh` reprova `prevent_destroy` (inclusive `= false`) e `destroy = false` em `.tf` e `.tf.json` sob `infra/aws`; passo próprio do `validar`, com `tests/lifecycle/rodar.sh` |
| B-A1 | O destroy do agendado saiu do YAML para `scripts/destruir-se-vencido.sh`: `plan -destroy -out`, prazo lido da **tag `expires_at` do cluster** no `prior_state` (filtro `expires-at-do-cluster.jq`), guarda da fila manual repetida antes do apply, `terraform apply <plano>`, output `destruiu`; a verificação de zerado do agendado só roda com `destruiu == 'true'` |
| B-A2 a B-A4 | README: "Lock preso" só com AWS CLI, passo 4 com `cd infra/aws` próprio, prazo de "cerca de 1 h 30 min"; `tests/readme/rodar.sh`; `README.md` entrou nos `paths` do gatilho de PR |
| `validar` | passos novos: Parar migração, Agendado, Sem prevent_destroy, Workflow, README; o `shellcheck` do job lista `infra/aws/scripts/*.sh` e todos os 9 `rodar.sh` |
| `release.yml` | só a supressão `# zizmor: ignore[self-repository]` na linha 40 (`uses: ./.github/workflows/ci.yml`), com comentário em português; decisão do Matheus de 20/11/2026 |

## Baseline e execução (20/11/2026)

| Verificação | Comando | Resultado |
|---|---|---|
| Build | `dotnet build --no-incremental` | `0 Warning(s)  0 Error(s)` |
| Testes | `dotnet test --no-build` (solution) | Application 132, Domain 332, Architecture 8, Infrastructure 172, Api 68 = **712**, 0 falhas, 0 ignorados |
| Escopo | `git status` sobre `src`, `tests`, `Dockerfile`, `*.csproj`, `global.json`, `.config`; `find -newer` | nenhuma alteração nesses caminhos |

Contagem de testes .NET: **712 antes (linha de base e verificação anterior), 712 depois**, sem queda e sem teste .NET
novo (a fase não tem código .NET novo). O que a fase tem de teste vive em `infra/aws/tests/`.

### Terraform (Linux, contêiner, 1.17.0)

| Passo | Resultado |
|---|---|
| `terraform fmt -check -recursive -diff` | exit 0, sem saída |
| `terraform init -backend=false` | instalou `hashicorp/aws v6.73.0` (assinado pela HashiCorp), `Terraform has been successfully initialized!` |
| `terraform validate` | `Success! The configuration is valid.` |
| `terraform test` | `Success! 54 passed, 0 failed.` |

Só rodei o Terraform do Linux desta vez (o do Windows não foi repetido: nenhum `.tf` mudou desde 19/11).

### Suítes de shell (contêiner Linux, bash 5.2, jq 1.7.1, com `terraform` no PATH)

| Pasta | Contagem impressa | Exit |
|---|---|---|
| `tests/trava-custo/rodar.sh` | `53 casos ok` | 0 |
| `tests/verificar-zerado/rodar.sh` | `19 casos ok` | 0 |
| `tests/bloquear-recriacao/rodar.sh` | `15 casos ok` | 0 |
| `tests/bootstrap/rodar.sh` | `6 verificações ok` | 0 |
| `tests/parar-migracao/rodar.sh` | `11 casos ok` | 0 |
| `tests/agendado/rodar.sh` | `24 casos ok`; os 3 casos `terraform_real_*` rodaram (`filtro_le_a_tag_do_state_e_nao_o_output`, `tag_futura_com_var_vencida_nao_destroi`, `tag_vencida_destroi_o_state`); nenhuma linha de caso pulado ("PULADOS com aviso" não apareceu) | 0 |
| `tests/lifecycle/rodar.sh` | `18 casos ok` (inclui `arvore_atual_aprova`) | 0 |
| `tests/workflow/rodar.sh` | `9 casos ok` | 0 |
| `tests/readme/rodar.sh` | `18 casos ok` | 0 |
| **Total** | **173** (antes, na verificação de 19/11: 93 = 53 + 19 + 15 + 6) | |
| `scripts/verificar-sem-prevent-destroy.sh` na árvore | `APROVADO: nenhum prevent_destroy nem destroy = false em 15 arquivo(s) .tf.` | 0 |

### Linters (imagens fixadas por digest)

| Ferramenta | Alvo | Saída | Exit |
|---|---|---|---|
| shellcheck 0.10.0 (`koalaman/shellcheck@sha256:2097951f...`) | os 18 `.sh` de `infra/` (9 em `scripts/` e `bootstrap/`, 9 `rodar.sh`) | sem saída | 0 |
| actionlint 1.7.12 (`rhysd/actionlint@sha256:b1934ee5...`) | `infra-aws.yml` e `release.yml` | sem saída | 0 |
| zizmor 1.32.0 (`ghcr.io/zizmorcore/zizmor@sha256:3014cc8f...`), `--offline` | `infra-aws.yml` | `No findings to report. Good job!` | 0 |
| zizmor 1.32.0, `--offline --persona pedantic` | `infra-aws.yml` | `No findings to report. Good job!` | 0 |
| zizmor 1.32.0, `--offline` | `release.yml` | `No findings to report. Good job! (1 ignored)` | 0 |
| zizmor 1.32.0, `--offline --persona pedantic` | `release.yml` | `No findings to report. Good job! (1 ignored)` | 0 |

### Ciclo no Floci (o job `validar`, reproduzido passo a passo)

Floci `floci/floci@sha256:a0fbf944d7f338cc0856f1ef83831ea749ff2be71956eb7a7fce08fc21458e19`, porta 4566, `-u root`,
socket do Docker; o contêiner de execução compartilhou a rede do Floci (`--network container:`). Cópia de `infra/aws`
dentro do contêiner: `floci_override.tf`, `floci.tfstate` e `floci.tfplan` nunca tocaram o repositório. Os passos de
Terraform e de scripts são os do `infra-aws.yml` (mesmas `-var`, `-var-file=environments/floci.tfvars`); o passo
"Floci: remover artefatos que o emulador não apaga" foi **extraído do YAML** e rodado inteiro.

| Passo | Resultado |
|---|---|
| role `renda-fixa-api-execution` | criada (`arn:aws:iam::000000000000:role/renda-fixa-api-execution`) |
| `gerar-floci-override.sh` e `init -reconfigure` | ok |
| `plan` (`service_desired_count=0`) | ok |
| trava de custo sobre o plano | `APROVADO 27 itens do plano dentro da lista positiva e dos limites`, exit 0 |
| bloqueio de recriação | `nenhum RDS nem versão de secret será recriado`, exit 0 |
| `apply` | 96 s, exit 0 (o plano tinha 27 itens) |
| `verificar-zerado.sh` com o ambiente aplicado | `REPROVADA (22 achado(s)...)`, exit 1 |
| `destroy` | `Destroy complete! Resources: 27 destroyed.` (95 s); `terraform state list`: 0 recursos |
| `verificar-zerado.sh` antes do passo de artefatos | `REPROVADA (2 achado(s))`: `log-group /aws/rds/instance/renda-fixa-api/error` (D1) e `recurso-com-tag ...:service/renda-fixa-api/renda-fixa-api` (D2), exit 1 |
| passo "Floci: remover artefatos..." | `Floci D1: log group removido: /aws/rds/instance/renda-fixa-api/error`; `Floci D2: tag project removida do serviço apagado: arn:aws:ecs:us-east-1:000000000000:service/renda-fixa-api/renda-fixa-api`; exit 0 |
| `verificar-zerado.sh` final | `APROVADA (nada sobrando na região us-east-1)`, exit 0 |

Tempo do ciclo completo, com o `init`, os dois `terraform show`, a leitura do state e as verificações: 430 s.
Nota: na primeira passada o awk que extraiu o passo D1/D2 parou na linha em branco entre os dois blocos e só rodou o
D1; o D2 foi rodado depois, com a extração corrigida (até o próximo `- name:`), contra o mesmo Floci. Os dois estão
na tabela acima.

**Extra (fecha em parte uma pendência do P8):** a leitura do prazo pelo agendado com o recurso real. Com
`aws_ecs_cluster.this` aplicado no Floci (`-target`, `expires_at` = agora + 2 h) e um `plan -destroy` com
`expires_at` diferente (agora + 3 h), o filtro `expires-at-do-cluster.jq` devolveu o valor do **state**
(`2026-11-21T01:13:28Z`, a tag do cluster) e o output `expires_at` do `prior_state` devolveu o valor da **`-var`**
(`2026-11-21T02:13:28Z`). Ou seja, o desenho do B-A1 (ler a tag, não o output) funciona com o recurso real do provider
no Floci. Esse ciclo parcial terminou com `Destroy complete! Resources: 1 destroyed.` e verificação `APROVADA`. Que a
tag chegue assim na AWS real continua a conferir na primeira execução (P8). Um primeiro intento desse extra, com
`expires_at=2099-...`, falhou na `validation` de 12 h da variável (esperado) e foi refeito com 3 h.

### Critério 7 — busca repetida

Universo: `git ls-files` mais `git ls-files --others --exclude-standard`, 317 arquivos (306 na verificação anterior),
sem `graphify-out` e sem este arquivo.

| Busca | Resultado |
|---|---|
| `(AKIA\|ASIA\|AIDA\|AROA)[A-Z0-9]{16}` | 0 |
| `BEGIN (RSA \|EC \|OPENSSH )?PRIVATE KEY` | 0 |
| `ghp_`, `github_pat_`, `xox[bp]-` | só a descrição da própria busca em `docs/VALIDACAO-F8.md` |
| `aws_secret_access_key\|AWS_SECRET_ACCESS_KEY\|SecretAccessKey` | `infra-aws.yml:79` (`AWS_SECRET_ACCESS_KEY: test`, credencial fictícia do Floci, só no `validar`) e, nos docs de QA (`QA-F11-ROTEIRO.md`, `QA-F11.md`), só `-e AWS_SECRET_ACCESS_KEY=test`, `env -u ...` e um `x` de teste de credencial inválida |
| `Password=` | em `infra/` só `modules/app/main.tf:12` (`"Password=${var.db_password}"`, interpolação); `release.yml` (`SMOKE_CONNECTION`, já existia), README, `docker-compose.yml` e docs de fases anteriores (senhas locais e descartáveis); nos docs de QA da F11 a senha é sempre `$SENHA` ou `***`, e as linhas citam `grep -c Password=` igual a 0 |
| `password\|secret_string\|token = "<literal>"` em `infra/` e `.github/` | 2, de teste: `banco.tftest.hcl:10` e `aplicacao.tftest.hcl:36` (`"senha-de-teste-que-nunca-vai-para-o-state"`) |
| números de 12 dígitos em `infra/`, `.github/`, README e spec | só `123456789012`, `000000000000` e `111111111111` (conta fictícia em `tests/parar-migracao/rodar.sh`) |
| `git check-ignore -v` | `floci_override.tf` (`.gitignore:508`), `floci.tfstate` e `.backup` (`:492-493`), `terraform.tfstate`, `*.tfplan` (`:507`), `.terraform/` (`:491`), `secret.tfvars` (`*.tfvars`, `:494`): ignorados; `aws.tfvars` e `floci.tfvars` liberados só em `environments/` (`!` em `:506`) |
| `*.tfvars` e `*.tfstate` versionáveis | só `environments/aws.tfvars` e `environments/floci.tfvars`, lidos: `region`, `service_desired_count = 0` e, no Floci, `image_sha` de 40 zeros; `aws.backend.hcl` sem bucket nem segredo |
| `git status --short --ignored infra` ao fim | `?? infra/` e `!! infra/aws/.terraform/` (já existia, ignorado, com o provider do Windows; não toquei); nenhum `floci_override.tf`, `tfstate` ou `tfplan` |

## Critérios 1 a 16 (veredito de 20/11/2026)

| # | Critério | Evidência (20/11/2026) | Resultado |
|---|---|---|---|
| 1 | `fmt -check -recursive`, `validate` e `terraform test` verdes | `fmt` exit 0 sem saída; `init -backend=false` ok; `validate`: `Success! The configuration is valid.`; `terraform test`: `Success! 54 passed, 0 failed.` (Linux, 1.17.0) | **ATENDIDO** |
| 2 | Ciclo no Floci (`apply` com `service_desired_count = 0` e `destroy`) verde no PR | Reproduzido de novo, local: trava `APROVADO 27 itens`, `nenhum RDS nem versão de secret será recriado`, `apply` ok, `Destroy complete! Resources: 27 destroyed.`, state com 0, passo de artefatos do Floci e `verificar-zerado.sh` `APROVADA`. Os passos novos do `validar` (Parar migração, Agendado, Sem prevent_destroy, Workflow, README) rodaram fora do Floci, todos verdes (tabelas acima) | **Provado localmente; a prova no PR continua pendente do push do Matheus** |
| 3 | A trava de custo reprova planos com NAT, Multi-AZ e `desired_count = 2` | `tests/trava-custo/rodar.sh`: `53 casos ok`, com as fixtures nominais `reprova-nat-gateway.json`, `reprova-multi-az.json` e `reprova-desired-count-2.json` presentes; o plano real do Floci (27 itens) foi aprovado (exit 0). O script e as fixtures não mudaram desde a verificação de 19/11, e a mutação do plano real daquela verificação (NAT, `multi_az`, `desired_count = 2`, todos exit 1) não foi repetida | **ATENDIDO** |
| 4 | A verificação de zerado reprova um ambiente com recurso sobrando e aprova um vazio | No Floci: `REPROVADA (22 achado(s))` com o ambiente aplicado, `REPROVADA (2 achado(s))` depois do destroy sem o passo de artefatos, `APROVADA` com o ambiente vazio; `tests/verificar-zerado/rodar.sh`: `19 casos ok` (inclui a saída 2 fail closed). O script não mudou | **ATENDIDO** |
| 5 | A imagem de migração aplica as migrations num PostgreSQL limpo e a API sobe em seguida | **Não reexecutado.** A prova de 19/11 (histórico abaixo) segue válida: `Dockerfile.migrator`, `Dockerfile`, `src/` e as migrations não mudaram desde então (datas de arquivo), e o `release.yml` só ganhou a supressão do zizmor (diff lido: o build e o smoke da `-migrator` são os mesmos) | **ATENDIDO** (evidência de 19/11, inalterada) |
| 6 | Senha e connection string fora do state (prova local) | **Não reexecutada** a contagem no state. A prova de 19/11 (0, 0 e 0 no `terraform show -json`, no state e no plano; senha só no secret) segue válida: nenhum `.tf`, `*.tftest.hcl` ou `*.tfvars` mudou desde então, e os 54 testes (`banco`, `aplicacao`, `raiz-aplicacao` incluídos) passam de novo | **ATENDIDO** (evidência de 19/11, inalterada) |
| 7 | Nenhum segredo, state ou `*.tfvars` com segredo no repositório (busca registrada) | Busca repetida sobre 317 arquivos, tabela "Critério 7" acima: zero chave AWS, zero chave privada, nenhum state, plano ou override fora do `.gitignore`, os 2 tfvars sem segredo | **ATENDIDO** |
| 8 | `actionlint` e `zizmor` sem achado no `infra-aws.yml` e no `release.yml` | `actionlint` 1.7.12: sem saída, exit 0, nos dois. `zizmor` 1.32.0, `--offline` e `--offline --persona pedantic`: `infra-aws.yml` `No findings to report. Good job!`, exit 0 (duas personas); `release.yml` `No findings to report. Good job! (1 ignored)`, exit 0 (duas personas). O 1 ignorado é o `self-repository` de `release.yml:40` (`uses: ./.github/workflows/ci.yml # zizmor: ignore[self-repository]`, com comentário do motivo acima da linha). **Decisão do Matheus, 20/11/2026**: suprimir esse aviso pré-existente (a fase não o introduziu; `./` é a forma documentada para workflow reutilizável local); registrada em "Rodada 2" da spec. Nada mais mudou no arquivo | **ATENDIDO** (com a supressão decidida em 20/11/2026) |
| 9 | `dotnet build` sem warning e `dotnet test` verde, com 712 ou mais | `0 Warning(s)  0 Error(s)`; 712 testes, 0 falhas, 0 ignorados (132 + 332 + 8 + 172 + 68) | **ATENDIDO** |
| 10 | `subir` publica a API pelo DNS do ALB, com smoke verde e `plan` seguinte sem diferença | Precisa da conta e do GitHub (P8) | **PENDENTE (P8)** |
| 11 | A importação do BC termina sem erro no primeiro ciclo (log) | P8 | **PENDENTE (P8)** |
| 12 | O TTL vencido destrói tudo sem passo manual, e a verificação de zerado passa | P8 (o `agendado` e o `destruir-se-vencido.sh` só têm prova local: 24 casos e o Floci) | **PENDENTE (P8)** |
| 13 | `subir` cancelado fora de um `apply` é limpo pelo `limpar`; `destruir` manual termina com zerado verde | P8 | **PENDENTE (P8)** |
| 14 | Nenhuma senha ou connection string nos logs do Actions | P8 | **PENDENTE (P8)** |
| 15 | Custo bruto das subidas conferido no Cost Explorer e registrado | P8 | **PENDENTE (P8)** |
| 16 | A conta continua no Free account plan ao fim da fase | P8 | **PENDENTE (P8)** |

Total: **8 atendidos** (1, 3, 4, 5, 6, 7, 8, 9; o 8 passou a atendido com a supressão decidida em 20/11/2026, na verificação anterior eram 7); **1 provado localmente com a prova no PR pendente** (2); **7 pendentes do P8** (10 a 16). Nenhum critério ao vivo foi marcado.

## Observações da reverificação

1. **Critério 8 fechado.** O achado `self-repository` não foi corrigido, foi suprimido por decisão do Matheus
   (20/11/2026). A saída `(1 ignored)` registrada acima é o que mostra que a supressão está em vigor e que nada mais
   foi escondido: a mesma execução no `infra-aws.yml` não tem nenhum ignorado.
2. **Os critérios 5 e 6 não foram reexecutados**, de propósito e explicitamente: dependem de um build do bundle e de um
   ciclo completo com contagem no state, e nada que os afeta mudou desde 19/11 (datas de arquivo e leitura do diff).
   Se o Matheus quiser a prova refeita na árvore final, é repetir as duas verificações; não é necessário para o veredito.
3. **Risco baixo conhecido do `verificar-sem-prevent-destroy.sh`** (do `docs/QA-F11.md`, não bloqueia): o `find`
   ignora `floci_override.tf` em qualquer profundidade (`! -name floci_override.tf`), então um `floci_override.tf` num
   módulo com `prevent_destroy` passaria. Leitura confirmada no script. O QA sugeriu restringir a exceção ao da raiz;
   não aplicado. É busca textual, não análise do HCL.
4. **A janela do B-A1** (o agendado aplicar o destroy antes de o `subir` gravar o state) continua aceita e documentada
   no README e no cabeçalho do script; só o P8 mostra a largura real dela.
5. **`continue-on-error` no passo de parar a migração** deixa o job verde se só esse passo falhar e o `destroy` e o
   zerado passarem. É a decisão da rodada 2 (o `workflow/rodar.sh` a exige por mutantes).
6. **README linha 577** ainda tem `(preencher no P0: data de criação + 6 meses)`: pendência do P0, não da parte local.
7. **O que o Floci não prova**, como na verificação anterior: tarefa rodando, ALB roteando, IAM real, OIDC, injeção do
   secret e logs. Nada disso foi marcado.

## Pendências (nada foi tratado aqui)

- [ ] Push e PR: o job `validar` verde no GitHub fecha o critério 2.
- [ ] P0 e P8: critérios ao vivo 10 a 16, nenhum marcado.
- Pendências do P8 que a spec lista, todas abertas: o output `iniciou` com job cancelado; o atraso da tagging API;
  KMS gerenciado no RDS e no Secrets Manager; enforcement do Deny em `ModifyDBInstance`; ausência de segredo nos logs
  (inclusive `aws logs tail` da falha da migração); `iam update-role` no Floci; `aws s3 rm` do `.tflock` e o delete
  marker no bucket versionado; a largura real da janela do B-A1; a tag `expires_at` do cluster no `prior_state` do
  plano real (**provada no Floci** nesta reverificação, ver o extra acima; falta a AWS); tagging API com task
  definitions INACTIVE e tarefas STOPPED (I1); `list-clusters` com cluster INACTIVE (S4); permissões não exercidas no
  Floci (`logs:TagResource`/`logs:TagLogGroup`, KMS no RDS, `RunTask` com `--propagate-tags`, `StopTask`); data de
  fechamento da conta no README; cancelamento de um `subir` durante o `apply` (I2); a primeira execução do
  `release.yml` com a `-migrator`; D1 e D2 do Floci confirmados na AWS real.

## Limpeza

Removidos ao fim: contêiner do Floci (`qa11-floci`, com o volume anônimo `/app/data`) e o volume de cache de providers
`qa11-tfcache`. Os contêineres de execução eram `--rm`. Os filhos `floci-aws-*` já tinham sido removidos pelo
`destroy`, e o `dotnet test` não deixou contêiner nem volume. Conferido contra a lista tirada antes de começar:
volumes, contêineres e redes **iguais aos do início**; nenhum contêiner `floci` ou `qa11`. `fixedincome-api`,
`fixedincome-db` e o volume `renda-fixa-api_fixedincome-db-data` não foram tocados (continuam no ar). As imagens
ficaram (incluindo `qa11-runner:tmp`), como pedido. Os volumes anônimos de antes desta reverificação (os que o
`docs/QA-F11.md` cita como "não removidos, de propósito") continuam lá.

## Veredito (20/11/2026)

**APROVADA COM PENDÊNCIAS (P8 e PR).**

Tudo o que podia ser provado sem conta na AWS e sem o GitHub foi provado de novo na árvore atual: `dotnet build` sem
warning e 712 testes verdes (712 na linha de base); `fmt`, `validate` e 54 testes do Terraform; 173 casos de shell
nas nove suítes (inclusive os 3 casos de Terraform real do `agendado`, e o `verificar-sem-prevent-destroy.sh` na
árvore); shellcheck, actionlint e zizmor (as duas personas) com saída 0 nos dois workflows, o `release.yml` com 1
aviso ignorado pela supressão decidida em 20/11/2026; o ciclo completo no Floci (27 recursos criados e destruídos,
trava, bloqueio de recriação, zerado reprovando e depois aprovando, passo D1/D2 do `validar`); busca de segredos
limpa. O escopo ficou onde a spec manda: nada em `src/`, `tests/`, `Dockerfile`, `Dockerfile.migrator`, `ci.yml`,
`mutation.yml` nem `.csproj`. As correções da rodada 2 (BUG-1, BUG-3, B-A1 a B-A4) têm teste e passam.

- **Atendidos:** 1, 3, 4, 5, 6, 7, 8 e 9. O **8** deixou de ser pendência (decisão do Matheus, 20/11/2026).
- **Provado localmente, prova no PR pendente:** 2.
- **Pendentes do P8:** 10 a 16.

Critérios que este arquivo aprova (o orquestrador pode marcar no ROADMAP, nada foi marcado por mim):

- [x] `fmt -check -recursive`, `validate` e `terraform test` verdes
- [ ] Ciclo no Floci verde no PR (provado localmente; marcar só com a execução no PR)
- [x] A trava de custo reprova planos com NAT, Multi-AZ e `desired_count = 2`
- [x] A verificação de zerado reprova um ambiente com recurso sobrando e aprova um vazio
- [x] A imagem de migração aplica as migrations num PostgreSQL limpo e a API sobe em seguida
- [x] Senha e connection string fora do state (prova local)
- [x] Nenhum segredo, state ou `*.tfvars` com segredo no repositório
- [x] `actionlint` e `zizmor` sem achado no `infra-aws.yml` e no `release.yml` (com a supressão do `self-repository` decidida em 20/11/2026)
- [x] `dotnet build` sem warning e `dotnet test` verde, com 712 testes ou mais
- [ ] Critérios ao vivo 10 a 16 (P8)

---

# Histórico: verificação de 19/11/2026 (parte local, P1 a P7)

> Mantida como estava, para registro. Trechos marcados com `[SUBSTITUÍDO em 20/11/2026]` perderam a validade e valem os da reverificação acima (critério 8, contagem das suítes de shell e veredito). Os demais trechos são evidência histórica; os critérios 5 e 6 se apoiam nela (ver a reverificação).


Verificação independente da parte local da F11, feita em 19/11/2026. Cada afirmação abaixo vem de comando
rodado ou arquivo lido nesta verificação; nada veio de relatório de implementador. Nenhum código foi
alterado, nada foi commitado nem adicionado ao índice, e nada foi escrito na AWS real nem no GitHub. O único
arquivo criado é este.

- Data: 2026-11-19
- Commit-base: `f141590` (HEAD da branch `f11-nuvem-aws`). A árvore tinha alterações não commitadas, esperado
  porque a fase ainda não foi commitada: 8 arquivos modificados (`.claude/commands/executar-fase.md`,
  `.github/dependabot.yml`, `.github/workflows/release.yml`, `.gitignore`, `CLAUDE.md`, `README.md`,
  `docs/ESPECIFICACAO.md`, `docs/ROADMAP.md`) e 4 entradas novas não rastreadas (`.github/workflows/infra-aws.yml`,
  `Dockerfile.migrator`, `docs/SPEC-F11-EXECUCAO.md`, `infra/`). `git diff --cached` vazio, `git stash list` vazio.
  Com este arquivo são 5 entradas novas.
- Fontes: `docs/SPEC-F11-EXECUCAO.md` (aprovada em 19/11/2026, com "Decisões de implementação" e pendências do
  P8), seção F11 de `docs/ROADMAP.md`, `CLAUDE.md`.
- Ambiente: Windows 11, .NET SDK 8, Docker Desktop (Linux/WSL2), Terraform 1.17.0 (Windows, na máquina) e
  Terraform 1.17.0 (Linux, em contêiner).
- Fora do alcance, e não verificado: P0 (conta AWS) e P8 (validação ao vivo), que são com o Matheus.

## Como o ciclo no Floci foi reproduzido

O PR ainda não existe, então o job `validar` do `infra-aws.yml` foi reproduzido passo a passo, localmente:

- Imagem de execução montada só para esta verificação, descartada no fim: base
  `amazon/aws-cli@sha256:538a9a80...` (aws-cli 2.41.1, bash 5.2, GNU date), com `terraform` 1.17.0
  (`hashicorp/terraform:1.17.0`) e `jq` 1.7.1 (`ghcr.io/jqlang/jq@sha256:096b8386...`) copiados das imagens oficiais.
  Nada foi instalado na máquina.
- Floci `floci/floci:2.5.0@sha256:a0fbf944d7f338cc0856f1ef83831ea749ff2be71956eb7a7fce08fc21458e19`, porta 4566,
  `-u root`, socket do Docker montado. O contêiner de execução compartilhou a rede do Floci
  (`--network container:`), então `localhost:4566` vale como no runner.
- A configuração foi **copiada** de `infra/aws` para um volume Docker descartável: o `floci_override.tf`, o
  `floci.tfstate` e o `floci.tfplan` nunca tocaram o repositório. O primeiro intento com a pasta montada do
  Windows foi interrompido por lentidão e refeito no volume.
- Variáveis do job: `AWS_ENDPOINT_URL=http://localhost:4566`, credenciais `test`/`test`, `AWS_REGION=us-east-1`.

## Baseline e execução

| Verificação | Comando | Resultado |
|---|---|---|
| Build | `dotnet build --no-incremental` | `0 Warning(s)  0 Error(s)` |
| Testes | `dotnet test --no-build` (solution) | Application 132, Domain 332, Architecture 8, Infrastructure 172, Api 68 = **712**, 0 falhas, 0 ignorados |
| Skip | `grep -rn "Skip" tests --include=*.cs` (fora de `bin` e `obj`) | uma ocorrência, o comentário `// Nada de Skip: ...` em `tests/Shared/PostgreSqlFixture.cs:35`; nenhum `Skip =` |
| Escopo | `git diff --stat` e `git status` sobre `src`, `tests`, `Dockerfile`, `ci.yml`, `mutation.yml`, `*.csproj`, `*.sln`, `global.json`, `.config` | **zero alterações**, nenhum arquivo novo nesses caminhos; nenhum pacote novo |

Contagem de testes: **712 antes (linha de base), 712 depois**, sem queda e sem teste .NET novo, como a spec
prevê (não há código .NET novo). O que a fase ganhou de teste vive em `infra/aws/tests/`: 54 casos de
`terraform test` e quatro suítes de shell (53 + 19 + 15 + 6 verificações), todas abaixo.

## Critérios locais (1 a 9)

| # | Critério | Evidência | Resultado |
|---|---|---|---|
| 1 | `fmt -check -recursive`, `validate` e `terraform test` verdes | Em `infra/aws`, Terraform 1.17.0: `terraform fmt -check -recursive -diff` exit 0 sem saída; `init -backend=false` ok; `validate`: `Success! The configuration is valid.`; `terraform test`: `Success! 54 passed, 0 failed` (6 arquivos: variaveis 18, aplicacao 18, rede 6, banco 6, raiz-aplicacao 4, raiz 2). Repetido no Linux (contêiner): `fmt` exit 0, `init` instala `hashicorp/aws v6.73.0` verificado pelo lock, `validate` ok, `54 passed, 0 failed`. O `.terraform.lock.hcl` tem 18 hashes (windows e linux) e fixa `6.73.0` com `~> 6.73.0` | **ATENDIDO** |
| 2 | Ciclo no Floci (`apply` com `service_desired_count = 0` e `destroy`) verde no PR | Local, nos passos do `validar`: role `renda-fixa-api-execution` criada no Floci; `gerar-floci-override.sh` e `init -reconfigure`; `plan -out` com `floci.tfvars` e `service_desired_count=0`; trava de custo `APROVADO 27 itens`; `bloquear-recriacao` `nenhum RDS nem versão de secret será recriado`; `apply`: `Apply complete! Resources: 27 added` (1 min 55 s; o RDS levou 1 min 29 s); `destroy`: `Destroy complete! Resources: 27 destroyed`, `terraform state list` com 0 recursos; passo de artefatos do Floci (extraído do `infra-aws.yml`, linhas 199 a 243, rodado sem alteração) removeu o log group do RDS (D1) e a tag do serviço apagado (D2); `verificar-zerado.sh` `APROVADA` | **Provado localmente; a prova no PR fica pendente do push do Matheus** |
| 3 | A trava de custo reprova planos com NAT, Multi-AZ e `desired_count = 2` | `bash tests/trava-custo/rodar.sh`: `53 casos ok`; nominalmente `reprova-nat-gateway.json` (`aws_nat_gateway ... tipo fora da lista positiva`, exit 1), `reprova-multi-az.json` (`multi_az deve ser false`, exit 1) e `reprova-desired-count-2.json` (`desired_count deve ser no maximo 1`, exit 1); `aprova-plano-valido.json` exit 0. **Independente das fixtures**: o plano real do Floci (27 itens, aprovado, exit 0) foi alterado com `jq` e reenviado: com um `aws_nat_gateway` acrescentado, exit 1; com `multi_az = true` no RDS, exit 1 (só essa mensagem); com `desired_count = 2` no serviço, exit 1 | **ATENDIDO** |
| 4 | A verificação de zerado reprova um ambiente com recurso sobrando e aprova um vazio | Contra o Floci: com o ambiente aplicado, `verificar-zerado.sh` saiu 1 com `REPROVADA (22 achado(s))` (cluster, serviço, ALB, target group, RDS, secret, 2 log groups e 14 ARNs por tag); depois do `destroy` e do passo de artefatos, saiu 0 com `APROVADA`. Teste extra: um secret avulso `renda-fixa-api/sobra-de-proposito` fez sair 1 (`RESIDUO secret ...`), e depois de apagado saiu 0. Sem credencial válida (`AWS_ENDPOINT_URL` inexistente) saiu 2 (`NAO CONCLUIDA`), não 0. `bash tests/verificar-zerado/rodar.sh`: `19 casos ok` | **ATENDIDO** |
| 5 | A imagem de migração aplica as migrations num PostgreSQL limpo e a API sobe em seguida | Bundle gerado como o job `bundle` (`dotnet ef migrations bundle --self-contained --target-runtime linux-x64 --configuration Release --force`, 125.171.059 bytes). `docker build -f Dockerfile.migrator` com o contexto só no diretório do `efbundle` (usuário `app`, `ENTRYPOINT ["/app/efbundle"]`, 100,9 MB). `postgres:16@sha256:f9a7b9eb...` descartável em rede própria, com 0 tabelas no início. A imagem rodou como `app`, exit 0: `Applying migration '20260818070004_InitialCreate'`, `'20261030222641_AddHolidaysAndIndexRates'`, `'20261112203900_ExtendAnbimaHolidaysTo2050'`, `Done.`; tabelas `fixed_income_asset`, `holiday`, `index_rate`, `position`, `__EFMigrationsHistory` (3 linhas, igual aos 3 arquivos de migration). Imagem da API pelo `Dockerfile`, em `Production`: `/health` 200 em 4 s; `/api/assets` 200; o job de importação concluiu (`1727 de CDI, 0 descartadas e 82 de IPCA`; 1.809 linhas em `index_rate`). Segunda execução da migração: `No migrations were applied`, exit 0 (idempotente). Não usei `fixedincome-api` nem `fixedincome-db`; a API ficou na porta 18080 | **ATENDIDO** |
| 6 | Senha e connection string fora do state (prova local) | No Floci, com o ambiente aplicado: `aws secretsmanager get-secret-value` leu o secret (109 caracteres; forma `Host=...;Port=...;Database=fixedincome;Username=fixedincome;Password=***`, senha de 32 caracteres, valor nunca impresso). Contagens: em `terraform show -json` (42.834 bytes, 30 recursos) a senha aparece **0** vezes, a connection string inteira **0** e `Password=` **0**; no arquivo `floci.tfstate`, **0**, **0** e **0**; no `terraform show -json floci.tfplan`, senha **0** e `Password=` **0**. Controle positivo da busca: o host do banco, que está na connection string, aparece 2 vezes no state. No state, `password_wo = null`, `password = null`, `secret_string = ""`, `secret_string_wo = null`, `secret_string_wo_version = 1`; nenhum recurso efêmero nem data source de secret; nenhum output sensível (os 6 outputs são DNS do ALB, cluster, `expires_at`, `image_sha`, subnets e SG) | **ATENDIDO** |
| 7 | Nenhum segredo, state ou `*.tfvars` com segredo no repositório | Ver "Critério 7". Zero chave AWS, zero chave privada, `floci_override.tf`, `*.tfstate*`, `*.tfplan` e `.terraform/` ignorados; os 2 tfvars versionáveis e o `aws.backend.hcl` não têm segredo | **ATENDIDO** |
| 8 | **[SUBSTITUÍDO em 20/11/2026: ATENDIDO, ver a reverificação]** `actionlint` e `zizmor` sem achado no `infra-aws.yml` e no `release.yml` | `actionlint` 1.7.12: saída vazia, exit 0, nos dois. `zizmor` 1.32.0 (`--offline`, e `--offline --persona pedantic`): `infra-aws.yml` `No findings to report`, exit 0 nas duas personas; `release.yml` **1 achado baixo** nas duas, exit 12: `help[self-repository]`, `release.yml:37`, `uses: ./.github/workflows/ci.yml` (`use '$/...' instead of './...'`). O achado é **pré-existente**: `git show HEAD:.github/workflows/release.yml` tem o mesmo `uses: ./.github/workflows/ci.yml` (linha 33 antes; a fase só deslocou a linha) | **NÃO ATENDIDO como escrito; pendente de decisão do Matheus** (a fase não introduziu o achado) |
| 9 | `dotnet build` sem warning e `dotnet test` verde, com 712 ou mais | `0 Warning(s)  0 Error(s)`; 712 testes, 0 falhas, 0 ignorados (acima) | **ATENDIDO** |

**[SUBSTITUÍDO em 20/11/2026: agora 8 atendidos]** Total: 7 atendidos (1, 3, 4, 5, 6, 7, 9), 1 provado localmente com a prova no PR pendente (2), 1 não atendido
como escrito e pendente de decisão (8).

### Critério 7 — busca registrada

Universo: `git ls-files` mais `git ls-files --others --exclude-standard` (arquivos versionáveis, ou seja, não
ignorados), 306 arquivos, sem `graphify-out`.

| Busca | Resultado |
|---|---|
| `(AKIA\|ASIA\|AIDA\|AROA)[A-Z0-9]{16}` | 0 |
| `BEGIN (RSA \|EC \|OPENSSH )?PRIVATE KEY` | 0 |
| `aws_secret_access_key\|AWS_SECRET_ACCESS_KEY\|SecretAccessKey` | 1: `infra-aws.yml:78`, `AWS_SECRET_ACCESS_KEY: test`, a credencial fictícia do Floci, só no job `validar` |
| `Password=` | 0 em `infra/` com valor literal: a única ocorrência em código é `modules/app/main.tf:12`, `"Password=${var.db_password}"` (interpolação), e `aplicacao.tftest.hcl:191` (assertiva de que a task definition não a contém). Os demais são pré-existentes e locais (`docker-compose.yml`, `appsettings.Development.json`, README, docs de QA) ou o `SMOKE_CONNECTION` do `release.yml` (banco descartável do smoke, já existia) |
| `password\|secret_string\|token = "<literal>"` em `infra/` e `.github/` | 2, ambos de teste: `banco.tftest.hcl:10` e `aplicacao.tftest.hcl:36`, `"senha-de-teste-que-nunca-vai-para-o-state"` (valor de `mock_provider`, sem uso fora do teste) |
| Números de 12 dígitos em arquivos da F11 | só `123456789012` (a conta fictícia dos exemplos da AWS) e `000000000000` (Floci) |
| `git check-ignore -v` | `floci_override.tf` (`.gitignore:508`), `floci.tfstate` e `floci.tfstate.backup` (`:492-493`), `terraform.tfstate`, `*.tfplan` (`:507`), `.terraform/` (`:491`), `efbundle` (`:500`), `secret.tfvars` e `foo.auto.tfvars` (`*.tfvars`, `:494`): todos ignorados |
| `*.tfvars` versionáveis | `git check-ignore` mostra a exceção `!infra/aws/environments/*.tfvars` (`:506`) só para `environments/`. Conteúdo lido: `aws.tfvars` tem `region` e `service_desired_count = 0`; `floci.tfvars` tem `region`, `service_desired_count = 0` e `image_sha` de 40 zeros. Sem segredo. `aws.backend.hcl`: `key`, `region`, `encrypt`, `use_lockfile`; o bucket entra por `-backend-config` |
| `git status --short --ignored infra` | só `infra/aws/.terraform/` (ignorado); nenhum `floci_override.tf`, `tfstate` ou `tfplan` no repositório depois da minha execução |

## Suítes de shell e shellcheck (19/11/2026; contagens substituídas pelas de 20/11/2026: 173 casos em 9 suítes)

| Verificação | Comando | Resultado |
|---|---|---|
| Trava de custo | `bash tests/trava-custo/rodar.sh` | `53 casos ok`, exit 0 |
| Verificação de zerado (shim do `aws`) | `bash tests/verificar-zerado/rodar.sh` | `19 casos ok`, exit 0 |
| Bloqueio de recriação | `bash tests/bloquear-recriacao/rodar.sh` | `15 casos ok`, exit 0 |
| Policy do bootstrap | `bash tests/bootstrap/rodar.sh` | `6 verificações ok`, exit 0 (RunTask só no migrator com `ecs:cluster`, StopTask só em tarefas do cluster, PassRole só na execution role, sem Resource curinga em RunTask e StopTask) |
| shellcheck 0.10.0 (`koalaman/shellcheck@sha256:2097951f...`) | todos os 11 `.sh` de `infra/` (7 em `scripts/` e `bootstrap/`, 4 `rodar.sh`) | sem achado, exit 0 |

As quatro suítes rodaram no contêiner Linux (bash 5.2, jq 1.7.1), como no `ubuntu-24.04`.

## Escopo e documentação

| Item | Evidência | Resultado |
|---|---|---|
| Nada mudou em `src/`, `tests/` (.NET), `Dockerfile`, `ci.yml`, `mutation.yml`, `.csproj` | `git diff --stat` e `git status` vazios nesses caminhos; só 8 arquivos modificados, todos da lista da spec (`.claude/commands/executar-fase.md`, `dependabot.yml`, `release.yml`, `.gitignore`, `CLAUDE.md`, `README.md`, `ESPECIFICACAO.md`, `ROADMAP.md`) | ok |
| Pacotes | nenhum `.csproj`, `.config/dotnet-tools.json` nem `global.json` no diff; só entra o provider `hashicorp/aws ~> 6.73.0` (lock) e a imagem base `runtime-deps:8.0` | ok |
| `release.yml` | diff lido: `paths-ignore` de `infra/**` e do `infra-aws.yml`; build da `<sha>-migrator` com os mesmos rótulos; push das duas tags; o smoke remove a imagem local, puxa a `-migrator` do GHCR e a roda com `--network host`; `latest` só na API. O `release.yml` em si não foi executado (não há como localmente) | ok por leitura |
| `dependabot.yml` | `ignore` de major para `dotnet/runtime-deps` | ok |
| `AWS_ROLE_ARN` como variable de repositório | README (linhas 579 e 620) e `infra-aws.yml` (`vars.AWS_ROLE_ARN` nos `if` de `subir`, `destruir`, `limpar` e `agendado`, comentário da linha 260) batem; `AWS_REGION` e `TF_STATE_BUCKET` aparecem como variables do environment `aws` | ok |
| `expires_at` provisório na fase 0 | README (passo 2 do `subir`) e `infra-aws.yml` ("Calcular expires_at": definitivo e provisório de agora + 1 h, ou o definitivo se menor; fase 0 usa o provisório, fase 1 e plano final o definitivo) batem com a D4 e com o I3 da spec. A tag de `expires_at` só está no cluster e no output (`modules/app`) | ok |
| `ubuntu-24.04` | todos os 5 jobs do `infra-aws.yml` usam `ubuntu-24.04`; `ci.yml`, `mutation.yml` e `release.yml` continuam `ubuntu-latest` (a decisão vale só para o novo) | ok |
| Scripts auxiliares | `scripts/verificar-zerado-apos-destroy.sh` (repete uma vez após 90 s só na saída 1, nunca na 2) e `scripts/parar-migracao.sh` existem e são usados nos jobs `destruir`, `limpar` e `agendado`, como a spec diz. O README os trata como "scripts de parada" e a tabela de resíduos, sem citar os nomes | ok (README menos específico que a spec) |
| Ações fixadas por SHA, `persist-credentials: false`, `permissions` mínimas | `infra-aws.yml`: `checkout`, `setup-terraform` e `configure-aws-credentials` por SHA com comentário de versão; 5 `persist-credentials: false`; `contents: read` no topo; `id-token: write` só nos jobs que falam com a AWS | ok |
| README | contém a seção "Nuvem na AWS (F11)": diagrama, aviso do Free plan, bootstrap, GitHub, subir e destruir, camadas, resíduos, custo, riscos, lock preso, validação | ok |
| Lacuna conhecida | README linha 577: `(preencher no P0: data de criação + 6 meses)` | pendente do P0 |

## Divergências e observações

1. **[SUBSTITUÍDO em 20/11/2026: resolvido pela supressão decidida pelo Matheus]** **Critério 8 (zizmor no `release.yml`).** O achado `self-repository` é pré-existente e de severidade baixa. O
   critério, como está escrito, pede "sem achado"; a evidência diz que não está atendido. Não decido por ele: as
   saídas possíveis são do Matheus (corrigir a sintaxe do `uses`, aceitar o achado como conhecido ou reescrever o
   critério). Até lá o item 8 do ROADMAP fica desmarcado.
2. **Divergências do Floci (D1 e D2) confirmadas e reproduzidas.** Sem o passo "Floci: remover artefatos que o
   emulador não apaga", a verificação de zerado depois do `destroy` **reprova com 2 achados**:
   `log-group /aws/rds/instance/renda-fixa-api/error` e `recurso-com-tag ...:service/renda-fixa-api/renda-fixa-api`.
   Com o passo, aprova. O passo só existe no job do Floci; nenhum job da AWS real o tem (conferido por leitura),
   e remove só esses dois casos. Que o RDS real não deixa o log group e que a tagging API não devolve o serviço
   apagado não é provado localmente: é pendência do P8 (a spec já lista).
3. **Fixture `reprova-multi-az.json` não isola o Multi-AZ.** A saída traz também `storage_type deve ser gp3`
   (a fixture não define o tipo de disco); o teste só confere o trecho `multi_az`, então passa. A cobertura do
   critério 3 não depende disso: a mutação do plano real do Floci reprovou só por `multi_az`. É melhoria opcional
   da fixture, sem efeito no veredito.
4. **A prova de "tarefa rodando, ALB roteando, IAM, OIDC, injeção do secret e logs" não existe aqui**, e a spec
   diz que o Floci não a dá. No ciclo local, `service_desired_count = 0` e nenhuma tarefa subiu. O smoke de
   `/health` e `/api/assets` deste relatório é o da imagem da API contra o PostgreSQL local, não o do ALB.
5. **Reprodução do `release.yml`.** O smoke do `release.yml` usa `--network host`, que o Docker Desktop do
   Windows não entrega do mesmo jeito. Usei uma rede Docker própria, com o mesmo bundle, a mesma imagem de
   migração e o mesmo `postgres:16@sha256:f9a7b9eb...`. O que o `release.yml` faz de diferente (pull do GHCR,
   `--network host`) fica para a primeira execução real.
6. **A importação do BC rodou** (1727 de CDI e 82 de IPCA) a partir desta máquina. A partir de `us-east-1`, na
   AWS, isso continua pendente do P8 (a spec avisa que o smoke não detecta bloqueio do SGS).
7. **`infra/aws/.terraform/` já existia** (ignorado) com o provider do Windows, de uma execução anterior ao
   meu trabalho; ficou como estava (o `init -backend=false` do critério 1 o reutilizou). Nada mais ficou em
   `infra/aws`.

## Regras da fase e o que cobre cada uma

A fase não tem regra de negócio nem cálculo. As regras de infraestrutura e o que as exercita:

| Regra | Cobertura |
|---|---|
| `expires_at` aceita até 12 h (exato) e rejeita 12 h e 1 min, fora de RFC 3339; passado vale como marcação do `destruir` | `variaveis.tftest.hcl` (`expires_at_em_exatamente_12h_e_aceito`, `expires_at_12h_mais_1_minuto_e_rejeitado`, `expires_at_fora_do_formato_rfc3339_e_rejeitado`, `expires_at_no_passado_e_aceito_como_marcacao_de_destruicao`) |
| `service_desired_count` padrão 0, aceita 0 e 1, rejeita 2 e negativo | `variaveis.tftest.hcl` (4 casos) |
| `image_sha` só com 40 hexadecimais minúsculos; sem tag mutável | `variaveis.tftest.hcl` (4 casos de rejeição), `aplicacao.tftest.hcl` (`nenhuma_imagem_usa_tag_mutavel`) |
| API e migração usam o mesmo SHA | `aplicacao.tftest.hcl` (`api_e_migracao_usam_o_mesmo_sha_com_os_sufixos_certos`) |
| Senha fora do state; connection string só no secret, de escrita única; versões de escrita iguais no RDS e no secret | `banco.tftest.hcl`, `aplicacao.tftest.hcl` (`connection_string_vem_do_secret_e_nunca_em_texto`, `secret_some_na_hora_e_a_connection_string_e_de_escrita_unica`), `raiz-aplicacao.tftest.hcl` e a prova do critério 6 |
| Nenhum output sensível nem com senha | `aplicacao.tftest.hcl` (`outputs_do_modulo_nao_sao_sensiveis_e_nao_carregam_a_senha`), `raiz-aplicacao.tftest.hcl` (`nenhum_output_do_raiz_e_sensivel`), `outputs_sensiveis: []` do critério 6 |
| Rede em cadeia ALB, tarefa e RDS, rota só para o internet gateway, duas AZs | `rede.tftest.hcl` (6 casos), `banco.tftest.hcl` (`uma_subnet_so_e_rejeitada_porque_o_rds_exige_duas_azs`) |
| RDS barato, single-AZ, privado, criptografado, sem resíduo ao destruir | `banco.tftest.hcl`, e a trava de custo |
| Lista positiva e limites de custo no plano | `tests/trava-custo/rodar.sh` (53 casos) e o plano real do Floci |
| Reprovar substituição do RDS e da versão do secret | `tests/bloquear-recriacao/rodar.sh` (15 casos) e o plano real do Floci |
| Verificação de zerado fail closed (0, 1, 2) | `tests/verificar-zerado/rodar.sh` (19 casos) e o Floci (itens 2 e 4) |
| Permissões da role do CI (RunTask, StopTask, PassRole) | `tests/bootstrap/rodar.sh` (6 verificações) |
| Imagem de migração e ordem migrar antes de subir a API | critério 5, com a imagem e a API reais |

## Critérios ao vivo (10 a 16): pendentes do P8

Nenhum é marcado. Cada um precisa da conta e do GitHub, com autorização do Matheus a cada escrita.

| # | Critério | O que precisa ser provado no P8 |
|---|---|---|
| 10 | `subir` publica a API pelo DNS do ALB, com smoke verde e `plan` seguinte sem diferença | `subir` com `horas=1`: ID da execução, smoke `/health` e `/api/assets` 200 e o passo "Plan sem diferença" com exit 0. Abrir o DNS do ALB. Conferir `health_check_grace_period_seconds = 120` (estimativa) |
| 11 | A importação do BC termina sem erro no primeiro ciclo | Log do CloudWatch com `Importação de cotações concluída`; o smoke não detecta falha nem bloqueio do SGS a partir de `us-east-1` |
| 12 | O TTL vencido destrói tudo sem passo manual, e a verificação de zerado passa | Esperar o `expires_at`; execução do `agendado` que destrói e passa na verificação; ID da execução |
| 13 | `subir` cancelado fora de um `apply` é limpo pelo `limpar`; `destruir` manual termina com zerado verde | Cancelar na espera da migração; o `limpar` destrói e fica verde; `subir` e `destruir` manual com zerado verde. O cancelamento durante o `apply` fica de fora e é registrado à parte (I2) |
| 14 | Nenhuma senha ou connection string nos logs do Actions | Conferir todos os logs das execuções, inclusive o `aws logs tail` do caminho de falha da migração |
| 15 | Custo bruto das subidas conferido no Cost Explorer e registrado | No dia seguinte, custo bruto das subidas e estado do budget (`include_credit = false`, alertas em US$ 1, 3 e 5) |
| 16 | A conta continua no Free account plan ao fim da fase | Tela de billing; sem upgrade, sem Organizations |

Pendências do P8 que a spec lista (todas continuam abertas, nenhuma foi tratada aqui):

- O output `iniciou` com job cancelado.
- O atraso da tagging API (a repetição da verificação cobre o caso).
- KMS gerenciado no RDS e no Secrets Manager, sem `kms:*` na role do CI.
- Enforcement do Deny em `ModifyDBInstance`.
- Ausência de segredo nos logs, inclusive no `aws logs tail` da falha da migração.
- `iam update-role` não provado no Floci.
- Achado baixo pré-existente do zizmor 1.32.0 no `release.yml` (`self-repository`), para decisão do Matheus
  (é o item 8 acima).
- Se a tagging API devolve task definitions INACTIVE e tarefas STOPPED depois do destroy (I1), e se devolver
  também cluster ou serviço INACTIVE, a regra precisa ser estendida (a varredura por tag os trata como resíduo).
- Se `list-clusters` lista cluster INACTIVE recém-apagado (S4); o Floci não o lista.
- Permissões não exercidas no Floci, a conferir com `AccessDenied` no primeiro `subir`: `logs:TagResource` e
  `logs:TagLogGroup` no `CreateLogGroup` com tags; KMS (`kms:CreateGrant`, `kms:DescribeKey`) no
  `CreateDBInstance` criptografado com `aws/rds`; `RunTask` com `--propagate-tags` e `ecs:cluster`; `StopTask`
  no ARN `task/renda-fixa-api/<id>`.
- Preencher no README a data de fechamento da conta (criação + 6 meses).
- Cancelamento de um `subir` durante o `apply` (I2): se deixou órfão ou lock preso, e se o roteiro do README basta.
- Também ficam do P8, vindos desta verificação: confirmar na AWS real que o RDS não deixa o log group nem o
  serviço apagado volta pela tagging API (D1 e D2 do Floci); a primeira execução do `release.yml` com a
  `-migrator` (pull do GHCR, `--network host`); a execução do job `validar` num PR (critério 2).

## Limpeza

Removidos ao fim: o contêiner do Floci (`f11v-floci`), o contêiner e a rede do PostgreSQL e da API
descartáveis (`f11v-pg`, `f11v-api`, `f11v-net`), o volume `f11v-work` (com `floci_override.tf`, `floci.tfstate`,
`floci.tfplan` e a cópia de `infra/aws`), as imagens `f11-verif-runner:tmp`, `f11v-api:tmp` e `f11v-migrator:tmp`,
o bundle de 125 MB e os logs e scripts temporários do scratchpad. `fixedincome-api` e `fixedincome-db` (do
Matheus) continuam como estavam. As camadas de build do Docker (cache do BuildKit) e as imagens pull-adas por
digest para as ferramentas (`floci`, `zizmor`, `actionlint`, `shellcheck`, `jq`, `aws-cli`) ficam no cache local do
Docker, como antes.

## Veredito de 19/11/2026 **[SUBSTITUÍDO pelo veredito de 20/11/2026, acima]**

**APROVADA para a parte local (P1 a P7), com uma ressalva.**

Tudo o que podia ser provado sem conta na AWS e sem o GitHub foi provado e refeito aqui: `fmt`, `validate` e 54
testes do Terraform (no Windows e no Linux); o ciclo completo no Floci (27 recursos criados e destruídos) com
trava de custo, bloqueio de recriação e verificação de zerado (que reprovou o ambiente aplicado, reprovou um
secret avulso, saiu 2 sem credencial e aprovou o vazio); a trava reprovando NAT, Multi-AZ e `desired_count = 2`
nas fixtures e num plano real alterado; a imagem de migração aplicando as 3 migrations num PostgreSQL limpo e a
API subindo em seguida com `/health` e `/api/assets` em 200; senha e connection string com contagem zero no
state, no plano e em `terraform show -json`; nenhuma credencial no repositório; `actionlint` limpo; shellcheck
limpo; as quatro suítes de shell verdes; `dotnet build` sem warning e 712 testes verdes (712 na linha de base).
O escopo ficou onde a spec manda: nada em `src/`, `tests/`, `Dockerfile`, `ci.yml`, `mutation.yml` nem `.csproj`.

Critérios locais: **atendidos** 1, 3, 4, 5, 6, 7 e 9; **provado localmente, prova no PR pendente** 2; **não
atendido como escrito, pendente de decisão do Matheus** 8 (um achado baixo pré-existente do zizmor no
`release.yml`, que a fase não introduziu).

Pendente do Matheus:

- [ ] Push e PR: o job `validar` verde no GitHub fecha o critério 2.
- [ ] Decisão sobre o `self-repository` do zizmor no `release.yml` (critério 8).
- [ ] P0 e P8: critérios ao vivo 10 a 16, nenhum marcado.

Critérios que este arquivo aprova (o orquestrador pode marcar no ROADMAP, nada foi marcado por mim):

- [x] `fmt -check -recursive`, `validate` e `terraform test` verdes
- [ ] Ciclo no Floci verde no PR (provado localmente; marcar só com a execução no PR)
- [x] A trava de custo reprova planos com NAT, Multi-AZ e `desired_count = 2`
- [x] A verificação de zerado reprova um ambiente com recurso sobrando e aprova um vazio
- [x] A imagem de migração aplica as migrations num PostgreSQL limpo e a API sobe em seguida
- [x] Senha e connection string fora do state (prova local)
- [x] Nenhum segredo, state ou `*.tfvars` com segredo no repositório
- [ ] `actionlint` e `zizmor` sem achado no `infra-aws.yml` e no `release.yml` (não atendido como escrito)
- [x] `dotnet build` sem warning e `dotnet test` verde, com 712 testes ou mais
