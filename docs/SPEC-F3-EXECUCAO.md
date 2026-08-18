# Spec de execução — F3: Persistência

Documento de execução para agente. Objetivo: mapear o domínio para o banco com Entity
Framework Core, criar a migration inicial e implementar os repositórios.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão
em `CLAUDE.md`. Ambas prevalecem sobre qualquer suposição.

Esta fase toca três projetos:

- `src/FixedIncome.Application` — apenas as interfaces de repositório
- `src/FixedIncome.Infrastructure` — DbContext, configurações, repositórios
- `tests/FixedIncome.Infrastructure.Tests` — testes de repositório

O projeto `FixedIncome.Domain` não deve ser modificado. Se algum ajuste nele parecer
necessário, pare e reporte antes de alterar.

## Fora de escopo

Não implemente: DTOs, casos de uso, controllers, injeção de dependência na API,
endpoints, tratamento de exceção HTTP.

Não adicione pacotes NuGet — os necessários já foram instalados na F1.

## Estrutura alvo

```
src/FixedIncome.Application/
└── Repositories/
    ├── IFixedIncomeAssetRepository.cs
    └── IPositionRepository.cs

src/FixedIncome.Infrastructure/
├── Context/
│   └── FixedIncomeDbContext.cs
├── Configurations/
│   ├── FixedIncomeAssetConfiguration.cs
│   └── PositionConfiguration.cs
├── Repositories/
│   ├── FixedIncomeAssetRepository.cs
│   └── PositionRepository.cs
└── Migrations/
    └── (gerado pelo dotnet ef)

tests/FixedIncome.Infrastructure.Tests/
├── RepositoryTestBase.cs
└── Repositories/
    ├── FixedIncomeAssetRepositoryTests.cs
    └── PositionRepositoryTests.cs
```

## Implementação

### P1 — Interfaces de repositório

Declaradas em `FixedIncome.Application/Repositories`. É a inversão de dependência: a
camada de aplicação declara o contrato, a de infraestrutura implementa.

`IFixedIncomeAssetRepository`:

| Método | Retorno |
|---|---|
| `GetAllAsync()` | `Task<IEnumerable<FixedIncomeAsset>>` |
| `GetByIdAsync(Guid id)` | `Task<FixedIncomeAsset?>` |
| `AddAsync(FixedIncomeAsset asset)` | `Task` |
| `UpdateAsync(FixedIncomeAsset asset)` | `Task` |
| `DeleteAsync(FixedIncomeAsset asset)` | `Task` |

`IPositionRepository`:

| Método | Retorno |
|---|---|
| `GetAllAsync()` | `Task<IEnumerable<Position>>` |
| `GetByIdAsync(Guid id)` | `Task<Position?>` |
| `GetByAssetIdAsync(Guid assetId)` | `Task<IEnumerable<Position>>` |
| `AddAsync(Position position)` | `Task` |

`GetByAssetIdAsync` existe para atender a regra de que um título com aportes não pode ser
removido, e para a consolidação da carteira na F5.

### P2 — DbContext

`FixedIncomeDbContext` herda `DbContext`, com construtor recebendo
`DbContextOptions<FixedIncomeDbContext>`.

Expõe `DbSet<FixedIncomeAsset>` e `DbSet<Position>`.

Em `OnModelCreating`, aplica as configurações via
`ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())`. Não configure
entidade diretamente no `OnModelCreating`.

### P3 — Configurações de entidade

Uma classe por entidade, implementando `IEntityTypeConfiguration<T>`, com `ToTable`,
`HasKey` e `HasColumnName` em SNAKE_CASE conforme `CLAUDE.md`.

**FixedIncomeAssetConfiguration** — tabela `fixed_income_asset`:

| Propriedade | Coluna | Observação |
|---|---|---|
| Id | `id` | chave primária |
| Name | `name` | obrigatório, máximo 120 |
| Issuer | `issuer` | obrigatório, máximo 120 |
| AssetType | `asset_type` | persistido como texto |
| IndexType | `index_type` | persistido como texto |
| Rate | `rate` | precisão 18, escala 6 |
| IssueDate | `issue_date` | |
| MaturityDate | `maturity_date` | |

**PositionConfiguration** — tabela `position`:

| Propriedade | Coluna | Observação |
|---|---|---|
| Id | `id` | chave primária |
| AssetId | `asset_id` | obrigatório |
| InvestedAmount | `invested_amount` | precisão 18, escala 2 |
| ApplicationDate | `application_date` | |

Configure o relacionamento a partir de `Position`, com `HasOne<FixedIncomeAsset>()` sem
propriedade de navegação, `WithMany()`, `HasForeignKey(p => p.AssetId)` e
`OnDelete(DeleteBehavior.Restrict)`.

O domínio não expõe propriedade de navegação por decisão da F2 — a `Position` guarda
apenas o `AssetId`. A configuração precisa refletir isso sem introduzir navegação.

Enums persistidos como texto, não como inteiro: valor legível no banco e imune a
reordenação futura do enum.

### P4 — Materialização

As entidades têm setters privados e construtor privado sem parâmetros, criados na F2.

Verifique se o EF Core consegue materializar as duas entidades. Se algum mapeamento
exigir configuração adicional para acessar os campos, use
`UsePropertyAccessMode(PropertyAccessMode.Field)` na propriedade afetada.

Não altere o domínio para acomodar o EF Core.

### P5 — Repositórios

Implementações em `FixedIncome.Infrastructure/Repositories`, recebendo o
`FixedIncomeDbContext` por construtor.

Métodos de leitura usam `AsNoTracking()`. Métodos de escrita chamam `SaveChangesAsync()`.

`GetAllAsync` de títulos ordena por `Name`. `GetAllAsync` de posições ordena por
`ApplicationDate` decrescente.

### P6 — Migration inicial

Com o container do Postgres em execução:

```bash
dotnet ef migrations add InitialCreate --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api
```

Se o comando exigir configuração de design time, crie um `IDesignTimeDbContextFactory`
em `Infrastructure`, lendo a connection string de `appsettings.Development.json` da API.

Após gerar, inspecione o arquivo de migration e confirme que os nomes de tabela e coluna
saíram em SNAKE_CASE. Reporte qualquer divergência.

Não aplique a migration ao banco nesta fase — apenas gere e valide o arquivo.

### P7 — Testes de repositório

`RepositoryTestBase`: classe base fornecendo um `FixedIncomeDbContext` com provider
InMemory e nome de banco único por instância, para isolamento entre testes.

Cobertura mínima:

**FixedIncomeAssetRepositoryTests** — persistir e recuperar por id; recuperar id
inexistente retornando nulo; listar todos ordenado por nome; atualizar; remover.

**PositionRepositoryTests** — persistir e recuperar por id; listar por título; listar por
título sem aportes retornando coleção vazia; listar todos ordenado por data de aporte
decrescente.

Siga a skill `testes-xunit`: padrão AAA, nomes em português descrevendo comportamento,
sem biblioteca de mock.

## Verificação

```bash
dotnet build
dotnet test
```

Build sem erro nem warning. Todos os testes passando, incluindo os 51 de domínio.

## Restrições

- Não modifique `src/FixedIncome.Domain`
- Não altere `CLAUDE.md`, `docs/ESPECIFICACAO.md` nem specs de fases anteriores
- Não execute `git add` nem `git commit`
- Não aplique a migration ao banco
- Não adicione pacotes NuGet

## Critérios de aceite

1. `dotnet build` conclui sem erro nem warning
2. Todos os testes passam
3. `FixedIncome.Domain` permanece inalterado
4. `FixedIncome.Application` não referencia `FixedIncome.Infrastructure`
5. A migration gerada usa SNAKE_CASE em todas as tabelas e colunas
6. Nenhuma propriedade de navegação foi introduzida no domínio
7. Nenhum commit foi criado

## Relatório final

Reporte:

- Resultado de `dotnet build` e `dotnet test`, com a contagem de testes
- Arquivos criados
- Trecho da migration mostrando a criação das duas tabelas
- Se foi necessário `IDesignTimeDbContextFactory` e por quê
- Decisões tomadas que não estavam especificadas aqui
- Qualquer ponto que se mostrou ambíguo durante a implementação
