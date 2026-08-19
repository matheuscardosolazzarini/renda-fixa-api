# Smoke test da F5 — consolidação com carteira limpa

Documento de execução para agente. Objetivo: validar a consolidação da carteira contra o
banco real, com dados controlados e valores conferíveis à mão.

Não altere código de produção. Se algum valor divergir do esperado, pare e reporte antes
de corrigir qualquer coisa — o diagnóstico importa mais que a correção rápida.

## Pré-condições

O banco foi recriado do zero e a migration reaplicada. Confirme que está vazio:

```powershell
docker compose ps
docker exec fixedincome-db psql -U fixedincome -d fixedincome -c "SELECT count(*) FROM fixed_income_asset;"
docker exec fixedincome-db psql -U fixedincome -d fixedincome -c "SELECT count(*) FROM position;"
```

As duas contagens devem ser zero. Se não forem, pare e reporte.

Suba a aplicação com `dotnet run --project src/FixedIncome.Api`.

## Dados de teste

Dois títulos idênticos em tudo, exceto o tipo. É isso que isola a isenção como única
variável entre eles.

| Campo | Título A | Título B |
|---|---|---|
| Name | CDB Teste | LCI Teste |
| Issuer | Banco Teste | Banco Teste |
| AssetType | Cdb | Lci |
| IndexType | PreFixed | PreFixed |
| Rate | 12 | 12 |
| IssueDate | dois anos atrás | dois anos atrás |
| MaturityDate | um ano à frente | um ano à frente |

Um aporte em cada, ambos de 1000, com data de aplicação exatamente 400 dias antes de hoje.

Calcule as datas a partir da data corrente e reporte quais usou.

## Execução

### S1 — Criar os dois títulos

POST em `/api/assets` para cada um. Espera 201. Guarde os ids.

Confirme que `assetType` e `indexType` vêm como texto legível na resposta, não como número.

### S2 — Registrar os aportes

POST em `/api/positions` para cada título, 1000 cada, mesma data de aplicação.
Espera 201.

### S3 — Projeções individuais

GET em `/api/positions/{id}/projection` para cada posição, sem `referenceDate`.

Reporte os sete campos de cada projeção.

Verificações:

1. `ElapsedDays` igual a 400 nas duas
2. `GrossAmount`, `GrossYield` e `InvestedAmount` idênticos entre as duas — mesma taxa,
   mesmo prazo, mesmo aporte
3. CDB com `TaxRate` igual a 17.5, faixa de 361 a 720 dias
4. LCI com `TaxRate` igual a 0
5. Em cada uma, `NetAmount` igual a `GrossAmount` menos `TaxAmount`
6. No CDB, `TaxAmount` igual a 17,5% do `GrossYield` — confira à mão e reporte a conta

### S4 — Consolidação

GET em `/api/portfolio/summary`, sem `referenceDate`. Reporte o corpo completo.

Verificações:

1. `PositionCount` igual a 2
2. `TotalInvestedAmount` igual a 2000
3. `TotalGrossAmount` igual à soma dos dois `GrossAmount` de S3
4. `TotalTaxAmount` igual ao `TaxAmount` do CDB, já que o da LCI é zero
5. `TotalNetAmount` igual à soma dos dois `NetAmount` de S3 — esta é a RN-07
6. `TotalGrossAmount` menos `TotalTaxAmount` igual a `TotalNetAmount`
7. `ByAssetType` com dois itens, `assetType` como texto
8. **A LCI aparece primeiro**, com participação maior que a do CDB

O item 8 é a verificação mais importante. Os dois títulos receberam o mesmo aporte, com a
mesma taxa e o mesmo prazo. A única diferença é que a LCI não paga imposto, então seu valor
líquido é maior. Se o CDB aparecer primeiro, ou se as participações forem iguais, a isenção
não está sendo aplicada na consolidação — reporte imediatamente.

Some as duas participações e confirme que dão 100.

### S5 — Consolidação com data de referência explícita

GET em `/api/portfolio/summary?referenceDate=` com uma data posterior ao vencimento dos
títulos.

Verificação: os valores devem ser maiores que os de S4, mas o `ElapsedDays` implícito é
truncado no vencimento pela RN-02. Confirme que a consolidação nessa data e em qualquer
data posterior produz exatamente o mesmo resultado — teste com duas datas distintas, ambas
após o vencimento.

### S6 — Carteira vazia

Remova as duas posições e os dois títulos, na ordem correta, e chame novamente o resumo.

Verificações:

1. DELETE do título antes de remover o aporte retorna 400
2. Após a limpeza, o resumo retorna 200, não 404
3. Todos os totais são zero, `PositionCount` é zero e `ByAssetType` é vazio

Se não houver endpoint para remover posição, reporte e faça a limpeza direto no banco com
`DELETE FROM position;`, registrando que foi necessário.

## Conferência no banco

```powershell
docker exec fixedincome-db psql -U fixedincome -d fixedincome -c "SELECT name, asset_type, index_type, rate FROM fixed_income_asset;"
```

Confirme que `asset_type` gravou como `Cdb` e `Lci` em texto.

## Relatório final

Reporte:

- As datas calculadas e usadas
- Os sete campos de cada projeção de S3, com a conta do imposto do CDB feita à mão
- O corpo completo do resumo de S4
- O resultado de cada verificação numerada, indicando aprovado ou divergente
- O resultado de S5 e S6
- Qualquer divergência entre o esperado e o obtido, sem corrigir

Não execute `git add` nem `git commit`.
