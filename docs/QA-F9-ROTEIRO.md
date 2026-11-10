# Roteiro de QA — F9: Qualidade (Testcontainers, NetArchTest e Stryker.NET)

Roteiro exploratório para o agente `qa-exploratorio`. O relatório vai em `docs/QA-F9.md`.
**Status: aguardando aprovação do Matheus** (ver "Decisões antes de executar", no fim).

A F9 não mudou comportamento da API: no código de produção entraram só 14 linhas de comentário
`// Stryker disable` em `src/FixedIncome.Domain`. Mudou a infraestrutura de testes. Por isso o
"usuário" desta fase é quem clona o repositório e segue o README: roda os testes com e sem Docker,
roda o Stryker, lê os workflows e confere a documentação.

A `docs/VALIDACAO-F9.md` já provou, e isso **não** se repete aqui: build sem warning em Debug e
Release; 656 testes verdes na árvore de trabalho; contêineres `postgres:16` e Ryuk vistos no
`docker ps` e removidos ao fim de **uma** execução normal; cada classe de integração passando
sozinha; Stryker com 99,33% (95,30% sem os `disable`) e a lista das 21 desabilitações;
`actionlint` 1.7.7 e `zizmor` 1.14.2 limpos; leitura do `mutation.yml` item a item; cobertura por
camada.

O que a validação **não** cobriu, e é o foco deste roteiro:

- a jornada de quem clona: cópia limpa, sem `bin/obj`, comandos do README copiados literalmente;
- Docker de fato indisponível (a mensagem só foi provada por simulação dentro do código);
- limpeza depois de interrupção, execuções simultâneas e convivência com o `docker compose`;
- variáveis de ambiente que um desenvolvedor costuma ter definidas no terminal;
- o Stryker fora do caminho feliz: outra pasta, execução repetida, score abaixo do limite;
- regras de arquitetura diante de violações que a prova do P5 não tentou;
- os workflows da perspectiva do commit que vai ser feito (arquivos ainda não rastreados) e do
  `release.yml`, que chama o `ci.yml`;
- regressão rápida da API pelo caminho do usuário final;
- instabilidade (flaky) em execuções repetidas e sob carga;
- coerência entre README, `CLAUDE.md`, `ESPECIFICACAO.md`, `SPEC-F9-EXECUCAO.md` e
  `VALIDACAO-F9.md`.

Regras herdadas do agente: caixa-preta primeiro (o código de `src/` e de `tests/` só é aberto para
explicar um achado já reproduzido); cada achado traz comando, saída obtida, saída esperada e a fonte
da expectativa; nada é corrigido; nenhum `docker compose down -v`, nenhum volume apagado; dados de
teste com prefixo `QA-`.

## Ambiente

### O que precisa estar de pé

- Docker Desktop aberto, engine Linux (o `docker --version` desta máquina é 29.7.2).
- .NET SDK 8 (`dotnet --version` 8.0.x; não há `global.json`, então registre também
  `dotnet --list-sdks`: um SDK 9 ou 10 instalado seria escolhido no lugar do 8).
- `dotnet tool restore` executado **na cópia** (traz `dotnet-ef` 8.0.11 e `dotnet-stryker` 4.16.0).
- Imagens já presentes na máquina e usadas aqui: `postgres:16`, `testcontainers/ryuk:0.14.0`,
  `mcr.microsoft.com/dotnet/sdk:8.0`, `rhysd/actionlint:1.7.7`, `ghcr.io/zizmorcore/zizmor:1.14.2`,
  `postgres:16-alpine`. Nenhum pull novo é necessário, exceto nos itens marcados como opcionais.

### O que não pode ser tocado

- **O repositório do Matheus** (`c:\Users\matheuss\Desktop\renda-fixa\renda-fixa-api`): só leitura.
  Nada de `dotnet build`, `dotnet test`, `dotnet stryker` nem `docker compose` nele. Todo comando
  que compila, gera arquivo ou altera algo roda numa **cópia descartável** no scratchpad
  `C:\Users\matheuss\AppData\Local\Temp\claude\8c9101ad3d07d8d0\bae42765-baa0-4198-8cbe-db4c78a3e8cd\scratchpad\`.
  Motivo: o `dotnet test` e o Stryker escrevem em `bin/`, `obj/`, `TestResults/` e `StrykerOutput/`
  e disputariam essas pastas com o IDE do Matheus.
- **Contêineres, volumes e imagens que já existem** antes do QA (foto no passo 0). Em especial os
  nomes `fixedincome-db` e `fixedincome-api` e o volume `renda-fixa-api_fixedincome-db-data`.
- **O Docker Desktop**: não parar, não pausar, não reiniciar, não trocar de engine sem a decisão 1.
- **O serviço do Windows `postgresql-x64-17`** (parado, ressalva vii da validação): não iniciar.
- **Imagens**: não remover `postgres:16` nem a do Ryuk (remover forçaria pull no Docker Hub e
  afetaria o Matheus).

### Preparação (Git Bash)

Os comandos estão em Git Bash. No PowerShell 5.1, `curl` é alias de `Invoke-WebRequest`; use
`curl.exe`.

0. Variáveis da sessão e foto do estado, só leitura:

   ```bash
   export MSYS_NO_PATHCONV=1
   REPO=/c/Users/matheuss/Desktop/renda-fixa/renda-fixa-api
   QA=/c/Users/matheuss/AppData/Local/Temp/claude/8c9101ad3d07d8d0/bae42765-baa0-4198-8cbe-db4c78a3e8cd/scratchpad/qaf9
   mkdir -p "$QA"
   cd "$REPO" && git rev-parse HEAD && git status --short > "$QA/git-status-antes.txt"
   docker ps -a --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' > "$QA/antes-ps.txt"
   docker images --format '{{.Repository}}:{{.Tag}}' | sort > "$QA/antes-images.txt"
   docker volume ls -q | sort > "$QA/antes-volumes.txt"
   docker network ls --format '{{.Name}}' | sort > "$QA/antes-networks.txt"
   netstat -ano | grep -E ':(5432|8080) ' || echo "5432 e 8080 livres"
   dotnet --list-sdks; docker --version
   env | grep -iE 'DOCKER_HOST|TESTCONTAINERS|ConnectionStrings|ASPNETCORE|DOTNET_ENV' || echo "sem variáveis relevantes"
   ls ~/.testcontainers.properties 2>/dev/null || echo "sem ~/.testcontainers.properties"
   ```

1. **Cópia descartável.** A fase ainda não está commitada (HEAD `a741bf4` é anterior a ela), então
   um `git clone` traria a F8, não a F9. A cópia leva só o que entraria num commit: arquivos
   rastreados mais os não rastreados que o `.gitignore` não ignora. Sem `bin/`, `obj/`,
   `TestResults/` e `StrykerOutput/`.

   ```bash
   C="$QA/clone"; mkdir -p "$C"
   cd "$REPO" && git ls-files -co --exclude-standard -z | tar --null -T - -cf - | tar -xf - -C "$C"
   find "$C" -type d \( -name bin -o -name obj -o -name StrykerOutput -o -name TestResults \) | wc -l   # 0
   cd "$C" && git init -q && git add -A && git -c user.name=qa -c user.email=qa@local commit -qm "qa: base"
   ```

   O `git init` é **da cópia**, para o `git status` mostrar depois o que a execução gerou fora do
   `.gitignore`. Se a decisão 4 for executar depois do commit, troque o passo por
   `git clone https://github.com/matheuscardosolazzarini/renda-fixa-api.git "$C"` (o que o README
   manda) e `git checkout` da branch.

2. Uma segunda cópia, para as execuções simultâneas e de carga (QA9-06, QA9-14):

   ```bash
   C2="$QA/clone2"; mkdir -p "$C2"; (cd "$C" && git ls-files -z | tar --null -T - -cf -) | tar -xf - -C "$C2"
   ```

3. Monitor de contêineres do Testcontainers, reaproveitado em vários cenários (os contêineres do
   Testcontainers levam o rótulo `org.testcontainers=true`):

   ```bash
   tc()  { docker ps -a --filter label=org.testcontainers=true --format '{{.ID}} {{.Image}} {{.Status}} {{.Ports}}'; }
   tcdb() { for c in $(docker ps -q --filter label=org.testcontainers=true --filter ancestor=postgres:16); do
              u=$(docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' $c | sed -n 's/^POSTGRES_USER=//p')
              echo "$c: $(docker exec $c psql -U "${u:-postgres}" -d postgres -tAc \
                "select count(*) filter (where datname like 'test\_%'), count(*) filter (where datname='template_fixedincome') from pg_database")"
            done; }
   ```

Se o Docker não estiver disponível, ficam `NÃO EXECUTADO` todos os cenários, exceto QA9-02 (a parte
sem Docker), QA9-11 (leitura), QA9-13 e a parte de leitura de QA9-16.

## Cenários

Legenda: **BUG** quando o resultado contraria a spec, o README ou uma regra do agente (comando
documentado que não funciona, teste vermelho numa cópia limpa, sobra de contêiner ou de banco,
teste que escreve fora do próprio banco); **RISCO** quando é defensável pela spec, mas surpreende
quem segue o README ou só uma execução real confirma.

Severidade dos bugs no relatório:

- **alta**: quem segue o README não consegue rodar os testes ou o Stryker numa máquina com Docker;
  teste vermelho na cópia limpa; teste que lê ou escreve no banco do `docker compose`; contêineres
  que se acumulam a cada execução; arquivo necessário ao CI que ficaria de fora do commit.
- **média**: teste instável (pelo menos 1 falha em 3 execuções); mensagem de erro que não diz o que
  fazer; comando documentado que só funciona de uma pasta ou num shell sem o README avisar; limite
  do Stryker que não reprova.
- **baixa**: número, versão ou nome divergente entre documentos; arquivo gerado fora do
  `.gitignore`/`.dockerignore` sem efeito funcional.

### Jornada de quem clona

#### QA9-01 — README do zero, na cópia limpa

Siga a seção "Testes" do README literalmente, na cópia `$C`, uma vez em Git Bash e uma vez em
Windows PowerShell 5.1 (no PowerShell, só os comandos `dotnet`).

```bash
cd "$C"
time dotnet tool restore
time dotnet restore 2>&1 | tail -5
time dotnet build 2>&1 | grep -E 'Warning\(s\)|Error\(s\)|warn|NU1'
time dotnet test 2>&1 | tee "$QA/01-dotnet-test.txt" | grep -E 'Passed!|Failed!|Total|Duration'
tc                                  # vazio ao fim
git status --short                  # tudo que a execução gerou e o .gitignore não cobre
```

Esperado (README, "Testes"; VALIDACAO-F9): `0 Warning(s)`, nenhum `NU1605`; 656 testes, 0 falhas,
0 ignorados, com Domain 332, Application 112, Architecture 7, Infrastructure 148 e Api 57;
`git status --short` vazio; nenhum contêiner com o rótulo do Testcontainers ao fim.

Registre também: tempo total de cada comando (o README não promete tempo; a validação mediu 62 s
para o `dotnet test` da solution com o build já feito) e se a primeira execução baixou alguma
imagem.

- BUG (alta): qualquer teste vermelho ou compilação quebrada na cópia (indica arquivo que só existe
  na árvore do Matheus, ignorado pelo `.gitignore`); `git status` mostrando arquivo gerado.
- RISCO: `dotnet test` da solution passando de 2 minutos sem o README avisar.

#### QA9-02 — Projetos isolados e os que dispensam Docker

Na cópia, depois do QA9-01. Para provar que os três projetos "sem Docker" não tocam no Docker sem
desligá-lo, aponte o cliente Docker do processo para um endereço morto (só afeta aquele comando):

```bash
cd "$C"
for p in Domain Application Architecture; do
  DOCKER_HOST=tcp://127.0.0.1:1 dotnet test tests/FixedIncome.$p.Tests 2>&1 | grep -E 'Passed!|Failed!'; done
# a partir da pasta do projeto, como um usuário faria pelo terminal do IDE
(cd tests/FixedIncome.Architecture.Tests && dotnet test 2>&1 | grep -E 'Passed!|Failed!')
# os dois de integração sozinhos, com Docker
time dotnet test tests/FixedIncome.Infrastructure.Tests 2>&1 | grep -E 'Passed!|Failed!'; tc
time dotnet test tests/FixedIncome.Api.Tests 2>&1 | grep -E 'Passed!|Failed!'; tc
# filtro por classe, a forma de rodar um teste só
dotnet test tests/FixedIncome.Infrastructure.Tests --filter "FullyQualifiedName~HolidayRepositoryTests" 2>&1 | grep -E 'Passed!|Failed!'
```

Esperado (README: "Os testes de `Domain`, `Application` e `Architecture` não dependem de Docker"):
os três verdes com o `DOCKER_HOST` morto; Infrastructure 148 e Api 57 verdes; o filtro roda só a
classe (5 testes) e sobe um contêiner só.

- BUG (alta): Domain, Application ou Architecture falhando com o Docker inacessível.
- RISCO: o `--filter` de uma classe sobe mais de um contêiner, ou leva o mesmo tempo da suíte.

#### QA9-03 — Docker indisponível

Promessa do README: "Sem Docker, esses testes falham com a mensagem 'Os testes de integração
precisam do Docker em execução. Inicie o Docker e rode os testes de novo.' Eles nunca são pulados."
A validação só provou isso com uma exceção injetada no código (item aberto ii).

**a. Simulado, sem parar o Docker Desktop** (não precisa de autorização). O Testcontainers lê o
`DOCKER_HOST`; apontá-lo para um endereço sem daemon reproduz "Docker fora" só para o processo:

```bash
cd "$C"
for h in tcp://127.0.0.1:1 npipe:////./pipe/qaf9_nao_existe; do
  echo "== $h"
  time DOCKER_HOST=$h dotnet test tests/FixedIncome.Infrastructure.Tests --no-build 2>&1 | tee "$QA/03a-infra-$(echo $h | tr -c 'a-z0-9' _).txt" \
    | grep -E 'Passed!|Failed!|Skipped|precisam do Docker' | sort | uniq -c | head
  time DOCKER_HOST=$h dotnet test tests/FixedIncome.Api.Tests --no-build 2>&1 | tee "$QA/03a-api-$(echo $h | tr -c 'a-z0-9' _).txt" \
    | grep -E 'Passed!|Failed!|Skipped|precisam do Docker' | sort | uniq -c | head
  echo "exit=$?"
done
```

Para cada variante, registre: exit code; quantos testes falharam, passaram e foram pulados; se a
mensagem em português aparece (e quantas vezes: uma por teste ou uma por coleção); se a saída
começa pela mensagem ou a enterra numa pilha longa; o tempo até falhar.

Esperado: exit diferente de 0; 0 pulados; Infrastructure com todos os testes da coleção
`PostgreSQL` falhando (na validação, as 6 classes de integração somam 58 testes; os demais do
projeto não usam banco e passam); Api com os 50 da coleção falhando e os 7 de fábrica própria
passando; a mensagem do README visível em cada falha; falha em poucos segundos, sem travar.

- BUG (alta): algum teste de integração `Skipped` ou verde; o processo trava (passa de 2 minutos).
- BUG (média): a mensagem não aparece, ou aparece só a exceção do Docker.Dotnet em inglês.
- RISCO: a mensagem certa aparece, mas depois de dezenas de linhas de pilha.

**b. Docker Desktop parado de verdade** (decisão 1). Como fazer com segurança: confirmar que
`docker ps` não lista contêiner do Matheus em uso; anotar quais contêineres estavam rodando; sair
do Docker Desktop pela bandeja ("Quit Docker Desktop"), sem "Reset" nem "Clean / Purge data";
esperar `docker info` falhar; rodar os dois comandos do item a **sem** `DOCKER_HOST`; abrir o Docker
Desktop de novo; esperar `docker info` responder; conferir que a foto do passo 0 bate (os
contêineres do Compose não têm `restart:` e não voltam sozinhos: se algum estava rodando, o Matheus
decide se o QA os sobe de volta com `docker start`).

**c. Docker Desktop aberto com a engine pausada** (menu "Pause", opcional, decisão 1): o socket
existe, mas não responde. Registre se o teste falha com a mesma mensagem ou fica esperando.

#### QA9-04 — Limpeza depois de uma execução normal

```bash
cd "$C"
dotnet test --no-build > "$QA/04-run.txt" 2>&1 &
for i in $(seq 1 90); do echo "t=$i $(tc | wc -l) contêineres"; tcdb; sleep 1; done > "$QA/04-monitor.txt"
wait
sleep 15; tc; docker volume ls -q | sort | comm -13 "$QA/antes-volumes.txt" -
docker network ls --format '{{.Name}}' | sort | comm -13 "$QA/antes-networks.txt" -
```

Esperado (SPEC-F9, "Um contêiner por coleção, um banco por teste"): durante a execução, um
`postgres:16` e um Ryuk por assembly de integração (quatro no total); o número de bancos `test_*`
em cada contêiner fica pequeno (os testes da coleção rodam em série) e não cresce com o tempo; o
`template_fixedincome` existe uma vez; ao fim, nenhum contêiner, volume ou rede nova.

- BUG (alta): bancos `test_*` acumulando durante a execução (sinal de `DROP DATABASE` que falhou
  em silêncio); contêiner, volume anônimo ou rede sobrando depois de 15 s.
- RISCO: Ryuk sobrando por alguns segundos após o fim (esperado do desenho do Ryuk; registrar
  quanto tempo leva).

#### QA9-05 — Execução interrompida

O Ryuk é quem remove os contêineres quando o processo de teste morre sem rodar o `Dispose`.

**a. Morte forçada do processo** (PowerShell, para ter o PID do Windows):

```powershell
$C = "C:\Users\matheuss\AppData\Local\Temp\claude\8c9101ad3d07d8d0\bae42765-baa0-4198-8cbe-db4c78a3e8cd\scratchpad\qaf9\clone"
$p = Start-Process dotnet -ArgumentList 'test','tests/FixedIncome.Api.Tests','--no-build' -WorkingDirectory $C -PassThru -NoNewWindow
Start-Sleep -Seconds 20          # o contêiner já subiu e os testes estão rodando
docker ps -a --filter label=org.testcontainers=true
taskkill /PID $p.Id /T /F
1..12 | ForEach-Object { Start-Sleep 5; "t=$($_*5)s"; docker ps -a --filter label=org.testcontainers=true --format '{{.Image}} {{.Status}}' }
Get-Process testhost -ErrorAction SilentlyContinue      # testhost órfão?
```

Repita matando só o `testhost` (`Get-Process testhost | Stop-Process -Force`) em vez da árvore
inteira.

Depois de cada variante: `dotnet test tests/FixedIncome.Api.Tests --no-build` de novo, verde, sem
erro de arquivo travado em `bin/` nem de porta.

**b. Ctrl+C de verdade** (opcional, decisão 5): o Matheus roda `dotnet test` num terminal da
cópia, aperta Ctrl+C depois de uns 20 s, e o QA observa `tc` em outro terminal. É a única forma
fiel: o Git Bash não entrega SIGINT de forma confiável a um processo .NET no Windows.

Esperado: em até 60 s depois da morte, nenhum contêiner com o rótulo do Testcontainers (o
`postgres:16` é removido pelo Ryuk; o Ryuk sai em seguida); nenhum `testhost` órfão; a execução
seguinte verde.

- BUG (alta): `postgres:16` sobrando depois de 60 s (cada interrupção deixaria um contêiner).
- RISCO: `testhost` órfão segurando o contêiner vivo enquanto não for morto.

#### QA9-06 — Duas execuções ao mesmo tempo

```bash
(cd "$C" && dotnet build -v q) && (cd "$C2" && dotnet build -v q)
(cd "$C"  && time dotnet test --no-build > "$QA/06-c1.txt" 2>&1) &
(cd "$C2" && time dotnet test --no-build > "$QA/06-c2.txt" 2>&1) &
sleep 25; tc; wait
grep -E 'Passed!|Failed!' "$QA/06-c1.txt" "$QA/06-c2.txt"; tc
# variação: duas execuções na mesma pasta, mesmo build
(cd "$C" && dotnet test tests/FixedIncome.Infrastructure.Tests --no-build > "$QA/06-m1.txt" 2>&1) &
(cd "$C" && dotnet test tests/FixedIncome.Infrastructure.Tests --no-build > "$QA/06-m2.txt" 2>&1) &
wait; grep -E 'Passed!|Failed!' "$QA/06-m1.txt" "$QA/06-m2.txt"
```

Esperado (SPEC-F9, "porta aleatória"): as duas execuções verdes; durante, quatro `postgres:16` com
portas de host diferentes, e um ou mais Ryuk (o Testcontainers usa um Ryuk por processo); ao fim,
nada sobrando. Na mesma pasta, as duas verdes (ou falha clara de arquivo travado em `TestResults`,
que é RISCO, não BUG).

- BUG (alta): uma execução falha por porta em uso, banco de outra execução ou contêiner removido
  pelo Ryuk da outra.
- RISCO: a soma dos tempos é bem maior que o dobro de uma execução isolada (anotar).

#### QA9-07 — Convivência com o `docker compose`

Depende da decisão 2 (o `docker-compose.yml` fixa `container_name: fixedincome-db` e
`fixedincome-api` e as portas 5432 e 8080: só dá para subir se nenhum contêiner com esses nomes
existir e as portas estiverem livres). Roda **na cópia**, com nome de projeto próprio, para o volume
ser `qaf9_fixedincome-db-data` e não o do Matheus:

```bash
cd "$C"
docker ps -a --filter name=fixedincome --format '{{.Names}}'        # tem de ser vazio
docker compose -p qaf9 up -d db
until docker exec fixedincome-db pg_isready -U fixedincome >/dev/null 2>&1; do sleep 1; done
docker exec fixedincome-db psql -U fixedincome -d postgres -tAc "select datname from pg_database order by 1" > "$QA/07-dbs-antes.txt"
# a. testes com o banco do Compose ocupando a 5432
dotnet test --no-build 2>&1 | grep -E 'Passed!|Failed!'
# b. a mesma coisa, com a variável que um desenvolvedor teria no terminal apontando para o Compose
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=fixedincome;Username=fixedincome;Password=fixedincome" \
  dotnet test --no-build 2>&1 | grep -E 'Passed!|Failed!'
docker exec fixedincome-db psql -U fixedincome -d postgres -tAc "select datname from pg_database order by 1" | diff - "$QA/07-dbs-antes.txt" && echo "nenhum banco novo no Compose"
docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc "\dt" | head     # sem migration aplicada pelos testes
# c. o inverso: Compose subindo enquanto os testes rodam
docker compose -p qaf9 stop db
(dotnet test tests/FixedIncome.Api.Tests --no-build > "$QA/07-c.txt" 2>&1) & sleep 15; docker compose -p qaf9 up -d db; wait
grep -E 'Passed!|Failed!' "$QA/07-c.txt"
```

Esperado (SPEC-F9: "Não suba o PostgreSQL por `docker compose` nem dependa dele nos testes"; "uma
variável definida na máquina ou no runner" não pode vencer o teste): verde nos três casos; nenhum
banco `test_*`, nenhuma tabela e nenhuma linha `QA-`/de teste no `fixedincome-db`; o Compose sobe
normalmente com os testes em andamento.

- BUG (alta): qualquer banco, tabela ou linha criada no `fixedincome-db` pelos testes; teste
  vermelho só porque a 5432 está ocupada ou a variável está definida.

Ao fim: `docker compose -p qaf9 stop` (o QA9-12 reaproveita o projeto `qaf9`).

#### QA9-08 — Variáveis de ambiente do desenvolvedor

Na cópia, sem Compose. Cada linha é uma execução de `dotnet test --no-build` com a variável
definida só para o comando:

| Caso | Variável | Esperado |
|---|---|---|
| a | `ConnectionStrings__DefaultConnection=Host=qaf9-nao-existe;Database=x;Username=x;Password=QaF9Senha` | verde; `grep -c QaF9Senha` na saída = 0 |
| b | `ASPNETCORE_ENVIRONMENT=Production` | registre: o `Program` precisa de connection string não vazia e ela vem do `appsettings.Development.json` (SPEC-F9). Em Production o arquivo não é lido: os testes do `ApiTestBase` continuam verdes? |
| c | `DOTNET_ENVIRONMENT=Production` | idem |
| d | `TESTCONTAINERS_RYUK_DISABLED=true` | verde; registre se o `postgres:16` sobra ao fim (sem Ryuk, só o `Dispose` remove) e, se sobrar, remova-o pelo ID |
| e | `IndexRatesImport__Enabled=true` | verde e sem chamada ao Banco Central (o teste `IndexRatesImportJobDisabledInTestsTests` existe para isso); confira na saída que não há `api.bcb.gov.br` |

- BUG (alta): caso a ou e vermelho, ou com chamada real ao Banco Central; senha na saída.
- RISCO: casos b e c vermelhos sem o README avisar que os testes de API dependem do ambiente
  Development; caso d deixando contêiner (o README não menciona o Ryuk).

### Stryker

#### QA9-09 — `dotnet stryker` como o README manda, e fora dele

Sempre na cópia `$C`. O Stryker compila o projeto de testes: não rode ao mesmo tempo que um
`dotnet test` da mesma cópia.

```bash
cd "$C"
# a. o caminho do README
dotnet tool restore
cd tests/FixedIncome.Domain.Tests && time dotnet stryker 2>&1 | tee "$QA/09a.txt" | grep -E 'mutation score|Killed|Survived|Timeout|No ?Coverage|Time Elapsed|threshold'
echo "exit=${PIPESTATUS[0]}"; ls StrykerOutput/*/reports/
cd "$C" && git status --short                      # StrykerOutput não aparece
# b. de outras pastas
(cd "$C" && dotnet stryker 2>&1 | tail -15; echo "exit=$?")
(cd "$C/tests" && dotnet stryker 2>&1 | tail -15; echo "exit=$?")
(cd "$C" && dotnet stryker --config-file tests/FixedIncome.Domain.Tests/stryker-config.json 2>&1 | tail -15)
(cd "$C/src/FixedIncome.Domain" && dotnet stryker 2>&1 | tail -15)
# c. sem dotnet tool restore, numa terceira cópia nova (sem a ferramenta restaurada no perfil)
#    só se `dotnet tool list` mostrar que a restauração é por repositório; registre a mensagem
# d. execução repetida
cd "$C/tests/FixedIncome.Domain.Tests" && dotnet stryker > "$QA/09d.txt" 2>&1; ls StrykerOutput; du -sh StrykerOutput
# e. o que vai para o contexto do docker build
cd "$C" && git check-ignore -v tests/FixedIncome.Domain.Tests/StrykerOutput/x
grep -n -i stryker .dockerignore || echo "StrykerOutput fora do .dockerignore"
```

Esperado (README, seção "Qualidade"; `CLAUDE.md`, seção "Testes"): a, exit 0, score impresso igual
ao do README e da validação (ver QA9-13), relatório `html` e `json` em
`StrykerOutput/<data>/reports/`, nenhum Docker usado (o README diz "não precisa de Docker"; confirme
com `DOCKER_HOST=tcp://127.0.0.1:1` numa das execuções); b, de outra pasta ou falha com mensagem que
diga onde rodar, ou funciona com o `--config-file`; registre a mensagem de cada pasta; d, a segunda
execução tem o mesmo score e cria outra pasta datada; e, `StrykerOutput/` ignorado pelo git.

- BUG (média): o comando do README falha na cópia limpa; score diferente entre duas execuções sem
  mudança de código; a execução de outra pasta "funciona" sem a configuração (sem `break`) e sai
  com exit 0 sem avisar.
- RISCO: `StrykerOutput/` fora do `.dockerignore`: o `Dockerfile` só copia `src/`, então o efeito é
  só o tamanho do contexto do `docker compose up --build` crescer a cada execução (anote o `du`);
  pastas acumulando sem o README sugerir limpeza.

#### QA9-10 — O limite de 80% reprova de verdade

Só na cópia. Três formas, da menos invasiva para a mais:

```bash
cd "$C/tests/FixedIncome.Domain.Tests"
# a. limite pela linha de comando, acima do score atual
dotnet stryker --break-at 100 > "$QA/10a.txt" 2>&1; echo "exit=$?"; tail -5 "$QA/10a.txt"; ls -t StrykerOutput | head -1
# b. limite no arquivo, acima de high (a ferramenta exige break <= low <= high)
cp stryker-config.json "$QA/stryker-config.orig.json"
sed -i 's/"break": 80/"break": 95/' stryker-config.json
dotnet stryker > "$QA/10b.txt" 2>&1; echo "exit=$?"; tail -5 "$QA/10b.txt"
cp "$QA/stryker-config.orig.json" stryker-config.json
# c. score baixo de verdade: tire da cópia os testes de cálculo de posição
mkdir -p "$QA/testes-removidos" && mv Entities/Position*Tests.cs "$QA/testes-removidos/"
dotnet stryker > "$QA/10c.txt" 2>&1; echo "exit=$?"; grep -E 'mutation score|threshold|break' "$QA/10c.txt"
ls -t StrykerOutput | head -1      # relatório gerado mesmo reprovado?
mv "$QA/testes-removidos/"* Entities/
```

(Confirme os nomes reais dos arquivos com `ls Entities` antes do `mv`; o objetivo é derrubar o
score abaixo de 80%. Se não cair, tire mais arquivos, um por vez, e registre quais.)

Esperado (`stryker-config.json`, `"break": 80`; README: "score abaixo de 80% reprova o comando"):
a e c com exit diferente de 0 e mensagem que cite o limite; o relatório gerado mesmo quando
reprova (é o que o `mutation.yml` publica com `if: always()`); b, ou exit diferente de 0 por limite
não atingido, ou erro de configuração claro (`break` acima de `high`); registre qual.

- BUG (média): exit 0 com score abaixo do `break`; relatório não gerado na execução reprovada.
- RISCO: mensagem de reprovação que não diz o score nem o limite.

### Arquitetura

#### QA9-17 — Regras de camada diante de violações que a prova do P5 não tentou

A prova do P5 (relato) violou o Domain com EF Core e um controller com o `DbContext`. Aqui, outras
violações que um desenvolvedor cometeria. Cada uma numa cópia **nova** (`$QA/arq-<letra>`, feita
como no passo 1), compilada e testada com `dotnet test tests/FixedIncome.Architecture.Tests`, e
apagada depois. Cada violação é uma classe nova de poucas linhas, não alteração de código existente.

| Caso | Violação na cópia | Esperado pela regra |
|---|---|---|
| a | controller com campo de um **repositório concreto** de `FixedIncome.Infrastructure` (não o `DbContext`) | vermelho na regra 3 |
| b | classe que usa o `FixedIncomeDbContext` em **outro namespace da Api** (por exemplo `FixedIncome.Api.Endpoints`), chamada por um controller | registre: a regra 3 só olha `FixedIncome.Api.Controllers`; verde aqui é RISCO |
| c | controller com campo `Npgsql.NpgsqlConnection` | registre: Npgsql não está na lista da regra 3; verde é RISCO |
| d | classe da Application com campo `Microsoft.AspNetCore.Http.HttpContext` (exige `FrameworkReference` na cópia) | vermelho na regra 2 |
| e | classe do Domain que usa `System.Net.Http.HttpClient` | registre: `System.*` está na lista branca; verde é coerente com a spec, mas não com o espírito de "Domain puro" (RISCO, decisão do Matheus) |
| f | `ProjectReference` do Domain para a Application sem uso | verde (limitação registrada na spec e no próprio teste); confirme que o `dotnet build` acusa ciclo ou não |

Esperado: a e d vermelhos, com a mensagem nomeando o tipo; os demais registrados.

- BUG (média): a ou d verdes.
- RISCO: b, c, e com o `CLAUDE.md` ("Controller ... não contém regra de negócio: valida a rota,
  chama o caso de uso") mais amplo que o teste.

### Workflows

#### QA9-11 — `ci.yml`, `release.yml` e `mutation.yml` por leitura e `actionlint`

```bash
cd "$REPO"
docker run --rm -v "$(pwd -W):/repo" -w /repo rhysd/actionlint:1.7.7; echo "exit=$?"
```

(Só leitura do repositório do Matheus: o `actionlint` não escreve. Se preferir, rode na cópia.)

Para cada item, `OK`, `RISCO` ou `BUG`, com a linha:

| # | Pergunta | O que conferir |
|---|---|---|
| a | O `release.yml` chama o `ci.yml` por `workflow_call` | O passo "Testes de Arquitetura" e os testes de integração com Docker valem também para o release? O job `ci` chamado tem só `contents: read`: os testes com Testcontainers precisam de algo além disso no runner? |
| b | Ordem dos passos no `ci.yml` | Arquitetura roda **depois** de Infrastructure e Api. Uma violação de camada só aparece depois de ~1 min de testes de integração, e uma falha de Docker (por exemplo `toomanyrequests`) esconde o resultado da arquitetura. Registre como RISCO de ordem, se for o caso |
| c | `paths-ignore` do `release.yml` (`docs/**`, `*.md`) | `*.md` casa só na raiz? Um commit que mexe só em `tests/**`, `stryker-config.json` ou `.github/workflows/mutation.yml` gera imagem nova, bundle de ~120 MB e smoke, sem mudar a imagem. Um commit só em `docs/` ainda roda o `ci.yml` (sem `paths-ignore`), agora com Docker. Custo, não erro |
| d | `mutation.yml` referencia arquivos que existem | `tests/FixedIncome.Domain.Tests` (sim) e `stryker-config.json`, que **hoje não é rastreado** pelo git (está entre os `??` do `git status`). Confira em `git status --short` quais dos arquivos novos da fase precisam entrar no commit (ver QA9-16) |
| e | `mutation.yml` sem `concurrency` | Dois `workflow_dispatch` seguidos: os artefatos são por execução, então não conflitam. Confirme e registre OK |
| f | Falha antes do relatório | Se o `dotnet tool restore` ou o build falhar, `StrykerOutput` não existe: o `upload-artifact` com `if: always()` emite aviso (`if-no-files-found` padrão é `warn`) e o job fica vermelho pelo passo anterior. Coerente com a spec? |
| g | `schedule` só dispara na branch padrão | O README diz "toda segunda-feira": vale só depois do merge na `main`. Ele avisa? |
| h | SDK sem `global.json` | `setup-dotnet` 8.0.x instala o 8, mas o `ubuntu-latest` traz SDKs mais novos e, sem `global.json`, o `dotnet` usa o mais novo. O `dotnet-stryker` 4.16.0 com `rollForward: false` e os testes rodam no SDK que o runner escolher. A validação diz que a 5.0.0 "não instala no SDK 8": isso já é sinal de sensibilidade à versão. Só a execução real confirma; registrar |
| i | Imagem do banco | O smoke do `release.yml` fixa `postgres:16@sha256:...`; os testes usam `postgres:16` sem digest (decisão da SPEC-F9). Registre a divergência como informação |
| j | Pull do Docker Hub no runner | Cada `dotnet test` de integração baixa `postgres:16` e Ryuk; o `release.yml` chama o CI e também baixa `postgres:16` no smoke. Três a cinco pulls anônimos por execução de release. Registrar |

Opcional (decisão 3): `zizmor` 1.14.2 offline, como a validação, só se algum workflow mudar depois
dela.

- BUG: workflow que referencia arquivo inexistente no commit; passo de teste que o `release.yml` não
  herda.
- RISCO: b, c, g, h.

#### QA9-16 — O que precisa entrar no commit

Leitura no repositório do Matheus, sem alterar nada:

```bash
cd "$REPO"
git status --short | grep '^??'
git ls-files -o --exclude-standard                      # não rastreados que entrariam num `git add -A`
git check-ignore -v tests/FixedIncome.Domain.Tests/stryker-config.json tests/Shared/PostgreSqlFixture.cs \
  .github/workflows/mutation.yml tests/FixedIncome.Architecture.Tests/FixedIncome.Architecture.Tests.csproj \
  || echo "nenhum ignorado"
git ls-files -oi --exclude-standard | grep -vE '/(bin|obj|TestResults|StrykerOutput)/' | head   # ignorados fora das pastas de build
grep -n 'Shared' tests/*/*.csproj
```

Esperado: os arquivos novos da fase (`mutation.yml`, `stryker-config.json`,
`tests/FixedIncome.Architecture.Tests/`, `tests/Shared/`,
`tests/FixedIncome.Infrastructure.Tests/Integration/`) aparecem como não rastreados e **nenhum**
é ignorado; nada necessário ao build fica entre os ignorados. O QA9-01 já prova que a cópia só com
esses arquivos compila e passa.

- BUG (alta): arquivo necessário ignorado pelo `.gitignore` (o CI quebraria depois do merge).

### Regressão da API

#### QA9-12 — A API pelo caminho do usuário final

A fase não deveria afetar a API; é regressão rápida. Depende da decisão 2 (mesmas condições do
QA9-07). Na cópia, com o projeto `qaf9`, seguindo a seção "Com Docker, sem instalar nada" do README:

```bash
cd "$C"
docker compose -p qaf9 up --build -d
until curl -s -o /dev/null http://localhost:8080/health; do sleep 2; done
docker run --rm --network container:fixedincome-db -v "$(pwd -W):/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  sh -c 'dotnet tool restore && dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api'
docker compose -p qaf9 restart api
until curl -s -o /dev/null http://localhost:8080/health; do sleep 2; done
B=http://localhost:8080
curl -s -w ' %{http_code}\n' $B/health
curl -s -w ' %{http_code}\n' $B/api/assets
A=$(curl -s -X POST -H 'Content-Type: application/json' $B/api/assets \
  -d '{"name":"QA-F9 PRE12","issuer":"QA","assetType":"Cdb","indexType":"PreFixed","rate":12,"issueDate":"2026-01-01","maturityDate":"2028-01-01"}')
echo "$A"     # anote o id
P=$(curl -s -X POST -H 'Content-Type: application/json' $B/api/positions \
  -d '{"assetId":"<id do título>","investedAmount":1000,"applicationDate":"2026-09-01"}')
echo "$P"     # anote o id
curl -s -w ' %{http_code}\n' "$B/api/positions/<id do aporte>/projection?referenceDate=2026-10-01"
curl -s -w ' %{http_code}\n' "$B/api/portfolio/summary?referenceDate=2026-10-01"
curl -s -o /dev/null -w '%{http_code}\n' $B/swagger/index.html
docker compose -p qaf9 down          # sem -v
```

Atenção: o `docker run ... -v "$(pwd -W):/src"` monta a cópia e gera `bin/obj` nela pelo contêiner
Linux; rode o QA9-12 depois dos cenários que usam `$C` com `--no-build`, ou rode `dotnet build` de
novo na cópia antes deles.

Esperado (README, "Com Docker"; comportamento da F8): `/health` 200 `Healthy`; `/api/assets` 200;
título e aporte 201 (ou o código que o `.http` e o README mostram); projeção PreFixed 200 com
valores coerentes com a F8 (o mesmo pedido na `QA-F8.md`, se houver, serve de referência); resumo
200 se só houver PreFixed (a importação de CDI pode ter rodado ou não; anote); Swagger 200 em
Development.

- BUG (alta): qualquer diferença de comportamento em relação à F8; 500 em qualquer linha.

### Documentação

#### QA9-13 — Coerência entre README, `CLAUDE.md`, `ESPECIFICACAO.md`, `SPEC-F9` e `VALIDACAO-F9`

Procure contradição de números, versões, nomes e comandos. Pontos de partida, a confirmar no texto
vigente na hora da execução (o orquestrador tinha pendências de README depois da validação):

- **Contagem de testes**: README "656 testes" contra a tabela logo abaixo (hoje Domain 292 e
  Infrastructure 130, sem Architecture, e o comentário `<!-- cobertura F9: atualizar ... -->`
  ainda no arquivo). A soma da tabela tem de dar a contagem do texto.
- **Mutation score**: README "99,66%" contra `VALIDACAO-F9.md` "99,33%". O QA9-09 a mede de novo;
  registre os três números. README "partida 82,76%": a spec pede que o P6 o registre; confira se
  algum documento o sustenta.
- **Versões**: `Testcontainers.PostgreSql 4.16.0`, `NetArchTest.Rules 1.3.2`, `dotnet-stryker
  4.16.0` iguais nos `.csproj`, no `.config/dotnet-tools.json`, no README e na validação. A versão
  do Ryuk (`0.14.0`) não aparece em documento nenhum: informação, não achado.
- **Docker**: README "Docker precisa estar em execução" e a seção "Com Docker, sem instalar nada"
  ("Requer apenas Docker", para subir a API) contra "Localmente, com o SDK" ("Requer .NET 8 SDK e
  Docker"): quem só quer rodar os testes sabe que precisa dos dois?
- **Comando do Stryker**: README e `CLAUDE.md` mandam rodar em `tests/FixedIncome.Domain.Tests`;
  o resultado do QA9-09b diz se outra pasta funciona.
- **Mensagem sem Docker**: o texto do README é idêntico ao que o QA9-03 mostrou? (caractere a
  caractere, inclusive acentos).
- **Imagem**: ESPECIFICACAO "`postgres:16`" e o `docker-compose.yml` com `postgres:16-alpine`
  (diferença de collation explicada na SPEC-F9, não no README).
- **`CLAUDE.md`**: "Testes de repositório usam DbContext InMemory até a F8" continua correto como
  histórico; "Mutation testing do Domain ... abaixo disso o comando falha" bate com o QA9-10.
- **ROADMAP**: critérios da F9 ainda desmarcados e a "linha 142" que a aprovação mandou corrigir no
  fechamento: registrar o estado, sem classificar (é do orquestrador).
- **ESPECIFICACAO**, decisões técnicas: "graphify ... fora do versionamento" contra o `CLAUDE.md`
  ("A committed graphify-out/graph.json"), já anotado na F8; só registrar se ainda estiver lá.
- **Links**: todos os links relativos do README e do `CLAUDE.md` apontam para arquivo existente, e
  as âncoras (`#testes`) existem:

  ```bash
  cd "$REPO"
  grep -oE '\]\([^)#]+' README.md CLAUDE.md docs/ESPECIFICACAO.md | sed 's/.*](//' | grep -v '^http' | sort -u \
    | while read f; do [ -e "$f" ] || echo "QUEBRADO: $f"; done
  grep -oE '\]\(#[^)]+' README.md | sort -u
  ```

- BUG (baixa): número ou comando contraditório que leve o usuário a erro.

### Instabilidade

#### QA9-14 — Três execuções seguidas e sob carga

```bash
cd "$C" && dotnet build -v q
for i in 1 2 3; do
  /usr/bin/time -f "rodada $i: %e s" dotnet test tests/FixedIncome.Infrastructure.Tests --no-build 2>&1 | grep -E 'Passed!|Failed!|rodada|\[FAIL\]'
  /usr/bin/time -f "rodada $i: %e s" dotnet test tests/FixedIncome.Api.Tests --no-build 2>&1 | grep -E 'Passed!|Failed!|rodada|\[FAIL\]'
done 2>&1 | tee "$QA/14-sem-carga.txt"
# sob carga: Stryker na segunda cópia e uma suíte completa na segunda cópia ao mesmo tempo
(cd "$C2/tests/FixedIncome.Domain.Tests" && dotnet stryker > "$QA/14-stryker-carga.txt" 2>&1) &
(cd "$C2" && dotnet test --no-build > "$QA/14-c2-carga.txt" 2>&1) &
for i in 1 2 3; do
  dotnet test tests/FixedIncome.Infrastructure.Tests --no-build 2>&1 | grep -E 'Passed!|Failed!|\[FAIL\]'
  dotnet test tests/FixedIncome.Api.Tests --no-build 2>&1 | grep -E 'Passed!|Failed!|\[FAIL\]'
done 2>&1 | tee "$QA/14-com-carga.txt"
wait; tc
```

(Se `/usr/bin/time` não existir no Git Bash, use `time` do bash.) O `$C2` precisa de
`dotnet build` antes da carga. O Stryker e o `dotnet test` do `$C2` compilam na mesma árvore: se
disputarem `obj/`, troque a suíte do `$C2` por uma terceira cópia.

Atenção especial aos testes com espera por tempo: o teste do job de importação via DI
(`Job_de_importacao_inteiro_grava_as_cotacoes_no_banco_via_conteiner_de_di`, com prazo fixo) e os
de concorrência de upsert (`Upserts_concorrentes_...`). Se falharem, rode cada um 10 vezes sozinho
com `--filter` e conte.

Esperado: 6 execuções sem carga e 6 com carga, todas verdes; Api.Tests abaixo do teto de 60 s da
SPEC-F9 mesmo sob carga (anote o tempo impresso); nenhum contêiner sobrando.

- BUG (média): qualquer falha que não se repete na execução seguinte (flaky), com o nome do teste e
  a mensagem; deadlock `40P01` ou violação do índice único no teste de concorrência (seria achado
  de produção, Restrições da SPEC-F9).
- RISCO: Api.Tests acima de 60 s só sob carga.

#### QA9-15 — Em Linux, mais perto do runner

O CI roda em `ubuntu-latest`. Em Linux, caminho com `\` no `Compile Include="..\Shared\..."`,
diferença de maiúsculas em nomes de arquivo e fuso horário se comportam diferente do Windows.

a. **Sem Docker dentro do contêiner** (não precisa de autorização; imagem já presente): Domain,
Application e Architecture no `sdk:8.0`, com fusos diferentes. Monte uma **terceira cópia** (o build
Linux escreve `bin/obj` incompatíveis com os do Windows):

```bash
C3="$QA/clone-linux"; mkdir -p "$C3"; (cd "$C" && git ls-files -z | tar --null -T - -cf -) | tar -xf - -C "$C3"
for tz in UTC America/Sao_Paulo Pacific/Kiritimati America/Los_Angeles; do
  docker run --rm -e TZ=$tz -v "$(cygpath -w "$C3"):/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 sh -c \
    'dotnet test tests/FixedIncome.Domain.Tests -v q && dotnet test tests/FixedIncome.Application.Tests -v q && dotnet test tests/FixedIncome.Architecture.Tests -v q' \
    2>&1 | grep -E 'Passed!|Failed!|\[FAIL\]' | sed "s|^|$tz: |"
done
```

b. **Suíte completa em Linux** (decisão 3): o mesmo contêiner com o socket do Docker montado
(`-v //var/run/docker.sock:/var/run/docker.sock -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`)
e `dotnet test` da solution. O contêiner passa a controlar o Docker do Matheus, por isso pede
autorização.

Esperado: verde em todos os fusos (os testes de data usam `TimeProvider` fixo em
`America/Sao_Paulo`, SPEC-F9 P4); em b, 656 verdes.

- BUG (alta): falha só em Linux (o CI do PR quebraria).
- BUG (média): falha só em algum fuso.

## O que pode rodar em paralelo

O gargalo é o Docker: todo `dotnet test` de integração sobe dois contêineres por assembly, e
execuções paralelas mudam o tempo e podem mascarar flakiness. Por isso a maior parte é sequencial.

1. **Sequencial no início:** Preparação, passos 0 a 3, e QA9-01 (é o build de referência da cópia).
2. **Bloco A, sem Docker, a qualquer momento e em paralelo com tudo:** QA9-11, QA9-13, QA9-16
   (leituras), a parte com `DOCKER_HOST` morto do QA9-02 e o QA9-03a (não sobem contêiner; só não
   rode ao mesmo tempo que um build da mesma cópia).
3. **Bloco B, Docker, um de cada vez, na cópia `$C`:** QA9-02 (parte com Docker), QA9-04, QA9-05,
   QA9-08. O QA9-04 mede contêineres e não pode ter outra execução ao lado.
4. **Bloco C, concorrência deliberada:** QA9-06 e QA9-14, um depois do outro, com nada mais
   rodando. O QA9-14 sem carga antes do com carga.
5. **Bloco D, Stryker, na cópia `$C`, fora dos horários de `dotnet test` dessa cópia:** QA9-09,
   QA9-10. O QA9-10 c altera arquivos da cópia: devolva-os antes de qualquer outro cenário em `$C`.
6. **Bloco E, cópias próprias, em paralelo com B e D:** QA9-17 (`$QA/arq-*`) e QA9-15a
   (`$C3`). Evite rodar junto do QA9-04 e do QA9-14.
7. **Bloco F, com autorização:** QA9-07 e QA9-12 (decisão 2), um depois do outro, nesta ordem;
   QA9-03b/c (decisão 1), por último, com nada mais rodando; QA9-15b (decisão 3).

## Fora deste roteiro

Só uma execução real do GitHub Actions comprova (itens abertos da `VALIDACAO-F9.md`):

- o `ci.yml` verde num PR, com os testes de integração no Docker do runner e os pulls do Docker Hub;
- o `release.yml` chamando o CI com os passos novos;
- o `mutation.yml` por `workflow_dispatch` (só depois do merge) e pelo `schedule`, com o artefato
  `stryker-report`;
- o SDK que o runner escolhe (QA9-11 h).

Também fora: a reprodução da prova do P5 (`Domain` com EF Core e controller com `DbContext`), que é
do orquestrador; a inversão de ordem do P5b; os itens funcionais da F7 e da F8 (R-01 ao vivo,
calendário até 2030); `/health/ready` (F10); teste de carga da API.

## Limpeza ao fim

```bash
tc                                         # vazio; se houver algo, remova pelo ID e registre como achado
docker ps -a --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' | diff "$QA/antes-ps.txt" - 
docker images --format '{{.Repository}}:{{.Tag}}' | sort | comm -13 "$QA/antes-images.txt" -   # só a qaf9-api, se o QA9-12 rodou
docker volume ls -q | sort | comm -13 "$QA/antes-volumes.txt" -       # qaf9_fixedincome-db-data, se o QA9-07/12 rodou
docker network ls --format '{{.Name}}' | sort | comm -13 "$QA/antes-networks.txt" -   # qaf9_default, se o compose rodou
cd "$REPO" && git status --short | diff - "$QA/git-status-antes.txt" && echo "repositório do Matheus igual"
```

- Contêineres e rede do projeto `qaf9`: já parados pelo `docker compose -p qaf9 down` do QA9-12.
- **O volume `qaf9_fixedincome-db-data` e a imagem `qaf9-api` não são apagados pelo QA** (regra
  "não destrua dados"): ficam listados no relatório para o Matheus remover
  (`docker volume rm qaf9_fixedincome-db-data`, `docker image rm qaf9-api`), se quiser.
- As cópias `$QA/clone`, `$QA/clone2`, `$QA/clone-linux` e `$QA/arq-*` ficam no scratchpad; podem
  ser apagadas ao fim (são do QA, não do Matheus).
- Dados `QA-`: só no banco do projeto `qaf9` (QA9-12). O relatório lista os ids do título e do
  aporte criados.

## Decisões antes de executar

1. **Docker Desktop parado ou pausado de verdade** (QA9-03b e c). O QA sai do Docker Desktop pela
   bandeja, sem reset nem limpeza, roda dois comandos e o abre de novo. Contêineres que estavam
   rodando não voltam sozinhos. Autoriza? Sem autorização, fica só a simulação por `DOCKER_HOST`
   (QA9-03a).
2. **`docker compose` na cópia, projeto `qaf9`** (QA9-07 e QA9-12). Ocupa as portas 5432 e 8080 e
   usa os nomes fixos `fixedincome-db` e `fixedincome-api`; só roda se nenhum contêiner com esses
   nomes existir. Deixa o volume `qaf9_fixedincome-db-data` e a imagem `qaf9-api` para o Matheus
   remover. Autoriza?
3. **Contêiner com o socket do Docker montado** (QA9-15b) e **`zizmor`** (QA9-11, opcional).
   Executar ou deixar `NÃO EXECUTADO`?
4. **Momento**: executar sobre a árvore de trabalho atual, copiada para o scratchpad (fase não
   commitada; o relatório cita `a741bf4` mais "árvore de trabalho"), ou depois do commit, com um
   `git clone` de verdade, como o README manda?
5. **Ctrl+C manual** (QA9-05b): o Matheus aperta Ctrl+C num terminal enquanto o QA observa. Quer
   participar, ou fica só a morte forçada (QA9-05a)?
6. **Classificação de casos sem regra na spec**, para o QA não decidir sozinho: regra 3 que não
   olha fora de `FixedIncome.Api.Controllers` nem Npgsql (QA9-17 b e c); `System.*` liberado no
   Domain (QA9-17 e); `StrykerOutput/` fora do `.dockerignore`; Architecture depois dos testes de
   integração no `ci.yml`; commit só de `tests/**` gerando release. Contam como `BUG` ou `RISCO`?
