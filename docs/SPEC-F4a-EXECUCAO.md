# Spec de execução — F4a: Casos de uso

Documento de execução para agente. Objetivo: implementar a camada de aplicação — mutação
no domínio, DTOs, envelope de resposta e casos de uso — sem tocar na API.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão
em `CLAUDE.md`. Ambas prevalecem sobre qualquer suposição.

Esta fase toca três projetos:

- `src/FixedIncome.Domain` — apenas o método de atualização descrito em P1
- `src/FixedIncome.Application` — DTOs, envelope de resposta, casos de uso
- `tests/FixedIncome.Domain.Tests` e `tests/FixedIncome.Application.Tests`

## Fora de escopo

Não implemente: controllers, `Program.cs`, injeção de dependência, middleware, Swagger,
qualquer arquivo em `FixedIncome.Api`.

Não altere `FixedIncome.Infrastructure`, exceto se a assinatura de uma interface de
repositório mudar — nesse caso, ajuste a implementação correspondente e reporte.

Não adicione pacotes NuGet.

## Regra de negócio nova

**RN-06 — Alteração de título com aportes.** Um título que já possui aportes registrados
não pode ter `IssueDate` nem `MaturityDate` alteradas. Permitir isso tornaria
retroativamente inválido um aporte que era válido no momento do registro, violando a
RN-05 sem que ninguém tenha feito nada errado.

`Name`, `Issuer` e `Rate` permanecem alteráveis em qualquer situação.

Acrescente esta regra à seção "Regras de negócio" de `docs/ESPECIFICACAO.md`, seguindo a
numeração e o formato das existentes. É a única alteração autorizada naquele documento.

## Implementação

### P1 — Mutação no domínio

Adicione a `FixedIncomeAsset`:

```
Update(string name, string issuer, decimal rate, DateOnly issueDate,
       DateOnly maturityDate, bool hasPositions)
```

Comportamento:

1. Se `hasPositions` for verdadeiro e `issueDate` ou `maturityDate` diferirem dos valores
   atuais, lança `DomainException` (RN-06)
2. Revalida todas as invariantes de construção — nome, emissor, taxa e ordem das datas
3. Atribui os novos valores

`AssetType` e `IndexType` não são alteráveis. Mudar a natureza do título equivale a criar
outro; a spec funcional não prevê essa operação.

O parâmetro `hasPositions` existe porque a entidade não pode consultar o banco. Quem sabe
se há aportes é o caso de uso, que pergunta ao repositório e repassa a resposta. A decisão
permanece no domínio; apenas o dado vem de fora. Registre isso em comentário no método.

Reaproveite a validação já existente no construtor em vez de duplicá-la — extraia para um
método privado se necessário.

### P2 — Envelope de resposta

`ApiResponse<T>` em `FixedIncome.Application/Common`, conforme `CLAUDE.md`.

Propriedades: `StatusCode` (int), `Success` (bool), `Message` (string?), `Data` (T?).

Fábricas estáticas:

| Fábrica | StatusCode |
|---|---|
| `Success(T data)` | 200 |
| `Created(T data)` | 201 |
| `NoContent()` | 204 |
| `BadRequest(string message)` | 400 |
| `NotFound(string message)` | 404 |
| `InternalError(string message)` | 500 |

### P3 — DTOs

Em `FixedIncome.Application/DTOs`. Todos como `record`.

**Entrada:**

- `CreateAssetRequest` — Name, Issuer, AssetType, IndexType, Rate, IssueDate, MaturityDate
- `UpdateAssetRequest` — Name, Issuer, Rate, IssueDate, MaturityDate
- `CreatePositionRequest` — AssetId, InvestedAmount, ApplicationDate

**Saída:**

- `AssetResponse` — Id e todos os campos do título
- `PositionResponse` — Id, AssetId, InvestedAmount, ApplicationDate
- `PositionProjectionResponse` — os sete campos de `PositionProjection`, mais Id da
  posição e Id do título

Os enums nos DTOs de entrada são `string`, convertidos e validados no caso de uso. Valor
inválido resulta em `BadRequest`, não em exceção não tratada.

Mapeamento manual entre entidade e DTO, em métodos estáticos junto ao próprio DTO. Não
adicione biblioteca de mapeamento.

### P4 — Configuração de indexadores

`IndexRatesOptions` em `FixedIncome.Application/Common`, com `Cdi` e `Ipca` decimais, e
um método convertendo para o `IndexRates` do domínio.

O registro em DI e a leitura de `appsettings` são da F4b. Aqui apenas a classe.

### P5 — Casos de uso

Um por operação, cada um com sua interface, em `FixedIncome.Application/UseCases`.
Recebem dependências por construtor e retornam `ApiResponse<T>`.

| Caso de uso | Retorno |
|---|---|
| `CreateAsset` | `ApiResponse<AssetResponse>` — 201 |
| `GetAllAssets` | `ApiResponse<IEnumerable<AssetResponse>>` |
| `GetAssetById` | `ApiResponse<AssetResponse>` — 404 se não existir |
| `UpdateAsset` | `ApiResponse<AssetResponse>` |
| `DeleteAsset` | `ApiResponse<object>` — 204 |
| `CreatePosition` | `ApiResponse<PositionResponse>` — 201 |
| `GetAllPositions` | `ApiResponse<IEnumerable<PositionResponse>>` |
| `GetPositionProjection` | `ApiResponse<PositionProjectionResponse>` |

Comportamentos específicos:

**UpdateAsset** — consulta `GetByAssetIdAsync` para saber se há aportes e repassa o
resultado a `Update`. Retorna 404 se o título não existir.

**DeleteAsset** — retorna 400 se o título possuir aportes, com mensagem explicando o
motivo. Retorna 404 se não existir.

**CreatePosition** — retorna 404 se o título informado não existir.

**GetPositionProjection** — recebe id da posição e data de referência. Carrega posição e
título, chama `Project` e monta a resposta. Retorna 404 se a posição não existir.

Toda `DomainException` é capturada no caso de uso e convertida em `BadRequest` com a
mensagem da exceção. Exceção de domínio é violação de regra de negócio, não erro de
servidor.

### P6 — Testes

**Domínio** — `Update` alterando os três campos livres com aportes existentes; `Update`
rejeitando alteração de cada uma das datas quando há aportes; `Update` permitindo
alteração das datas quando não há aportes; cada invariante revalidada e violada.

**Aplicação** — um arquivo por caso de uso. Dublês de repositório como classes
`private sealed` no próprio arquivo, conforme a skill `testes-xunit`. Cobrir o caminho de
sucesso e cada caminho de erro previsto acima, incluindo enum inválido resultando em 400.

## Verificação

```bash
dotnet build
dotnet test
```

Build sem erro nem warning. Todos os testes passando, incluindo os 60 existentes.

## Restrições

- Não crie nem altere arquivos em `src/FixedIncome.Api`
- Não altere `CLAUDE.md` nem specs de fases anteriores
- A única alteração permitida em `docs/ESPECIFICACAO.md` é acrescentar a RN-06
- Não execute `git add` nem `git commit`
- Não adicione pacotes NuGet

## Critérios de aceite

1. `dotnet build` conclui sem erro nem warning
2. Todos os testes passam
3. `FixedIncome.Api` permanece inalterado
4. `FixedIncome.Application` não referencia `FixedIncome.Infrastructure`
5. `FixedIncome.Domain` continua sem referência de projeto e sem pacote
6. Nenhuma `DomainException` escapa de um caso de uso
7. A RN-06 está na especificação funcional e coberta por teste
8. Nenhum commit foi criado

## Relatório final

Reporte:

- Resultado de `dotnet build` e `dotnet test`, com a contagem de testes
- Arquivos criados
- O método `Update` de `FixedIncomeAsset` na íntegra
- Decisões tomadas que não estavam especificadas aqui
- Qualquer ponto que se mostrou ambíguo durante a implementação
