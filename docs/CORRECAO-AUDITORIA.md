# Correção dos achados da auditoria

Documento de execução para agente. Objetivo: corrigir o defeito de contrato de resposta e
fechar a lacuna de cobertura do middleware.

## Contexto

A auditoria em `docs/AUDITORIA-GERAL.md` classificou um defeito e uma lacuna que valem
correção antes de avançar. Os demais achados são de documentação, tratados fora deste
documento, ou observações sem ação.

Esta correção toca:

- `src/FixedIncome.Api` — configuração de validação e, se necessário, ajuste no middleware
- `tests/FixedIncome.Api.Tests` — testes dos casos corrigidos

Não altere `Domain`, `Application` nem `Infrastructure`.

## C1 — Contrato de resposta em falha de model binding

### O problema

Quando o `[ApiController]` rejeita a requisição antes de chegar ao controller — JSON
malformado, corpo vazio, campo obrigatório ausente, GUID inválido na rota, data em formato
inválido — o ASP.NET Core responde com `ProblemDetails`, não com `ApiResponse<T>`.

O Swagger, por sua vez, declara `ApiResponse<T>` para os 400 dessas rotas. A documentação
afirma um contrato que o código não cumpre no caminho de erro mais comum de qualquer
integração.

### A correção

Em `Program.cs`, configure `ConfigureApiBehaviorOptions` com um
`InvalidModelStateResponseFactory` que devolva `ApiResponse<T>` no mesmo formato usado
pelos casos de uso, com status 400.

A mensagem deve consolidar os erros de validação de forma legível para quem consome a API.
Erro em mais de um campo produz uma mensagem única que os menciona.

Nunca inclua no corpo: nome de tipo do .NET, `StackTrace`, mensagem bruta de exceção do
deserializador, ou `traceId`. Uma requisição malformada deve informar o que está errado na
requisição, não expor como o servidor é construído.

Registre em comentário por que essa configuração existe: sem ela, o contrato de erro do
projeto se aplica apenas aos erros que passam pelo model binding.

### Casos que devem passar a responder no envelope

Cada um destes precisa retornar 400 com `ApiResponse<T>`, e o `StatusCode` do corpo deve
corresponder ao status HTTP:

1. POST de título com JSON malformado
2. POST de título com corpo vazio
3. POST de título sem o campo `Name`
4. POST de título com `Name` nulo explícito
5. GET de título com id que não é GUID válido
6. POST de aporte com `AssetId` que não é GUID válido
7. Projeção com `referenceDate` em formato inválido

Os casos 5 e 7 falham no binding da rota e da query string, não do corpo. Verifique se a
mesma configuração os cobre; se algum continuar fora do envelope, reporte antes de tratar
de outra forma.

### Verificação no Swagger

Após a correção, confirme que a declaração de `ProducesResponseType` para 400 corresponde
ao que a API realmente devolve nesses casos. Reporte qualquer divergência remanescente.

## C2 — Cobertura do middleware de exceção

O `ExceptionHandlingMiddleware` não tem nenhum teste, e `ApiResponse<T>.InternalError` não
é exercitado em lugar nenhum do projeto. É o único caminho de código sem cobertura.

Adicione testes em `FixedIncome.Api.Tests` cobrindo:

1. `DomainException` que escape de um caso de uso resulta em 400 com a mensagem da exceção
2. Exceção não tratada resulta em 500 com mensagem genérica
3. A resposta de 500 não contém `StackTrace`, nome de tipo de exceção, nem qualquer
   detalhe interno
4. As duas respostas usam `ApiResponse<T>`

Para provocar as exceções, registre um endpoint de teste apenas no host de teste, ou
substitua um caso de uso por um dublê que lance. Não adicione endpoint de teste ao código
de produção.

Siga a skill `testes-xunit`: AAA, nomes em português descrevendo comportamento, sem
biblioteca de mock.

## Verificação

```powershell
dotnet build
dotnet test
```

Com a API em execução, reexecute manualmente os sete casos de C1 e reporte o corpo da
resposta de cada um.

## Restrições

- Não altere `Domain`, `Application` nem `Infrastructure`
- Não altere `CLAUDE.md`, `docs/ESPECIFICACAO.md` nem specs anteriores
- Não altere o README — será atualizado separadamente
- Não adicione pacotes NuGet
- Não execute `git add` nem `git commit`
- Não implemente nada da F5

## Critérios de aceite

1. `dotnet build` sem erro nem warning
2. Todos os testes passam, incluindo os 101 existentes
3. Os sete casos de C1 respondem 400 com `ApiResponse<T>`
4. Nenhuma resposta de erro expõe detalhe interno
5. `ApiResponse<T>.InternalError` passa a ter cobertura
6. Nenhum commit foi criado

## Relatório final

Reporte:

- Resultado de `dotnet build` e `dotnet test`, com a contagem de testes
- O trecho de `Program.cs` com a configuração adicionada
- O corpo da resposta de cada um dos sete casos de C1, antes e depois
- Se algum caso não foi coberto pela configuração e por quê
- Decisões tomadas que não estavam especificadas aqui
