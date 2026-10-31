# Controle de Investimentos em Renda Fixa

![CI](https://github.com/matheuscardosolazzarini/renda-fixa-api/actions/workflows/ci.yml/badge.svg)

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

A importação é controlada por `IndexRatesImport:Enabled` no `appsettings.json`. Ela fica
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
vazia. Data inicial posterior à final, PreFixed e indexador inexistente devolvem 400.

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
comum de qualquer integração, então a fábrica de resposta de validação foi substituída.

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

Sobe a API e o PostgreSQL. Aplique a migration uma vez:

```bash
docker compose exec api dotnet ef database update
```

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

O arquivo `src/FixedIncome.Api/FixedIncome.Api.http` traz requisições de exemplo na ordem
de um fluxo real: criar título, listar, registrar aporte, projetar, consolidar. No fim,
consultam-se as cotações de CDI e IPCA e são feitas projeções de um CDB em CDI e de um CDB
IPCA+.

As cotações de CDI e IPCA são importadas do Banco Central ao iniciar a API e a cada 6 horas.
Para desligar a importação, defina `IndexRatesImport:Enabled` como `false`. Sem cotações
importadas, a projeção de CDI e de IPCA+ falha; a de PreFixed não depende delas.

A migration não é aplicada na inicialização — a decisão está registrada acima e comentada
no `Program.cs`.

## Testes

```bash
dotnet test
```

439 testes, executados a cada push pelo GitHub Actions.

| Camada | Testes | Linha | Branch |
|---|---|---|---|
| Domain | 251 | 95,5% | 97,9% |
| Application | 74 | 93,1% | 100% |
| Infrastructure | 81 | 88,0% | 88,2% |
| Api | 33 | 94,4% | 91,2% |

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
