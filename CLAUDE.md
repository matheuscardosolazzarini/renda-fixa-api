# Convenções do projeto

API de controle de investimentos em renda fixa. A especificação técnica está em
`docs/ESPECIFICACAO.md` e é a fonte de verdade — consulte-a antes de implementar
qualquer coisa e não invente requisitos que não estejam nela.

## Fluxo de trabalho

Implemente uma fase por vez, conforme a seção "Fases" da especificação. Ao concluir
uma fase, pare e aguarde revisão antes de avançar. Não antecipe código de fases
seguintes.

Ao final de cada alteração, rode `dotnet build` e `dotnet test` e corrija o que falhar.

## Idioma

Comentários e mensagens de commit em português. Identificadores — classes, métodos,
variáveis, colunas — em inglês.

Comente apenas o que não é óbvio pelo código: decisão de negócio, motivo de uma
exceção, origem de uma regra tributária. Não comente o que o nome do método já diz.

## Arquitetura

Dependências apontam para dentro: `Api → Application → Domain`, com `Infrastructure`
implementando interfaces declaradas em `Application`. O projeto `Domain` não referencia
nenhum outro projeto nem pacote de infraestrutura.

Regras de cálculo — rentabilidade e apuração de IR — ficam no domínio, não nos casos
de uso.

## Padrões de código

**Controller** herda `ControllerBase` e retorna `StatusCode(response.StatusCode, response)`.
Não contém regra de negócio: valida a rota, chama o caso de uso, devolve a resposta.

**Resposta** encapsulada em `ApiResponse<T>`, com fábricas para Success, BadRequest,
NotFound e InternalError.

**Caso de uso** implementa uma interface, recebe as dependências por construtor e
retorna `ApiResponse<T>`.

**Entidade** herda `BaseEntity`, propriedades em PascalCase. Invariantes validadas no
próprio domínio, lançando exceção de domínio — nunca deixando a entidade em estado
inválido.

**Configuração de entidade** via `IEntityTypeConfiguration<T>`, com `ToTable`,
`HasKey` e `HasColumnName` em SNAKE_CASE. Propriedades calculadas marcadas com `Ignore`.

**Repositório** recebe o DbContext por injeção e expõe métodos assíncronos retornando
`Task<IEnumerable<T>>` para coleções.

## Testes

xUnit no padrão AAA, com as três seções visualmente separadas.

Não use Moq nem qualquer biblioteca de mock. Dublês são classes stub declaradas
`private sealed` dentro do próprio arquivo de teste.

Testes de repositório usam DbContext InMemory.

Nomeie os testes descrevendo o comportamento esperado, não o método chamado.
Cada regra de negócio da especificação precisa de teste, incluindo os casos de
fronteira — especialmente os limites das faixas da tabela regressiva de IR.

## Restrições

Não adicione pacotes NuGet além dos já presentes sem justificar a necessidade.

Não crie camadas, abstrações ou padrões que a especificação não pede. Interface com
uma única implementação só existe quando serve à inversão de dependência entre camadas.