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
uma data e a consolidação da carteira. Nove endpoints, documentados no Swagger.

Duas ausências são deliberadas:

**Sem remoção de aporte.** Registro de aporte é histórico financeiro, e histórico se
corrige por estorno, não por exclusão. Um endpoint de estorno seria a evolução natural;
um `DELETE` não é.

**Sem cotação real de indexadores.** CDI e IPCA vêm de configuração. Consultar valores
reais exigiria integração externa, tratamento de indisponibilidade e cache — trabalho que
não acrescenta nada ao que este projeto se propõe a demonstrar.

A especificação funcional completa está em [`docs/ESPECIFICACAO.md`](docs/ESPECIFICACAO.md),
e as especificações de cada fase de implementação estão na mesma pasta.

## Regras de negócio

O domínio implementa cinco regras, todas cobertas por teste:

**Rentabilidade** — capitalização composta sobre dias corridos, base 365, entre a data de
aporte e a data de referência.

**Limite de vencimento** — data de referência posterior ao vencimento é truncada para a
data de vencimento.

**Tabela regressiva de IR** — alíquota conforme o prazo decorrido: 22,5% até 180 dias,
20% até 360, 17,5% até 720 e 15% acima disso. O imposto incide exclusivamente sobre o
rendimento, nunca sobre o principal.

**Isenção** — LCI e LCA não sofrem incidência de IR para pessoa física, independentemente
do prazo.

**Vigência do aporte** — aporte com data anterior à emissão ou posterior ao vencimento do
título é rejeitado.

**Coerência do consolidado** — os totais da carteira somam os valores já arredondados de
cada projeção individual, em vez de arredondar a soma dos valores brutos. A diferença é de
centavos, mas o total precisa fechar exatamente com a lista que o usuário vê; um
consolidado que não bate com as parcelas é lido como erro do sistema.

**Alteração de título com aportes** — um título que já possui aportes não pode ter as datas
de emissão ou vencimento alteradas. Permitir isso tornaria retroativamente inválido um
aporte que era válido no momento do registro. Nome, emissor e taxa seguem alteráveis.

Indexadores usam taxas configuradas em `appsettings`. Consultar CDI e IPCA reais exigiria
integração externa, tratamento de indisponibilidade e cache — trabalho que não acrescenta
nada ao que este projeto se propõe a demonstrar.

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
de um fluxo real: criar título, listar, registrar aporte, projetar, consolidar.

As taxas de CDI e IPCA em `appsettings.json` são valores fixos de referência, não cotações
reais.

A migration não é aplicada na inicialização — a decisão está registrada acima e comentada
no `Program.cs`.

## Testes

```bash
dotnet test
```

124 testes, executados a cada push pelo GitHub Actions.

| Camada | Testes | Linha | Branch |
|---|---|---|---|
| Domain | 71 | 94,1% | 94,2% |
| Application | 28 | 87,8% | 97,2% |
| Infrastructure | 9 | 82,3% | 100% |
| Api | 16 | 93,3% | 86,1% |

Migrations são excluídas da medição por serem código gerado pelo `dotnet ef`; mantê-las no
denominador faria a cobertura de Infrastructure cair para cerca de 30% e comunicar algo
falso sobre o que está de fato testado.

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
