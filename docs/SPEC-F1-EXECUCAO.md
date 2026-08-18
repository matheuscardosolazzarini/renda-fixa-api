# Spec de execução — F1: Fundação

Documento de execução para agente. Objetivo: criar a estrutura completa do projeto,
sem nenhuma implementação de domínio.

## Contexto

Projeto: API de controle de investimentos em renda fixa, .NET 8.
A especificação funcional completa está em `docs/ESPECIFICACAO.md`.

Esta fase entrega apenas o esqueleto: solution, camadas, referências, pacotes e
ambiente local. Nenhuma entidade, regra de negócio, endpoint ou teste deve ser
escrito aqui.

## Pré-condições

Antes de começar, verifique e reporte:

- `dotnet --version` retorna 8.x
- `docker --version` responde e o daemon está ativo
- O diretório de trabalho está vazio ou contém apenas `docs/`

Se alguma verificação falhar, pare e reporte. Não tente instalar nada.

## Entregáveis

```
fixed-income-api/
├── .gitignore
├── CLAUDE.md
├── docker-compose.yml
├── FixedIncome.sln
├── .config/dotnet-tools.json
├── docs/
│   ├── ESPECIFICACAO.md
│   └── SETUP-F1.md
├── src/
│   ├── FixedIncome.Domain/
│   ├── FixedIncome.Application/
│   ├── FixedIncome.Infrastructure/
│   └── FixedIncome.Api/
└── tests/
    ├── FixedIncome.Domain.Tests/
    ├── FixedIncome.Application.Tests/
    └── FixedIncome.Infrastructure.Tests/
```

## Passos

### P1 — Inicialização

```bash
git init
dotnet new gitignore
dotnet new sln -n FixedIncome
```

### P2 — Criação dos projetos

```bash
dotnet new classlib -o src/FixedIncome.Domain -f net8.0
dotnet new classlib -o src/FixedIncome.Application -f net8.0
dotnet new classlib -o src/FixedIncome.Infrastructure -f net8.0
dotnet new webapi -o src/FixedIncome.Api -f net8.0 --use-controllers

dotnet new xunit -o tests/FixedIncome.Domain.Tests -f net8.0
dotnet new xunit -o tests/FixedIncome.Application.Tests -f net8.0
dotnet new xunit -o tests/FixedIncome.Infrastructure.Tests -f net8.0
```

A flag `--use-controllers` é obrigatória na API. Sem ela o template gera Minimal API,
o que contraria o padrão de controller definido em `CLAUDE.md`.

### P3 — Registro na solution

Adicione os sete projetos à solution.

### P4 — Referências

| Projeto | Referencia |
|---|---|
| Application | Domain |
| Infrastructure | Application |
| Api | Infrastructure, Application |
| Domain.Tests | Domain |
| Application.Tests | Application |
| Infrastructure.Tests | Infrastructure |

`Domain` não recebe referência a nenhum outro projeto. Não adicione.

### P5 — Pacotes

Em `FixedIncome.Infrastructure`:
- `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11
- `Microsoft.EntityFrameworkCore.Design` 8.0.11

Em `FixedIncome.Infrastructure.Tests`:
- `Microsoft.EntityFrameworkCore.InMemory` 8.0.11

Manifesto de ferramentas com `dotnet-ef` 8.0.11, para que CI e ambiente local usem a
mesma versão:

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.0.11
```

Não adicione nenhum pacote além destes.

### P6 — Remoção do código de template

Remova:
- `Class1.cs` das três class libraries
- `UnitTest1.cs` dos três projetos de teste
- `WeatherForecast.cs` e `Controllers/WeatherForecastController.cs` da API

Mantenha `Program.cs`, `appsettings.json` e a pasta `Controllers` vazia.

### P7 — Ambiente local

Crie `docker-compose.yml` na raiz com um serviço PostgreSQL 16 alpine:

- container `fixedincome-db`
- database, usuário e senha: `fixedincome`
- porta 5432 exposta
- volume nomeado para persistência
- healthcheck com `pg_isready`

### P8 — Configuração da API

Em `appsettings.Development.json`, adicione a connection string apontando para o
serviço do Compose:

```
Host=localhost;Port=5432;Database=fixedincome;Username=fixedincome;Password=fixedincome
```

Não registre DbContext nem configure injeção de dependência ainda — isso é F3.

### P9 — Verificação

Execute e reporte o resultado de cada comando:

```bash
dotnet build
dotnet test
docker compose up -d
docker compose ps
```

Esperado: build sem erro nem warning; `dotnet test` executando os três projetos com
zero testes; container do Postgres com status healthy.

Se o build falhar, corrija e rode de novo até passar. Se o Docker falhar, reporte e
siga — o restante da fase não depende dele.

## Restrições

- Não crie entidades, enums, DTOs, interfaces, repositórios, controllers ou testes
- Não escreva migrations
- Não altere `CLAUDE.md` nem `docs/ESPECIFICACAO.md`
- Não execute `git add` nem `git commit` — o commit é feito manualmente após revisão
- Não instale extensões, SDKs ou ferramentas de sistema

## Critérios de aceite

1. `dotnet build` conclui sem erro
2. `dotnet test` executa os três projetos de teste
3. A árvore de diretórios corresponde à seção Entregáveis
4. `FixedIncome.Domain.csproj` não contém nenhum `ProjectReference`
5. Nenhum arquivo de template permanece no repositório
6. `git status` mostra todos os arquivos como não rastreados ou modificados, sem
   nenhum commit criado

## Relatório final

Ao terminar, reporte:

- Resultado de cada comando de verificação
- Lista dos arquivos criados
- Qualquer decisão tomada que não estivesse especificada aqui
- Qualquer passo que falhou e o motivo
