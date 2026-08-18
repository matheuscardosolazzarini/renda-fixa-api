# Controle de Investimentos em Renda Fixa

API em .NET 8 para consolidação de carteira de renda fixa, com cálculo de rentabilidade
líquida de imposto de renda.

## O problema

Investidor pessoa física com títulos em mais de uma corretora não tem visão consolidada
da carteira. Cada instituição mostra apenas os próprios papéis, e o cálculo de
rentabilidade líquida — que depende de prazo, indexador e tributação — fica por conta do
investidor.

Esta API centraliza o cadastro dos títulos, registra os aportes e projeta a posição para
uma data de referência, já descontado o imposto devido.

## Status

Projeto em construção, entregue por fases.

| Fase | Escopo | Situação |
|---|---|---|
| F1 | Estrutura da solution e ambiente local | Concluída |
| F2 | Domínio, invariantes e cálculo | Concluída |
| F3 | Persistência com EF Core | Em andamento |
| F4 | Casos de uso e endpoints | Pendente |
| F5 | Consolidação da carteira | Pendente |
| F6 | CI e empacotamento | Pendente |

A especificação funcional completa está em [`docs/ESPECIFICACAO.md`](docs/ESPECIFICACAO.md).

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

**Sem biblioteca de mock nos testes.** Dublês, quando necessários, são classes
`private sealed` declaradas no próprio arquivo de teste. Testes de domínio não precisam de
nenhum, já que as entidades não têm dependência externa.

## Como executar

Pré-requisitos: .NET 8 SDK e Docker.

```bash
git clone https://github.com/matheuscardosolazzarini/renda-fixa-api.git
cd renda-fixa-api
docker compose up -d
dotnet build
dotnet test
```

O Compose sobe um PostgreSQL 16 na porta 5432. A API ainda não é executável — o endpoint
HTTP chega na F4.

## Testes

```bash
dotnet test
```

51 testes cobrindo as cinco regras de negócio, incluindo as seis fronteiras exatas da
tabela regressiva de IR (180, 181, 360, 361, 720 e 721 dias) e cada invariante de entidade
violada individualmente.

## Processo

O projeto é construído com Spec-Driven Development: cada fase começa por uma especificação
técnica versionada em `docs/`, com escopo, critérios de aceite e restrições explícitas. A
implementação só começa depois, e é revisada contra a spec antes do commit.

As convenções de código estão em [`CLAUDE.md`](CLAUDE.md) e os procedimentos recorrentes —
escrita de testes e revisão de fase — em `.claude/skills/`, para que a assistência de IA
siga o mesmo padrão a cada sessão em vez de depender de instrução repetida.

O histórico de commits reflete essa sequência: spec, implementação, revisão.

## Stack

.NET 8 · ASP.NET Core · Entity Framework Core 8 · PostgreSQL 16 · xUnit · Docker
