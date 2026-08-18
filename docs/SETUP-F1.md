# Setup — F1: Fundação

## Pré-requisitos

- .NET 8 SDK
- Docker (com Docker Compose)

## Restaurar e compilar

```bash
dotnet restore
dotnet build
```

## Rodar os testes

```bash
dotnet test
```

## Subir o banco local

```bash
docker compose up -d
docker compose ps
```

O serviço `db` expõe PostgreSQL 16 na porta 5432, com banco, usuário e senha
`fixedincome`. A connection string já está configurada em
`src/FixedIncome.Api/appsettings.Development.json`.

## Ferramentas

O manifesto em `.config/dotnet-tools.json` fixa a versão do `dotnet-ef` usada
pelo CI e pelo ambiente local:

```bash
dotnet tool restore
```
