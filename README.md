# Controle de Investimentos em Renda Fixa

![CI](https://github.com/matheuscardosolazzarini/renda-fixa-api/actions/workflows/ci.yml/badge.svg) ![Release](https://github.com/matheuscardosolazzarini/renda-fixa-api/actions/workflows/release.yml/badge.svg)

API em .NET 8 para consolidação de carteira de renda fixa, com cálculo de rentabilidade
líquida de imposto de renda.

## O problema

Investidor pessoa física com títulos em mais de uma corretora não tem visão consolidada
da carteira. Cada instituição mostra apenas os próprios papéis, e o cálculo de
rentabilidade líquida — que depende de prazo, indexador e tributação — fica por conta do
investidor.

Esta API centraliza o cadastro dos títulos, registra os aportes e projeta a posição para
uma data de referência, já descontado o imposto devido.

## Escopo

A API cobre o cadastro de títulos, o registro de aportes, a projeção de uma posição para
uma data, a consolidação da carteira e a consulta das cotações de CDI e IPCA importadas do
Banco Central. Dez endpoints, documentados no Swagger.

`GET /health` é um health check de liveness, à parte dessa contagem. Responde 200 com o texto
`Healthy` enquanto o processo está de pé e não consulta o banco: o 200 ali não garante conexão
com o PostgreSQL. Quem prova a conexão é `GET /api/assets`.

Uma ausência é deliberada:

**Sem remoção de aporte.** Registro de aporte é histórico financeiro, e histórico se
corrige por estorno, não por exclusão. Um endpoint de estorno seria a evolução natural;
um `DELETE` não é.

A especificação funcional completa está em [`docs/ESPECIFICACAO.md`](docs/ESPECIFICACAO.md),
e as especificações de cada fase de implementação estão na mesma pasta.

## Regras de negócio

O domínio implementa as regras de cálculo abaixo, todas cobertas por teste:

**Rentabilidade pré-fixada** — capitalização composta sobre dias úteis, base 252, entre a
data de aporte e a data de referência. Os prazos de IR e de IOF continuam em dias corridos.
Os indexadores pós-fixados e o IPCA+ têm regras próprias, descritas em
[Indexadores e cotações](#indexadores-e-cotações).

**Limite de vencimento** — data de referência posterior ao vencimento é truncada para a
data de vencimento.

**Tabela regressiva de IR** — alíquota conforme o prazo decorrido: 22,5% até 180 dias,
20% até 360, 17,5% até 720 e 15% acima disso. O imposto incide exclusivamente sobre o
rendimento, nunca sobre o principal.

**Isenção** — LCI e LCA não sofrem incidência de IR para pessoa física, independentemente
do prazo.

**IOF regressivo** — incide sobre o rendimento de resgates com menos de 30 dias, antes do
IR. LCA é isenta de IOF. Detalhes na seção de indexadores.

**Vigência do aporte** — aporte com data anterior à emissão ou posterior ao vencimento do
título é rejeitado.

**Coerência do consolidado** — os totais da carteira somam os valores já arredondados de
cada projeção individual, em vez de arredondar a soma dos valores brutos. A diferença é de
centavos, mas o total precisa fechar exatamente com a lista que o usuário vê; um
consolidado que não bate com as parcelas é lido como erro do sistema.

**Alteração de título com aportes** — um título que já possui aportes não pode ter as datas
de emissão ou vencimento alteradas. Permitir isso tornaria retroativamente inválido um
aporte que era válido no momento do registro. Nome, emissor e taxa seguem alteráveis.

## Indexadores e cotações

As cotações vêm da API SGS do Banco Central: a série 12 (CDI, em % ao dia) e a série 433
(IPCA, em % no mês). Um job em segundo plano importa as duas séries ao iniciar a API e depois
a cada 6 horas. As séries publicam no máximo uma vez por dia, então a repetição existe para
recuperar falhas temporárias do provedor sem intervenção manual.

A importação é controlada por `IndexRatesImport:Enabled` no `appsettings.json` (em variável de
ambiente, `IndexRatesImport__Enabled`). Ela fica
ligada por padrão; nos testes da API fica desligada, para nenhum teste depender da internet.

### Dias úteis

Dia útil é de segunda a sexta, exceto feriados nacionais da ANBIMA, cadastrados de 2020 a
2030. A base é de 252 dias úteis por ano. Na contagem entre duas datas, a inicial entra e a
final fica de fora: aplicação numa sexta com referência na segunda seguinte conta um dia
útil. Uma data fora do calendário cadastrado gera erro, nunca uma suposição.

### Pós-fixado em CDI

`Rate` é o percentual do CDI: 100 significa 100% do CDI. Para cada dia útil do período, o
fator diário é `1 + CDI do dia × Rate / 100`, com o CDI em fração (série 12 dividida por 100).

As casas decimais seguem a metodologia de DI da B3. A taxa diária tem 8 casas, sem
arredondamento. O acumulado é truncado em 16 casas a cada dia, e o fator final é arredondado
em 8 casas antes de multiplicar pelo principal.

Se faltar cotação de algum dia útil do período, a projeção não é calculada, e a resposta
informa a primeira data sem cotação. Não há valor padrão nem estimativa.

### IPCA+

O valor bruto é `principal × fator IPCA × (1 + Rate / 100) ^ (DU / 252)`, em que `Rate` é a
taxa real anual e DU é o número de dias úteis do período.

O fator IPCA é o produto de `(1 + IPCA do mês / 100)` de cada mês civil do período. Um mês
inteiro entra com a variação completa. Um mês parcial entra elevado à fração de dias úteis
do período sobre o total de dias úteis do mês civil.

Um mês cujo IPCA ainda não foi divulgado usa a última variação publicada. Nesse caso a
projeção sai com `isEstimated: true`. Quando o IPCA oficial é importado, as projeções
passam a usá-lo, sem nenhuma alteração no cadastro.

**Simplificações do IPCA.** A convenção de mercado usa a data de aniversário do título e
defasagem de um mês no índice, como fazem CDB e NTN-B. Aqui o período é o mês civil, sem
defasagem, então a projeção pode diferir da que o banco informa. Além disso, o fator é o
produto das variações mensais publicadas com duas casas decimais. A B3 usa a razão entre
números-índice, e o resultado difere um pouco dessa razão.

### IOF e IR

O IOF regressivo incide sobre o rendimento de resgates com 1 a 29 dias corridos, pela tabela
do anexo do Decreto 6.306/2007. A partir de 30 dias, a alíquota é zero. O IOF é apurado antes
do IR, e o IR incide sobre o rendimento menos o IOF.

LCA é isenta de IOF (Decreto 6.306/2007, art. 32, § 2º, V). LCI, CDB e Tesouro Direto pagam
IOF normalmente.

Quando o rendimento é menor ou igual a zero, IOF e IR são zero. A alíquota continua a ser a
da tabela para o prazo, e apenas o valor é zerado.

### Arredondamento

Cada projeção arredonda nesta ordem: valor bruto, IOF e IR, com o rendimento sendo a
diferença entre o bruto arredondado e o investido. O líquido é a subtração dos valores já
arredondados, o que faz cada projeção fechar exatamente: `bruto − IOF − IR = líquido`.

### Exemplo de chamada

As cotações são consultadas por indexador e período, com `from` e `to` obrigatórios no
formato `yyyy-MM-dd`. O período é fechado nos dois lados.

```
GET /api/indexes/Cdi?from=2026-09-14&to=2026-09-15
```

Resposta, com status 200:

```json
{
  "statusCode": 200,
  "success": true,
  "message": null,
  "data": [
    { "indexType": "Cdi", "referenceDate": "2026-09-14", "value": 0.051660 },
    { "indexType": "Cdi", "referenceDate": "2026-09-15", "value": 0.051660 }
  ]
}
```

Para IPCA, a data de referência é sempre o dia 1 do mês. Por isso o filtro compara com o
dia 1: `from=2026-01-15` não inclui janeiro. Um período sem cotações devolve 200 com a lista
vazia. Data inicial posterior à final, PreFixed e indexador inexistente devolvem 400. O indexador
vai pelo nome, sem diferenciar maiúsculas; o número do enum (`/api/indexes/1`) também devolve 400.

## Arquitetura

```
src/
  FixedIncome.Domain          entidades, invariantes, regras de cálculo
  FixedIncome.Application     casos de uso, DTOs, interfaces de repositório
  FixedIncome.Infrastructure  EF Core, configurações de entidade, repositórios
  FixedIncome.Api             controllers, middleware, injeção de dependência
tests/
  FixedIncome.Domain.Tests
  FixedIncome.Application.Tests
  FixedIncome.Infrastructure.Tests
  FixedIncome.Api.Tests
```

Dependências apontam para dentro: `Api → Application → Domain`, com `Infrastructure`
implementando as interfaces declaradas em `Application`. O projeto `Domain` não tem
nenhuma referência de projeto nem de pacote — restrição verificada a cada revisão de fase.

## Decisões técnicas

**Cálculo no domínio, não no caso de uso.** Rentabilidade e apuração de IR são regras que
independem de orquestração e precisam ser testáveis sem infraestrutura. Ficam na entidade
`Position` e na tabela `IncomeTaxTable`.

**Entidades que não existem em estado inválido.** Invariantes são validadas no construtor,
com setters privados e `DomainException` em caso de violação. O custo é um construtor
privado adicional para materialização pelo EF Core; o ganho é que nenhuma camada acima
precisa reconfirmar o que o domínio já garantiu.

**`decimal` em todo o cálculo monetário.** Representa valores de base 10 com exatidão,
diferente de `double`. A única exceção é a exponenciação da capitalização composta, já que
`Math.Pow` não tem sobrecarga para `decimal` — a conversão é isolada nessa operação e o
resultado é arredondado a centavos em seguida.

**Arredondamento apenas na saída.** Todo o cálculo intermediário permanece sem
arredondamento; só os valores do `PositionProjection` são arredondados, com
`MidpointRounding.AwayFromZero`.

**Enums persistidos como texto, não como inteiro.** Valor legível diretamente no banco e
imune a reordenação futura do enum — persistir o índice significa que inserir um novo valor
no meio da declaração reinterpreta silenciosamente os dados já gravados.

**Encapsulamento preservado no mapeamento.** `Id` não tem setter público, então o EF Core
não consegue materializá-lo pela propriedade. A saída foi `PropertyAccessMode.Field` na
configuração, e não afrouxar o domínio para acomodar o ORM. O mesmo princípio vale para o
relacionamento: `Position` guarda apenas `AssetId`, sem propriedade de navegação, e a
configuração declara a chave estrangeira sem introduzir uma.

**Exclusão restrita, não em cascata.** A chave estrangeira usa `Restrict`. Remover um
título que possui aportes deve falhar de forma explícita, não apagar o histórico do
investidor em silêncio.

**Cada camada registra as próprias dependências.** `AddApplication` vive em Application e
`AddInfrastructure` em Infrastructure; a API apenas compõe. O ponto de entrada não precisa
conhecer o interior de nenhuma camada para montá-las.

**Migration aplicada manualmente, não na inicialização.** Chamar `Database.Migrate()` no
startup é conveniente e comum, mas com múltiplas instâncias duas podem migrar ao mesmo
tempo, e uma falha de migration impede a aplicação de subir. O comando está documentado
abaixo e é executado uma vez.

**Consulta ao banco fora do domínio, decisão dentro.** A regra de alteração de título com
aportes precisa saber se existem aportes — informação que só o banco tem. A entidade recebe
isso como parâmetro (`hasPositions`) em vez de consultar um repositório: o dado vem de fora,
a decisão permanece no domínio.

**Middleware de exceção como rede de segurança.** Os casos de uso já convertem
`DomainException` em resposta 400, porque são eles que sabem qual regra foi violada. O
middleware existe para o que escapar, não para substituir esse tratamento.

**Erros de validação usam o mesmo envelope dos demais.** Por padrão, o `[ApiController]`
responde com `ProblemDetails` quando rejeita a requisição antes do controller — campo
ausente, GUID malformado, data inválida. Isso quebraria o contrato justamente no erro mais
comum de qualquer integração, então a fábrica de resposta de validação foi substituída. O mesmo vale
para 404 de rota inexistente, 405 e 415, que o framework devolve sem corpo ou como `ProblemDetails`:
são reescritos no envelope por `UseStatusCodePages`, só quando a resposta está vazia.

**Sem biblioteca de mock nos testes.** Dublês, quando necessários, são classes
`private sealed` declaradas no próprio arquivo de teste. Testes de domínio não precisam de
nenhum, já que as entidades não têm dependência externa.

## Como executar

### Com Docker, sem instalar nada

Requer apenas Docker.

```bash
git clone https://github.com/matheuscardosolazzarini/renda-fixa-api.git
cd renda-fixa-api
docker compose up --build -d
```

Sobe a API e o PostgreSQL. Aplique a migration uma vez. A imagem da API só tem o runtime, sem
o SDK, então o `dotnet ef` roda num contêiner do SDK que compartilha a rede do contêiner do
banco (`fixedincome-db`), onde o PostgreSQL responde em `localhost:5432`, a mesma conexão que o
`appsettings.Development.json` já usa:

```bash
docker run --rm --network container:fixedincome-db -v "${PWD}:/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  sh -c 'dotnet tool restore && dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api'
```

Execute-o na raiz do repositório, num terminal bash ou PowerShell (no cmd do Windows, troque
`${PWD}` pelo caminho da pasta). Em PowerShell, escreva o comando numa só linha. A primeira
execução baixa a imagem do SDK e os pacotes, e leva alguns minutos.

Depois da migration, reinicie a API:

```bash
docker compose restart api
```

A API importa as cotações de CDI e IPCA ao subir, antes de a migration existir, e a importação
falha por falta da tabela. O próximo ciclo só ocorreria 6 horas depois; sem o reinício, a
projeção de CDI e de IPCA+ falha até lá.

O Swagger fica em `http://localhost:8080/swagger`.

### Localmente, com o SDK

Requer .NET 8 SDK e Docker.

```bash
docker compose up -d db
dotnet tool restore
dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api
dotnet run --project src/FixedIncome.Api
```

O `dotnet tool restore` não é opcional: o `dotnet-ef` está fixado como ferramenta local em
`.config/dotnet-tools.json`, para que a versão usada aqui e no CI seja a mesma.

**Variável de ambiente que sobra no terminal.** Se `ConnectionStrings__DefaultConnection` estiver
definida no terminal, ela tem precedência sobre o `appsettings.Development.json` no `dotnet ef`, no
`dotnet run` e no bundle. Uma variável esquecida de uma sessão anterior faz o `dotnet ef database
update` migrar outro banco sem nenhum aviso. Para conferir sem imprimir a senha e para limpar:

```bash
[ -n "$ConnectionStrings__DefaultConnection" ] && echo definida || echo "não definida"
unset ConnectionStrings__DefaultConnection
```

```powershell
Test-Path Env:ConnectionStrings__DefaultConnection
Remove-Item Env:ConnectionStrings__DefaultConnection
```

Variável vazia ou só com espaços é ignorada pela fábrica do `dotnet ef`, que volta ao arquivo.
A opção `--connection` do `dotnet ef` vence o arquivo.

O arquivo `src/FixedIncome.Api/FixedIncome.Api.http` traz requisições de exemplo na ordem
de um fluxo real: criar título, listar, registrar aporte, projetar, consolidar. No fim,
consultam-se as cotações de CDI e IPCA e são feitas projeções de um CDB em CDI e de um CDB
IPCA+. A variável de base no topo do arquivo aponta para a porta 5124, a do `dotnet run`; para
a API em Docker (Compose ou imagem publicada, porta 8080), troque o número na primeira linha.

As cotações de CDI e IPCA são importadas do Banco Central ao iniciar a API e a cada 6 horas.
Para desligar a importação, defina `IndexRatesImport:Enabled` como `false` (em variável de
ambiente, `IndexRatesImport__Enabled=false`). Sem cotações importadas, a projeção de CDI e de
IPCA+ falha; a de PreFixed não depende delas.

A migration não é aplicada na inicialização — a decisão está registrada acima e comentada
no `Program.cs`.

### Imagem publicada

A cada merge na `main`, o workflow `release.yml` roda o CI e, com ele verde, publica a imagem
da API no GitHub Container Registry, em `ghcr.io/matheuscardosolazzarini/renda-fixa-api`. Antes
de a imagem ser promovida, o próprio workflow a testa:

1. Gera o migration bundle do EF Core e o anexa à execução como artefato `efbundle-<sha>`. O
   artefato fica disponível por 7 dias, e baixá-lo exige login no GitHub.
2. Publica a imagem com a tag do SHA do commit, `ghcr.io/matheuscardosolazzarini/renda-fixa-api:<sha>`.
3. Aplica o bundle num PostgreSQL limpo e sobe a imagem baixada do registro contra esse banco.
   A execução só passa se `/health` e `/api/assets` responderem 200.
4. Só depois disso move a tag `latest` para a mesma imagem, e só se o commit ainda for o topo da
   `main` naquele momento. Se a `main` já avançou (num re-run de uma execução antiga, por
   exemplo), o log traz um aviso e `latest` não se move.

Logo, `latest` é a versão aprovada pelo smoke test. A tag do SHA é a candidata: se o smoke
falhar, ela continua no registro, mas `latest` segue apontando para a versão anterior.

O workflow também pode ser disparado à mão (`workflow_dispatch`, pela aba Actions do GitHub), com a
mesma trava do `latest`: só move a tag a partir da `main` e com o commit ainda no topo. Push que só
altera arquivos de `docs/` ou `.md` da raiz (como este README) não dispara o release.

**Aplicando o bundle.** O download do artefato vem compactado em zip. O bundle é um executável
linux-x64, então roda em Linux ou no WSL. Ele lê a connection string pela mesma variável que a
API, `ConnectionStrings__DefaultConnection`. O `--connection` é opcional, mas a variável é
obrigatória mesmo assim, porque a fábrica do contexto a exige:

```bash
chmod +x efbundle
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=fixedincome;Username=fixedincome;Password=fixedincome" ./efbundle
```

Os blocos bash desta seção são para Linux ou WSL. No PowerShell 5.1, `chmod` e `VAR=valor comando`
não existem, e o bundle não roda direto no Windows. Há dois caminhos: abrir o terminal do WSL e
usar o bloco acima, ou executar o bundle num contêiner Linux `runtime-deps`, com a pasta do
`efbundle` montada e a variável passada com `-e`, numa linha só (`host.docker.internal` é o host do
banco a partir do contêiner no Docker Desktop):

```powershell
docker run --rm -v "${PWD}:/work" -w /work -e "ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;Database=fixedincome;Username=fixedincome;Password=fixedincome" mcr.microsoft.com/dotnet/runtime-deps:8.0 sh -c 'chmod +x ./efbundle && ./efbundle'
```

Para gerar o bundle a partir do código, em vez de baixar o artefato, use o mesmo comando do workflow,
com a saída na pasta atual (`--output efbundle`, em vez de `artifacts/efbundle`), depois de `dotnet tool restore`:

```bash
dotnet ef migrations bundle --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api --configuration Release --self-contained --target-runtime linux-x64 --output efbundle --force
```

**Migration alvo e reversão.** O bundle aceita uma migration como argumento e leva o banco até ela
(`./efbundle 20260818070004_InitialCreate`); sem argumento, aplica todas. Um nome que não existe
termina com erro (`The migration '...' was not found`). O argumento `0` reverte todas as migrations,
e o bundle não pede confirmação: `./efbundle 0` remove as tabelas da aplicação, com os dados, e
deixa só o histórico de migrations (`__EFMigrationsHistory`), vazio. Confira o banco apontado pela
connection string antes de rodar.

**Lendo um erro do bundle.** A causa vem na primeira linha da saída, e a pilha completa vem depois
(comportamento padrão do EF). Leia a primeira linha. Alguns exemplos:

- `28P01: password authentication failed`: usuário ou senha errados;
- `Name or service not known` ou `Connection refused`: host ou porta errados;
- `Host can't be null`: a connection string chegou sem host. Acontece com `--connection ""`, ou seja,
  a opção veio vazia (por exemplo, de uma variável de shell que não estava definida). Com
  `--connection` vazio o bundle não cai de volta na variável `ConnectionStrings__DefaultConnection`;
- `Connection string não encontrada`: a variável está ausente, vazia ou só com espaços. A mensagem
  cita também o `appsettings.Development.json`, que não vale para o bundle: o que resolve é definir a
  variável.

A senha não aparece na saída do bundle nem nos logs da API, nos casos exercitados no QA.

**Nome do banco.** Se o banco da connection string não existir, o bundle o cria e aplica as
migrations nele, e termina com sucesso: é o EF que cria o banco ao aplicar as migrations. Um erro
de digitação no `Database=` vira um banco novo e vazio, enquanto a API continua apontando para o
banco certo. Confira o nome do banco na connection string antes de rodar o bundle.

**Rodando a imagem publicada.** Suba o banco do Compose e aplique o bundle antes da API. Se o
serviço `api` do Compose estiver rodando, ele ocupa a porta 8080: pare-o antes com
`docker compose stop api`. A imagem publicada roda em Production, sem Swagger: o endereço
`/swagger` da seção anterior vale só para o Compose, que usa Development. Depois:

```bash
docker pull ghcr.io/matheuscardosolazzarini/renda-fixa-api:latest
docker run -d --name renda-fixa-api-ghcr -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=fixedincome;Username=fixedincome;Password=fixedincome" \
  ghcr.io/matheuscardosolazzarini/renda-fixa-api:latest
```

No PowerShell 5.1 a `\` não continua a linha: escreva o `docker run` numa linha só.

```powershell
docker run -d --name renda-fixa-api-ghcr -p 8080:8080 -e "ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;Database=fixedincome;Username=fixedincome;Password=fixedincome" ghcr.io/matheuscardosolazzarini/renda-fixa-api:latest
```

`host.docker.internal` é o host do banco a partir do contêiner no Docker Desktop (Windows e
macOS). No Linux, essa ponte não existe por padrão: troque `-p 8080:8080` por `--network host` e
o host por `localhost`.

**Importação de cotações na imagem.** Com a importação ligada (padrão do `appsettings.json`), a
imagem chama a API do Banco Central ao subir. Para desligá-la, acrescente
`-e IndexRatesImport__Enabled=false` ao `docker run`. Em variável de ambiente o separador é `__`,
não `:`: `IndexRatesImport:Enabled` é a forma do `appsettings.json`. Só `true` (ou a variável
ausente) mantém a importação ligada; qualquer outro valor, como `1` ou `yes`, a desliga, e a API
registra no log da partida que a importação está desligada.

**Ordem de subida.** Aplique o bundle antes de subir a imagem. Se ela subiu antes, enquanto o
bundle não for aplicado `GET /health` continua respondendo 200, mas todo `/api/*` responde 500
(as tabelas não existem). Depois de aplicar o bundle, as rotas de `/api/*` voltam a responder 200 sem
reiniciar a API. Com a importação ligada, porém, a da partida já falhou por falta da tabela
`index_rate`, e o próximo ciclo só ocorre em 6 horas: até lá a projeção de CDI e de IPCA+ falha.
Reinicie o contêiner (`docker restart renda-fixa-api-ghcr`) para a importação rodar de novo.

**Aviso sobre o pacote.** O pacote nasce privado no GHCR. Enquanto o autor do repositório não o
tornar público nas configurações do pacote, o `docker pull` só funciona com login no GitHub
(`docker login ghcr.io`).

### Por que a nuvem está adiada

A publicação em nuvem com Terraform está adiada desde 08/11/2026. A assinatura do Azure foi
desabilitada ao fim do crédito de avaliação, e não há orçamento para reativá-la. Terraform que
nunca rodou contra um ambiente real não prova nada, então a fase volta quando houver um destino.
Até lá, a entrega é a imagem no GHCR, validada no próprio pipeline. Os dois caminhos avaliados
estão descritos em [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Testes

```bash
dotnet test
```

591 testes, executados a cada push pelo GitHub Actions.

| Camada | Testes | Linha | Branch |
|---|---|---|---|
| Domain | 292 | 95,2% | 98,1% |
| Application | 112 | 92,6% | 98,9% |
| Infrastructure | 130 | 99,7% | 98,4% |
| Api | 57 | 96,4% | 91,3% |

Cobertura medida com o coverlet e o `coverlet.runsettings` do CI, projeto a projeto; cada linha da
tabela traz a cobertura do assembly da própria camada (os assemblies referenciados também são
instrumentados, mas não entram na conta). Migrations são excluídas da medição por serem código
gerado pelo `dotnet ef`; mantê-las no denominador faria a cobertura de Infrastructure cair para
cerca de 30% e comunicar algo falso sobre o que está de fato testado.

O domínio cobre as sete regras de negócio, incluindo as seis fronteiras exatas da tabela
regressiva de IR (180, 181, 360, 361, 720 e 721 dias) e cada invariante de entidade violada
individualmente.

Além dos testes automatizados, o fluxo foi validado manualmente contra o PostgreSQL real:
tipos de coluna, persistência dos enums como texto e a chave estrangeira com `RESTRICT`.
Provider InMemory não verifica nenhuma dessas três coisas — teste verde e sistema correto
são afirmações diferentes.

## Processo

O projeto é construído com Spec-Driven Development: cada fase começa por uma especificação
técnica versionada em `docs/`, com escopo, critérios de aceite e restrições explícitas. A
implementação só começa depois, e é revisada contra a spec antes do commit.

As convenções de código estão em [`CLAUDE.md`](CLAUDE.md) e os procedimentos recorrentes —
escrita de testes e revisão de fase — em `.claude/skills/`, para que a assistência de IA
siga o mesmo padrão a cada sessão em vez de depender de instrução repetida.

O histórico de commits reflete essa sequência: spec, implementação, revisão.

Ao final da F5 o projeto passou por uma auditoria completa, registrada em
[`docs/AUDITORIA-GERAL.md`](docs/AUDITORIA-GERAL.md), que verificou integridade das camadas,
robustez do contrato HTTP com dezenove entradas malformadas, e cada regra de negócio ponta a
ponta. Os defeitos encontrados foram corrigidos antes de seguir.

## Stack

.NET 8 · ASP.NET Core · Entity Framework Core 8 · PostgreSQL 16 · xUnit · Docker
