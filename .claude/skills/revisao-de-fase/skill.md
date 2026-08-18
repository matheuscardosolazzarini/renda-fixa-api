---
name: revisao-de-fase
description: Revisa uma fase concluída antes do commit. Use quando o usuário pedir para revisar a fase, verificar se está pronto para commitar, conferir se a implementação seguiu a spec, ou ao final de qualquer fase de implementação. Verifica escopo, camadas, pacotes, testes e resíduos de código.
---

# Revisão de fase

Execute na ordem e reporte o resultado de cada item. Não corrija nada sem antes
reportar — a decisão de corrigir é do usuário.

## 1. Escopo

Compare os arquivos alterados com a seção de escopo da spec da fase.

Liste qualquer arquivo modificado fora do escopo declarado. Liste qualquer item
marcado como fora de escopo que tenha sido implementado mesmo assim.

Antecipação de fase seguinte é problema, não adiantamento de trabalho: ela escapa da
revisão que a fase correspondente teria.

## 2. Camadas

```bash
dotnet list src/FixedIncome.Domain reference
dotnet list src/FixedIncome.Domain package
```

`Domain` não pode ter referência a projeto nem pacote. Qualquer resultado aqui indica
violação da direção das dependências.

Verifique também que `Application` não referencia `Infrastructure`.

## 3. Pacotes

Liste os pacotes adicionados nesta fase. Confronte com os autorizados na spec.

Pacote não previsto precisa de justificativa explícita. Biblioteca de mapeamento,
validação ou log adicionada por conveniência deve ser reportada para remoção — o
projeto declara não usar abstração que a spec não pede.

## 4. Build e testes

```bash
dotnet build
dotnet test
```

Build precisa passar sem erro e sem warning. Warning ignorado acumula e vira ruído.

Reporte a contagem de testes e o resultado. Se algum teste está marcado como Skip,
reporte qual e por quê.

## 5. Regras cobertas

Percorra as regras de negócio numeradas da especificação funcional e indique, para
cada uma, o teste que a cobre.

Regra sem teste correspondente é o achado mais grave desta revisão. Reporte
explicitamente em vez de deduzir que está coberta indiretamente.

## 6. Resíduos

Procure e reporte:

- Arquivo de template não removido
- Código comentado
- `TODO` ou `FIXME` sem contexto
- Método ou propriedade pública criada e nunca usada
- Comentário que apenas repete o que o nome do identificador já diz

## 7. Relatório

Entregue nesta ordem:

1. Itens que impedem o commit
2. Itens que merecem atenção mas não bloqueiam
3. Decisões tomadas durante a fase que não estavam na spec
4. Sugestão de mensagem de commit no formato `F<n>: <descrição>`

Não execute `git add` nem `git commit`.
