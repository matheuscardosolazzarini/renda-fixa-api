# Revisão da SPEC-F7 — especialista-renda-fixa

Modo: revisão. Data: 2026-10-30.
Revisado: `/mnt/user-data/outputs/f7/SPEC-F7-EXECUCAO.md` (rascunho), comparado com `docs/ESPECIFICACAO.md` (RN-01 a RN-11), com `Position.cs` (v1) e com os seis arquivos de teste v1.

Nenhuma regra da especificação foi tratada como alterada. Onde eu discordo de uma RN, isso aparece como recomendação. A decisão é do Matheus.

## Resumo

| # | Severidade | Tema |
|---|---|---|
| B1 | bloqueante | Arredondamento independente de cada campo quebra a RN-07 já dentro de uma posição (Líquido ≠ Bruto − IOF − IR) |
| B2 | bloqueante | Casas decimais do CDI não estão fixadas: P4 remete aos casos de referência, e a RN-09 remete à SPEC-F7 |
| B3 | bloqueante | Pro rata do IPCA (RN-10) sem denominador definido |
| I1 | importante | LCA tem alíquota zero de IOF pelo Decreto 6.306/2007; a RN-11 diz que paga |
| I2 | importante | Resumo da carteira com uma posição sem CDI, e a data de referência "hoje" antes da importação do CDI de ontem |
| I3 | importante | `MarketDataLoader` precisa do último IPCA publicado mesmo fora do período, para estimar |
| I4 | importante | O SGS devolve erro HTTP para período sem dado, não lista vazia; o stub do P7 testaria um comportamento que não existe |
| I5 | importante | RN-10 (mês civil, IPCA do próprio mês) diverge da convenção de CDB e NTN-B; recomendação em (b) |
| I6 | importante | Rendimento ≤ 0: falta dizer o que `IofRate`/`TaxRate` mostram |
| M1–M10 | menor | Ver seção "Menores" |

Respostas diretas às perguntas (a) a (d) vêm antes dos achados.

---

## (a) Casas decimais do CDI e a série 12 do SGS

**Metodologia B3/Cetip para percentual do DI** (Caderno de Fórmulas – Debêntures Cetip 21, item 2.1.1 "DI – Taxa DI-Over", págs. 12–13):

- `TDIk = (1 + DIk/100)^(1/252) − 1`, "calculada com 8 (oito) casas decimais, com arredondamento". DIk é a taxa anual "utilizada com 2 (duas) casas decimais".
- `FatorDI = Π (1 + TDIk × p/100)`, "da data de início de capitalização, inclusive, até a data de cálculo exclusive", "calculado com 8 (oito) casas decimais com arredondamento".
- Observação 1: "a cada fator diário acumulado, trunca-se o resultado com 16 (dezesseis) casas decimais, aplicando-se o próximo fator diário".
- `p` é "informado com 2 (duas) casas decimais".
- Fonte: https://www.b3.com.br/data/files/F6/26/EA/D2/F051F610AF4EF0F6AC094EA8/Caderno%20de%20Formulas%20-%20Debentures%20Cetip%2021.pdf

O caderno específico de CDB da B3 (https://www.b3.com.br/data/files/7D/A3/B2/BB/2054A710D25F12A7AC094EA8/CDBs-DIs-DPGE-LAM-LC-LF-LFS-LFSC-LFSN-IECI-RDB.pdf, versão de 28/06/2021) tem a seção "6 Juros Flutuantes", mas a extração parou na pág. 44 e não consegui lê-la. **A CONFIRMAR** se as regras de casas decimais do CDB são idênticas às da debênture. A expectativa é que sejam, porque o motor de cálculo é o mesmo.

**A série 12 é equivalente a calcular TDI a partir da taxa anual?** Sim, para as cotações conferidas:

| Data | SGS 4389 (DI % a.a.) | TDI calculada: `(1+DI)^(1/252)−1`, 8 casas | SGS 12 (% a.d.) / 100 |
|---|---|---|---|
| 14 a 16/09/2026 | 13,90 | 0,00051660 | 0,00051660 |
| 17 a 22/09/2026 | 13,65 | 0,00050788 | 0,00050788 |

A série 12 vem com 6 casas em percentual, que dão 8 casas em fração. É exatamente a `TDIk` arredondada da B3. Usar a série 12 dividida por 100 é a mesma coisa que calcular a TDI, e evita a potência fracionária. Fontes: https://api.bcb.gov.br/dados/serie/bcdata.sgs.12/dados?formato=json&dataInicial=11/09/2026&dataFinal=20/09/2026 e a mesma rota para a série 4389.

Uma consequência útil: com `TDIk` de 8 casas e `p` de até 2 casas, `TDIk × p/100` tem no máximo 12 casas. O fator diário é exato, então não precisa de regra de arredondamento. Só o acumulado (truncar em 16) e o fator final (arredondar em 8) mudam o resultado.

## (b) Pro rata do IPCA: convenção de mercado e a RN-10

O que o mercado faz:

- **NTN-B / Tesouro IPCA+** (Tesouro Direto, "Cálculo da Rentabilidade – Tesouro IPCA"): o VNA é atualizado no dia 15 de cada mês. Entre um dia 15 e o seguinte, usa-se o IPCA projetado: `VNA × (1 + IPCA projetado)^x`, com `x` em **dias corridos** entre dias 15. O VNA é truncado na 6ª casa. https://www.tesourodireto.com.br/data/files/7B/67/BC/64/53B2D610393A62D6894D49A8/Calculo_Rentabilidade_TesouroIPCA.pdf
- **CDB IPCA+ registrado na B3** (caderno de CDB acima, seções 2.2, 2.3 e 2.6):
  - o aniversário mensal é "o dia da data de vencimento em cada mês". Não é o dia da aplicação, nem o dia 1;
  - o número-índice usado é o "do mês imediatamente anterior ao mês de atualização", ou seja, há defasagem de um mês;
  - existe a regra de "correção defasada" para atualizações antes do dia 15;
  - os fatores são calculados com 8 casas, sem arredondamento;
  - o pro rata usa `dup/dut`. O próprio caderno marca "dias corridos ou úteis (-1?)" na seção 2.3.2, então a escolha depende do papel.
  - **A CONFIRMAR** no PDF original: os pontos acima vêm de extração de texto.

Na prática, cada CDB IPCA+ define no próprio termo a data de aniversário, a defasagem e a base do pro rata. Não existe uma convenção única de CDB que a RN-10 pudesse copiar.

**Posição:** para um projeto de portfólio, a simplificação da RN-10 (mês civil, pro rata em dias úteis, IPCA do próprio mês) é **aceitável**, desde que:

1. o denominador do pro rata fique definido (achado B3);
2. o README diga que o valor diverge do extrato do banco, por três motivos: aniversário, defasagem de um mês e projeção de mercado no lugar da repetição do último IPCA.

Um efeito colateral pesa a favor de mudar a regra. Com o IPCA do próprio mês, **toda projeção com data de referência no mês corrente sai `IsEstimated = true`**, porque o IPCA do mês M só sai por volta do dia 10 de M+1. Com a defasagem de um mês, a regra fica mais perto do CDB da B3, e a estimativa só seria usada entre o fim do mês e a divulgação.

**Recomendação (não alterei a especificação):** manter a RN-10 na F7 e acrescentar a ela:
- (i) a definição do pro rata do achado B3;
- (ii) uma frase explícita: "o IPCA do mês M rende sobre os dias úteis do mês M, sem defasagem; isso difere da convenção de aniversário dos CDBs e da NTN-B".

Mudar para aniversário com defasagem fica como item para a v3, se a meta for bater com extrato de banco.

Para meses cheios com aniversário no dia 1, a fórmula da B3 (número-índice do mês anterior à atualização sobre o do mês anterior à emissão) dá o mesmo produto da RN-10. As diferenças aparecem só nos meses parciais e na estimativa.

## (c) IOF

- **Tabela:** conferida contra o anexo publicado na Câmara (o planalto.gov.br bloqueou o acesso nesta sessão). As alíquotas "% limite do rendimento" são 96, 93, 90, 86, 83, 80, 76, 73, 70, 66, 63, 60, 56, 53, 50, 46, 43, 40, 36, 33, 30, 26, 23, 20, 16, 13, 10, 06, 03, 00 para os dias 01 a 30. **A tabela da RN-11 está correta.** https://www2.camara.leg.br/legin/fed/decret/2007/decreto-6306-14-dezembro-2007-566561-anexo-pe.pdf
- **Base legal:** art. 32, caput: "O IOF será cobrado à alíquota de um por cento ao dia sobre o valor do resgate, cessão ou repactuação, limitado ao rendimento da operação, em função do prazo, conforme tabela constante do Anexo". O § 1º, I, aplica o artigo "às operações realizadas no mercado de renda fixa". Texto atualizado: https://www2.camara.leg.br/legin/fed/decret/2007/decreto-6306-14-dezembro-2007-566561-normaatualizada-pe.pdf
- **Contagem de dias:** o decreto não define. A IN RFB 907/2009, art. 10, § 3º, diz que "a contagem dos prazos constantes do Anexo ao Decreto nº 6.306, de 2007, será feita com base nas datas da efetiva disponibilidade financeira dos recursos". A prática de mercado é dias corridos, excluindo o dia da aplicação: resgate em D+1 é o dia 1. É o mesmo que `ElapsedDays = referência − aplicação` do código, então a spec está coerente.
  - **A CONFIRMAR:** li a IN 907 em espelho não oficial (normaslegais.com.br), e um agregador a lista como revogada. Falta conferir o texto e a norma sucessora no site da Receita.
- **LCI:** não consta das hipóteses de alíquota zero do § 2º do art. 32. **Paga IOF regressivo.** A RN-11 está certa para LCI.
- **LCA:** o § 2º, V, põe em alíquota zero as operações "com Certificado de Direitos Creditórios do Agronegócio - CDCA, com Letra de Crédito do Agronegócio - LCA, e com Certificado de Recebíveis do Agronegócio - CRA" (incluído pelo Decreto 7.487/2011). **A LCA não paga IOF.** Ver achado I1.
- **Tesouro Direto (títulos públicos federais):** não está nas hipóteses de alíquota zero do § 2º, que incluem instituições financeiras, fundos, renda variável, CDCA/LCA/CRA, debêntures, CRI e LF, ETF de renda fixa e FGC. Segue a regra geral de renda fixa do § 1º, I: **paga IOF regressivo**.
- **IR sobre rendimento menos IOF:** a prática é tributar a diferença entre o valor de resgate líquido do IOF e o valor aplicado. A fonte usual é a Lei 8.981/1995, art. 65, § 1º.
  - **A CONFIRMAR:** não consegui abrir o texto oficial nesta sessão.
  - A tabela de IR da RN-03 foi conferida na Lei 11.033/2004, art. 1º, I a IV; a isenção da LCI no art. 3º, II, e a da LCA no art. 3º, IV: https://www2.camara.leg.br/legin/fed/lei/2004/lei-11033-21-dezembro-2004-535177-normaatualizada-pl.pdf

## (d) A data de referência fica fora do produtório

Confirmado. O caderno da B3 define o FatorDI "da data de início de capitalização, inclusive, até a data de cálculo exclusive" e usa a "taxa DI-Over do dia útil anterior à data de cálculo". O valor da série 12 datado D é o DI das operações de D, que rende de D para D+1. Por isso, a convenção da RN-08 (inclui o início, exclui o fim) aplicada à série 12 é a de mercado.

**A CONFIRMAR:** se a Calculadora do Cidadão segue a mesma convenção. As páginas de ajuda e de metodologia do BCB que consegui abrir não tratam do CDI. O caso 4.2 dos casos de referência traz o valor com e sem a taxa do último dia, para diagnosticar.

---

## Achados

### B1 — bloqueante — Arredondamento independente quebra a RN-07 por posição

**Trecho (P4):** "6. Líquido = bruto − IOF − IR. [...] O arredondamento final continua com duas casas, `MidpointRounding.AwayFromZero`, como hoje." E no P5: "`TotalNetAmount = TotalGrossAmount − TotalTaxAmount − TotalIofAmount`, somando valores já arredondados."

**Problema:** o código arredonda cada campo a partir do valor sem arredondamento. Assim, numa mesma posição, `NetAmount` pode diferir em R$ 0,01 de `GrossAmount − IofAmount − TaxAmount`. Com isso, o consolidado não consegue ser ao mesmo tempo a soma dos `NetAmount` e `Gross − Tax − Iof`, as duas coisas que a RN-07 exige.

- O problema já existe na v1 e não aparecia porque os valores eram exatos.
- Com base 252, o próprio teste v1 de 10% a.a. (2020) o mostra: Bruto 1.099,17, IR 17,35, Líquido arredondado 1.081,81, mas 1.099,17 − 17,35 = 1.081,82.
- Dos 22 casos de referência com projeção completa (v1, CDI, IOF e IPCA, sem contar o dia 0), 11 quebram a identidade.

**Correção proposta (SPEC-F7, P4; não muda RN):** fixar a ordem de arredondamento em centavos:
1. `G = round2(InvestedAmount × fator)`; `Y = G − InvestedAmount`
2. `IOF = round2(Y × alíquotaIOF / 100)` (zero se `Y ≤ 0`)
3. `IR = round2((Y − IOF) × alíquotaIR / 100)` (zero se `Y ≤ 0`)
4. `Net = G − IOF − IR`

Todos com `AwayFromZero`. Assim cada posição fecha, e a RN-07 vale no consolidado. Os casos de referência trazem as duas colunas: "spec atual" e "proposta B1". O Matheus escolhe qual vira teste.

**Fonte:** RN-07 da especificação. Bancos e corretoras apuram IOF e IR em centavos. Se arredondam ou truncam, **A CONFIRMAR**: não achei fonte oficial; o folheto de CDB da B3 mostra um IR que parece truncado. Fica `AwayFromZero` por coerência com o projeto.

### B2 — bloqueante — Casas decimais do CDI não fixadas

**Trecho (P4.3):** "Cdi (RN-09): produtório de `1 + CDI do dia / 100 × Rate / 100` [...]. Casas decimais conforme a seção 'Casos de referência'". A RN-09 diz que as casas "são fixadas na SPEC-F7".

**Problema:** a regra não está escrita em lugar nenhum. O implementador vai inventar.

**Correção proposta (texto para P4.3):**
> `TDIk = valor da série 12 / 100` (8 casas, exato). Fator diário `1 + TDIk × Rate/100`, sem arredondamento (é exato para `Rate` com até 2 casas). Acumulado: a cada multiplicação, truncar em 16 casas. Fator final: arredondar em 8 casas (`AwayFromZero`). Bruto = `InvestedAmount × fator final`, arredondado em 2 casas conforme B1. A B3 trunca o valor final ("sem arredondamento"); o projeto arredonda, e a diferença máxima é R$ 0,01, dentro do critério de aceite.

Acrescentar uma validação: `Rate` de título Cdi com no máximo 2 casas decimais. O caderno diz que `p` é "informado com 2 casas"; se `Rate` tiver mais casas, o fator diário deixa de ser exato.

**Fonte:** Caderno de Fórmulas Debêntures Cetip 21, item 2.1.1 (link em (a)).

### B3 — bloqueante — Pro rata do IPCA sem denominador

**Trecho (P4.3 e RN-10):** "com pro rata em dias úteis nos meses parciais".

**Problema:** não diz pro rata de quê sobre o quê. Há pelo menos três leituras: DU do mês civil, 21 fixos, ou dias corridos. Cada uma dá um valor diferente, e os testes ficam sem referência.

**Correção proposta (texto para P4.3, sem mudar a RN):**
> Para cada mês civil M tocado pelo intervalo `[aplicação, cálculo)`: `fator_M = (1 + IPCA_M/100) ^ (DU_M_no_intervalo / DU_M_total)`, onde `DU_M_no_intervalo = CountBusinessDays(max(aplicação, 1º de M), min(cálculo, 1º de M+1))` e `DU_M_total = CountBusinessDays(1º de M, 1º de M+1)`. Mês cheio resulta em expoente 1. Fator IPCA = produto dos `fator_M`, sem arredondamento intermediário. `IPCA_M` é o valor da série 433 com referência no 1º dia de M; se não houver, usa-se o último publicado e `IsEstimated = true`.

Os casos 6.x foram calculados com essa leitura e estão marcados assim.

**Fonte:** RN-10. A B3 usa `dup/dut` em dias úteis (caderno de CDB, seção 5.4.1, e 2.3.2 com ressalva).

### I1 — importante — LCA tem alíquota zero de IOF

**Trecho (RN-11 e P4/Testes):** "A isenção da RN-04 vale só para o IR: LCI e LCA também pagam IOF." Na spec: "LCI pagando IOF e isenta de IR".

**Problema:** o Decreto 6.306/2007, art. 32, § 2º, V, põe as operações com LCA em alíquota zero de IOF. LCI não tem essa previsão e paga.

**Correção proposta:** recomendo alterar a RN-11 para "LCI paga IOF; LCA tem alíquota zero de IOF (Decreto 6.306/2007, art. 32, § 2º, V)" e acrescentar o teste "LCA com 15 dias: IOF zero e IR zero". Enquanto a RN não mudar, a F7 implementa o texto atual. Nos casos de referência, só usei LCI, que é correto pelas duas leituras.

Observação: na prática, LCI e LCA têm prazo mínimo de carência definido pelo CMN, então IOF em LCI quase não acontece. O teste continua válido como teste de regra. **A CONFIRMAR** a resolução CMN vigente, que não verifiquei.

**Fonte:** https://www2.camara.leg.br/legin/fed/decret/2007/decreto-6306-14-dezembro-2007-566561-normaatualizada-pe.pdf (art. 32, § 2º, V e VI).

### I2 — importante — Resumo com CDI faltante e referência "hoje"

**Trecho:** "Sem cotação, sem projeção (RN-09)". O `GetPortfolioSummaryUseCase` aceita `referenceDate` nulo (hoje).

**Problema:**
- (i) A spec não diz o que o resumo da carteira faz quando uma única posição Cdi lança `DomainException`: a carteira inteira falha, ou a posição sai do resumo?
- (ii) Com referência = hoje, a projeção precisa do CDI do último dia útil. O job roda a cada 6 h, e o SGS só publica depois do fechamento. Por algumas horas de todo dia útil, qualquer carteira com CDB Cdi falha.
- (iii) Referência futura em título Cdi sempre falha. Está coerente com a RN-09, mas o usuário verá erro ao simular.

**Correção proposta:** decidir e escrever no P8:
- (a) o resumo falha por inteiro com 400 e a primeira data sem cotação (mais simples e coerente com a RN-09); ou
- (b) o resumo exclui a posição e lista as excluídas.

Em ambos os casos, incluir o teste. **A CONFIRMAR** o horário de publicação do CDI no SGS.

**Fonte:** RN-07 e RN-09 da especificação.

### I3 — importante — Último IPCA publicado fora do período

**Trecho (P8):** "`MarketDataLoader` [...] monta o `MarketData` para um período".

**Problema:** para estimar o IPCA de um mês sem divulgação, a RN-10 manda usar "a última variação mensal publicada". Num aporte de 2026-11-03 com referência em 2026-12-01, o período não tem nenhum IPCA. O último publicado (09/2026) fica fora dele, e o loader devolveria série vazia.

**Correção proposta:** o loader carrega o IPCA do período e também a última cotação de IPCA anterior ao início do período (`IIndexRateRepository` já prevê "obter a última data importada"). Teste no `MarketDataLoader` com repositório stub.

**Fonte:** RN-10.

### I4 — importante — SGS devolve erro para período vazio

**Trecho (P7):** "Período sem dado devolve lista vazia, não erro".

**Problema:** nas consultas desta revisão, períodos sem dado (série 433 em 01/10–30/10/2026; série 12 em 03/10–04/10/2026, um fim de semana) falharam com erro de cliente (4xx), em vez de `[]`. Se o stub do teste devolver `[]` com 200, o teste passa e a produção quebra.
- **A CONFIRMAR:** o status exato (a ferramenta só mostrou "client_error"; deve ser 404).

**Correção proposta:** "Para período sem dado, a API do SGS responde com status de erro 4xx; o provedor converte esse caso em lista vazia. Outros erros seguem para a política de resiliência." O stub reproduz o 4xx. Confirmar o status com uma chamada real no `VALIDACAO-F7.md`.

O limite de 10 anos está confirmado: "Consultas por período de datas serão limitadas a 10 anos, retornando erro caso não satisfaça essa condição" (https://dadosabertos.bcb.gov.br/dataset/1178-taxa-de-juros---selic-anualizada-base-252, aviso válido desde 26/03/2025).

### I5 — importante — Convenção do IPCA (RN-10) diferente da de mercado

Ver (b). **Correção proposta:** manter a RN-10 na F7, completar com B3 e acrescentar a nota de divergência no README. Mudar para aniversário com defasagem é decisão do Matheus para a v3.

### I6 — importante — Alíquotas exibidas quando o rendimento é ≤ 0

**Trecho:** "Rendimento negativo. [...] IOF e IR são zero". O mesmo vale para o dia 0 e para aporte em fim de semana ou feriado com DU = 0.

**Problema:** não diz se `IofRate` e `TaxRate` mostram a alíquota da tabela pelo prazo, com valor zero, ou se viram zero. Os testes vão divergir.

**Correção proposta:** "As alíquotas sempre refletem o prazo (tabela); só os valores (`IofAmount`, `TaxAmount`) zeram quando o rendimento é ≤ 0." É coerente com a v1, onde `taxRate` vem só de `IncomeTaxTable.RateFor(assetType, elapsedDays)`, sem depender do rendimento. Os casos 5.1 e 6.4 usam essa convenção.

**Fonte:** `Position.cs` v1, linha 66.

---

## Menores

**M1 — Cobertura do calendário só até 2030.**
- `GetPortfolioSummaryUseCaseTests.Data_de_referencia_ausente_usa_a_data_atual` usa `DateTime.UtcNow` e quebra em 2031.
- Títulos com vencimento depois de 2030 (Tesouro IPCA+ 2035, 2045) não poderão ser projetados para depois de 2030.
- A ANBIMA publica os feriados até 2099 (planilha Feriados.xls em https://www.anbima.com.br/feriados/feriados.asp).
- Proposta: semear até 2099, ou pelo menos 2050, e injetar `TimeProvider` no caso de uso.

**M2 — Data final na borda da cobertura.** `CountBusinessDays(2030-12-31, 2031-01-01)` não usa 2031-01-01, porque o fim é exclusivo. A spec diz que "data fora do período coberto lança exceção" sem dizer se vale para o `end`. Proposta: validar `start` e `end − 1 dia`.

**M3 — `IndexRate` Cdi em dia não útil.** A especificação diz "dia útil para Cdi", mas a entidade não tem como validar sem o calendário. Proposta: validar no `ImportIndexRatesUseCase` ou no `MarketData`, e registrar a decisão.

**M4 — Precisão do `decimal` no acumulado.** Acumulado com 16 casas × fator diário com até 12 casas dá 28 casas mais a parte inteira, no limite das 28–29 casas significativas do `decimal`. O arredondamento interno pode, em caso raríssimo, mexer na 16ª casa antes do truncamento. Proposta: comentar no código, ou truncar o fator diário em 16 casas antes de multiplicar (sem efeito para `Rate` com 2 casas).

**M5 — Série 433 com 2 casas.** O produto das variações mensais arredondadas difere um pouco da razão de números-índice que a B3 e o Tesouro usam. Aceitável; registrar no README.

**M6 — Aporte anterior a 2020.** A importação começa em 2020-01-01, e o calendário também. Uma posição com aplicação anterior a 2020 sempre dará erro. Proposta: mensagem de erro que diga "data anterior à cobertura do calendário (2020-01-01)".

**M7 — Fronteiras do IR.** A definição do especialista pede os dois lados (180/181, 360/361, 720/721). Os testes v1 cobrem só 180, 360, 720 e 900. Acrescentar 181, 361 e 721 é barato e protege a migração.

**M8 — `PortfolioSummaryTests`.** O fixture de `Participacao_percentual_e_zero_para_todos_quando_total_liquido_e_zero` tem bruto 1.000 e líquido 0 sem imposto. Se o `PortfolioSummary` passar a validar a identidade da RN-07, o fixture quebra. Proposta: ajustar o fixture junto com o `TotalIofAmount`.

**M9 — Seed de feriados nos testes de API.** O `HasData` só é aplicado no InMemory com `EnsureCreated`. Os testes de fluxo PreFixed passam a depender do calendário. Confirmar no `ApiTestBase`, que não foi enviado para revisão (**A CONFIRMAR**).

**M10 — Comentários dos testes v1.**
- O comentário "fator de capitalização exato de 1,10" em `PositionTests` deixa de valer em 2020.
- O comentário "rendimento pequeno (1.000)" vira 992,02.
- Em `GetPositionProjectionUseCaseTests` e nos testes de fluxo, o fator 1,10 continua exato por coincidência: 2025 tem exatamente 252 dias úteis. Vale comentar isso no teste, porque ele passa a funcionar como sentinela do feriado de 20/11. Sem o 20/11/2025 no seed, o bruto vira 1.100,42.

## O que conferi e está correto

- **RN-01 em base 252, com IR e IOF em dias corridos:** coerente com a Lei 11.033/2004 (prazo da aplicação) e com o anexo do decreto.
- **Tabela de IOF da RN-11:** idêntica ao anexo.
- **Dia 0 com alíquota zero:** sem efeito prático, porque o rendimento é zero.
- **Ordem IOF antes do IR:** correta.
- **Série 12 em % a.d. e série 433 em % no mês, com deflação aceita:** confirmado (IPCA 08/2026 = −0,32).
- **Calendário ANBIMA 2020–2030:** conferido ano a ano em anbima.com.br. As datas móveis batem com a Páscoa calculada. O 20/11 aparece a partir de 2024. O CDI existe em 31/12/2025 e na Quarta de Cinzas (18/02/2026), e não existe em 20/11/2025, o que é coerente com o calendário.

## Fontes consultadas

- ANBIMA, feriados nacionais 2020–2030: https://www.anbima.com.br/feriados/feriados.asp (páginas anuais `fer_nacionais/AAAA.asp`)
- B3, Caderno de Fórmulas Debêntures Cetip 21: link em (a)
- B3, Caderno de Fórmulas CDB/DI/LF/RDB (28/06/2021): link em (a)
- Tesouro Direto, Cálculo da Rentabilidade Tesouro IPCA: link em (b)
- Decreto 6.306/2007, texto atualizado e anexo (Câmara dos Deputados): links em (c)
- Lei 11.033/2004 (Câmara dos Deputados): link em (c)
- BCB SGS, séries 12, 433 e 4389: https://api.bcb.gov.br/dados/serie/bcdata.sgs.{n}/dados
- BCB Dados Abertos, limite de 10 anos: link em I4
- BCB Calculadora do Cidadão, metodologia e ajuda: https://www3.bcb.gov.br/CALCIDADAO/publico/metodologiaCorrigirIndice.do?method=metodologiaCorrigirIndice e https://www3.bcb.gov.br/CALCIDADAO/jsp/ajudaGeralCalCidadao.jsp (sem regra de CDI)
- IN RFB 907/2009, art. 10, § 3º (espelho não oficial): https://www.normaslegais.com.br/legislacao/inrfb907_2009.htm
