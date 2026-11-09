# Spec de execução — F8: Entrega contínua da imagem e pendências da F7

Status: aprovada (08/11/2026)

Documento de execução para agente. Objetivo: a cada merge na `main`, uma imagem versionada,
validada contra um PostgreSQL de verdade no próprio pipeline e publicada no GitHub Container
Registry (GHCR), sem passo manual. A fase também resolve as duas primeiras pendências herdadas
da F7 e cria o `GET /health`.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão em
`CLAUDE.md`. Ambas prevalecem sobre qualquer suposição. O plano da v2 está em
`docs/ROADMAP.md`, seção "F8".

Em 08/11/2026 a publicação em nuvem foi adiada: a assinatura do Azure está desabilitada e não há
orçamento (ver ROADMAP, "Adiado: publicação em nuvem com Terraform"). A F8 entrega o que dá para
provar sem nuvem: imagem testada no GHCR, que é gratuito.

Estado de partida: 558 testes (Domain 292, Application 105, Infrastructure 110, Api 51), build
sem warning. O `ci.yml` roda build e testes em push e PR para a `main`. O `Dockerfile` já existe
(F6), a imagem roda como `appuser` e escuta na porta 8080. O `dotnet-ef` 8.0.11 já é ferramenta
local em `.config/dotnet-tools.json`. Não existe `/health` no código.

Esta fase toca:

- `src/FixedIncome.Application` — `ImportIndexRatesUseCase` (pendência do CDI)
- `src/FixedIncome.Infrastructure` — `SgsNonJsonResponseHandler` (pendência do JSON truncado) e
  `FixedIncomeDbContextFactory` (para o bundle rodar fora de `src/`)
- `src/FixedIncome.Api` — registro e mapeamento do `/health`
- `.github/workflows/` — `release.yml` novo; `ci.yml` ganha o gatilho `workflow_call`
- `README.md` e `docs/` — documentação da fase
- Os projetos de teste correspondentes

## Fora de escopo

- Publicação em nuvem, Terraform e qualquer recurso do Azure (adiado; ROADMAP)
- Terceira pendência da F7, "R-01 ao vivo": exige repetir o cenário entre 21h e 24h de Brasília
  com a API rodando; fica aberta no ROADMAP
- Extensão do calendário ANBIMA além de 2030 (item de Manutenção do ROADMAP)
- Testcontainers, NetArchTest e Stryker (F9). Os testes de repositório continuam com InMemory
- `/health/live` e `/health/ready` com checagem do PostgreSQL (F10). Aqui o `/health` só diz que o
  processo está de pé
- `HEALTHCHECK` no `Dockerfile` (a imagem `aspnet` não traz `curl`), imagem multi-arquitetura,
  assinatura, SBOM e scan de vulnerabilidade da imagem
- Aplicar migration automaticamente em qualquer ambiente: a decisão da F4b (migration manual) vale.
  O bundle é a forma de aplicá-la sem o SDK
- Alterar regras de cálculo, casos de uso ou controllers além do que está nos passos P1, P2, P3 e P5

## Pacotes autorizados

Nenhum pacote NuGet novo. O health check faz parte do ASP.NET Core
(`Microsoft.NET.Sdk.Web`) e o `dotnet-ef` já está em `.config/dotnet-tools.json`.

O pipeline usa só `actions/checkout@v4`, `actions/setup-dotnet@v4` e `actions/upload-artifact@v4`,
as mesmas ações do `ci.yml`. O login, o build e o push da imagem usam a CLI do Docker que já vem no
runner, em vez de ações de terceiros (`docker/login-action`, `docker/build-push-action`): menos
código de terceiros com acesso ao `GITHUB_TOKEN`.

## Decisões de desenho

**Estrutura do pipeline.** `release.yml` dispara em `push` na `main`. Seu primeiro job chama o
`ci.yml` como workflow reutilizável (`uses: ./.github/workflows/ci.yml`; o `ci.yml` ganha
`workflow_call` além dos gatilhos atuais) e o job de release depende dele (`needs`). Alternativa
descartada: `workflow_run` sobre o CI. Ele só dispara a partir do arquivo na branch padrão, então
não se testaria o pipeline antes do merge, e exige cuidado extra para fazer checkout do commit
certo. Custo da escolha: um push na `main` roda o CI duas vezes (o standalone e o chamado). Aceito,
porque mantém o badge do `ci.yml` válido e o repositório é gratuito para Actions.

**Candidata e aprovada.** A imagem é publicada primeiro com a tag do SHA completo do commit
(`ghcr.io/matheuscardosolazzarini/renda-fixa-api:<sha>`): é a candidata. O smoke test sobe essa
imagem, baixada de novo do GHCR (o passo remove a cópia local e faz `docker pull`; sem isso o
`docker run` usaria o cache do build e não provaria o que está no registro), e só se passar a tag `latest` é movida para o mesmo digest. Se o smoke
falhar, a execução falha e `latest` continua na versão anterior. Uma tag de SHA cuja execução falhou
fica no registro; ela não é apagada pelo pipeline, e só `latest` indica uma versão aprovada.

**`latest` só a partir da `main`.** O passo que move `latest` tem `if: github.ref == 'refs/heads/main'`.
Isso permite, se o Matheus quiser provar o pipeline antes do merge, incluir a branch da fase
temporariamente em `on.push.branches` sem arriscar a `latest`. Essa inclusão é decisão e ação dele
(publica no GHCR a partir da branch) e não faz parte da entrega.

**Sem segredo novo.** Só o `GITHUB_TOKEN`, com `permissions` mínimas: `contents: read` no topo e
`packages: write` apenas no job de release. O token entra no `docker login` por variável de ambiente
e `--password-stdin`, com `-u ${{ github.actor }}`, nunca interpolado no script. O smoke usa um banco descartável com senha
literal no YAML, que não é segredo.

**Ordem dos envios concorrentes.** `concurrency: { group: release, cancel-in-progress: false }`:
uma execução por vez, e a mais antiga na fila é descartada quando chega outra, preservando a ordem
em que `latest` é movida. O SHA de uma execução descartada não vira imagem.

**Visibilidade do pacote.** O primeiro push cria um pacote privado no GHCR. Torná-lo público é um
passo manual único do Matheus, nas configurações do pacote, e não é feito pelo pipeline. O pipeline
vincula o pacote ao repositório com o rótulo OCI `org.opencontainers.image.source`. O README diz isso.

**`/health` sem envelope.** O `/health` usa `AddHealthChecks()` e `MapHealthChecks("/health")` do
framework, sem nenhuma checagem registrada: responde 200 com o texto `Healthy`. Não é controller, e
por isso não usa `ApiResponse<T>`: o consumidor é o orquestrador e o `curl --fail`, que só olham o
status. Não consulta o banco (isso é o `/health/ready` da F10). O `/api/assets` do smoke é que prova
a conexão com o banco e as migrations.

**Bundle.** O comando completo, executado na raiz do repositório:

```
dotnet ef migrations bundle --project src/FixedIncome.Infrastructure \
  --startup-project src/FixedIncome.Api --configuration Release \
  --self-contained --target-runtime linux-x64 --output artifacts/efbundle --force
```

É self-contained para rodar sem SDK nem runtime instalado. A conexão vai na execução pela variável
`ConnectionStrings__DefaultConnection`, a mesma que a API lê. Emenda do P6 (09/11/2026): a spec
previa só `--connection`, mas o bundle instancia o contexto pela fábrica antes de aplicá-lo, e a
fábrica lança exceção sem fonte (verificado: sem a variável o bundle sai com exit 1 mesmo com
`--connection`). `--connection` é opcional e, se vier junto, sobrescreve a variável. Hoje isso não funciona fora de `src/`: a `FixedIncomeDbContextFactory` (que o
`dotnet ef` e o bundle usam, no lugar do host da API) lê
`<diretório atual>/../FixedIncome.Api/appsettings.Development.json`, e o bundle falha com
erro de arquivo não encontrado quando executado da raiz ou de qualquer pasta que não seja irmã de
`FixedIncome.Api` (verificado na revisão da spec). A correção é o P5: a fábrica passa a usar a
variável `ConnectionStrings__DefaultConnection` quando existir, como o `Program.cs`, e só então cai
no arquivo. A criação do bundle não exige a variável (verificado); o implementador confirma na
reprodução e só a define no passo se isso mudar.

O `upload-artifact@v4` não preserva o bit de execução: quem baixar precisa de `chmod +x efbundle`,
e o README diz isso. O artefato é `efbundle-<sha>`, com `retention-days: 30` (o bundle
self-contained tem cerca de 120 MB por merge, 125.026.675 bytes medidos no P6). Em repositório
privado no plano gratuito, a cota de 500 MB de armazenamento pode estourar com a retenção de 30
dias; reduzir `retention-days` é decisão do Matheus. Baixar artefato exige login no GitHub e ele expira.

**Smoke test.** Service container `postgres:16` com `pg_isready` como health check. Passos, em
ordem:

1. aplica o bundle no banco limpo, com a conexão na variável `ConnectionStrings__DefaultConnection`
   (sem `--connection`);
2. remove a imagem local, baixa `$IMAGE:$SHA` do GHCR e a sobe com `docker run --network host`, com
   `ConnectionStrings__DefaultConnection` apontando para `localhost` e `IndexRatesImport__Enabled=false`
   (a importação chamaria o Banco Central);
3. espera `GET /health` responder 200, por até 60 segundos;
4. exige 200 em `GET /health` e em `GET /api/assets` (`curl --fail`);
5. se qualquer passo falhar, imprime `docker logs` do contêiner e falha a execução.

A imagem roda em `Production` (o `Dockerfile` não define ambiente), sem Swagger.

## Regras novas

Duas correções de comportamento, herdadas da F7. As duas são decididas aqui e passam a valer como
parte desta spec.

**CDI: a busca incremental parte do próximo dia útil.** Hoje a busca parte do dia seguinte à última
data importada (`last + 1`). Cotação de CDI em dia não útil é descartada e não entra na tabela;
se ela for a mais recente devolvida pelo provedor, a última data importada não avança e cada
execução (a cada 6 horas) busca de novo a mesma data e repete o mesmo aviso. A nova regra: o início
da busca é `calendar.NextBusinessDay(last)`, o primeiro dia útil estritamente posterior à última
data importada. Dias não úteis entre a última cotação e o próximo dia útil nunca são pedidos. Na
primeira execução (sem cotação importada) o início continua em 2020-01-01.

Consequências:

- `from > today` continua encerrando a execução sem chamar o provedor. Numa sexta importada, a
  execução de sábado e a de domingo não chamam o provedor.
- O calendário passa a ser carregado antes da busca, e não só depois.
- Cotação de CDI em dia não útil dentro do intervalo buscado continua descartada e registrada em
  log, com a contagem em `CdiDiscarded`. Só não é mais pedida de novo depois que uma cotação
  posterior avança a última data, ou quando está entre a última data e o próximo dia útil.
- Resíduo aceito: se existir dia útil sem cotação antes da data descartada (lacuna no provedor), a
  busca continua a partir desse dia útil e o aviso se repete até a lacuna fechar. Nesse caso a
  repetição é o comportamento correto, porque a busca precisa mesmo continuar.
- A falha por data fora da cobertura do calendário (a partir de 2031) continua como está, e é item
  de Manutenção do ROADMAP.

**SGS: corpo precisa ser JSON bem formado.** Hoje o `SgsNonJsonResponseHandler` trata como falha
transitória (e o Polly repete) só o corpo de sucesso que não começa com `{` ou `[`; um JSON
truncado que começa com `[` chega ao provedor e falha com `JsonException` sem nova tentativa. A nova
regra: resposta de sucesso é aceita só se o corpo, depois de `TrimStart`, começa com `{` ou `[` **e**
é JSON bem formado por inteiro (`JsonDocument.Parse` com as opções padrão, que o provedor também
usa). Caso contrário, o handler lança `HttpRequestException` e o retry repete (3 repetições, como
as demais falhas de transporte). Esgotadas as tentativas, a falha propaga e a importação a
registra. Status de erro continua passando sem alteração para o provedor decidir.

Esta regra **substitui** a decisão da F7 (`SPEC-F7-EXECUCAO.md`, NB-02) de que "JSON malformado que
começa com `{` ou `[` não é repetido". O teste `Json_malformado_nao_e_repetido_porque_nao_e_corpo_de_outro_formato`
afirma o comportamento antigo e é substituído por testes do novo, não apagado.

## Implementação

Cada passo indica o modelo do `implementador`: Sonnet para passo com regra ou desenho, Haiku para
passo mecânico. Em todo passo de código, o teste vem antes e falha pelo motivo certo antes da
correção.

### P1 — CDI: busca a partir do próximo dia útil (Sonnet)

Em `ImportIndexRatesUseCase.ImportCdiAsync`, carregue o `BusinessDayCalendar` antes da busca e
calcule `from` como descrito em "Regras novas". Um único calendário serve à busca e ao filtro de dia
útil. Atualize o comentário do método com o motivo.

Testes novos em `ImportIndexRatesUseCaseTests` (valores no calendário de teste de 2025; a Paixão é
18/04 e Tiradentes 21/04):

- última cotação na quinta 17/04: a busca parte de 22/04, pulando a Paixão, o fim de semana e
  Tiradentes
- última cotação na sexta 25/04, IPCA já importado até o mês corrente e "hoje" no domingo 27/04: o
  provedor não é chamado, nem para CDI nem para IPCA (próximo dia útil, 28/04, é posterior a hoje)
- última cotação na sexta 25/04 e "hoje" na segunda 28/04: o provedor é chamado com 28/04 a 28/04
  (fronteira: `from == today`)
- cotação descartada que é a mais recente (provedor devolve 17/04 e o sábado 19/04): a primeira execução
  importa 1 e descarta 1 com um aviso; a segunda execução parte de 22/04 e não repete o aviso
- o teste existente `Execucao_incremental_do_cdi_parte_do_dia_seguinte_a_ultima_data_importada` (última
  16/04, parte de 17/04) continua passando, mas passa a se chamar
  `Execucao_incremental_do_cdi_parte_do_proximo_dia_util_apos_a_ultima_data_importada`

### P2 — SGS: JSON truncado é falha transitória (Sonnet)

Em `SgsNonJsonResponseHandler`, depois da checagem do primeiro caractere, valide o corpo inteiro com
`using var document = JsonDocument.Parse(body)`; `JsonException` vira a mesma
`HttpRequestException` de hoje. A mensagem dos dois casos (HTML e JSON truncado) contém "não é JSON",
que é o que o teste existente confere. Confira as opções de leitura do `SgsIndexRateProvider` para a
validação do handler não ser mais estrita nem mais frouxa do que a do provedor. Atualize o
comentário da classe.

Testes em `SgsNonJsonResponseHandlerTests`:

- substitui `Json_malformado_nao_e_repetido...` por: JSON truncado na primeira tentativa e válido na
  segunda devolve a cotação, com 2 requisições (pipeline real de produção, como os testes vizinhos)
- JSON truncado em todas as tentativas propaga `HttpRequestException` depois de 4 requisições
- ampliar a theory de corpo inválido com `[{"data":"26/10/2026","valor":`, `{`, `[1,2` e `{"a":1}}`
- os casos de corpo válido (`[...]`, `{"erro":{"statusCode":404}}`, `   [] `) continuam passando
  sem alteração
- o corpo que já falhava antes (vazio, texto, HTML) continua falhando

### P3 — `GET /health` (Sonnet)

No `Program.cs`: `builder.Services.AddHealthChecks()` e `app.MapHealthChecks("/health")`, ao lado do
`MapControllers`, restrito a `GET` e `HEAD` com
`.WithMetadata(new HttpMethodMetadata(new[] { "GET", "HEAD" }))`. Sem isso o `MapHealthChecks` aceita
qualquer verbo (verificado na revisão: `POST /health` responde 200). Sem checagem registrada.
Comentário curto dizendo que é liveness e que o `/health/ready` com o banco é da F10.

Teste em `Api.Tests` (`HealthEndpointTests : ApiTestBase`): `GET /health` responde 200 com o corpo
`Healthy`. Teste de que `POST /health` responde 405 no envelope `ApiResponse` (a restrição de verbo
faz o 405 sair vazio, e o `UseStatusCodePages` do projeto escreve o envelope): protege o contrato dos
erros de rota.

### P4 — `release.yml`: build e push da imagem (Sonnet)

- `ci.yml` ganha `workflow_call:` em `on`, sem alterar os passos nem os gatilhos atuais.
- `release.yml`: `on.push.branches: [main]`; `permissions: contents: read` no topo; `concurrency`
  conforme as decisões. Job `ci` que chama `./.github/workflows/ci.yml`. Job `release` com
  `needs: ci`, `permissions: { contents: read, packages: write }` e `env.IMAGE:
  ghcr.io/matheuscardosolazzarini/renda-fixa-api`.
- Passos do `release`: checkout; `docker login ghcr.io -u ${{ github.actor }}` com `GITHUB_TOKEN`
  (em `env`) por `--password-stdin`;
  `docker build` com `--tag $IMAGE:${{ github.sha }}`, `--label org.opencontainers.image.source=https://github.com/${{ github.repository }}`
  e `--label org.opencontainers.image.revision=${{ github.sha }}`; `docker push` da tag do SHA.
- O push de `latest` é o último passo (P7), só depois do smoke.

### P5 — Fábrica de design-time lê a connection string do ambiente (Sonnet)

Em `FixedIncomeDbContextFactory`, use `ConnectionStrings__DefaultConnection` quando definida e não
vazia; sem ela, mantenha a leitura do `appsettings.Development.json` (o `dotnet ef` local continua
funcionando). Sem a variável e sem o arquivo, lance exceção com mensagem que cite as duas fontes.
Atualize o comentário da classe. Testes em `Infrastructure.Tests`: com a variável definida a fábrica
cria o contexto com aquela conexão, sem ler arquivo; sem a variável e com o diretório atual fora de
`src/`, a exceção cita as duas fontes. Restaure o valor da variável ao fim de cada teste, e coloque
esses testes numa classe própria (variável de ambiente é estado global do processo).

### P6 — Migration bundle como artefato (Sonnet)

No job `release`, antes do build da imagem: `setup-dotnet`, `dotnet tool restore`, o comando de
bundle da decisão "Bundle" e `actions/upload-artifact@v4` com o nome `efbundle-${{ github.sha }}` e
`retention-days: 30`. Reproduza o comando localmente, na raiz, e reporte: o bundle gera; roda num
contêiner `mcr.microsoft.com/dotnet/runtime-deps:8.0` (traz o que o self-contained precisa), com a
raiz montada como diretório de trabalho, contra o PostgreSQL do `docker-compose.yml`; e deixa o
esquema completo (tabelas e seed de feriados).

### P7 — Smoke test e tag `latest` (Sonnet)

No job `release`, depois do push do SHA: `services.postgres` (`postgres:16`, `POSTGRES_DB`, `POSTGRES_USER` e
`POSTGRES_PASSWORD` descartáveis, `pg_isready` no health check, porta 5432 publicada) e os passos da
decisão "Smoke test". Por último, o passo `latest`: `docker tag $IMAGE:$SHA $IMAGE:latest` e
`docker push $IMAGE:latest`, com `if: github.ref == 'refs/heads/main'`.

Reproduza os passos do smoke localmente com Docker Desktop, adaptando só o host do banco, e rode o
`actionlint` fixado em versão (`docker run --rm -v "${PWD}:/repo" -w /repo rhysd/actionlint:1.7.7`)
sobre os dois workflows. Um smoke que falha de propósito (por exemplo, sem aplicar o bundle) precisa
terminar com exit code diferente de zero.

### P8 — README e documentos (Haiku)

- README: badge do `release.yml` ao lado do badge do CI; seção "Imagem publicada" com como baixar a
  imagem do GHCR, como aplicar o bundle (`chmod +x`; artefato exige login no GitHub e expira em 30
  dias) e como subir a imagem contra o PostgreSQL do Compose; aviso
  de que o pacote do GHCR é privado até o Matheus torná-lo público; por que a nuvem está adiada
  (resumo do ROADMAP); `GET /health` na lista de endpoints; contagem de testes e tabela de cobertura
  com os números medidos pelo verificador
- `docs/ESPECIFICACAO.md`: substituir o parágrafo "A F1 previa um health check, mas ele não existe no
  código" pelo estado real e registrar que o `/health` é exceção ao "respostas encapsuladas em
  `ApiResponse<T>`" (texto `Healthy`, só status); registrar as duas regras novas na regra de importação, se houver lugar
  natural, ou na seção de decisões técnicas
- `FixedIncome.Api.http`: requisição de `GET /health`
- `docs/ROADMAP.md` só é alterado no fechamento, pelo orquestrador

## Testes

Siga a skill `testes-xunit`: AAA com seções separadas, nome descrevendo o comportamento, dublês
`private sealed` e nenhuma biblioteca de mock. Esta fase não tem casos de referência de cálculo: os
valores esperados dos testes de P1 vêm das datas do calendário de 2025 já usado na classe de teste,
conferidas contra os feriados de abril (Paixão 18/04, Tiradentes 21/04).

O pipeline em si não tem teste automatizado de unidade. A evidência dele é: `actionlint` limpo, a
reprodução local de cada passo (P6 e P7), e a primeira execução real no GitHub, que só existe depois
do push. Os critérios que dependem dessa execução ficam abertos no ROADMAP até haver o link da
execução.

## Restrições

- Não altere regras de cálculo, entidades, controllers existentes nem migrations
- Não adicione pacotes NuGet nem ações de terceiros além das listadas
- Não adicione segredo, variável de repositório nem permissão além das descritas
- Não execute `git add`, `git commit` nem `git push`. Publicar no GHCR é ação do pipeline, depois do
  push do Matheus
- Não crie camadas, interfaces ou serviços novos para as correções de P1 e P2

## Critérios de aceite

Os mesmos do ROADMAP, mais a verificação de build e testes:

1. As duas pendências da F7 resolvidas, cada uma com teste escrito antes da correção
2. `GET /health` responde 200, com teste
3. `release.yml` publica a imagem com a tag do SHA em push na `main`, só com o `GITHUB_TOKEN`, depois
   do CI verde
4. O bundle é gerado e anexado como artefato
5. O smoke aplica o bundle num PostgreSQL limpo, e a imagem responde 200 em `/health` e `/api/assets`;
   smoke que falha reprova a execução e não move `latest`
6. Nenhum segredo novo no repositório nem nos logs
7. README com o badge, as instruções de execução da imagem e o motivo do adiamento da nuvem
8. `dotnet build` sem warning e `dotnet test` verde, com a contagem de testes igual ou maior que
   558
9. Nenhum commit foi criado

## Relatório final de cada passo

Reporte: arquivos alterados, resultado de `dotnet build` e `dotnet test` com a contagem, o teste que
falhou antes da correção (nome e motivo da falha), e qualquer decisão tomada que não estava
especificada aqui.

## Emenda pós-QA (09/11/2026, decidida pelo Matheus)

O QA exploratório (`docs/QA-F8.md`) deixou 17 riscos. Decisão: nenhum fica pendente. Cada um é
corrigido (com teste quando é código) ou aceito com o motivo registrado no ROADMAP. Esta emenda vale
como parte da spec e substitui o que contradiz o texto acima (retenção do bundle, gatilhos do
`release.yml`, expressões em `run`).

**Corrigir.**

- **R-02.** O passo `latest` só roda se `github.sha` ainda for o topo da `main` (consulta ao
  remoto na hora, por `gh api repos/$GITHUB_REPOSITORY/commits/main`) e o ref for a `main`; senão
  pula com `::warning::` no log. Vale também para `workflow_dispatch`.
- **R-03.** `persist-credentials: false` em todos os checkouts; `permissions: contents: read` no
  `ci.yml`; `github.actor`, `github.sha` e `github.repository` entram em `run` só via `env:` e com
  aspas; ações fixadas por SHA de commit e `postgres:16` por digest, com a versão em comentário;
  `.github/dependabot.yml` para `github-actions` e `docker`. `zizmor` de novo: zero achados altos e
  médios; os baixos que sobrarem são justificados. Se o `zizmor` exigir (`excessive-permissions`),
  separe o bundle num job com `contents: read` e deixe `packages: write` só no job que publica.
- **R-15.** `paths-ignore` para `docs/**` e `*.md` no `release.yml`; `workflow_dispatch` no
  `release.yml`, sujeito à trava do R-02; laço de espera com prazo real de 60 s; `overwrite: true`
  no upload do `coverage-report`; `retention-days: 7` no bundle (era 30; a mudança vem do tamanho de
  ~120 MB por merge).
- **R-05.** Log de informação na partida quando a importação de cotações está desligada.
- **R-10.** A mensagem do resumo da carteira sem cotação diz qual posição falhou e lembra que a
  importação pode estar desligada.
- **R-17.** Sem connection string, a API falha na partida com mensagem clara, sem pilha e sem
  exit 139. Com teste.
- **R-07.** `FixedIncome.Api.http` com variável de base que cobre as duas portas (5124 com
  `dotnet run`, 8080 com Docker).
- **README, seção da imagem.** R-04, R-06, R-12, R-13 e R-14 (detalhes no passo de documentação).
- **R-16.** Só o comentário do provedor: registra o que o QA observou. O contorno fica.

**Aceitar, com o motivo no ROADMAP.** R-08 e R-09 (tratados no `/health/ready` da F10); R-11
(comportamento do EF, documentado no README); R-16 (efeito nulo); CI duas vezes por merge (decisão
desta spec); R-15, o smoke que usa o arquivo gerado no workspace em vez do baixado do artefato (o
`chmod` já simula o bit de execução perdido); R-15, tags de candidatas que se acumulam no GHCR
(limpeza fica para quando o volume importar).
