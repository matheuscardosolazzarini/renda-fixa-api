# Validação da F4b — migration e smoke test contra Postgres real

Documento de execução para agente. Objetivo: resolver o conflito de porta, aplicar a
migration e validar o fluxo completo contra o banco real.

Não é uma fase nova. Nenhum código de produção deve ser alterado, salvo o previsto em V4.

## Contexto

A F4b foi concluída com build limpo e 101 testes passando, mas a migration nunca foi
aplicada: um PostgreSQL nativo do Windows ocupa a porta 5432 no host, à frente do
mapeamento do container.

Os testes existentes usam provider InMemory. Eles provam que a lógica funciona, não que o
mapeamento contra Postgres está correto — tipos de coluna, conversão de enum e chave
estrangeira só se verificam contra o banco real.

## V1 — Diagnóstico

Confirme o conflito antes de agir:

```powershell
Get-Service postgresql*
Get-NetTCPConnection -LocalPort 5432 -State Listen | Select-Object OwningProcess
Get-Process -Id <OwningProcess>
docker compose ps
```

Reporte: nome exato do serviço nativo, se está em execução, qual processo detém a porta, e
o estado do container.

Se o processo que detém a porta for o do Docker, não há conflito — pule para V3 e reporte
que o diagnóstico não se confirmou.

## V2 — Resolução do conflito

Parar um serviço do Windows exige elevação. Você provavelmente não a tem.

Monte os comandos com o nome real do serviço encontrado em V1 e apresente ao usuário para
execução em PowerShell como administrador:

```powershell
Stop-Service <nome-do-servico>
Set-Service <nome-do-servico> -StartupType Manual
```

O tipo de inicialização passa a Manual em vez de Disabled: desabilita o início automático
sem impedir que o usuário use o Postgres nativo em outro projeto.

Não tente contornar remapeando a porta no `docker-compose.yml`. Isso alteraria connection
string e README por causa de uma particularidade de máquina, e quem clonar o repositório
teria configuração divergente da documentada.

Aguarde confirmação do usuário. Depois valide:

```powershell
docker compose ps
Get-NetTCPConnection -LocalPort 5432 -State Listen | Select-Object OwningProcess
```

A porta deve estar com o processo do Docker. Se o container tiver parado, suba novamente
com `docker compose up -d`.

## V3 — Aplicação da migration

```powershell
dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api
```

Se falhar por autenticação, confirme que a connection string em
`appsettings.Development.json` corresponde às credenciais do `docker-compose.yml`.

Após aplicar, inspecione o resultado no banco:

```powershell
docker exec fixedincome-db psql -U fixedincome -d fixedincome -c "\dt"
docker exec fixedincome-db psql -U fixedincome -d fixedincome -c "\d fixed_income_asset"
docker exec fixedincome-db psql -U fixedincome -d fixedincome -c "\d position"
```

Reporte a saída dos três comandos. Confirme explicitamente:

- Tabelas `fixed_income_asset` e `position` criadas, além de `__EFMigrationsHistory`
- Colunas em snake_case
- `asset_type` e `index_type` como `text`
- `rate` como `numeric(18,6)` e `invested_amount` como `numeric(18,2)`
- Chave estrangeira de `position` para `fixed_income_asset` com `ON DELETE RESTRICT`

## V4 — Smoke test do fluxo completo

Suba a aplicação e exercite o fluxo real, na ordem. Use o `FixedIncome.Api.http` ou
chamadas HTTP diretas.

1. **Criar título** — CDB prefixado, taxa 12, emissão há dois anos, vencimento daqui a um
   ano. Espera 201. Guarde o id.
2. **Listar títulos** — espera 200 com o título criado.
3. **Criar aporte** — 1000, com data de aplicação há exatamente 400 dias. Espera 201.
4. **Obter projeção** — sem `referenceDate`. Espera 200.
5. **Conferir a projeção**: `ElapsedDays` igual a 400; `TaxRate` igual a 17.5, que é a
   faixa de 361 a 720 dias; `TaxAmount` correspondente a 17,5% do rendimento, não do
   principal; `NetAmount` igual a `GrossAmount` menos `TaxAmount`.
6. **Tentar remover o título** — espera 400, porque possui aporte.
7. **Criar título LCI**, um aporte nele, e obter a projeção — espera `TaxRate` igual a 0.
8. **Buscar título inexistente** — espera 404.
9. **Criar título com taxa negativa** — espera 400 com mensagem de domínio.

Confira também no banco que os dados persistiram:

```powershell
docker exec fixedincome-db psql -U fixedincome -d fixedincome -c "SELECT id, name, asset_type, index_type, rate FROM fixed_income_asset;"
docker exec fixedincome-db psql -U fixedincome -d fixedincome -c "SELECT id, asset_id, invested_amount, application_date FROM position;"
```

Confirme que `asset_type` gravou como texto legível, não como número.

Se algum passo divergir do esperado, pare e reporte antes de corrigir. Divergência aqui
indica defeito de mapeamento que os testes InMemory não capturam — o diagnóstico importa
mais que a correção rápida.

## Restrições

- Não altere código de produção, salvo correção de defeito encontrado em V4, e somente
  após reportar
- Não altere `docker-compose.yml`
- Não altere `CLAUDE.md`, `docs/ESPECIFICACAO.md` nem specs anteriores
- Não execute `git add` nem `git commit`
- Não adicione pacotes NuGet

## Relatório final

Reporte:

- Resultado do diagnóstico e como o conflito foi resolvido
- Saída dos comandos de inspeção do esquema
- Resultado de cada um dos nove passos do smoke test, com os valores da projeção
- Qualquer divergência entre o comportamento contra Postgres e contra InMemory
- Decisões tomadas que não estavam especificadas aqui
