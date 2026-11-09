# Spec de execução — F8: CD automatizado e IaC

Status: rascunho revisado (08/11/2026), aguardando aprovação do Matheus

Documento de execução para agente. Objetivo: descrever em Terraform a infraestrutura que já
existe no Azure, autenticar o GitHub no Azure por OIDC e fazer cada merge na `main` publicar
a nova versão sozinho, com migrations aplicadas pelo pipeline e um smoke test que reprova o
deploy.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md` e as convenções em `CLAUDE.md`;
ambas prevalecem. Passos e critérios da fase estão em `docs/ROADMAP.md`. Esta fase não tem
regra de negócio nem cálculo novos: não há RN nova e o especialista em renda fixa não entra.

Esta fase toca:

- `src/FixedIncome.Api` — endpoint `/health` e o comentário das migrations no `Program.cs`
- `src/FixedIncome.Application` e `src/FixedIncome.Infrastructure` — apenas as duas
  pendências herdadas da F7 (P2 e P3)
- `infra/` (novo) — Terraform, tfvars sem segredo e script de bootstrap
- `.github/workflows/` — `infra.yml` e `deploy.yml` novos; `ci.yml` não muda
- `README.md` e `.gitignore`
- Os projetos de teste correspondentes

### Estado real do Azure (lido em 08/11/2026, só leitura)

Assinatura "Azure subscription 1" (`05f2b405-a35a-47dc-b1e6-cf6e8643affe`), resource group
`rg-renda-fixa`:

| Recurso | Região | Observação |
|---|---|---|
| `pg-renda-fixa` | brazilsouth | Flexible Server 16, Burstable `Standard_B1ms`, 32 GB, backup 7 dias, rede pública habilitada, admin `seuadmin`. **Estado `Stopped`** |
| `workspace-rgrendafixa2ZEb` | eastus | Log Analytics |
| `renda-fixa-api-env` | eastus | Container Apps Environment, perfil Consumption |
| `cab1c9c07832acr` | eastus | ACR Basic, `adminUserEnabled = true` |
| `renda-fixa-api` | eastus | Container App **com `provisioningState = Failed`**, `deploymentErrors = ContainerAppNotFoundInCluster`, sem template, sem ingress e sem segredos |

**Premissa bloqueante:** `az account show` devolve `"state": "Disabled"` para a assinatura.
Assinatura desabilitada aceita leitura, mas não escrita: é o que explica o Postgres parado e
o Container App quebrado. Enquanto ela não voltar a `Enabled`, tudo o que escreve no Azure
(bootstrap, `apply`, deploy, smoke) não roda. A implementação do código, do Terraform e dos
workflows (P1 a P8) não depende disso e é validada localmente; a validação ao vivo (P9)
depende.

## Fora de escopo

- Key Vault, VNet, private endpoint e qualquer mudança de rede além do firewall da D3. O
  segredo vive como secret do Container App, conforme o ROADMAP
- Ambiente permanente além de `prod`. Existe só o ambiente temporário de validação do
  critério "recriar do zero" (ver P9), destruído ao fim
- Rollback automático, deploy canário e divisão de tráfego entre revisões
- Application Insights, `/health/live` e `/health/ready` (F10). O `/health` desta fase só
  responde 200 se o processo está de pé
- Alertas sobre o log do job. As pendências da F7 são corrigidas antes de existir alerta, mas
  o alerta em si não faz parte da fase
- Usuário de banco com menos privilégio que `seuadmin`. Testcontainers e demais itens da F9.
  Calendário ANBIMA além de 2030 (seção Manutenção)
- Mover o PostgreSQL de região ou trocar o SKU
- Alterar `ci.yml`. Nenhuma regra de negócio, cálculo ou contrato de API muda

## Decisões para aprovação do Matheus

**D1 — Quem cria a identidade OIDC.** O ROADMAP diz que o Storage Account do state é o único
recurso criado fora do Terraform. A identidade do GitHub tem um problema de ovo e galinha: o
Terraform do CI precisa dela para rodar, então não pode ser quem a cria. Proposta: o script
de bootstrap cria o Storage Account **e** a identidade, e o README documenta os dois.
Alternativa rejeitada: provider `azuread` no Terraform, que daria à identidade do CI
permissão de Graph (`Application.ReadWrite.All`).

**D2 — Container App quebrado.** Importar um app com template vazio e `Failed` pode não
convergir. Se o provider forçar substituição, a proposta é destruir e recriar só o Container
App, que não guarda estado. Isso conflita com o critério "o primeiro plan não propõe destruir
nada" e precisa de aprovação explícita antes do apply. Os demais recursos nunca são recriados.

**D3 — Firewall do PostgreSQL.** O servidor tem rede pública e o Container App sai por uma
lista longa de IPs. Proposta: uma regra `0.0.0.0` (acesso de serviços Azure) para o Container
App e uma regra temporária com o IP do runner durante a migration, removida num passo
`always()`. Risco aceito e documentado no README: a regra `0.0.0.0` libera qualquer recurso
Azure, e a barreira passa a ser senha forte e TLS obrigatório. Regras de firewall que já
existam no servidor são listadas no P9 e importadas ou removidas só com aprovação.

**D4 — Origem da senha do banco.** O ROADMAP fala em senha "gerada". O servidor já existe com
uma senha que o Terraform não consegue ler, então a senha é **fornecida** (secret do GitHub) e
precisa ser a atual do `seuadmin`, ou rotacionada pelo Matheus no P9. A senha usa só letras e
números e tem pelo menos 24 caracteres, para não quebrar a connection string montada em shell.

## Regras da fase

### Segredos e onde cada um fica

| Item | Onde fica | Observação |
|---|---|---|
| Senha do admin do PostgreSQL | Secret `PG_ADMIN_PASSWORD` do GitHub Environment `production` | Entra no Terraform por `TF_VAR_postgres_admin_password`; nunca em `*.tfvars` |
| Connection string da API | Secret `db-connection-string` do Container App, lida por `secretRef` em `ConnectionStrings__DefaultConnection` | Ver regra de escrita única abaixo |
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | Variables (não secrets) do GitHub | São identificadores |
| `PG_DATABASE`, `PG_ADMIN_USER` | Variables do GitHub | O host vem de `az postgres flexible-server show` |
| Credencial do Azure no GitHub | Nenhuma: OIDC com credencial federada | Sem client secret |
| State do Terraform | Blob no Storage Account, acesso só por Entra, sem chave | Container privado |

Regra do ROADMAP: nenhum segredo vai para o state em texto aberto **quando houver
alternativa**. O `implementador` lê a documentação do provider fixado antes de decidir e
registra a evidência:

1. Senha do admin: usar `administrator_password_wo` (escrita única) se o provider o oferecer.
   Para o servidor importado, `administrator_password` entra em `ignore_changes`.
2. Secret do Container App: usar o equivalente de escrita única se existir. Se não existir, o
   Terraform cria o secret com valor de marcação e `ignore_changes` nele, e o `deploy.yml`
   grava o valor real com `az containerapp secret set` antes de trocar a imagem. Assim a senha
   nunca chega ao state.
3. Se nenhuma das duas alternativas for viável, o valor fica no state (privado, só Entra) e o
   README registra a limitação.

O plano salvo (`-out`) contém variáveis sensíveis: `plan` e `apply` rodam **no mesmo job** e o
arquivo de plano nunca vira artifact. `terraform show -json` imprime valores sensíveis em claro:
a prova de "senha fora do state" é feita localmente pelo Matheus ou pelo agente, nunca no
Actions.

### Infraestrutura (`infra/`)

- Provider `azurerm` com versão fixada em faixa menor; `required_version` declarado;
  `.terraform.lock.hcl` commitado, gerado com `terraform providers lock
  -platform=linux_amd64 -platform=windows_amd64`. Nenhum outro provider sem justificativa
- Backend `azurerm` com configuração parcial em `infra/environments/<ambiente>.backend.hcl`
  (sem segredo) e `use_azuread_auth`; no CI, `use_oidc` e `ARM_USE_OIDC=true` com
  `ARM_CLIENT_ID`, `ARM_TENANT_ID` e `ARM_SUBSCRIPTION_ID`
- O Terraform **não gerencia o resource group**: o nome entra por variável, e o grupo é criado
  fora (o de produção já existe; o de validação é criado pelo bootstrap). O Storage Account do
  state fica em outro grupo, `rg-renda-fixa-tfstate`, para nunca ser afetado por um `destroy`
  da aplicação
- Estrutura: `infra/*.tf`, `infra/modules/{log_analytics,container_registry,
  container_apps_environment,postgresql,container_app}`, `infra/environments/prod.tfvars` e
  `validation.tfvars` (nomes globais próprios, RG próprio e chave de state própria)
- Os cinco recursos existentes entram por blocos `import` em `infra/imports.tf`, mais o banco
  da aplicação (`azurerm_postgresql_flexible_server_database`, nome lido no P9 com o servidor
  ligado). `ignore_changes` é permitido em qualquer recurso importado, sempre com comentário
  dizendo qual divergência do provider ele cobre (por exemplo zona do PostgreSQL ou
  `log_analytics_workspace_id` do ambiente)
- Registry com `admin_enabled = false`. O Container App puxa a imagem por identidade
  gerenciada atribuída pelo usuário, com AcrPull criado **antes** do app
- Container App: um container, 0,25 vCPU e 0,5 GiB, ingress externo na 8080,
  `min_replicas = 0` e `max_replicas = 1` (limita custo e deixa o job de importação com uma só
  instância). Probes padrão (TCP), sem probe HTTP. Variáveis de ambiente:
  `ASPNETCORE_ENVIRONMENT=Production` e a connection string por `secretRef`
- Imagem inicial: um placeholder público que escute na 8080 (por exemplo
  `mcr.microsoft.com/dotnet/samples:aspnetapp`; verificar). O recurso usa `ignore_changes` na
  imagem: quem a troca é o deploy, e o `plan` depois dele não pode mostrar diferença
- `.gitignore` já ignora `.terraform/`, `*.tfstate*` e `*.tfvars`. Acrescentar a exceção
  `!infra/environments/*.tfvars` (sem segredo) e `*.tfplan`

O job de importação roda dentro do processo da API (já habilitado por padrão no
`appsettings.json`), então com `min_replicas = 0` executa a cada partida a frio e a cada 6
horas enquanto houver instância. A importação é idempotente; só não há garantia de horário.

### Bootstrap (`infra/bootstrap/`)

Script único (bash com `az`), idempotente, que cria: `rg-renda-fixa-tfstate` com o Storage
Account do state (container privado, versionamento e soft delete de blob, TLS 1.2 mínimo,
acesso anônimo desligado); o resource group de validação; app registration, service principal
e **uma** credencial federada para `repo:matheuscardosolazzarini/renda-fixa-api:environment:
production`; Contributor nos resource groups de produção e de validação; Storage Blob Data
Contributor no Storage Account; AcrPush no registry; e **Role Based Access Control
Administrator** nos dois resource groups, com condição que só permite atribuir AcrPull (o
Contributor não cria o `roleAssignment` do AcrPull que o Terraform declara). Imprime os três
identificadores para o Matheus cadastrar como variables. Não imprime nem grava segredo.

### Pipeline de infra (`.github/workflows/infra.yml`)

- `pull_request` com `paths: infra/**`: `terraform fmt -check -recursive`, `init`, `validate`
  e `plan`. O plano vai para o resumo do job (`GITHUB_STEP_SUMMARY`)
- `workflow_dispatch`: `plan` e `apply` no mesmo job, com `concurrency`. O job de apply exige
  `github.ref == 'refs/heads/main'`. O disparo manual é o passo humano: quem dispara já leu o
  plano do PR
- Jobs que falam com o Azure usam `environment: production`, `permissions: id-token: write` e
  `contents: read`, e `azure/login` com as variables. O environment **não** tem required
  reviewers (senão "sem passo manual" falha). Risco aceito e registrado no README: o `plan` de
  PR roda no environment `production`, então qualquer branch do repositório recebe Contributor
  e a senha; o repositório é de um único dono

### Pipeline de deploy (`.github/workflows/deploy.yml`)

Gatilho: `workflow_run` do workflow `CI`, tipo `completed`, branch `main`. O job só roda se
`conclusion == 'success'`, `event == 'push'`, `head_repository.full_name == github.repository`
e a variable `DEPLOY_ENABLED == 'true'`. A variable é ligada pelo Matheus depois do primeiro
`apply` (P9): sem ela, o merge que traz os workflows para a `main` não dispara um deploy
contra infraestrutura ainda não aplicada. Todos os passos usam `github.event.workflow_run.
head_sha`, não o `GITHUB_SHA`. `concurrency` com `cancel-in-progress: false`.

Ordem dos passos:

1. Checkout do `head_sha`. Se ele já não for o HEAD da `main`, o deploy termina com aviso sem
   publicar: um CI mais novo fará o seu (evita publicar imagem mais velha por último)
2. Login no Azure por OIDC e `az acr login`
3. `docker build` com o `Dockerfile` existente e push com tag `<head_sha>`. Sem `latest`
4. `dotnet tool restore` (o `dotnet-ef` 8.0.11 do manifesto) e migration bundle autocontido
   `linux-x64`
5. Regra de firewall temporária com o IP do runner; bundle executado com a connection string
   montada de `PG_ADMIN_PASSWORD`, `PG_DATABASE`, `PG_ADMIN_USER` e do host lido por `az`,
   mascarada com `::add-mask::` antes de qualquer uso; regra removida em passo `if: always()`
6. Se aplicável (regra de escrita única, item 2), `az containerapp secret set`
7. `az containerapp update --image <registry>/renda-fixa-api:<head_sha>`
8. Smoke test: FQDN por `az containerapp show`; `GET https://<fqdn>${SMOKE_PATH}` com `curl
   --retry` e `--retry-all-errors` (a partida a frio demora), onde `SMOKE_PATH` é a variable
   de mesmo nome ou `/health`. Depois exige `latestReadyRevisionName == latestRevisionName` e
   que a imagem **dessa revisão** tenha a tag do `head_sha`. Em single revision mode a
   revisão antiga continuaria respondendo 200 se a nova não subisse, então o `/health` sozinho
   não prova nada
9. Falha em 5, 7 ou 8 deixa o workflow vermelho. Não há rollback automático

O "smoke forçado a falhar" do P9 é feito definindo `SMOKE_PATH=/nao-existe` e reexecutando o
workflow pela interface. As migrations rodam **antes** da troca da imagem, então cada uma
precisa ser compatível com a versão ainda em execução (só adicionar, nunca remover ou renomear
no mesmo deploy). Isso entra no README.

### Endpoint `/health`

`GET /health` responde 200 com corpo simples, sem acessar o banco e sem o envelope
`ApiResponse<T>`. Usa o health check embutido do ASP.NET Core (`AddHealthChecks`,
`MapHealthChecks`): nenhum pacote NuGet novo. Fora do Swagger e sem autorização. A imagem
`aspnet:8.0` só define `ASPNETCORE_HTTP_PORTS=8080`, então o `UseHttpsRedirection` não tem
porta HTTPS e não redireciona; isso é uma constatação, não algo que o endpoint trate. Se um
dia o container definir porta HTTPS, a rota precisará ser isentada do redirecionamento. O
`UseStatusCodePages` e o `ExceptionHandlingMiddleware` não interferem (a resposta tem corpo).

### Pendências herdadas da F7 (ROADMAP, passo 10)

**CDI descartado buscado para sempre.** Hoje a busca incremental parte do dia seguinte à
última cotação gravada. Uma cotação de CDI em dia não útil é descartada e não grava nada, então
a data inicial não avança e a mesma busca e o mesmo aviso se repetem a cada execução. Regra
nova: a busca incremental de CDI parte do **próximo dia útil** após a última cotação
(`BusinessDayCalendar.NextBusinessDay`, estritamente posterior), de modo que dia não útil nunca
é pedido ao provedor. Se esse dia for posterior a hoje, não há chamada. O descarte continua
para dado espúrio dentro de uma janela com dias úteis. A primeira importação continua partindo
de 01/01/2020. O calendário passa a ser carregado antes da busca, e uma última cotação no fim
da cobertura do calendário faz o CDI falhar (registrado) antes do fetch, sem afetar o IPCA.

**JSON truncado sem retry.** O `SgsNonJsonResponseHandler` hoje só rejeita corpo que não
começa com `{` ou `[`. Regra nova: o corpo de uma resposta de sucesso precisa ter **raiz
objeto ou array e ser totalmente parseável**. Corpo vazio, HTML, escalar JSON (`123`, `"x"`,
`null`), JSON truncado e JSON com lixo depois do fim (`[]xyz`) viram `HttpRequestException`,
tratada como transitória pelo retry; a mensagem diz que o JSON está incompleto ou inválido e
traz o trecho limitado a 100 caracteres. JSON válido de formato inesperado (`{"foo":1}`)
continua passando pelo handler e falhando no provedor, sem retry. Resposta de status de erro
segue intacta para o provedor.

## Implementação em passos

Passos com código .NET seguem o fluxo do projeto: testes primeiro, depois o código. Terraform
e workflows não têm teste automatizado .NET; o equivalente é `terraform fmt -check`, `validate`
e a leitura do `plan`, registrados no relatório. Ao fim de cada passo: `dotnet build` sem
warning e `dotnet test` verdes, com a contagem igual ou maior que a anterior.

- **P1 — `/health`** (Haiku). Testes no `FixedIncome.Api.Tests` com a `WebApplicationFactory`:
  `GET /health` responde 200 e corpo não vazio; responde 200 com uma factory própria cujo
  Npgsql aponta para um host inexistente e sem `EnsureCreated` (o `ApiTestBase` usa InMemory e
  nunca fica sem banco). Depois o registro e o mapeamento no `Program.cs`.
- **P2 — Pendência CDI** (Sonnet). Testes no `ImportIndexRatesUseCaseTests` com stubs: última
  cotação na véspera de um feriado e hoje igual ao feriado, sem chamada; mesma última cotação
  e hoje na terça seguinte, com chamada partindo da segunda; última na sexta e hoje no domingo,
  sem chamada; `from == today` numa segunda útil chama o provedor com `(segunda, segunda)`;
  última cotação em 31/12 do último ano coberto falha o CDI e mantém o IPCA; primeira
  importação inalterada; dado em dia não útil dentro de janela com dias úteis ainda descartado
  e contado. Renomear `Execucao_incremental_do_cdi_parte_do_dia_seguinte_a_ultima_data_importada`
  para refletir a regra nova. Depois a mudança no caso de uso.
- **P3 — Pendência JSON truncado** (Sonnet). O teste existente `Json_malformado_nao_e_repetido_
  porque_nao_e_corpo_de_outro_formato` afirma o comportamento antigo e é **invertido** (agora
  repete 4 vezes e lança `HttpRequestException`), nunca removido. Novos testes: JSON truncado
  em objeto e em array; escalar; `[]xyz`; corpo vazio e HTML continuam rejeitados; `{"foo":1}`
  passa pelo handler e falha no provedor com 1 requisição; mensagem contém "incompleto ou
  inválido" e o trecho é limitado; JSON válido com espaços passa; status de erro passa
  intacto. Depois o handler.
- **P4 — Esqueleto do Terraform e bootstrap** (Sonnet). `infra/` com providers, backend
  parcial, variáveis (a senha `sensitive`, sem default), outputs, `prod.tfvars`,
  `validation.tfvars`, os `.backend.hcl`, script de bootstrap, `.gitignore` e lock file.
  Registra a evidência de documentação sobre atributos de escrita única. `init
  -backend=false` e `validate` verdes.
- **P5a — Módulos** (Sonnet). Os cinco módulos e o banco da aplicação. `fmt -check -recursive`
  e `validate` verdes.
- **P5b — Identidade, firewall e imports** (Sonnet). Identidade do app com AcrPull, regras da
  D3, `imports.tf` e `ignore_changes` justificados. Se a assinatura estiver `Enabled`, `plan`
  real; se não, o plan fica para o P9.
- **P6 — `infra.yml`** (Sonnet). Validação de sintaxe YAML e conferência manual de
  `permissions`, `environment`, `concurrency` e da guarda de `ref`.
- **P7 — `deploy.yml` e migration bundle** (Sonnet). O bundle é gerado e executado localmente
  contra o PostgreSQL do `docker-compose` para provar que aplica as duas migrations. Atualiza o
  comentário de migration manual do `Program.cs`, que deixa de ser verdade.
- **P8 — README** (Haiku). Badge do deploy, diagrama Mermaid da infra, passo a passo do
  bootstrap, tabela de segredos, como saber qual commit está rodando
  (`az containerapp show --query properties.template.containers[0].image`), a regra de
  migration compatível, o risco da D3 e do environment sem reviewers, e o aviso de que parar
  o PostgreSQL para economizar faz toda migration falhar.
- **P9 — Validação ao vivo** (com o Matheus; exige assinatura `Enabled`). Cada escrita no
  Azure ou no GitHub só acontece com autorização dele naquele momento. Sequência, em ordem:
  1. Religar o PostgreSQL; listar banco e regras de firewall existentes
  2. Rodar o bootstrap; cadastrar variables e o secret `PG_ADMIN_PASSWORD`; **não** ligar
     `DEPLOY_ENABLED`
  3. Primeiro `plan` e `apply` **localmente** pelo Matheus (`az login`, backend remoto), com os
     imports (D2 se aplicar), porque o `workflow_dispatch` só existe depois que o `infra.yml`
     chega à `main`
  4. `plan` seguinte sem diferença; prova local de senha fora do state
  5. Recriação do zero: `apply` com `validation.tfvars` no resource group de validação, `plan`
     sem diferença, `destroy` e remoção do grupo
  6. PR com mudança em `infra/` (plan no PR); merge na `main`
  7. Ligar `DEPLOY_ENABLED`; reexecutar o `deploy.yml` até o smoke; conferir imagem e SHA
  8. `workflow_dispatch` do `infra.yml` (apply sem mudança) para provar o caminho do CI
  9. Smoke forçado a falhar com `SMOKE_PATH`; desfazer
  10. R-01 ao vivo entre 21h e 24h de Brasília
  O resultado de cada item vai para o `VALIDACAO-F8.md` com o ID da execução ou a saída.

## Testes

- Baseline: 558 testes (Domain 292, Application 105, Infrastructure 110, Api 51). A contagem
  não pode cair; esperam-se ao menos 14 novos (P1: 2, P2: 6, P3: 8, descontado o teste
  invertido, que não conta como novo)
- Convenções do `CLAUDE.md`: AAA com seções separadas, sem biblioteca de mock, dublês `private
  sealed` no arquivo, nomes que descrevem o comportamento
- Terraform e workflows: validação estática local e, no P9, execução real. Critério que
  depende de execução no Azure ou no GitHub só é marcado com a evidência dessa execução

## Critérios de aceite

Locais (sem o Azure):

1. `dotnet build` sem warning e `dotnet test` verde, contagem maior que 558
2. `GET /health` responde 200 sem banco
3. A busca de CDI nunca pede dia não útil ao provedor; teste de fronteira do feriado passa
4. JSON truncado, escalar e com lixo final provocam retry; nenhum teste antigo removido sem
   substituto
5. `terraform fmt -check -recursive` e `terraform validate` verdes
6. Nenhum `*.tfstate`, `*.tfvars` com segredo, senha ou connection string no repositório
   (busca registrada); backend sem fallback local
7. O migration bundle aplica as duas migrations no PostgreSQL local
8. `infra.yml` e `deploy.yml` com YAML válido, `permissions` mínimas, guardas do `workflow_run`
   e `head_sha` na tag
9. README com badge, diagrama, bootstrap, segredos e riscos aceitos

Ao vivo (dependem da assinatura `Enabled` e do GitHub; sem marca no ROADMAP até haver
evidência):

10. Os cinco recursos importados e o primeiro `plan` sem destruição (ou a exceção da D2)
11. `apply` num resource group vazio recria tudo e o `plan` seguinte não mostra diferença
12. Merge na `main` publica a nova versão sem passo manual, com imagem etiquetada pelo SHA
13. As migrations são aplicadas pelo pipeline
14. Smoke test que falha marca o deploy como falho
15. Nenhuma senha ou connection string nos logs do Actions

## Restrições

- Não adicionar pacote NuGet. Ferramenta: só o `dotnet-ef` já fixado. Provider novo de
  Terraform só com justificativa
- Não alterar regra de negócio, cálculo, contrato de API ou `ci.yml`
- Não criar camada nem abstração nova no .NET; o único código de produção novo é o `/health`
  e as duas correções
- Nenhuma escrita no Azure ou no GitHub (bootstrap, apply, secrets, push) sem autorização do
  Matheus naquele momento
- Não executar `git add` nem `git commit`

## Relatório de cada passo

Arquivos criados e alterados, saída de `dotnet build` e `dotnet test` com a contagem, para os
passos de Terraform a saída de `fmt`, `validate` e `plan`, e as decisões tomadas que esta
spec não cobria.

## Revisão da spec (`revisor-arquitetura`, 08/11/2026)

Reprovada na primeira versão por 3 bloqueantes, todos incorporados:

- B1 permissão para o AcrPull → RBAC Administrator condicionado no bootstrap
- B2 ordem do P9 e deploy no primeiro merge → apply local antes do merge e `DEPLOY_ENABLED`
- B3 recriação do zero → RG de validação, `validation.tfvars`, state em RG separado

Importantes incorporados: I1 (justificativa errada sobre `IndexRatesImport:Enabled`, removida;
a chave está no `appsettings.json`), I2 (exceção no `.gitignore`), I3 (teste invertido), I4
(definição de JSON válido), I5 (testes do `/health`), I6 (smoke por revisão), I7 (guardas do
`workflow_run`), I8 (apply só na `main`, risco registrado), I9 (plano no mesmo job), I10 (escrita
única e alternativa), I11 (`ignore_changes` geral), I12 (placeholder e probes TCP), I13 (variáveis
do banco, banco e firewall existentes). Sugestões S1 a S4, S6 a S11 incorporadas.

Não incorporado:

- I5, isenção de redirecionamento no `/health`: o container não define porta HTTPS, então não
  há redirecionamento a isentar. Virou constatação na spec, sem teste que não discrimina nada
- S5, tirar o AcrPush do bootstrap: mantido para não depender de Contributor bastar para o
  push; se o P9 provar que basta, o papel sai
