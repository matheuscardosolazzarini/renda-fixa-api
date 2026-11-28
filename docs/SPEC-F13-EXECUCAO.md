# Spec de execução — F13: Contrato v3 para o front

Status: aprovada (27/11/2026)

Aprovada pelo Matheus sem alterações, com os achados das revisões e os casos de referência como estão.

Documento de execução para agente. Objetivo: entregar ao front `renda-fixa-web` quatro dados que as
telas usam e que ele só conseguiria calculando, o que a D-07 da arquitetura dele proíbe: o ganho
líquido, a data em que o imposto de cada aporte cai de faixa, a data em que o IOF zera e os totais de
cada título na lista do consolidado. A fonte é `docs/ESPECIFICACAO.md` (RN-02, RN-03, RN-04, RN-07,
RN-11, RN-15 a RN-18 e a seção "Endpoints") e a seção F13 de `docs/ROADMAP.md`. O contrato muda só
por adição de campos; nenhum campo existente muda de nome, de tipo ou de valor.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão em
`CLAUDE.md`. Ambas prevalecem sobre qualquer suposição. As regras desta fase já estão escritas na
`ESPECIFICACAO.md` (RN-15 a RN-18 e "Endpoints") na preparação da fase.

Estado de partida (linha de base medida em 27/11/2026): branch `f13-contrato-front-v3`, HEAD igual a
`origin/main` (`9174345`), alterações só em `docs/` e `.claude/` (preparação da fase), build sem
warning, **863 testes verdes** (Domain 411, Application 184, Infrastructure 173, Api 87,
Architecture 8).

Como o código está hoje:

- `Position.Project` (Domain) calcula a projeção. Os dias corridos (`ElapsedDays`) são contados até a
  data de cálculo, que é a referência limitada ao vencimento (RN-02). `TaxRate` sai de
  `IncomeTaxTable.RateFor(assetType, elapsedDays)`, que devolve 0 para LCI e LCA e usa um `switch`
  com os limites 180, 360 e 720. `IofRate` sai de `IofTable.RateFor(elapsedDays)`, exceto LCA, que é
  0 no próprio `Project`.
- `PositionProjection` (Domain) é um record posicional; o último parâmetro é `IsProjected`.
- `PortfolioSummary.Create` (Domain) recebe `(PositionProjection Projection, AssetType AssetType)`,
  soma os totais (RN-07), agrupa `ByAssetType` e calcula `IsProjected`.
- `PositionProjector` (Application/Services) aplica as regras de data do consolidado e devolve a
  lista de `(Position, Asset, Projection, Date)`. `GetPortfolioSummaryUseCase` e
  `GetAssetProjectionUseCase` chamam `PortfolioSummary.Create` sobre essa lista.
- `PositionProjectionResponse`, `PortfolioSummaryResponse` e `AssetProjectionResponse`
  (Application/DTOs) são records posicionais; o JSON não ignora nulos (`Program.cs` só registra o
  `JsonStringEnumConverter`), então um campo nulo sai como `null`, sempre presente.
- O Swagger é o Swashbuckle 6.6.2 (OpenAPI 3.0): tipo anulável sai com `"nullable": true`.

Esta fase toca:

- `src/FixedIncome.Domain` — `PositionProjection`, `PortfolioSummary`, `IncomeTaxTable`, `IofTable`,
  `Position`
- `src/FixedIncome.Application` — os três DTOs e os dois casos de uso que chamam
  `PortfolioSummary.Create`
- `src/FixedIncome.Api` — só `FixedIncome.Api.http`
- testes de Domain, Application e Api, e `README.md`

## Fora de escopo

- Liquidez do título: o front deixou de mostrá-la (decisão de 27/11/2026 no ROADMAP)
- Ajustar `nextTaxRateDate` ou `iofFreeDate` para dia útil: as duas datas são contadas em dias
  corridos (RN-16, RN-17), como os prazos de IR e IOF (RN-01), e podem cair em fim de semana ou
  feriado (o README diz isso)
- Registrados pelo `especialista-renda-fixa` na revisão, sem mudança nesta fase: vencimento em dia não
  útil, pago no dia útil seguinte, que pode mudar a faixa de IR no resgate (o sistema calcula no
  `MaturityDate`, RN-02); no Tesouro Direto, a contagem a partir da liquidação da compra e não da
  ordem (o sistema usa a `ApplicationDate` informada); a carência mínima de LCI e LCA (Res. CMN
  5.118/2024), que torna o `iofFreeDate` da LCI inalcançável na prática, embora correto pela RN-17
- Marca de estimado ou projetado em `byAsset` (RN-18)
- Mudança de nome, tipo ou valor de campo existente; mudança de cálculo
- Endpoint, migration, tabela, coluna ou pacote NuGet novo
- As pendências herdadas da F12 (formato de data, parâmetro repetido, `indexType` com espaços,
  mensagens de IPCA, desempenho, `PreFixed` no enum do Swagger, escala da taxa no `PUT`)
- Qualquer arquivo em `../renda-fixa-web`

## Regras novas

As regras são as da `ESPECIFICACAO.md`, transcritas aqui no que a implementação precisa decidir.

**RN-15 — Ganho líquido.** `netYield = NetAmount − InvestedAmount`, os dois já arredondados. Pode ser
negativo (IPCA+ com deflação, sem imposto) e é zero no dia do aporte. `totalNetYield` é a soma dos
`netYield` dos itens e é sempre igual a `TotalNetAmount − TotalInvestedAmount`.

**RN-16 — Próxima faixa de IR.** Com `d` = `ElapsedDays` (dias corridos até a data de cálculo,
limitada pela RN-02):

| Faixa em `d` | `TaxRate` | `NextTaxRate` | `NextTaxRateDate` |
|---|---|---|---|
| `d` ≤ 180 | 22,5 | 20,0 | aporte + 181 |
| 181 ≤ `d` ≤ 360 | 20,0 | 17,5 | aporte + 361 |
| 361 ≤ `d` ≤ 720 | 17,5 | 15,0 | aporte + 721 |
| `d` ≥ 721 | 15,0 | nulo | nulo |

Nulos também quando o título é LCI ou LCA (RN-04) e quando `NextTaxRateDate` é posterior ao
vencimento. `NextTaxRateDate` igual ao vencimento é devolvida: no vencimento a posição alcança a
faixa. Não depende de cotação e não muda `IsProjected` nem `IsEstimated`.

**RN-17 — Fim do IOF.** `IofFreeDate = aporte + 30` quando `d` < 30. Nulo quando `d` ≥ 30, quando o
título é LCA ou quando a data é posterior ao vencimento (igual ao vencimento é devolvida). LCI, CDB e
Tesouro Direto recebem a data. No dia do aporte (`d` = 0) a alíquota de IOF é 0 (RN-11, sem
rendimento), mas `IofFreeDate` é aporte + 30: a alíquota volta a incidir no dia 1.

**RN-18 — Totais por título.** `byAsset` no consolidado: um item por título com pelo menos uma
posição em `positions`, na ordem da primeira aparição do título em `positions`, com `assetId`,
`positionCount` e os totais de investido, bruto, rendimento bruto, IR, IOF, líquido e ganho líquido,
cada um a soma dos itens daquele título. Carteira vazia: lista vazia.

## Mudanças de comportamento

Nenhuma. Todos os campos existentes mantêm nome, tipo e valor; os testes existentes que mudam são só
as listas fechadas de propriedades e as construções mecânicas listadas abaixo.

## Decisões de desenho

**D-1. Os ganhos líquidos são propriedades calculadas.** `PositionProjection` ganha `public decimal
NetYield => NetAmount - InvestedAmount;`, e `PortfolioSummary` e `AssetSummary` (D-5) ganham
`public decimal TotalNetYield => TotalNetAmount - TotalInvestedAmount;`, todos fora dos parâmetros
posicionais: não há como construir uma projeção ou um total com um ganho que não feche. Em decimal,
`TotalNetAmount − TotalInvestedAmount` é exatamente a soma dos `NetYield` dos itens (a forma da RN-15),
e o teste 3 prova essa igualdade. Nenhum record muda de assinatura no P1.

**D-2. As faixas de IR ficam numa tabela só.** `IncomeTaxTable` passa a ter uma lista privada de
faixas `(int FromDay, decimal Rate)`: (0; 22.5m), (181; 20.0m), (361; 17.5m), (721; 15.0m), com os
literais escritos assim: a escala de `TaxRate` e de `NextTaxRate` no JSON (`20.0`) vem dos literais
da tabela (`decimal.Round` só reduz a escala, nunca completa casas). `RateFor` devolve a alíquota da última faixa com
`FromDay ≤ d` (comportamento idêntico ao de hoje, todos os testes existentes de `IncomeTaxTable`
verdes sem alteração) e o método novo `NextBracketFor(AssetType assetType, int elapsedDays)` devolve
`(int FromDay, decimal Rate)?`, na mesma ordem da lista: a primeira faixa com `FromDay > d`, ou nulo
para LCI, LCA ou `d` ≥ 721. Com `d` negativo, lança a mesma `DomainException` de `RateFor`. Assim o
limite 180/181 não pode divergir entre a alíquota de hoje e a próxima. `NextBracketFor` não sabe de
vencimento nem de data.

**D-3. O dia de fim do IOF sai da tabela do IOF.** `IofTable` expõe `public static int FreeFromDay =>
Rates.Length;` (30, o dia cuja alíquota é 0%). `RateFor` não muda.

**D-4. As datas e o corte no vencimento ficam em `Position.Project`.** É onde estão o aporte, o
vencimento (RN-02) e o tipo do título. Com `d` já calculado:

- `NextTaxRateDate = ApplicationDate.AddDays(FromDay)` da faixa de `NextBracketFor`; se for posterior
  a `asset.MaturityDate`, `NextTaxRate` e `NextTaxRateDate` ficam nulos (os dois juntos, nunca um só);
- `IofFreeDate = ApplicationDate.AddDays(IofTable.FreeFromDay)` se `d < FreeFromDay` e o título não é
  LCA; se for posterior a `asset.MaturityDate`, nulo.

A isenção de IOF da LCA vira uma variável local de `Project` (`isIofExempt`), usada pela alíquota e
por `IofFreeDate`, para a regra ficar num ponto só (achado M-5 do especialista). As datas usam
`AddDays`, nunca `AddMonths` nem `AddYears`: 721 dias não são dois anos.

`NextTaxRate` sai com o mesmo valor e escala da tabela (20,0, 17,5, 15,0), como `TaxRate`.
`PositionProjection` ganha `NextTaxRate` (`decimal?`), `NextTaxRateDate` (`DateOnly?`) e
`IofFreeDate` (`DateOnly?`) como os três últimos parâmetros posicionais, depois de `IsProjected`.

Invariante (teste): com `NextTaxRateDate` não nulo, `IncomeTaxTable.RateFor(tipo, FromDay)` é
`NextTaxRate` e `IncomeTaxTable.RateFor(tipo, FromDay − 1)` é a alíquota da faixa de `d`; com
`IofFreeDate` não nulo, `IofTable.RateFor(30)` é 0 e `IofTable.RateFor(29)` é 3.

**D-5. `byAsset` é calculado no domínio, com a mesma soma da raiz.** `PortfolioSummary.Create` passa
a receber `(PositionProjection Projection, Guid AssetId, AssetType AssetType)`. Um método privado
soma os totais de uma lista de itens e serve à raiz e a cada título, para os dois não divergirem; ele
não cria tipo público novo. O record ganha `ByAsset` (`IReadOnlyList<AssetSummary>`) como último
parâmetro, depois de `IsProjected`, com `AssetSummary` aninhado em `PortfolioSummary`, como
`AssetTypeSummary`: `AssetSummary(Guid AssetId, int PositionCount, decimal TotalInvestedAmount,
decimal TotalGrossAmount, decimal TotalGrossYield, decimal TotalTaxAmount, decimal TotalIofAmount,
decimal TotalNetAmount)`, mais o `TotalNetYield` calculado da D-1. A ordem é a da primeira aparição
(`GroupBy` do LINQ preserva essa ordem; o teste intercala títulos para prová-lo). `ByAssetType` não
muda.

`GetAssetProjectionUseCase` continua chamando `PortfolioSummary.Create` com os itens do título e usa
só os totais da raiz: os totais de um título em `byAsset` e os da projeção por título saem do mesmo
método sobre os mesmos itens, desde que as projeções sejam iguais. São, porque o `PositionProjector`
resolve a mesma data nos dois e o `MarketDataLoader` sempre carrega a última cotação de CDI e a
janela do IPCA do banco (D-1 da F12), então o mercado carregado de um período mais curto dá a mesma
projeção: o que cada posição consulta fora do próprio período (`LastCdi`, a janela de 12 meses do
IPCA, `Today` e o calendário) é o mesmo nas duas cargas. Duas ressalvas, que não mudam o critério: sem
data informada, as duas requisições resolvem a data padrão cada uma, e só coincidem se nenhuma
cotação de CDI for importada entre elas; e o consolidado responde 400 se outro título falhar, enquanto
a projeção por título responde 200. Por isso os testes de igualdade (16 e 20) usam data informada, e
o 16 também sem data, com os stubs fixos.

**D-6. Contrato: campos novos no fim de cada record, como na F12.**

- `PositionProjectionResponse`: `NetYield` (decimal), `NextTaxRate` (decimal?), `NextTaxRateDate`
  (DateOnly?), `IofFreeDate` (DateOnly?), depois de `IsProjected`.
- `PortfolioSummaryResponse`: `TotalNetYield` (decimal) e `ByAsset`
  (`IReadOnlyList<AssetSummaryResponse>`), depois de `IsProjected`. `AssetSummaryResponse` é record
  aninhado em `PortfolioSummaryResponse`, como `AssetTypeSummaryResponse`, com `FromSummary`.
- `AssetProjectionResponse`: `TotalNetYield` (decimal), depois de `Positions`. Sem `byAsset` (é um
  título só) e sem `isProjected` (A-1 da F12).

Os itens de `positions` do consolidado e da projeção por título ganham os quatro campos da projeção
por serem o mesmo DTO. Nulo sai como `null`, com o campo presente.

No `swagger.json` (Swashbuckle 6.6.2 sem `SupportNonNullableReferenceTypes`), todo `Nullable<T>` e
toda propriedade de tipo referência saem com `nullable: true`. Os três campos anuláveis da projeção
saem assim, e `byAsset` também, como `byAssetType` e `positions` já saem hoje. O `Describe` do teste
lê só `type` e `format` e não precisa mudar; o teste 21 compara à parte o conjunto fechado de
propriedades `nullable` de cada schema, para provar que nenhum campo existente virou anulável.

### Contrato resultante

`GET /api/positions/{id}/projection` — `data` ganha, depois de `isProjected`:

```json
{
  "…": "…",
  "isProjected": false,
  "netYield": 2.67,
  "nextTaxRate": 20.0,
  "nextTaxRateDate": "2025-09-29",
  "iofFreeDate": "2025-05-01"
}
```

(Ilustrativo: aporte em 01/04/2025 e referência 22/04/2025, 21 dias corridos. Os valores esperados
dos testes são os da seção "Casos de referência".)

`GET /api/portfolio/summary` — `data` ganha, depois de `isProjected`:

```json
{
  "…": "…",
  "isProjected": false,
  "totalNetYield": 5.34,
  "byAsset": [
    {
      "assetId": "…", "positionCount": 2,
      "totalInvestedAmount": 2000, "totalGrossAmount": 2009.86, "totalGrossYield": 9.86,
      "totalTaxAmount": 1.56, "totalIofAmount": 2.96, "totalNetAmount": 2005.34, "totalNetYield": 5.34
    }
  ]
}
```

`GET /api/assets/{id}/projection` — `data` ganha `totalNetYield` depois de `positions`.

### Achados das revisões da spec (27/11/2026)

`especialista-renda-fixa`: sem erro de regra. As três perguntas de mercado conferem: as faixas
começam em aporte + 181, + 361 e + 721 (Lei 11.033/2004, art. 1º, prazo contado da aplicação; CC,
art. 132), o IOF zera em aporte + 30 (Decreto 6.306/2007, anexo), a LCA tem IOF zero (art. 32, § 2º,
V) e a LCI paga, e o ganho líquido só fica negativo com rendimento bruto ≤ 0. Incorporados I-1 (teste
2), I-2 (testes 4, 6 e 10), M-1 (29/02 e README), M-2 (README), M-3 (escala no teste 9), M-4 (`d`
negativo) e M-5 (`isIofExempt` e o `Theory` do teste 10). F-1, F-2 e F-3 em "Fora de escopo".

`revisor-arquitetura`: reprovou por um bloqueante, a lista fechada de
`AssetProjectionFlowTests`, agora na tabela de testes que mudam. Incorporados também o importante 2
(`TotalNetYield` calculado, D-1), o importante 3 (conjunto fechado de `nullable`, D-6 e teste 21) e os
menores 4 (ressalvas da D-5), 5 (D-2), 6 (tabela de testes que mudam) e 7 (D-5).

Não incorporado: o menor 8 do revisor (teste 8 quase redundante). Fica, por ser barato e por
proteger a invariante se a tabela da D-2 for desfeita no futuro.

## Implementação em passos

Cada passo: teste antes do código, `dotnet build` sem warning e `dotnet test` verde ao final, sem
queda de contagem. Os testes de API e as asserções do `swagger.json` de cada campo nascem no passo
que cria o campo.

### Testes existentes que mudam

Só estes. Qualquer outro teste existente que quebrar: o implementador para e reporta.

| Teste | Por quê | Adaptação |
|---|---|---|
| `SwaggerContractTests.Swagger_mantem_a_lista_fechada_de_propriedades_da_projecao_e_do_resumo_com_tipo_e_formato` | Lista fechada | Os campos novos entram no fim de cada lista (P1, P2, P3); os existentes ficam iguais, na mesma ordem |
| `AssetProjectionSwaggerTests` (lista fechada de `AssetProjectionResponse`) | Lista fechada | `totalNetYield` no fim (P1) |
| `AssetProjectionFlowTests.Projecao_do_titulo_devolve_o_contrato_com_os_totais_do_caso_de_referencia` | Lista fechada e ordenada das propriedades de `data` | `totalNetYield` no fim (P1) |
| `PositionProjectionFlowTests.Projecao_mantem_os_campos_antigos_com_os_mesmos_nomes_e_acrescenta_so_as_duas_datas_e_is_projected` | Conjunto fechado de nomes; o nome do teste fica falso | Os nomes novos entram no conjunto (P1, P2) e o teste é renomeado para descrever os campos acrescentados |
| O teste de conjunto fechado de nomes de `data` em `PortfolioSummaryFlowTests` (o do comentário "as 8 propriedades antigas mais…") | Conjunto fechado de nomes | Os nomes novos entram no conjunto (P1, P3) e o comentário é atualizado |
| `PortfolioSummaryTests`: o helper `CriarProjecao`, as chamadas de `PortfolioSummary.Create` e o `Array.Empty<(PositionProjection Projection, AssetType AssetType)>()` | Assinatura nova | Mecânica: o helper passa os três parâmetros novos nulos (P2); as tuplas e o tipo explícito ganham o `AssetId` (P3). Nenhum valor esperado muda |

### Passos

**P1 — RN-15 (Sonnet).**
`NetYield` em `PositionProjection` e `TotalNetYield` em `PortfolioSummary` (D-1). Os três DTOs ganham
os campos (D-6). Testes de Domain, Application e Api do RN-15 e as listas fechadas.

**P2 — RN-16 e RN-17 (Sonnet).**
Tabela de faixas e `NextBracketFor` em `IncomeTaxTable` (D-2), `FreeFromDay` em `IofTable` (D-3), as
datas e o corte em `Position.Project` (D-4), os três parâmetros novos de `PositionProjection` e de
`PositionProjectionResponse`. Testes das fronteiras, da invariante e de API.

**P3 — RN-18 (Sonnet).**
`ByAsset` e a assinatura nova de `PortfolioSummary.Create` (D-5), os dois casos de uso passando o
`AssetId`, `AssetSummaryResponse` e `ByAsset` em `PortfolioSummaryResponse`. Testes de Domain,
Application e Api, inclusive a igualdade com a projeção por título.

**P4 — Documentação (Haiku).**
`README.md`: os campos novos da projeção, do consolidado e da projeção por título, com o que cada
nulo significa e a observação de que as datas são aporte + 181, 361 ou 721 dias corridos e aporte +
30 (nunca "6 meses", "1 ano" ou "2 anos"), sem ajuste para dia útil, e podem cair em fim de semana
ou feriado.
`FixedIncome.Api.http`: nada novo de rota; um comentário na projeção e no consolidado apontando os
campos novos. Conferir que a `ESPECIFICACAO.md` bate com o implementado.

## Testes

Padrão do `CLAUDE.md` e da skill `testes-xunit`. Os valores esperados dos casos marcados com
**(ref)** são os da seção "Casos de referência". Os testes de API usam só PreFixed (e CDI, se
precisarem de cotação), como na F12.

Domain — RN-15 (P1):

1. **(ref)** `NetYield` igual a `NetAmount − InvestedAmount` em casos com IOF e IR: um CDB PreFixed
   com IOF e os casos 1 e 2 da F12 (CDI projetado).
2. **(ref)** `NetYield` negativo num IPCA+ com taxa real baixa (2% a.a., ou outra que o oráculo fixe)
   num intervalo dentro de um mês de deflação, com `d` < 30: `IofRate` e `TaxRate` pela tabela, IOF e
   IR zero, `NetYield` < 0, e `NextTaxRateDate` e `IofFreeDate` presentes mesmo sem rendimento. (O
   IPCA+ 6% da F12 não fica negativo com a deflação de 08/2026.) E `NetYield` zero no dia do aporte.
3. `PortfolioSummary.TotalNetYield` é igual à soma dos `NetYield` dos itens e a `TotalNetAmount −
   TotalInvestedAmount`, com itens de IOF; zero com a lista vazia.

Domain — RN-16 e RN-17 (P2):

4. **(ref)** Fronteiras da RN-16 num CDB PreFixed com vencimento depois de aporte + 721 (sem
   depender de cotação nem do corte): `d` = 0, 180, 181, 360, 361, 720 e 721, com `TaxRate`,
   `NextTaxRate` e `NextTaxRateDate` de cada um. Um dos casos atravessa 29/02/2028, para pegar
   `AddMonths` ou `AddYears` no lugar de `AddDays`.
5. **(ref)** LCI e LCA: `NextTaxRate` e `NextTaxRateDate` nulos em qualquer prazo.
6. **(ref)** Corte no vencimento, com os dois lados de cada fronteira e a referência antes e depois
   do vencimento (RN-02): IR com vencimento = aporte + 181, data devolvida, e vencimento = aporte + 180,
   nulos; IOF com vencimento = aporte + 30, devolvida, e vencimento = aporte + 29, nula.
7. Tesouro Direto recebe `NextTaxRate` e `IofFreeDate` como o CDB.
8. Invariante da D-4, em `Theory` sobre os prazos do teste 4 e do teste 10.
9. `IncomeTaxTable.NextBracketFor` direto: as fronteiras, LCI, LCA, `d` ≥ 721 e `d` negativo
   (`DomainException`); a escala, com `Rate.ToString(CultureInfo.InvariantCulture)` igual a `"20.0"`,
   `"17.5"` e `"15.0"`.
10. **(ref)** Fronteiras da RN-17 num CDB PreFixed com vencimento longo: `d` = 0, 29 e 30; LCA nulo;
    LCI com data. Um `Theory` por `AssetType` prova que `IofFreeDate` é nulo com `d` < 30 se e só se a
    alíquota de IOF do título é sempre zero.
11. As datas não dependem do mercado: no caso 1 da F12 (CDI projetado) e no caso 6 (IPCA estimado e
    projetado), `NextTaxRateDate` e `IofFreeDate` são as do aporte, e `IsProjected` e `IsEstimated`
    continuam os da F12.

Domain — RN-18 (P3):

12. `ByAsset` com dois títulos intercalados (A, B, A): ordem A, B; `PositionCount` 2 e 1; cada total
    igual à soma dos itens do título; a soma de cada total em `ByAsset` igual ao total da raiz.
13. `ByAsset` vazio com a lista vazia; com um título só, igual aos totais da raiz.

Application, com stubs `private sealed`:

14. (P1) Os três casos de uso levam `NetYield` e `TotalNetYield` ao DTO, e `TotalNetYield` é igual a
    `TotalNetAmount − TotalInvestedAmount` no resumo e na projeção por título.
15. (P2) `NextTaxRate`, `NextTaxRateDate` e `IofFreeDate` no DTO da projeção e nos itens do resumo;
    sem data informada, o aporte posterior à data padrão (projetado na própria data, `d` = 0) recebe
    aporte + 181 e aporte + 30.
16. (P3) **(ref)** Resumo com dois aportes do mesmo título e um de outro: `ByAsset` com os totais do
    título iguais à soma dos itens de `Positions` dele e iguais aos totais de
    `GetAssetProjectionUseCase` na mesma data, com data informada e sem data.
17. (P3) Com data informada, um título cujos aportes ficam todos depois dela não aparece em `ByAsset`;
    carteira vazia, `ByAsset` vazio.

Api, contra o PostgreSQL do Testcontainers:

18. (P1) `netYield` na projeção, `totalNetYield` no resumo e na projeção por título, conferidos no
    literal do JSON.
19. (P2) `nextTaxRate` (literal `20.0`), `nextTaxRateDate` e `iofFreeDate` em `yyyy-MM-dd`; numa LCA,
    os três presentes e `null`.
20. (P3) Resumo com dois títulos: `byAsset` com o conjunto fechado de nomes de cada item, e os totais de
    cada título iguais aos de `GET /api/assets/{id}/projection` na mesma data.
21. (P1, P2, P3) `swagger.json`: as listas fechadas com os campos novos no fim; `byAsset` como
    `array<AssetSummaryResponse>` e a lista fechada de `AssetSummaryResponse` com tipo e formato; e o
    conjunto fechado de propriedades com `nullable: true` de cada schema:
    `PositionProjectionResponse` = {nextTaxRate, nextTaxRateDate, iofFreeDate};
    `AssetSummaryResponse` = {}; `PortfolioSummaryResponse` = {byAssetType, positions, byAsset};
    `AssetProjectionResponse` = {positions}.

Mutation testing: o Domain muda, então o Stryker roda no fim da fase (`dotnet stryker` em
`tests/FixedIncome.Domain.Tests`), com score de 80% ou mais.

## Casos de referência

Produzidos pelo `especialista-renda-fixa` (modo oráculo) em 27/11/2026, à mão em decimal, sem sair do
código. As datas da RN-16 e da RN-17 não dependem de cotação: são `ApplicationDate.AddDays(n)`. Os
valores em dinheiro só aparecem onde o teste precisa deles. O coordenador recalculou de forma
independente (PowerShell) os dias úteis, os três fatores marcados com (†), os valores arredondados
dos casos 1a, 2a e 16 e todas as datas com o dia da semana, com o mesmo resultado. O arredondamento
final mais próximo de um ponto médio fica a 0,0039 dele (bruto do caso 1a).

### Convenções

| Id | Convenção | Fonte |
|---|---|---|
| C1 | Dias úteis pela RN-08 (inclui a inicial, exclui a final), calendário `AnbimaHolidays` 2020–2030. Feriados nos casos: 18/04/2025 (Paixão), 21/04/2025 (Tiradentes), 01/05/2025, 19/06/2025 (Corpus Christi), 28 e 29/02/2028 (Carnaval). Agosto de 2026 não tem feriado e tem 21 dias úteis | RN-08; ANBIMA |
| C2 | PreFixed: `(1 + Rate/100)^(DU/252)`, com `DecimalMath.Pow`. IPCA+: `Π (1 + IPCA_M/100)^(DU_M no intervalo / DU do mês civil) × (1 + Rate/100)^(DU/252)` | RN-01, RN-10 |
| C3 | Ordem: bruto arredondado, IOF sobre o rendimento, IR sobre (rendimento − IOF), cada um com 2 casas `AwayFromZero`; líquido = bruto − IOF − IR. Rendimento ≤ 0 não gera IOF nem IR, mas as alíquotas saem pela tabela | RN-03, RN-07, RN-11 |
| C4 | IOF por dia corrido: 0 no dia 0; dia 1 = 96%, 20 = 33%, 21 = 30%, 28 = 6%, 29 = 3%; do dia 30 em diante, 0%. LCA tem IOF zero; LCI paga | RN-11; Decreto 6.306/2007, anexo e art. 32, § 2º, V |
| C5 | IR: 22,5 até `d` = 180; 20,0 de 181 a 360; 17,5 de 361 a 720; 15,0 a partir de 721. LCI e LCA: 0, com `NextTaxRate` e `NextTaxRateDate` nulos | RN-03, RN-04; Lei 11.033/2004, art. 1º |
| C6 | `d` = dias corridos até a data de cálculo (referência limitada ao vencimento, RN-02). `NextTaxRateDate` = aporte + 181, 361 ou 721; `IofFreeDate` = aporte + 30 se `d` < 30. Data posterior ao vencimento: nula; igual: devolvida. Sem ajuste para dia útil | RN-16, RN-17; CC, art. 132 |
| C7 | `NetYield = NetAmount − InvestedAmount`; `TotalNetYield = TotalNetAmount − TotalInvestedAmount` | RN-15 |

Nas tabelas, "—" é nulo.

### 1. `NetYield` com IOF e IR (testes 1 e 11)

**1a. CDB PreFixed 10% a.a. (caso 29 da F12), um item.** R$ 1.000, aporte 01/04/2025 (terça),
referência 22/04/2025 (terça), vencimento 01/01/2027. 13 DU (18/04 e 21/04 feriados), `d` = 21. Fator
1,10^(13/252) = 1,004928902261.

| Campo | Esperado |
|---|---|
| GrossAmount / GrossYield | 1.004,93 / 4,93 |
| IofRate / IofAmount | 30 / 1,48 |
| TaxRate / TaxAmount | 22,5 / 0,78 (3,45 × 22,5% = 0,77625) |
| NetAmount | 1.002,67 |
| **NetYield** | **2,67** |
| NextTaxRate / NextTaxRateDate | **20,0 / 2025-09-29** (segunda) |
| IofFreeDate | **2025-05-01** (quinta, feriado; não é ajustada) |

**1b. Casos 1 e 2 da F12** (CDB 100% e 110% do CDI, R$ 1.000, aporte 16/11/2026, referência
26/11/2026, `d` = 10, vencimento 01/01/2028, `today` 2026-11-19, `IsProjected` true).

| Caso | NetAmount | **NetYield** | TaxRate | NextTaxRate | NextTaxRateDate | IofFreeDate |
|---|---|---|---|---|---|---|
| 1 (100%) | 1.000,94 | **0,94** | 22,5 | 20,0 | **2027-05-16** (domingo) | **2026-12-16** (quarta) |
| 2 (110%) | 1.001,03 | **1,03** | 22,5 | 20,0 | 2027-05-16 | 2026-12-16 |

**Teste 11.** No caso 1 da F12, as datas acima. No caso 6 da F12 (IPCA+ 6%, aporte 01/10/2026,
referência 15/12/2026, `d` = 75, estimado e projetado): `NextTaxRate` 20,0, `NextTaxRateDate`
**2027-03-31** (quarta), `IofFreeDate` **nulo** (`d` ≥ 30); `IsEstimated` e `IsProjected` true, como
na F12.

### 2. `NetYield` negativo e zero, IPCA+ (teste 2)

CDB IPCA+ **2% a.a.**, R$ 1.000, emissão 01/01/2026, vencimento 01/01/2028 (sábado, feriado), aporte
**03/08/2026** (segunda). Stub de IPCA: **08/2026 = −0,32** (o valor do stub da F12; o caso não
depende de ser o real). `today` = `TestMarket.DefaultToday` (2031-01-01): nada estimado nem
projetado.

2a, referência 31/08/2026 (segunda): `d` = 28; DU de [03/08, 31/08) = 20; DU de agosto = 21.
(†) 0,9968^(20/21) × (†) 1,02^(20/252) = **0,9985202274**.

| Campo | 2a: referência 31/08/2026 (`d` = 28) | 2b: referência 03/08/2026 (`d` = 0) |
|---|---|---|
| BusinessDays | 20 | 0 |
| GrossAmount / GrossYield | **998,52 / −1,48** | 1.000,00 / 0,00 |
| IofRate / IofAmount | **6 / 0,00** (sem rendimento) | 0 / 0,00 |
| TaxRate / TaxAmount | **22,5 / 0,00** | 22,5 / 0,00 |
| NetAmount | 998,52 | 1.000,00 |
| **NetYield** | **−1,48** | **0** (comparar por valor; a escala sai 0,00) |
| NextTaxRate / NextTaxRateDate | 20,0 / **2027-01-31** (domingo) | 20,0 / 2027-01-31 |
| IofFreeDate | **2026-09-02** (quarta) | **2026-09-02** |
| IsEstimated / IsProjected | false / false | false / false |

Por que 2%: com −0,32% num mês de 21 DU, o rendimento só fica negativo com taxa real abaixo de
1,00321^12 − 1 ≈ 3,9% a.a. Com 6%, o bruto daria cerca de 1.001,6.

### 4. Fronteiras da RN-16 (testes 4 e 8)

CDB PreFixed 10% a.a., R$ 1.000, emissão e aporte **01/09/2027** (quarta), vencimento **01/07/2030**
(segunda), depois de aporte + 721. O bruto não é necessário.

| `d` | Referência | TaxRate | NextTaxRate | NextTaxRateDate | IofFreeDate |
|---|---|---|---|---|---|
| 0 | 2027-09-01 (quarta) | 22,5 | 20,0 | **2028-02-29** (terça, Carnaval) | 2027-10-01 (sexta) |
| 180 | 2028-02-28 (segunda, Carnaval) | 22,5 | 20,0 | **2028-02-29** | — |
| 181 | 2028-02-29 (terça, Carnaval) | **20,0** | 17,5 | **2028-08-27** (domingo) | — |
| 360 | 2028-08-26 (sábado) | 20,0 | 17,5 | 2028-08-27 | — |
| 361 | 2028-08-27 (domingo) | **17,5** | 15,0 | **2029-08-22** (quarta) | — |
| 720 | 2029-08-21 (terça) | 17,5 | 15,0 | 2029-08-22 | — |
| 721 | 2029-08-22 (quarta) | **15,0** | — | — | — |

Armadilhas: `AddMonths(6)` daria 2028-03-01, `AddYears(1)` 2028-09-01 e `AddYears(2)` 2029-09-01.

### 5. LCI e LCA (teste 5)

LCI e LCA PreFixed 10% a.a., R$ 1.000, emissão 01/01/2025, aporte 01/04/2025 (terça), vencimento
01/04/2027 (quinta).

| `d` | Referência | TaxRate | NextTaxRate / Date | LCI: IofRate / IofFreeDate | LCA: IofRate / IofFreeDate |
|---|---|---|---|---|---|
| 0 | 2025-04-01 | 0 | — / — | 0 / **2025-05-01** (feriado) | 0 / — |
| 21 | 2025-04-22 | 0 | — / — | 30 / **2025-05-01** | 0 / — |
| 180 | 2025-09-28 (domingo) | 0 | — / — | 0 / — | 0 / — |
| 181 | 2025-09-29 | 0 | — / — | 0 / — | 0 / — |
| 721 | 2027-03-23 (terça) | 0 | — / — | 0 / — | 0 / — |

Em `d` = 21 (13 DU, fator do caso 1a): LCI bruto 1.004,93, IOF 1,48, IR 0,00, líquido 1.003,45,
**NetYield 3,45**; LCA bruto 1.004,93, IOF 0,00, IR 0,00, líquido 1.004,93, **NetYield 4,93**.

### 6. Corte no vencimento (teste 6)

CDB PreFixed 10% a.a., R$ 1.000, emissão igual ao aporte. Referência depois do vencimento calcula no
vencimento (RN-02).

**IR: aporte 02/04/2025 (quarta).** + 180 = 29/09/2025 (segunda); + 181 = 30/09/2025 (terça); + 361 =
29/03/2026 (domingo); + 30 = 02/05/2025 (sexta).

| # | Vencimento | Referência | `d` | TaxRate | NextTaxRate | NextTaxRateDate | IofRate | IofFreeDate |
|---|---|---|---|---|---|---|---|---|
| 6a-1 | 2025-09-30 (+181) | 2025-04-02 | 0 | 22,5 | 20,0 | **2025-09-30** (= vencimento) | 0 | 2025-05-02 |
| 6a-2 | 2025-09-30 | 2025-09-29 | 180 | 22,5 | 20,0 | **2025-09-30** | 0 | — |
| 6a-3 | 2025-09-30 | 2025-09-30 (= vencimento) | 181 | **20,0** | — | — (+361 > vencimento) | 0 | — |
| 6a-4 | 2025-09-30 | 2025-10-15 (depois) | **181** | 20,0 | — | — | 0 | — |
| 6b-1 | 2025-09-29 (+180) | 2025-04-02 | 0 | 22,5 | **—** | **—** (+181 > vencimento) | 0 | **2025-05-02** |
| 6b-2 | 2025-09-29 | 2025-09-26 (sexta) | 177 | 22,5 | — | — | 0 | — |
| 6b-3 | 2025-09-29 | 2025-10-15 (depois) | **180** | 22,5 | — | — | 0 | — |

**IOF: aporte 02/06/2025 (segunda).** + 29 = 01/07/2025 (terça); + 30 = 02/07/2025 (quarta); + 181 =
30/11/2025 (domingo), depois dos dois vencimentos.

| # | Vencimento | Referência | `d` | TaxRate | NextTaxRate / Date | IofRate | IofFreeDate |
|---|---|---|---|---|---|---|---|
| 6c-1 | 2025-07-02 (+30) | 2025-07-01 | 29 | 22,5 | — / — | **3** | **2025-07-02** (= vencimento) |
| 6c-2 | 2025-07-02 | 2025-07-02 (= vencimento) | 30 | 22,5 | — / — | 0 | — |
| 6c-3 | 2025-07-02 | 2025-07-15 (depois) | **30** | 22,5 | — / — | 0 | — |
| 6d-1 | 2025-07-01 (+29) | 2025-06-23 | 21 | 22,5 | — / — | 30 | **—** (+30 > vencimento) |
| 6d-2 | 2025-07-01 | 2025-07-15 (depois) | **29** | 22,5 | — / — | **3** | — |

O 6d-2 é um título que vence em 29 dias: paga IOF de 3% no vencimento e nunca chega à isenção.

### 10. Fronteiras da RN-17 (teste 10)

Mesmo título do caso 4 (aporte 01/09/2027, vencimento 01/07/2030), e LCI e LCA com as mesmas datas.

| `d` | Referência | CDB: IofRate / IofFreeDate | LCI: IofRate / IofFreeDate | LCA: IofRate / IofFreeDate |
|---|---|---|---|---|
| 0 | 2027-09-01 (quarta) | 0 / **2027-10-01** (sexta) | 0 / 2027-10-01 | 0 / — |
| 1 | 2027-09-02 (quinta) | **96** / 2027-10-01 | 96 / 2027-10-01 | 0 / — |
| 29 | 2027-09-30 (quinta) | **3** / 2027-10-01 | 3 / 2027-10-01 | 0 / — |
| 30 | 2027-10-01 (sexta) | **0 / —** | 0 / — | 0 / — |

### 16. Resumo com dois títulos (teste 16)

Referência **22/04/2025**:

- **A**: CDB PreFixed 10% a.a., emissão 01/01/2025, vencimento 01/01/2027. Dois aportes de R$ 1.000 em
  **01/04/2025** (13 DU, `d` = 21): o caso 29 da F12.
- **B**: LCI PreFixed 10% a.a., emissão 01/01/2025, vencimento 01/01/2027. Um aporte de **R$ 1.500 em
  02/04/2025** (quarta): 12 DU, `d` = 20. (†) Fator 1,10^(12/252) = **1,004548894944**.

B foi aportado um dia depois de A, então vem primeiro em `positions` (o mais recente primeiro) e a
ordem de `byAsset` é determinada: **B, A**. Com os três aportes no mesmo dia, a ordem ficaria
indefinida (RN-14).

| Item | Investido | Bruto | Rendimento | IOF | IR | Líquido | NetYield | TaxRate | NextTaxRate / Date | IofRate / IofFreeDate |
|---|---|---|---|---|---|---|---|---|---|---|
| B | 1.500,00 | 1.506,82 | 6,82 | 2,25 (33%) | 0,00 | 1.504,57 | 4,57 | 0 | — / — | 33 / 2025-05-02 (sexta) |
| A (cada) | 1.000,00 | 1.004,93 | 4,93 | 1,48 | 0,78 | 1.002,67 | 2,67 | 22,5 | 20,0 / 2025-09-29 | 30 / 2025-05-01 (feriado) |

| | PositionCount | TotalInvested | TotalGross | TotalGrossYield | TotalTax | TotalIof | TotalNet | **TotalNetYield** |
|---|---|---|---|---|---|---|---|---|
| `byAsset[0]` = B | 1 | 1.500,00 | 1.506,82 | 6,82 | 0,00 | 2,25 | 1.504,57 | **4,57** |
| `byAsset[1]` = A | 2 | 2.000,00 | 2.009,86 | 9,86 | 1,56 | 2,96 | 2.005,34 | **5,34** |
| Raiz | 3 | 3.500,00 | 3.516,68 | 16,68 | 1,56 | 5,21 | 3.509,91 | **9,91** |

`GetAssetProjectionUseCase` de A em 22/04/2025 devolve os totais da linha A; o de B, os da linha B.
Recalculado sobre R$ 2.000, A daria IR 1,55 e líquido 2.005,35: o teste recusa (RN-07). Sem data
informada, um stub com a última cotação de CDI em **17/04/2025** resolve a data padrão em 22/04/2025
(18/04 feriado, 19 e 20 fim de semana, 21/04 feriado), com os mesmos valores.

### Fontes

- Lei 11.033/2004, art. 1º, I a IV, e § 1º, II, "b" (prazo contado da aplicação)
- Código Civil, art. 132 (exclui o dia do começo, inclui o do vencimento)
- Decreto 6.306/2007, art. 32, § 2º, V (LCA com alíquota zero), e anexo (dia 29 = 3%, dia 30 = 0%)
- ANBIMA, feriados nacionais (`tests/FixedIncome.Domain.Tests/TestData/AnbimaHolidays.cs`)
- SPEC-F12, casos 1, 2, 6 e 29

## Critérios

Os do ROADMAP, seção F13, marcados só com evidência em `docs/VALIDACAO-F13.md`:

- [ ] `netYield` e `totalNetYield` fecham com `líquido − investido` em todos os casos de referência,
  inclusive com IOF
- [ ] As fronteiras da RN-16 batem com os casos de referência do `especialista-renda-fixa`: dias 180,
  181, 360, 361, 720 e 721, título isento e faixa depois do vencimento
- [ ] As fronteiras da RN-17 batem: dias 29 e 30, LCA, LCI e data depois do vencimento
- [ ] Em `byAsset`, os totais de cada título são iguais à soma dos itens de `positions` dele e iguais
  aos da projeção por título na mesma data
- [ ] Só aditivo: o teste do `swagger.json` mostra os campos novos e nenhum campo existente alterado
- [ ] `dotnet build` sem warning e `dotnet test` verde, com 863 testes ou mais
- [ ] Mutation score do Domain continua em 80% ou mais
