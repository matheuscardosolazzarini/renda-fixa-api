# Especificação — API de Controle de Investimentos em Renda Fixa

## Problema

Investidor pessoa física que possui títulos de renda fixa em mais de uma corretora não
tem visão consolidada da carteira. Cada instituição mostra apenas os próprios papéis, e
o cálculo de rentabilidade líquida — que depende de prazo, indexador e tributação — fica
por conta do investidor.

Esta API centraliza o cadastro dos títulos, registra os aportes e calcula a posição
projetada para uma data, já líquida de imposto de renda.

## Escopo

### Dentro do escopo

- Cadastro de títulos de renda fixa (CDB, LCI, LCA, Tesouro Direto)
- Registro de aportes vinculados a um título
- Cálculo de rentabilidade bruta até uma data de referência
- Aplicação da tabela regressiva de IR e das isenções por tipo de título
- Consolidação da carteira

### Fora do escopo (v1)

- Autenticação e multiusuário
- Resgates parciais
- IOF sobre resgates com menos de 30 dias
- Integração com API de mercado para cotação de indexadores
- Marcação a mercado do Tesouro Direto

Indexadores usam taxas fixas configuradas em `appsettings.json`. Buscar CDI e IPCA reais
exige integração externa, que multiplicaria o escopo sem acrescentar valor arquitetural.

## Domínio

### Entidades

**FixedIncomeAsset** — o título disponível para investimento.

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | |
| Name | string | obrigatório, até 120 caracteres |
| Issuer | string | obrigatório, até 120 caracteres |
| AssetType | enum | CDB, LCI, LCA, TreasuryBond |
| IndexType | enum | PreFixed, Cdi, Ipca |
| Rate | decimal | maior que zero |
| IssueDate | DateOnly | |
| MaturityDate | DateOnly | posterior a IssueDate |

O significado de `Rate` depende de `IndexType`: taxa anual em PreFixed, percentual do CDI
em Cdi, e taxa anual somada à variação do IPCA em Ipca.

**Position** — o aporte do investidor em um título.

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | |
| AssetId | Guid | FK para FixedIncomeAsset |
| InvestedAmount | decimal | maior que zero |
| ApplicationDate | DateOnly | entre IssueDate e MaturityDate do título |

### Regras de negócio

**RN-01 — Rentabilidade bruta.** Capitalização composta sobre os dias corridos entre a data
de aplicação e a data de referência, com base em 365 dias.

**RN-02 — Data de referência limitada.** Se a data de referência ultrapassar o vencimento
do título, o cálculo é feito até o vencimento.

**RN-03 — Tabela regressiva de IR.** Alíquota determinada pelo prazo decorrido:

| Prazo | Alíquota |
|---|---|
| até 180 dias | 22,5% |
| de 181 a 360 dias | 20,0% |
| de 361 a 720 dias | 17,5% |
| acima de 720 dias | 15,0% |

O imposto incide apenas sobre o rendimento, nunca sobre o principal.

**RN-04 — Isenção.** LCI e LCA são isentos de IR para pessoa física. A alíquota é zero
independentemente do prazo.

**RN-05 — Aporte fora da vigência.** Aporte com data anterior à emissão ou posterior ao
vencimento é rejeitado.

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/assets` | Cadastra título |
| GET | `/api/assets` | Lista títulos |
| GET | `/api/assets/{id}` | Detalha título |
| PUT | `/api/assets/{id}` | Atualiza título |
| DELETE | `/api/assets/{id}` | Remove título sem aportes |
| POST | `/api/positions` | Registra aporte |
| GET | `/api/positions` | Lista aportes |
| GET | `/api/positions/{id}/projection?referenceDate=` | Projeção da posição |
| GET | `/api/portfolio/summary?referenceDate=` | Consolidado da carteira |

A projeção retorna valor investido, valor bruto, rendimento, alíquota aplicada, imposto
devido e valor líquido.

## Arquitetura

```
src/
  FixedIncome.Domain          entidades, enums, exceções de domínio
  FixedIncome.Application     casos de uso, DTOs, interfaces de repositório
  FixedIncome.Infrastructure  EF Core, configurações, repositórios
  FixedIncome.Api             controllers, middleware, DI
tests/
  FixedIncome.Domain.Tests
  FixedIncome.Application.Tests
  FixedIncome.Infrastructure.Tests
```

Dependências apontam para dentro: Api → Application → Domain, com Infrastructure
implementando as interfaces declaradas em Application.

O cálculo de rentabilidade e a apuração de IR ficam no domínio, não nos casos de uso.
São regras que independem de orquestração e precisam ser testáveis sem infraestrutura.

## Decisões técnicas

| Item | Escolha |
|---|---|
| Runtime | .NET 8 |
| Banco | PostgreSQL 16 |
| ORM | Entity Framework Core 8 com Npgsql |
| Testes | xUnit, padrão AAA, stubs inline |
| Documentação | Swagger / OpenAPI |
| Execução local | Docker Compose |
| CI | GitHub Actions — build e testes |

Padrões mantidos do projeto: controller herda `ControllerBase` e retorna
`StatusCode(response.StatusCode, response)`; respostas encapsuladas em `ApiResponse<T>`;
configuração de entidade via `IEntityTypeConfiguration` com `HasColumnName` em SNAKE_CASE;
repositórios assíncronos retornando `Task<IEnumerable<T>>`.

Comentários em português, identificadores em inglês.

## Fases

**F1 — Fundação.** Solution com os quatro projetos, referências entre camadas, Docker
Compose com PostgreSQL, health check respondendo.

**F2 — Domínio.** Entidades, enums, validações de invariante, cálculo de rentabilidade e
apuração de IR. Testes de domínio cobrindo RN-01 a RN-05, incluindo as quatro faixas da
tabela regressiva e a isenção de LCI/LCA.

**F3 — Persistência.** DbContext, configurações de entidade, migration inicial,
repositórios. Testes com InMemory.

**F4 — Casos de uso e API.** Casos de uso, DTOs, controllers, tratamento global de exceção
de domínio para 400, Swagger.

**F5 — Consolidação.** Endpoint de projeção e de resumo da carteira.

**F6 — Entrega.** Workflow de CI, README com contexto do problema e decisões, coleção de
requisições de exemplo.

Cada fase termina com os testes passando. F1 a F3 concentram o valor de demonstração —
se o tempo apertar, é melhor entregar até F4 bem feito que chegar em F6 corrido.

## Critérios de conclusão

- Testes verdes no CI, com badge visível no README
- `docker compose up` sobe API e banco sem passo manual
- Swagger acessível e com todos os endpoints documentados
- Cobertura de testes acima de 80% nas camadas de domínio e aplicação
- README explicando o problema, as decisões de arquitetura e como executar
