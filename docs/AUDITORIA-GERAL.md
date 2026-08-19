# Auditoria geral — estado do projeto após F4b

Documento de execução para agente. Objetivo: avaliar o projeto inteiro e produzir um
diagnóstico honesto do que está sólido e do que está frágil.

**Esta auditoria não corrige nada.** Não altere nenhum arquivo de código, configuração ou
documentação. Encontrar e classificar é o entregável. Correção vem depois, com decisão do
usuário.

Se um comando de leitura falhar, reporte e siga. Não contorne com alteração de código.

## Preparação

Confirme antes de começar:

```powershell
docker compose ps
dotnet build
dotnet test
```

Container healthy, build limpo, 101 testes passando. Se algo divergir, pare e reporte.

## A — Integridade arquitetural

Verifique e reporte cada item com evidência de comando, não por leitura de código:

1. `FixedIncome.Domain` sem `ProjectReference` e sem `PackageReference`
2. `FixedIncome.Application` referencia apenas `Domain` e o pacote de abstrações de DI
3. `FixedIncome.Infrastructure` referencia `Application`, nunca `Api`
4. Nenhum `using FixedIncome.Domain` em arquivos de `Controllers`
5. Nenhum `using FixedIncome.Infrastructure` em `Application`
6. Nenhuma referência a `DbContext`, `Npgsql` ou EF Core fora de `Infrastructure`

Para os itens 4 a 6, use busca textual no código e liste os arquivos encontrados.

## B — Cobertura de testes

Levante a cobertura real por projeto:

```powershell
dotnet test --collect:"XPlat Code Coverage"
```

Se a ferramenta de cobertura não estiver disponível, reporte isso e substitua por uma
análise manual: liste os métodos públicos de `Domain` e `Application` e indique quais não
são exercitados por nenhum teste.

Reporte o percentual por camada e identifique especificamente:

- Métodos públicos sem nenhum teste
- Caminhos de erro declarados nos casos de uso sem teste correspondente
- Ramos condicionais não exercitados

O critério da especificação é acima de 80% em domínio e aplicação. Diga se cumpre.

## C — Robustez do contrato HTTP

Com a API em execução, exercite entradas que os testes atuais não cobrem. Para cada uma,
reporte o código HTTP e o corpo da resposta.

**Malformação e ausência:**

1. POST de título com corpo JSON inválido (chave sem aspas)
2. POST de título com corpo vazio
3. POST de título sem o campo `Name`
4. POST de título com `Name` nulo explícito
5. GET de título com id que não é um GUID válido
6. POST de aporte com `AssetId` que não é um GUID válido

**Limites de valor:**

7. POST de título com `Name` de 121 caracteres
8. POST de título com `Rate` igual a zero
9. POST de título com `Rate` extremamente alto, como 999999
10. POST de aporte com `InvestedAmount` de valor muito alto, como 99999999999
11. POST de título com `MaturityDate` igual a `IssueDate`

**Datas e projeção:**

12. Projeção com `referenceDate` explícito em formato válido
13. Projeção com `referenceDate` em formato inválido, como `"31/12/2026"`
14. Projeção com `referenceDate` anterior à data do aporte
15. Projeção com `referenceDate` muito posterior ao vencimento

**Endpoint nunca exercitado:**

16. PUT de título alterando apenas `Name`, sem aportes vinculados
17. PUT de título alterando `MaturityDate` com aporte vinculado — espera 400 pela RN-06
18. PUT de título inexistente
19. DELETE de título sem aportes — espera 204

Para cada resposta, avalie três coisas: o código HTTP é semanticamente correto; a mensagem
é útil para quem consome a API; nenhum detalhe interno vaza, como nome de classe,
`StackTrace` ou mensagem de exceção do EF Core.

## D — Regras de negócio ponta a ponta

Cada regra da especificação deve ser verificável pela API, não apenas por teste de
unidade. Para cada uma, monte uma chamada que a exercite e reporte o resultado:

| Regra | Verificação |
|---|---|
| RN-01 | Projeção com valores conferíveis à mão |
| RN-02 | Projeção com data posterior ao vencimento é truncada |
| RN-03 | Alíquota correta em cada uma das quatro faixas |
| RN-04 | LCI e LCA com alíquota zero |
| RN-05 | Aporte fora da vigência rejeitado |
| RN-06 | Datas bloqueadas quando há aportes |

Para a RN-03, exercite as quatro faixas com aportes de prazos diferentes e confira as
alíquotas: 22,5 / 20 / 17,5 / 15.

Reporte se alguma regra não é alcançável pela API no estado atual.

## E — Consistência do contrato

1. Todas as respostas usam o mesmo formato `ApiResponse<T>`, inclusive as de erro
2. `StatusCode` do corpo sempre corresponde ao status HTTP da resposta
3. Endpoints de criação retornam o recurso criado com o id preenchido
4. Swagger documenta todos os códigos que cada endpoint pode retornar
5. Nomes de campo consistentes entre requisição e resposta

## F — Documentação

1. O README descreve o estado real do projeto, sem prometer o que não existe
2. Os comandos do README funcionam a partir de um clone limpo
3. As decisões técnicas documentadas correspondem ao código
4. `docs/ESPECIFICACAO.md` está coerente com o implementado, incluindo a RN-06
5. `CLAUDE.md` descreve padrões que o código realmente segue
6. O arquivo `.http` cobre todos os endpoints e é executável na ordem

Aponte especificamente qualquer divergência entre o que a documentação afirma e o que o
código faz.

## G — Higiene

1. Nenhum segredo, senha ou token em arquivo versionado
2. `git status` limpo, sem artefato de build não ignorado
3. Nenhum `Console.WriteLine` em código de produção
4. Nenhum `TODO`, `FIXME` ou código comentado
5. Nenhum método público sem consumidor, exceto os previstos em spec
6. Nenhum warning de compilação suprimido sem justificativa

## Relatório final

Classifique cada achado em uma das quatro categorias, e agrupe por elas:

**Defeito** — comportamento incorreto, resposta HTTP semanticamente errada, regra de
negócio alcançável por caminho indevido, ou vazamento de informação interna.

**Lacuna** — ausência de cobertura, caminho não testado, ou regra sem verificação
ponta a ponta.

**Divergência** — código em desacordo com especificação, README ou `CLAUDE.md`.

**Observação** — melhoria possível sem erro atual.

Para cada achado, informe: onde está, o que se esperava, o que aconteceu, e o impacto real
para quem consome a API.

Ao final, responda de forma direta: **este projeto está pronto para ser mostrado a um
avaliador técnico?** Se não, quais são os três achados que mais atrapalhariam essa
avaliação.

Não corrija nada. Não execute `git add` nem `git commit`.
