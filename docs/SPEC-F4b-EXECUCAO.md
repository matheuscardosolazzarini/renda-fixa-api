# Spec de execução — F4b: API

Documento de execução para agente. Objetivo: expor os casos de uso por HTTP — controllers,
injeção de dependência, configuração e documentação. Ao final desta fase a aplicação
executa.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão
em `CLAUDE.md`. Ambas prevalecem sobre qualquer suposição.

Esta fase toca:

- `src/FixedIncome.Api` — controllers, configuração, DI, middleware
- `src/FixedIncome.Infrastructure` — apenas o método de extensão de registro em DI
- `tests/FixedIncome.Api.Tests` — projeto novo, criado nesta fase

## Fora de escopo

Não altere `FixedIncome.Domain` nem os casos de uso de `FixedIncome.Application`.

Não implemente o endpoint de consolidação da carteira (`/api/portfolio/summary`) — é F5.

Não aplique migration na inicialização da aplicação. A execução é manual, conforme
decisão registrada em P6.

## Pacotes autorizados

Apenas estes, e somente se necessários:

- `Microsoft.EntityFrameworkCore.Design` em `FixedIncome.Api` — para o `dotnet ef` operar
  com `--startup-project`
- `Microsoft.AspNetCore.Mvc.Testing` em `FixedIncome.Api.Tests`
- `Microsoft.EntityFrameworkCore.InMemory` em `FixedIncome.Api.Tests`

Nenhum outro. Se algo parecer faltar, pare e reporte antes de instalar.

## Implementação

### P1 — Projeto de teste

```bash
dotnet new xunit -o tests/FixedIncome.Api.Tests -f net8.0
dotnet sln add tests/FixedIncome.Api.Tests
dotnet add tests/FixedIncome.Api.Tests reference src/FixedIncome.Api
```

Remova o `UnitTest1.cs` gerado.

### P2 — Registro de dependências

Método de extensão `AddInfrastructure(this IServiceCollection services, string connectionString)`
em `FixedIncome.Infrastructure`, registrando o `FixedIncomeDbContext` com Npgsql e os dois
repositórios com tempo de vida `Scoped`.

Método de extensão `AddApplication(this IServiceCollection services)` em
`FixedIncome.Application`, registrando os oito casos de uso como `Scoped`.

Cada camada registra o que é seu. A API compõe, não conhece o interior de nenhuma.

### P3 — Configuração

Em `appsettings.json`, seção `IndexRates` com `Cdi` e `Ipca`. Use valores plausíveis e
documente na seção correspondente do README que são taxas fixas de referência, não
cotações reais.

Registre `IndexRatesOptions` via `Configure<IndexRatesOptions>` e resolva com
`IOptions<IndexRatesOptions>` no ponto de composição — os casos de uso continuam
recebendo a classe pura, conforme decidido na F4a.

### P4 — Middleware de exceção

Middleware de tratamento global registrado antes dos controllers.

| Exceção | Resposta |
|---|---|
| `DomainException` | 400, com a mensagem da exceção |
| Qualquer outra | 500, com mensagem genérica |

Nunca exponha `StackTrace` nem detalhe interno na resposta. A resposta usa o mesmo
`ApiResponse<T>` dos casos de uso, para que o formato seja uniforme.

Os casos de uso já convertem `DomainException` em `BadRequest`. O middleware é a rede de
segurança para o que escapar — não substitui aquele tratamento.

### P5 — Controllers

Dois controllers em `FixedIncome.Api/Controllers`, herdando `ControllerBase`, com
`[ApiController]` e rota `[Route("api/[controller]")]`.

Cada action recebe o caso de uso por construtor, chama, e retorna
`StatusCode(response.StatusCode, response)` conforme `CLAUDE.md`. Nenhuma regra de negócio
no controller.

**AssetsController** — `api/assets`:

| Método | Rota | Caso de uso |
|---|---|---|
| POST | `/` | CreateAsset |
| GET | `/` | GetAllAssets |
| GET | `/{id}` | GetAssetById |
| PUT | `/{id}` | UpdateAsset |
| DELETE | `/{id}` | DeleteAsset |

**PositionsController** — `api/positions`:

| Método | Rota | Caso de uso |
|---|---|---|
| POST | `/` | CreatePosition |
| GET | `/` | GetAllPositions |
| GET | `/{id}/projection` | GetPositionProjection |

`referenceDate` da projeção vem por query string. Ausente, usa a data atual.

Documente cada action com `[ProducesResponseType]` para os códigos que ela pode retornar.

### P6 — Program.cs

Composição: configuração, `AddApplication`, `AddInfrastructure`, controllers, Swagger,
middleware de exceção, `MapControllers`.

Swagger habilitado em ambiente de desenvolvimento, com título e versão da API.

Não chame `Database.Migrate()`. Aplicar migration na inicialização parece conveniente,
mas em ambiente com múltiplas instâncias duas podem migrar simultaneamente, e uma falha
de migration impede a aplicação de subir. A execução é manual:

```bash
dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api
```

Registre essa decisão em comentário no `Program.cs`.

### P7 — Arquivo de requisições

`FixedIncome.Api.http` na raiz do projeto da API, com uma requisição de exemplo para cada
endpoint, na ordem de um fluxo real: criar título, listar, criar aporte, projetar.

Use variáveis para host e ids, de modo que o arquivo seja executável de ponta a ponta.

### P8 — Testes de integração

Em `FixedIncome.Api.Tests`, usando `WebApplicationFactory` com o `DbContext` substituído
por InMemory.

Cobertura mínima:

- POST de título válido retorna 201 e o corpo com o id gerado
- POST de título com taxa negativa retorna 400
- POST de título com `AssetType` inexistente retorna 400
- GET de id inexistente retorna 404
- DELETE de título com aportes retorna 400
- Fluxo completo: criar título, criar aporte, obter projeção com 200 e valores coerentes

Siga a skill `testes-xunit` no que se aplica: AAA, nomes em português descrevendo
comportamento, sem biblioteca de mock.

## Verificação

```bash
dotnet build
dotnet test
```

Com o container do Postgres em execução, aplique a migration e suba a API:

```bash
dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api
dotnet run --project src/FixedIncome.Api
```

Confirme que o Swagger responde e que os endpoints aparecem documentados. Reporte a URL.

## Restrições

- Não altere `FixedIncome.Domain` nem os casos de uso existentes
- Não altere `CLAUDE.md`, `docs/ESPECIFICACAO.md` nem specs de fases anteriores
- Não execute `git add` nem `git commit`
- Não instale pacotes além dos três autorizados
- Não implemente o endpoint de consolidação da carteira

## Critérios de aceite

1. `dotnet build` conclui sem erro nem warning
2. Todos os testes passam, incluindo os 95 existentes
3. A aplicação sobe e o Swagger lista os oito endpoints
4. Nenhum controller contém regra de negócio
5. `FixedIncome.Api` não referencia `FixedIncome.Domain` diretamente nos controllers
6. Nenhuma resposta de erro expõe `StackTrace` ou detalhe interno
7. Nenhum commit foi criado

## Relatório final

Reporte:

- Resultado de `dotnet build` e `dotnet test`, com a contagem de testes
- Arquivos criados
- Confirmação de que a aplicação subiu, com a URL do Swagger
- O `Program.cs` na íntegra
- Decisões tomadas que não estavam especificadas aqui
- Qualquer ponto que se mostrou ambíguo durante a implementação
