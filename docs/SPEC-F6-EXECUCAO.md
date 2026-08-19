# Spec de execução — F6: Entrega

Documento de execução para agente. Objetivo: tornar o projeto executável a partir de um
clone limpo, com integração contínua e medição de cobertura confiável.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão
em `CLAUDE.md`.

Esta é a última fase planejada. O critério não é acrescentar funcionalidade, e sim garantir
que outra pessoa consiga clonar o repositório e rodar tudo sem conhecimento prévio.

Esta fase toca:

- Raiz do repositório — `Dockerfile`, `.dockerignore`, `docker-compose.yml`
- `.github/workflows/` — pipeline de CI
- `src/FixedIncome.Api` — apenas configuração de connection string por variável de ambiente
- Os `.csproj` de teste — configuração de exclusão na cobertura

## Fora de escopo

Não altere `Domain`, `Application` nem `Infrastructure`. Não altere regras, cálculos,
casos de uso ou controllers. Não adicione pacotes NuGet.

Não altere o README — será atualizado separadamente ao final.

## Implementação

### E1 — Dockerfile da API

`Dockerfile` na raiz, multi-stage:

- Estágio de build sobre a imagem do SDK 8.0, restaurando e publicando em Release
- Estágio final sobre a imagem ASP.NET runtime 8.0, apenas com o publicado

Copie primeiro os arquivos de projeto e rode o restore, e só depois copie o restante do
código. Isso permite que o cache de camadas do Docker seja aproveitado quando apenas o
código muda, sem refazer o restore.

Exponha a porta 8080, que é a padrão do ASP.NET Core em container a partir do .NET 8.

Não execute como root: crie um usuário sem privilégio no estágio final e use `USER`.

`.dockerignore` na raiz excluindo pelo menos: `bin`, `obj`, `.git`, `.vs`, `.vscode`,
`TestResults`, `**/*.user`.

### E2 — Connection string por variável de ambiente

Dentro do container, o banco não está em `localhost` — está no serviço `db` da rede do
Compose. A connection string precisa ser resolvida por ambiente, não fixada em arquivo.

Ajuste o `Program.cs` para ler a connection string de variável de ambiente quando presente,
com fallback para a configuração. Não remova o valor de `appsettings.Development.json`, que
continua servindo à execução local com `dotnet run`.

O nome da variável deve seguir a convenção do ASP.NET Core para configuração aninhada.
Documente qual é no relatório.

### E3 — Compose completo

Acrescente o serviço da API ao `docker-compose.yml`, mantendo o serviço `db` como está.

O serviço da API:

- Constrói a partir do `Dockerfile` da raiz
- Depende do `db`, aguardando a condição de healthy
- Recebe a connection string por variável de ambiente, apontando para o host `db`
- Expõe a porta 8080 no host
- Define o ambiente como Development, para que o Swagger fique acessível

A migration continua sendo aplicada manualmente, conforme a decisão registrada na F4b. Não
adicione passo de migration ao container nem ao entrypoint.

Valide que `docker compose up --build` sobe os dois serviços e que o Swagger responde.

### E4 — Cobertura confiável

A execução conjunta dos quatro projetos de teste produziu um relatório de cobertura vazio
para um deles. Configure a coleta de forma que o resultado seja determinístico.

Crie um `coverlet.runsettings` na raiz com:

- Formato `cobertura`
- Exclusão de código gerado: migrations, snapshot do modelo e qualquer classe marcada com
  `ExcludeFromCodeCoverage`
- Exclusão dos arquivos sob `**/Migrations/*.cs`

A exclusão das migrations é necessária porque são código gerado pelo `dotnet ef`. Mantê-las
no denominador faz a cobertura de `Infrastructure` cair para cerca de 30%, número que
comunica algo falso: os repositórios estão integralmente cobertos.

Documente no relatório o comando que produz cobertura confiável para os quatro projetos.

### E5 — Pipeline de CI

`.github/workflows/ci.yml`, disparando em push e pull request para `main`.

Passos:

1. Checkout
2. Configuração do .NET 8
3. Restore
4. Build em Release, sem restore
5. Teste em Release, sem build, com coleta de cobertura usando o runsettings de E4
6. Publicação do relatório de cobertura como artefato do workflow

O job deve falhar se qualquer teste falhar. Não configure deploy.

Se a execução conjunta se mostrar instável no CI, execute os projetos de teste
sequencialmente em vez de mascarar o problema com `continue-on-error`.

### E6 — Validação a partir de clone limpo

Simule a experiência de quem encontra o repositório pela primeira vez:

```powershell
docker compose down -v
```

Depois, a partir de um clone em diretório temporário separado, execute apenas o que o
README instrui, sem nenhum passo adicional. Reporte cada comando e o resultado.

O objetivo é descobrir passos implícitos: variável não documentada, ordem que só funciona
porque você já sabe, arquivo que existe na sua máquina mas não no repositório.

Ao final, remova o clone temporário.

## Verificação

```powershell
dotnet build
dotnet test
docker compose up --build -d
docker compose ps
```

Ambos os serviços saudáveis, Swagger respondendo, 124 testes passando.

## Restrições

- Não altere `Domain`, `Application` nem `Infrastructure`
- Não altere `CLAUDE.md`, `docs/ESPECIFICACAO.md` nem specs anteriores
- Não altere o README
- Não adicione pacotes NuGet
- Não configure deploy nem publicação de imagem
- Não execute `git add` nem `git commit`

## Critérios de aceite

1. `docker compose up --build` sobe API e banco, com o Swagger acessível
2. O container da API não executa como root
3. `dotnet run` local continua funcionando, sem regressão
4. A coleta de cobertura produz relatório válido para os quatro projetos, de forma
   reprodutível
5. Migrations estão excluídas da medição de cobertura
6. O workflow de CI está sintaticamente válido e falha quando um teste falha
7. A validação de clone limpo não revelou passo não documentado, ou os que revelou estão
   reportados
8. Nenhum commit foi criado

## Relatório final

Reporte:

- Resultado de `dotnet build` e `dotnet test`, com a contagem
- Conteúdo do `Dockerfile`, do `docker-compose.yml` e do workflow
- O nome da variável de ambiente da connection string
- A cobertura dos quatro projetos após a exclusão das migrations
- O passo a passo da validação de clone limpo, com cada comando e resultado
- Qualquer passo implícito descoberto em E6
- Decisões tomadas que não estavam especificadas aqui
