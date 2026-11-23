# QA exploratório — F11: Nuvem na AWS

Relatório do `qa-exploratorio`. Roteiro: `docs/QA-F11-ROTEIRO.md`. Este arquivo é escrito por mais de
uma instância, cada uma na sua seção ("Trilha A" e "Trilhas B, C, D"); o coordenador junta a lista de
bugs e o veredito no fim.

## Cabeçalho

- **Data:** 19/11/2026 (os relógios dos contêineres estão em UTC; o Windows está em UTC-3).
- **Branch:** `f11-nuvem-aws`.
- **Commit-base:** `f141590`. **A base é a árvore de trabalho** (a fase não está commitada): o que
  entraria num commit (`git ls-files -co --exclude-standard`, 308 arquivos), copiado para o rascunho do
  QA. O repositório do Matheus não foi alterado (`git status --short` igual antes e depois, sem
  `floci_override.tf`, `.terraform/` com a mesma data).
- **Sem AWS real e sem GitHub.** Nenhuma chamada à AWS real nem ao GitHub (`gh`, `git clone`,
  `ls-remote`, `ghcr.io`). Os únicos acessos externos foram os downloads autorizados abaixo.

### Decisões do Matheus sobre o roteiro (19/11/2026)

1. **Escopo: só os roteiros P1.** Os P2 e P3 entram no relatório como `NÃO EXECUTADO (fora do escopo
   aprovado)`: QA11-03, 06, 10, 14, 15, 16, 17, 19, 21, 24, 27, 32, 34 e 35 (os P2/P3 da lista do
   roteiro). O QA11-35 (higiene local) não é cenário do escopo, mas a limpeza final do roteiro
   ("Limpeza ao fim") é feita do mesmo jeito.
2. **Classificação do que a spec não prevê.** Caso fora da spec que afeta custo (pode deixar algo
   ligado ou cobrando, impedir o `destroy`, criar recurso fora da região verificada, ou uma corrida que
   destrói ou mantém o ambiente) é `BUG` (média ou alta). Os demais casos fora da spec são `RISCO`.
3. **Autorizado:** duas instâncias em paralelo; downloads fixados e necessários
   (`hashicorp/terraform:1.17.0`, `mcr.microsoft.com/dotnet/runtime-deps:8.0`, provider `aws` 6.73.0,
   restore do NuGet), sem instalar nada na máquina; leitura de documentação pública da AWS.
4. **Não autorizado:** ligar a importação do BC (`IndexRatesImport__Enabled=false`). A imagem da API,
   quando necessária, é montada a partir da árvore atual.
5. **Decisão 7 do roteiro** (backend S3 no Floci, QA11-14) não se aplica: QA11-14 é P2.

## Ambiente

### O que foi preparado (instância única, 19/11/2026)

Docker Desktop (engine Linux, containerd snapshotter) com **3,47 GiB de memória e 8 CPUs** visíveis
(`docker info`: `memoria=3722055680 cpus=8`). O roteiro pede pelo menos 6 GB livres para duas trilhas
com Floci e RDS filhos: **a máquina tem menos**. Mesmo assim, dois `apply` em paralelo coube (pico
amostrado de 2,2 GiB somando todos os contêineres, nenhum evento de OOM), mas sobra pouca margem para
somar a trilha D (SDK .NET, PostgreSQL, API) por cima de dois `apply` (ver QA11-00).

Portas 4566, 4567, 25432 e 28080 estavam livres no início e no fim. `fixedincome-api` (8080) e
`fixedincome-db` (5432) estavam no ar há 8 h e continuam do mesmo jeito (`StartedAt` e `RestartCount=0`
inalterados); a API respondeu 200 em `/health` no fim.

| Item | Valor |
|---|---|
| Imagem de execução (local) | `qa11-runner:tmp`, id `4d871da8f905`, digest `sha256:4d871da8f9052de61b2b6c7f9c79de3093edbba3ac201c35021dfece0eb4bfdf`, 827 MB |
| Conteúdo | Terraform 1.17.0 `linux_amd64`, jq 1.7.1, aws-cli 2.41.1, bash 5.2.15, GNU coreutils 8.32, awk, sed, grep, curl. **Sem** `tar`, `git`, `ps`, `pgrep` |
| Base da imagem | `amazon/aws-cli@sha256:538a9a80bc7c78e3228bc65100404a5dbd89ad812a547004c05ea20109543432` (no cache como `latest`) |
| Terraform copiado de | `hashicorp/terraform:1.17.0` (baixado; lista de manifestos `sha256:4d1b496a256f2989bccdb04b9104a4ede97234b10fba90259e173e905ec4fb58`, `linux/amd64` = `sha256:9f98ed860e9ddc549159bbcdc1b15945b60876ba68d631e98db7f258e26be87a`). Fica só no cache do build, não aparece em `docker images` |
| jq copiado de | `ghcr.io/jqlang/jq@sha256:096b83865ad59b5b02841f103f83f45c51318394331bf1995e187ea3be937432` (1.7.1, no cache) |
| Floci | `floci/floci@sha256:a0fbf944d7f338cc0856f1ef83831ea749ff2be71956eb7a7fce08fc21458e19` (2.5.0, no cache, sem tag) |
| PostgreSQL do QA | `postgres:16@sha256:f9a7b9ebf6f56201f949db745e47bc423c0f0f8294bafe991cc10bf74b029657` (no cache) |
| Filho RDS do Floci | `postgres:16-alpine` (`sha256:1d5b02d3a9941fbd52437db4107f8c52d6297dac553ca1e5c299ebb3756b4857`, já estava no cache) |
| `.NET` (trilha D) | `mcr.microsoft.com/dotnet/sdk:8.0` (no cache); `mcr.microsoft.com/dotnet/runtime-deps:8.0` **baixado agora** (`sha256:54c5e25d41d6df433e520750bacfa4eed1f664140cd36b37c189dd6c6df33d79`, 180 MB). Nenhuma imagem da API foi montada |
| Provider | `hashicorp/aws` 6.73.0 `linux_amd64` (772 MB descompactado), no volume `qa11a-plugins` e copiado para `qa11b-plugins` |

**Baixado com autorização e que ficou no cache do Docker** (para o Matheus remover se quiser):
`hashicorp/terraform:1.17.0` (cache do build), `mcr.microsoft.com/dotnet/runtime-deps:8.0` e os
volumes de cache do provider `qa11a-plugins` e `qa11b-plugins` (772 MB cada). Os dois volumes foram
**mantidos de propósito**: o primeiro `terraform init` baixa o provider (1 min 23 s), com o cache cai
para 13 s.

**Rascunho do QA** (fora do repositório): `C:\Users\matheuss\AppData\Local\Temp\claude\55e80b08ad8be6d5\68d52ecf-8aa3-43af-8f09-4aafd899dfa1\scratchpad\qaf11`
(no Git Bash: `/c/Users/matheuss/AppData/Local/Temp/claude/55e80b08ad8be6d5/68d52ecf-8aa3-43af-8f09-4aafd899dfa1/scratchpad/qaf11`):

- `arvore/`: a cópia da árvore de trabalho (sem `.terraform`, sem `floci_override.tf`, sem CRLF).
- `passos/`: os scripts auxiliares do QA (`init.sh`, `sobe.sh`, `desce.sh`, `segredo.sh`), os 12
  passos do `infra-aws.yml` extraídos (`wf-*.sh` e `artefatos.sh`, nenhum vazio, os passos "Ler
  expires_at e image_sha do state" do `destruir` e do `limpar` são idênticos) e os `shims/`. São do QA,
  **não** da fase. As fixtures do QA11-20 (`passos/fixtures/`) ainda não foram escritas.
- `lib.sh`: as funções do roteiro já corrigidas (`sobe_floci`, `sobe_runner`, `rx`/`ra`/`rb`/`rc`,
  `wf`). **Use `source lib.sh`** em vez de copiar a Preparação 3 do roteiro (ver os defeitos abaixo).
- `antes-ps.txt`, `antes-images.txt`, `antes-volumes.txt`, `antes-networks.txt`,
  `git-status-antes.txt`, `terraform-dir-antes.txt`: a foto do estado inicial, para a limpeza final.
- `saidas/`: evidências do QA11-00.

**Como usar** (Git Bash):

```bash
source "$QA/lib.sh"          # define REPO, QA, WQA, ENVS, sobe_floci, sobe_runner, rx, ra, rb, rc, wf
sobe_floci a && sobe_runner a  # trilha A (4566); `b` para a trilha B (4567); `sobe_runner c` sem rede
ra 'bash /passos/init.sh'      # primeira vez em cada runner (override, init, execution role)
```

Notas: `sobe_runner` já copia a árvore para `/work/repo` (o `lib.sh` usa o caminho do Windows no
`docker cp`). Os volumes `qa11a-work`/`qa11b-work` foram removidos; cada `sobe_runner` os recria.

### Defeitos do roteiro encontrados na preparação (não são da fase)

Registrados aqui para quem executar as próximas trilhas. Nenhum afeta a fase.

1. **`sobe_floci` nunca vê o Floci "no ar".** A Preparação 0 faz `export MSYS_NO_PATHCONV=1`, e com ela
   o `curl` do Git Bash (curl.exe nativo do Windows) devolve **23** em `curl -s -o /dev/null ...`
   (tenta gravar em `\dev\null`). O laço `until curl -s -o /dev/null ...` roda até o limite (90 s) e
   imprime "Floci a não respondeu", embora o Floci esteja pronto em cerca de 1 s. Reproduzir:
   `MSYS_NO_PATHCONV=1 curl -s -o /dev/null http://localhost:4566/; echo $?` (23) contra
   `curl -s http://localhost:4566/ > /dev/null` (0). Correção usada no `lib.sh`: redirecionar com `>`.
   Como a função vem encadeada com `&&`, o `sobe_runner` que vem depois do `&&` não roda; se vier em
   outra linha, roda mesmo assim.
2. **`docker cp "$QA/arvore/." ...` falha** com `MSYS_NO_PATHCONV=1` (o `docker.exe` recebe o caminho
   POSIX `/c/Users/...` e responde `GetFileAttributesEx c:\c: The system cannot find the file
   specified`). Usar o caminho do Windows (`"$WQA\\arvore\\."`), como o `lib.sh` faz.
3. O `docker run ... qa11-runner:tmp` do roteiro não traz `tar`, `git`, `ps` nem `pgrep`. Nada do
   roteiro depende deles no runner (o QA11-19 roda no Git Bash e o `kill`/`wait` do QA11-13 são
   *builtins*), mas quem precisar de `ps` não o encontra.
4. A primeira linha do `docker build` mostra `CACHED` para os `COPY --from`: o Terraform e o jq já
   estavam no cache do build; o digest de `hashicorp/terraform:1.17.0` acima foi lido do registry
   (`docker buildx imagetools inspect`, só leitura).

## QA11-00 — O Floci, os contêineres filhos e a linha de base

**Resultado: OK.** Veredito: **as trilhas A e B podem rodar `apply` com RDS ao mesmo tempo**, cada uma
no seu Floci, **desde que**: (1) a trilha D (SDK .NET, bundle) e o resto do trabalho pesado não somem
uma terceira carga durante os dois `apply` (ver memória abaixo); (2) cada trilha use contêineres
`qa11a-*`/`qa11b-*` e remova os filhos pelo nome (`floci-aws-*`).

Ciclo completo: init, `sobe.sh`, `verificar-zerado.sh` com ambiente, `destroy` (`desce.sh`),
`verificar-zerado.sh` final. Tudo contra o Floci A, em seguida o mesmo ciclo em A e B simultaneamente.

### Resposta 1: os filhos do Floci usam nome ou porta fixa?

**Não. Nem nome fixo nem porta fixa no host.** Nenhum filho publica porta no host (`PortBindings` vazio
em todos), então **não há tentativa de usar a 5432** (do `fixedincome-db`) nem a 8080.

| Filho | Nome observado | Imagem | Rede e porta |
|---|---|---|---|
| RDS | `floci-aws-rds-db-<24 hex em maiúsculas>-<6 hex>`. Os 24 hex mudam a cada criação (`4DBC78D1...`, `564809458...`, `7740B3C9...`, `F51EC327...`, `3C8ABB01...`); **não** derivam do identificador da instância (o `qa11-db` criado nos dois Flocis gerou nomes diferentes) | `postgres:16-alpine` | `bridge`, IP dinâmico (172.17.0.3, .4, .5), `5432/tcp` apenas exposta. O Floci devolve o *endpoint* `<IP do próprio Floci>:7001`: a porta 7001 existe **dentro** do namespace de rede de cada Floci |
| ECS (tarefa) | `floci-aws-ecs-<id da tarefa, 32 hex>-<nome do contêiner>` (ex.: `floci-aws-ecs-ad88dab3...-x`) | a da task definition | `bridge`, IP dinâmico, portas da task definition só expostas (a `containerPort` 8080 **não** foi publicada: `PortBindings` vazio) |
| Rede da VPC | `floci-aws-vpc-4566-us-east-1-<vpc-id>`, criada por VPC e removida no `destroy` (log do Floci: "Removed Docker network ...") | | O `4566` do nome é a porta *interna* do Floci, igual nos dois Flocis; o `vpc-id` é aleatório. Depois do ciclo, `docker network ls` não tinha nada novo |

Rótulos dos filhos criados por `apply`: `floci=true`, `io.floci.service=rds|ecs`,
`io.floci.resource-id=<identificador>`. Os filhos de ECS trazem também `io.floci.owner=4566` e
`floci_owner_port=4566`, **iguais nos dois Flocis** (é a porta interna). Ou seja, os rótulos não
distinguem o Floci A do B.

### Resposta 2: dois Flocis (4566 e 4567) podem rodar `apply` com RDS ao mesmo tempo?

**Sim, sem colidir entre si nem com o `fixedincome-db`.** Evidência (trilha A e B, `sobe.sh` lançados
no mesmo instante, ambos com o RDS `renda-fixa-api`, mesmo nome de recurso e mesmo `expires_at`):

- os dois terminaram `sobe: ok`, trava `APROVADO 27 itens`, "nenhum RDS nem versão de secret será
  recriado";
- dois filhos RDS com nomes distintos (`...564809458...-dc847f` e `...7740B3C9...-14346a`), IPs
  distintos, nenhum com porta no host; Floci A em 172.17.0.2 e Floci B em 172.17.0.3, ambos com
  *endpoint* na porta 7001 (sem conflito: namespaces de rede diferentes);
- depois, `run-task` simultâneo nos dois: dois filhos ECS com nomes distintos;
- `fixedincome-db` e `fixedincome-api`: `Status` e `StartedAt` inalterados, 5432/8080 intocadas;
- o isolamento de estado entre os Flocis é total: cada um listava só o seu RDS e o seu cluster.

**Memória (limite real do paralelismo).** Cada runner chegou a 0,6 a 1,2 GiB (o Terraform com o provider
`aws` é o que pesa), os Flocis a 60 a 190 MiB, cada filho RDS a 35 a 65 MiB. Pico amostrado somando
todos os contêineres durante os dois `apply`: **cerca de 2,2 GiB de 3,47 GiB**, sem evento `oom`. Sobra
cerca de 1,2 GiB. A trilha D, que monta a imagem de migração e usa o SDK .NET, não deve coincidir com os
dois `apply`.

**Efeito do paralelismo no tempo.** Os `apply` simultâneos levaram **3 min 32 s e 3 min 34 s** (sozinho:
1 min 51 s), o `destroy` simultâneo **2 min 36 s** cada (sozinho: 2 min 04 s) e a verificação **49 s**
cada (sozinha: 41 s). O gargalo é CPU/IO do Docker Desktop (muito `sys` nos dois). O paralelismo
ainda compensa (ciclo de A e B em cerca de 7 min contra cerca de 9 min em fila), mas **cada
`apply` fica quase 2 vezes mais lento**: se o roteiro estimou a trilha B com `apply` curtos, conte com
isso.

### Resposta 3: quanto dura um ciclo de referência?

Floci A sozinho, nesta máquina (Docker Desktop, 8 CPUs, 3,47 GiB), com cache de provider vazio na
primeira execução:

| Etapa | Tempo | Resultado |
|---|---|---|
| `init.sh` (1ª vez, baixa o provider 6.73.0, 772 MB) | 1 min 23 s | ok |
| `init.sh` (com o cache do provider) | 13 s | ok (runner B) |
| `sobe.sh` (plan, trava, bloqueio, apply) | **1 min 51 s** (112 s de relógio) | trava `APROVADO 27 itens`, bloqueio sem recriação |
| `verificar-zerado.sh` com o ambiente no ar | **42,6 s**, saída **1** | `REPROVADA (22 achado(s))`: cluster, serviço, ALB, target group, RDS, secret, 2 log groups (um deles `/aws/rds/instance/renda-fixa-api/error`, o D1 do Floci) e 12 `recurso-com-tag` |
| `desce.sh` (destroy + artefatos D1/D2) | **2 min 04 s** (125 s) | `Destroy complete! Resources: 27 destroyed.`; D1 e D2 removidos |
| `verificar-zerado.sh` final | **40,8 s**, saída **0** | `APROVADA (nada sobrando na região us-east-1)` |
| Contêineres sobrando após o `destroy` | nenhum | `docker ps -a` igual ao de antes do `apply` |

Ciclo `sobe` + verificação + `desce` + verificação, sem o `init`: **cerca de 5 min 40 s**. Bate com a
VALIDACAO-F11 (critérios 2 e 4): trava aprovada, `apply` em cerca de 2 min, 22 achados, 27 recursos
destruídos, verificação final 0, nenhum filho sobrando.

**Implicação para o tempo dos roteiros:** `verificar-zerado.sh` leva **cerca de 40 s mesmo com o Floci
vazio** (são dezenas de chamadas da CLI, cada uma com a partida do `aws`; `user` de 32 s). Cenários que
criam resíduos e rodam a verificação duas vezes por tipo (QA11-08 tem 17 tipos, QA11-09 mais uns 10
casos) gastam sozinhos cerca de 25 a 40 min só nas verificações, fora o tempo de criar o recurso. As
estimativas do roteiro (45 min cada) ficam apertadas; considere uma hora.

### Achados colaterais (ainda não classificados como bug da fase)

- **Remover o Floci não remove os filhos.** `docker rm -f qa11b-floci` deixou o filho RDS vivo (`Up`)
  e o volume dele (`floci-aws-rds-db-...`). Recriar o Floci na mesma porta **não** o recolhe nem o
  reconhece (sem *reaper* no start) e **não** mexe nos filhos do outro Floci (o RDS do Floci A seguiu
  `available`). Consequências: (a) o QA11-28 ("Floci B recriado") é seguro com a trilha A em pleno
  `apply`, mas deixa um filho RDS órfão da trilha B que consome memória até ser removido pelo nome
  (`docker rm -f floci-aws-rds-db-<...>` e `docker volume rm` do volume homônimo); (b) a "Limpeza ao
  fim" do roteiro não menciona esses volumes (`floci-aws-rds-db-*` e três volumes anônimos criados durante o QA): rodar
  `docker volume ls -q | sort | comm -13 antes-volumes.txt -` e remover o que for novo e não estiver em
  uso. Isso é do emulador, não da fase; para a fase, o que vale é o README de "Validação local" (job
  `validar` remove só os contêineres de nome `floci`; os filhos `floci-aws-*` contêm "floci" no nome).
- **Floci "no ar" pela porta do host.** O Floci responde dentro do contêiner em cerca de 1 s, mas via
  `-p` do Docker Desktop do Windows a primeira resposta no host nem sempre é imediata. Se
  o `curl` falhar de verdade (e não por causa do defeito 1 acima), esperar um pouco mais antes de
  concluir que o Floci não subiu.
- O filho de ECS (`run-task`) subiu com a imagem `postgres:16@sha256:f9a7...` e `entryPoint
  ["sleep","600"]`; `parar-migracao.sh` só para tarefas da família `renda-fixa-migrator`, por isso
  não tocou na tarefa do QA (`qa11-td`), parada à mão com `stop-task` (`STOPPED`, filho removido em
  poucos segundos).

**Limpeza do QA11-00:** `qa11a-runner`, `qa11a-floci`, `qa11b-runner`, `qa11b-floci`, os filhos
`floci-aws-*`, os volumes `qa11a-work`, `qa11b-work`, os 3 volumes anônimos criados durante o QA e os 2
`floci-aws-rds-db-*` foram removidos. Ficaram `qa11a-plugins` e `qa11b-plugins` (cache do provider) e a
imagem `qa11-runner:tmp`. `docker ps -a`, `docker network ls` e os volumes fora esses dois são iguais
aos de antes. Recursos criados nos Flocis (todos `qa11`/`renda-fixa-api`, somem com o contêiner): o
ambiente completo (27 recursos, nos dois Flocis), `qa11-td`, `qa11-db` (só nos dois Flocis, removidos
com eles).

## Trilha A (instância 1)

Instância 1, Floci A (porta 4566), contêineres e volumes `qa11a-*`. Rodada de 20/11/2026 (a anterior foi
interrompida por limite de uso da API antes de gravar resultados; nada se perdeu, só se refez o
ambiente). Roteiros P1 desta trilha: QA11-05 (parte A), 07, 11, 12, 13, 23, 25, 26 (e, se sobrar tempo,
QA11-33). O roteiro não marca nenhum outro P1 como trilha A (o QA11-00 já foi feito na preparação).
Auxiliar do QA desta rodada: `passos/mut07.sh` (aplica um `sed` na cópia, planeja, lista os `delete`,
roda `bloquear-recriacao.sh` e `trava-custo.sh` e restaura o arquivo).

### QA11-07 — Recriação forçada por mudança de operador

**Resultado: OK, com um RISCO (vpc_cidr, abaixo).** Ambiente no ar no Floci A (`sobe.sh`, 1 min 50 s,
trava `APROVADO 27 itens`), copiado para `/work/r` (com state e `.terraform`). Cada mudança foi aplicada
com `sed` na cópia, planejada com `-var expires_at=+3h`, e o arquivo foi restaurado (plano final em
`/work/r`: só o update do cluster e as 3 diferenças conhecidas de `referenced_security_group_id`).
Comando de cada linha: `bash /passos/mut07.sh <rótulo> <arquivo> '<sed>'`, que roda
`terraform show -json x.tfplan | bash scripts/bloquear-recriacao.sh`.

| Mudança (na cópia) | Plano | `delete` no plano | `bloquear-recriacao.sh` | `trava-custo.sh` |
|---|---|---|---|---|
| `username` do banco (`fixedincome` para `qa11user`) | 1 add, 4 change, 1 destroy | `aws_db_instance.this` (delete+create) | **1**, `REPROVADO aws_db_instance module.database.aws_db_instance.this: o plano recria o recurso` | 0 (27 itens) |
| `db_name` | idem | `aws_db_instance.this` | **1**, idem | 0 |
| `storage_encrypted = false` | idem | `aws_db_instance.this` | **1**, idem | 0 |
| nome do secret (`renda-fixa-api/qa11`) | 4 add, 5 change, 4 destroy | secret, **versão do secret**, as 2 task definitions | **1**, `REPROVADO aws_secretsmanager_secret_version ...connection_string: o plano recria o recurso` | 0 |
| `name` do módulo `database` (`renda-fixa-qa11`) | 1 add, 5 change, 1 destroy | só `aws_db_subnet_group.this`; o RDS muda no lugar (identifier) | 0 (`nenhum RDS nem versão de secret será recriado`) | 0 |
| `vpc_cidr` do módulo `network` (`10.30.0.0/16`) | 15 add, 7 change, 15 destroy | VPC, 2 subnets, 3 SGs, rota, 2 associações, 5 regras de SG e o target group; o RDS **não** é substituído | 0 | 0 |
| `-replace=module.database.aws_db_instance.this` | ok | RDS | **1** | n/a |
| `-replace=...aws_secretsmanager_secret_version.connection_string` | ok | versão | **1** | n/a |

Esperado (S5, README "Recriação do banco ou do secret"): saída 1 sempre que RDS ou versão do secret forem
substituídos, sem valores, com orientação de `destruir` e depois `subir`. **Obtido: bate nos 5 casos de
substituição.** A saída 1 traz só o endereço e o tipo (nenhuma senha) e a mensagem diz: "Rode o workflow
com acao = destruir (o job limpar já destrói o ambiente depois desta falha: espere ele terminar) e depois
rode o subir de novo." Coerente com o README (linha 663 e seguintes) e com os passos do workflow (o
`bloquear-recriacao.sh` roda nas linhas 180, 424 e 524 do `infra-aws.yml`). Esse `limpar` automático
depois da falha é do workflow e fica para o P8.

**RISCO R-A1 (vpc_cidr).** Mudar o `vpc_cidr` (que é `default` do módulo, não variável da raiz, então só
vale numa edição deliberada de código) produz um plano que **recria a VPC e as subnets com o RDS e o ALB
no ar**, e nem a trava nem o bloqueio reprovam (a trava aprova porque não há recurso novo fora da lista
positiva, e o RDS é "update in-place" do SG e do subnet group). No Floci o plano é gerado; **não apliquei**
essa mudança. Na AWS real, uma VPC e subnets em uso por um RDS e um ALB não são apagadas
(`DependencyViolation`), então o `apply` falharia no meio, com state parcial, caindo no roteiro de "Lock
preso"/limpeza. Não confirmado (depende da AWS; vai para o P8). Fora do escopo da spec (S5 só fala de RDS e
secret) e sem custo extra por si, por isso `RISCO` e não `BUG`; sugestão: incluir a VPC (e `aws_subnet`)
na lista de substituições que o `bloquear-recriacao.sh` reprova, ou documentar no README.

Dados criados: o ambiente de 27 recursos no Floci A (removido no fim); `/work/r`.

### QA11-11 — Subir duas vezes seguidas (prorrogar)

**Resultado: OK.** Passos do roteiro, no Floci A, sobre o ambiente do QA11-07 (E1 = `2026-11-20T05:29:55Z`,
criado pelo `sobe.sh` de 03:29 UTC; E2 = `2026-11-20T08:37:20Z`, +5 h).

| Verificação | Antes (E1) | Depois (E2) | Esperado |
|---|---|---|---|
| `terraform output -raw expires_at` | `2026-11-20T05:29:55Z` | `2026-11-20T08:37:20Z` | muda |
| tag `expires_at` do cluster (`ecs describe-clusters --include TAGS`) | `05:29:55Z` | `08:37:20Z` (e `managed_by`, `project` intactas) | muda |
| `segredo.sh` (hash, tamanho, versões) | `sha256=8cec682706ebaaa2 tamanho=109`, uma versão `AWSCURRENT` | **idênticos** | não muda |
| `psql ... select 1` com a senha lida do secret | `1` | `1` | abre antes e depois |
| plano da prorrogação (`jq` nos não-`no-op`) | `update aws_ecs_cluster.this` mais 3 updates de `referenced_security_group_id` | n/a | só o cluster, mais a diferença conhecida do Floci |
| `grep -c` da senha e da connection string no `floci.tfstate`; `Password=` | 0, 0, 0 | n/a | 0 |
| `terraform show -json` e `terraform show -json p2.tfplan`, senha | 0 e 0 | n/a | 0 |
| `plan -detailed-exitcode` depois do segundo `apply` | `plan=2`, **só** os 3 updates conhecidos (`alb_to_task`, `rds_postgres`, `task_http`) e `Plan: 0 to add, 3 to change, 0 to destroy` | n/a | nada além da diferença conhecida |

Trava `APROVADO 27 itens` e "nenhum RDS nem versão de secret será recriado" nas duas execuções. O segundo
`sobe.sh` levou **24 s** (contra 1 min 50 s do primeiro). Nada de recriado; a promessa da D6 e do README
("prorrogar não reescreve senha nem secret") vale no Floci. (A senha foi lida para uma variável do shell
e descartada; nada foi impresso.) O que só a AWS confirma (a `password_wo_version` realmente não reescrever
a senha no RDS real) vai para o P8.

### QA11-12 — `destroy` com marcações, state ausente e state parcial

**Resultado: OK nos cinco itens ((c) com ressalva de emulador).**
Tudo no Floci A, `/work/repo/infra/aws`, ambiente criado por `bash /passos/sobe.sh`.

**(a) Marcações.** `sobe.sh` (expires +5 h) e `bash /passos/desce.sh 1970-01-01T00:00:00Z
0000000000000000000000000000000000000000` (as marcações do `destruir`): `Destroy complete! Resources: 27
destroyed.`, artefatos D1 e D2 do Floci removidos, `desce=0`, depois `bash scripts/verificar-zerado.sh`
saiu **0** (`APROVADA (nada sobrando na região us-east-1)`). Ciclo destroy+verificação 2 min 36 s.

**(b) State sumido** (`mv floci.tfstate /work/state-escondido.tfstate`, ambiente de pé).
`terraform destroy -auto-approve ... -var expires_at=1970-01-01T00:00:00Z` saiu **0** com `No changes.
No objects need to be destroyed` / `Destroy complete! Resources: 0 destroyed.`, e
`VERIFICAR_ESPERA_SEGUNDOS=10 bash scripts/verificar-zerado-apos-destroy.sh` saiu **1** nas duas
tentativas, listando as 22 linhas do ambiente inteiro (cluster, serviço, ALB, target group, RDS, secret,
2 log groups, 12 `recurso-com-tag`), com o `AVISO resíduo logo depois do destroy: repetindo a
verificação uma vez, em 10s`. Tempo total da verificação com repetição: **1 min 49 s** (com 90 s reais
de espera seria cerca de 3 min 10 s). Com o state de volta e `desce.sh`: 27 destruídos e verificação 0.
É o caso que a camada 4 existe para pegar, e pegou (esperado: 1, atingido).

**(c) RDS fora do state** (`terraform state rm module.database.aws_db_instance.this`, depois `desce.sh`):
`Destroy complete! Resources: 26 destroyed.`, `desce=0` (o `destroy` **não falha** no Floci: ele apagou o
`aws_db_subnet_group` com o RDS vivo; na AWS real o subnet group em uso seria recusado, então o
`destroy` falharia e o `destruir` ficaria vermelho, o que também sinaliza o problema; não confirmado, P8).
`verificar-zerado.sh` saiu **2**: `ERRO rds describe-db-instances: DBSubnetGroupNotFoundFault` e
`NAO CONCLUIDA (0 achado(s), 1 erro(s) de chamada)`: fail closed, correto (não aprova sem conseguir
verificar). Na tentativa seguinte (cerca de 1 min depois, sem eu apagar nada: o `delete-db-instance` pela
CLI também respondeu `DBSubnetGroupNotFoundFault`) a verificação saiu 0 e o contêiner filho já não existia:
o Floci removeu a instância sozinho ("DB instance renda-fixa-api deleted" no log) e a janela em que
`describe-db-instances` falha é um comportamento do emulador. **Não dá para dizer, com o Floci, que o
destroy+verificação sempre listam o RDS órfão**: o que ficou provado é que, com o RDS fora do state, ou o
destroy falha, ou a verificação sai 1 ou 2 (nunca 0 com o RDS vivo). Risco de RDS vivo e verificação 0
não observado. Vai para o P8 repetir na AWS real (apagar o RDS do state de verdade).

**(d) Cluster ECS fora do state** (`terraform state rm module.app.aws_ecs_cluster.this`, `desce.sh`):
`Destroy complete! Resources: 26 destroyed.` (`desce=0`; o serviço foi destruído, o cluster ficou) e
`verificar-zerado.sh` saiu **1** com 2 achados: `RESIDUO ecs-cluster arn:...:cluster/renda-fixa-api` e
`RESIDUO recurso-com-tag arn:...:cluster/renda-fixa-api`. Limpeza pela CLI (passo 5 do README):
`aws ecs delete-cluster --cluster renda-fixa-api` devolveu `INACTIVE` e a verificação saiu 0. O
roteiro do README (passos 3 a 6) funciona para esse órfão. Observação sobre o README: no `destroy` sem o
output, a mensagem do Terraform ("Destroy complete! Resources: 26 destroyed.") não menciona que sobrou
algo; só a verificação mostra (por isso o passo 4 do README é essencial, e ele existe).

**(e) O que a `validation` de `expires_at` aceita no destroy.** Com o state do ambiente completo no ar
(`plan -destroy -refresh=false -var-file=environments/floci.tfvars -var expires_at=<v>`), a regex do passo
"Ler expires_at e image_sha do state" do `destruir` (`^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:.]+(Z|[+-][0-9]{2}:[0-9]{2})$`)
e o `date -u -d` do GNU, para cada valor:

| Valor | regex do workflow | `date -u -d` | `terraform plan -destroy` |
|---|---|---|---|
| `2026-11-19T10:00:00Z`, `...00.5Z`, `...+03:00`, `...-00:00`, `...00.123456789Z`, `2026-11-19T10:00Z` | aceita | ok | saída 0 |
| `amanhã` | recusa | falha | saída 0 |
| `2026-13-45T10:00:00Z`, `2026-11-19T10:00:00.Z`, `2026-11-19T24:00:00Z`, `...+25:00` | aceita | falha | saída 0 |

**A `validation` do Terraform não barra nada no modo destroy**: nenhum valor, nem `amanhã`, é recusado por
`plan -destroy` (nem por `terraform destroy`: `No changes. No objects need to be destroyed`, saída 0). Como
controle, o `plan` normal (`subir`) com `amanhã` e com `2026-11-19T10:00Z` falha com `Error: Invalid value
for variable`. Logo, não existe formato aceito pelo passo do workflow e recusado pela validation no
`destruir`/agendado/`limpar` (seria BUG alta): nenhum fica vermelho para sempre por isso. Notas: (1) o
comentário de `variables.tf` ("passado é aceito de propósito: o job destruir usa uma marcação antiga")
descreve uma proteção que o Terraform já dá ao destroy por si; (2) valores que a regex aceita mas o `date`
rejeita (`2026-13-45T10:00:00Z`, `T24:00:00Z`) caem no `else` do "checar o TTL" e contam como vencido
("expires_at ausente ou inválido... será destruído"): fail safe. Uma execução anterior, com o state vazio,
também saiu 0 para todos os valores (não prova nada sozinha: com state vazio o destroy nem avalia a
configuração; por isso a tabela acima foi refeita com o state completo).

### QA11-23 — "Ler o state" do agendado, com state real

**Resultado: OK, com 1 RISCO (state de 0 bytes) e 1 defeito do roteiro.** Passo `wf-ler-state.sh` (o
bloco `run:` do YAML, sem alteração) rodado com `wf a "" wf-ler-state.sh /work/ls/infra/aws` numa cópia
(`/work/ls`) do backend local do override do Floci (`floci.tfstate`).

| Situação | Como montei | `terraform state list` | Passo "Ler o state" | Esperado |
|---|---|---|---|---|
| nunca houve state | `rm -f floci.tfstate*` | saída 1, texto `No state file was found!` (+ 3 linhas de ajuda) | `codigo=0`, `ativo=false`, "State vazio: não há ambiente ativo." | `ativo=false`: **OK** (o texto que o `grep` procura é o que o Terraform 1.17.0 escreve) |
| depois de um `destroy` | state de um `desce.sh` (arquivo de 1656 bytes, sem recursos) | saída 0, vazia | `ativo=false` | OK |
| ambiente no ar | `sobe.sh` | 30 endereços (27 recursos mais 3 data sources) | `ativo=true`, "O state tem 30 recurso(s)." | OK (a contagem inclui data sources) |
| state truncado | `head -c $(( n/2 ))` num state de 2180 bytes (metade: 1090; ver a nota do defeito do roteiro) | saída 1, `Failed to load state: 2 problems: ... could not be parsed as JSON: syntax error at byte offset 1090` | `codigo=1`, `::error::Falha ao ler o state do Terraform:` e as 5 linhas do erro, sem `ativo` no `GITHUB_OUTPUT` | `codigo=1`: OK |
| state que não é JSON (`echo lixo`) | `echo lixo > floci.tfstate` | saída 1, `syntax error at byte offset 1` | `codigo=1`, `::error::...`, `GITHUB_OUTPUT` vazio | OK |
| só um data source no state | `terraform apply -target=data.aws_caller_identity.current` (`Apply complete! Resources: 0 added`) | 1 endereço (`data.aws_caller_identity.current`) | `ativo=true`; `wf-ttl.sh`: `::warning::expires_at ausente ou inválido no state: o ambiente conta como vencido.`, `vencido=true`, marcações `1970-01-01T00:00:00Z` e `0000...` | o agendado rodaria `parar-migracao.sh` e um `destroy` de 0 recursos (inofensivo) e a verificação: OK |
| lock preso por um `plan` em segundo plano | `terraform plan ... &` e, 3 s depois, `terraform state list` | 30 endereços, sem esperar (12 s no relógio, CPU) | n/a | `state list` **não** pega o lock: lê sem esperar. Não confirmei que o plano já segurava o lock nesse instante: observação, não prova |

**RISCO R-A2 (state de 0 bytes lido como "sem ambiente").** `: > floci.tfstate` (arquivo vazio, 0 bytes):
`terraform state list` responde `No state file was found!` e **saída 1**, e o passo dá `ativo=false` e
`codigo=0`. Ou seja, um state **vazio** (corrompido ao ponto de ficar vazio) é lido como "nunca houve
ambiente", e o agendado só roda a verificação de zerado, que lista o resíduo e reprova (camada 4). Não
é fail open de verdade porque a verificação pega o que ficou (e o agendado fica vermelho), mas o
vermelho dirá "resíduo na conta", sem sugerir que o state foi perdido. Com o S3 real, um objeto de 0
bytes é raro (o versionamento guarda o anterior); registro para o P8 (QA11-14 é P2 e não foi feito).
Classificação: `RISCO` baixo (não afeta custo, pelo efeito da verificação).

**Defeito do roteiro (não é da fase).** O roteiro manda `truncate -s 50% floci.tfstate`; o GNU
`truncate` 8.32 não aceita porcentagem (`truncate: Invalid number: '50%'`) e o arquivo ficou intacto
(o passo leu o state inteiro: `ativo=true`). Refiz com `head -c $((n/2))`, que é o resultado da
linha "state truncado" (a base foi o state de 2 KB do cenário de data source, não o do ambiente completo).

### QA11-25 — Corridas entre o agendado e um `subir`

**Resultado: BUG média (B-A1, prorrogar um ambiente já vencido durante a janela do agendado), mais 1 RISCO
(R-A3, resíduo falso no começo de um `subir`). (b) OK.** Os passos do agendado são os blocos `run:` do
YAML (`wf-ler-state.sh`, `wf-ttl.sh`) e o `destroy` do passo "Destruir" com `-lock-timeout=5m` (o real),
`parar-migracao.sh` antes. A guarda do `gh run list` fica de fora (é o QA11-24, P2): o roteiro quer
justamente a janela sem ela. Estrutura do workflow que torna a corrida possível: o agendado tem o grupo de
concurrency `infra-aws-agendado`, o `subir`/`destruir` têm `infra-aws-manual` e o `limpar`
`infra-aws-limpar` (`infra-aws.yml`, linhas 268, 619, 703 e 776): nada os serializa; a serialização é só o
lock do state e a guarda do início (que olha a fila uma vez).

**(a1) `subir` do zero, o agendado lendo durante a fase 0** (`rm -f floci.tfstate*`; `sobe.sh` em segundo
plano; leitura do state a cada ~30 s). Sequência obtida (UTC; cada linha é `terraform state list | wc -l`
e `terraform output -raw expires_at`):

```
04:24:37 recursos=0  expires=               (sem state)
04:25:41 recursos=0  expires=
04:26:06 recursos=0  expires=2026-11-20T05:24:37Z   (o output já está no state, ainda sem recursos)
04:26:41 recursos=25 expires=2026-11-20T05:24:37Z
04:28:00 recursos=30 expires=...                      (sobe: ok)
```

A condição que o roteiro procurava ("há recursos e `expires` ausente") **nunca ocorreu**: o output
`expires_at` só depende da variável e o Terraform o grava no state **antes** do primeiro recurso. Logo,
num `subir` do zero o agendado nunca vê `vencido=true` por falta do output: não destrói o ambiente que
está nascendo. **Mas** existe a janela de cerca de 35 s do Floci (e, na AWS real, até a primeira
persistência do state, com o RDS levando minutos, cerca de 20 s depois do primeiro recurso criado) em
que o state tem **0 recursos** e a conta já tem recursos: `wf-ler-state.sh` dá `ativo=false` e o agendado
roda só a verificação de zerado, que lista resíduo e **reprova** o ciclo (vermelho falso, sem destruir
nada). Isso é **RISCO R-A3 (baixo)**: um e-mail/ícone vermelho do agendado durante um `subir` legítimo que
a guarda não pegou; não afeta custo. (Não rodei a verificação dentro da janela; é a consequência do
`ativo=false` mais o que o QA11-00 mostra que a verificação faz com recurso de pé.)

**(a2) `subir` para prorrogar um ambiente que já venceu.** Ambiente no ar com `expires_at` no passado
(`sobe.sh 2026-11-20T00:00:00Z`, aceito pela validation); um `subir` (`sobe.sh +1h`) começa às 04:31:50;
**um segundo depois** o agendado lê o state e o TTL:

```
O state tem 30 recurso(s).                                   ativo=true
Ambiente vencido: será destruído.   vencido=true  expires_at=2026-11-20T00:00:00Z   (o prazo novo ainda não foi gravado)
== agendado: destroy real, lock-timeout=5m  04:32:03
Nenhuma tarefa de migração ativa no cluster renda-fixa-api.
destroy=0      Acquiring state lock...    Destroy complete! Resources: 27 destroyed.   (1 min 49 s)
```

O `destroy` do agendado levou o lock **entre o `plan` e o `apply` do `subir`**, destruiu os 27 recursos
(o ambiente que o operador queria prorrogar) e o `apply` do `subir` falhou, depois de
`Trava de custo: APROVADO 27 itens` e `Recriação: nenhum RDS...`, com
`Error: Saved plan is stale ... the state was changed by another operation after the plan was created.`,
`sobe-saida=1`. Estado final: state vazio, nada vivo (só os artefatos D1/D2 do Floci); no GitHub o
`subir` ficaria vermelho e o `limpar` destruiria o que sobrasse. Não há custo extra, mas **o ambiente
prorrogado é destruído** e a mensagem que o operador vê ("Saved plan is stale") não aponta a causa.
Se o `destroy` do agendado pegasse o lock **depois** do `apply` do `subir` (por exemplo, com o RDS levando
minutos), ele esperaria até 5 min (`-lock-timeout=5m`) e, passada a espera, destruiria o ambiente recém
prorrogado do mesmo jeito, ou falharia por tempo (vermelho). Por que pode acontecer no GitHub: o `subir`
leva checkout, credencial, setup e `init` (cerca de 1 min) antes de gravar o prazo novo, e a guarda do
agendado olha a fila uma única vez, no começo. Quão larga é a janela (agendado precisa começar antes do
`subir` ser disparado e ler o state antes de o `apply` dele gravar o prazo novo; o ambiente precisa já
estar vencido) só o P8 mede, mas pela decisão 2 do cabeçalho uma corrida que destrói o ambiente é `BUG`.
Severidade proposta: **média** (perde o ambiente prorrogado, sem custo extra nem dado financeiro; o
estado é consistente e o `limpar` cobre o resto).

- Passos para reproduzir: `bash /passos/sobe.sh 2026-11-20T00:00:00Z 0` (vencido); em segundo plano
  `bash /passos/sobe.sh <agora+1h> 0`; 1 s depois `wf a "" wf-ler-state.sh` e `wf a "" wf-ttl.sh`
  (`vencido=true`), então `parar-migracao.sh` e `terraform destroy -auto-approve -lock-timeout=5m ...`.
- Esperado (spec, "evita ler um state no meio do apply"; README "O agendado"): nada do agendado destrói um
  ambiente que está sendo prorrogado por um `subir` em andamento.
- Obtido: ambiente destruído; `subir` com `Saved plan is stale`.
- Correção sugerida (não aplicada): reconferir a fila manual (`gh run list`) imediatamente antes do
  `destroy` do agendado, ou reler `expires_at` depois de adquirir o lock e antes de destruir, ou um grupo
  de concurrency comum entre agendado e `subir`/`destruir`.
- Teste automatizado sugerido: `tests/agendado/rodar.sh`, caso
  `destroy_do_agendado_nao_destroi_ambiente_prorrogado_depois_da_leitura` (com shims de `terraform` e `gh`
  que simulam `expires_at` vencido na leitura e a fila manual não vazia antes do destroy); mais, no
  workflow, uma checagem do `actionlint`/grep de que o passo de destroy do agendado repete a guarda.

**(b) `subir` disparado enquanto o agendado destrói.** `desce.sh` em segundo plano; 5 s depois,
`terraform plan -lock-timeout=30s ...`:

```
Acquiring state lock. This may take a few moments...
Error: Error acquiring the state lock
  Error message: resource temporarily unavailable
  Lock Info:  ID: d404b372-...  Path: floci.tfstate  Operation: OperationTypeApply
              Who: root@813ed0a0bbdc  Version: 1.17.0  Created: 2026-11-20 04:40:13 +0000 UTC
plan=1                     (30 s depois, como o `-lock-timeout` de 30 s)
```

A mensagem diz **quem segura, desde quando e qual operação**. O `destroy` terminou (`desce-saida=0`), a
verificação deu 0. Resultado esperado atingido. Com o `-lock-timeout=5m` real, o `subir` esperaria 5 min
no `plan` e falharia do mesmo jeito se o destroy durasse mais (no Floci o destroy leva ~2 min; na AWS
real, RDS e ALB levam mais: não medido).

Dados criados: nenhum permanente (tudo destruído no Floci A).

### QA11-26 — O agendado de ponta a ponta no Floci

**Resultado: OK nos quatro estados.** Função `agendado` do roteiro (sem a guarda do `gh`), com os blocos
`run:` reais do YAML (`wf-ler-state.sh`, `wf-ttl.sh`), `parar-migracao.sh`, `terraform destroy
-lock-timeout=5m` com `expires_at`/`image_sha` lidos do passo, os artefatos D1/D2 do Floci e
`verificar-zerado-apos-destroy.sh` com `VERIFICAR_ESPERA_SEGUNDOS=10`. Execução inteira: 27 min 47 s.
Tempos de cada ciclo do agendado (o `time` é só do trecho destruir+verificação).

| # | Estado | Obtido | Esperado | Tempo do ciclo |
|---|---|---|---|---|
| 1 | sem ambiente (`rm -f floci.tfstate*`) | `ativo=false`, "State vazio: não há ambiente ativo."; `verificar-zerado.sh` saiu **0** (`APROVADA`) | verde, só confere a conta | **73 s** (verificação 53 s) |
| 2 | no prazo (`sobe.sh`, +2 h) | `ativo=true` (30 recursos), `vencido=false`, "Ambiente ativo e dentro do prazo: restam 116 min."; resumo "Não vence ainda: restam 1h56min (expira em 2026-11-20T06:44:49Z)"; **nada destruído** | verde, resumo com o tempo restante | 49 s |
| 3 | vencido (`sobe.sh +3 min`, `sleep 200`) | `vencido=true`, "Ambiente vencido: será destruído."; "Nenhuma tarefa de migração ativa no cluster renda-fixa-api."; destroy; D1 e D2; verificação **APROVADA**, `fim=0` | destruído e verificação 0 | **237 s** (destroy+verificação 3 min 08 s) |
| 4 | `expires_at` ausente (`state pull | jq 'del(.outputs.expires_at) | .serial += 1'` e `state push`; `terraform output -raw expires_at` passa a dar `Error: Output "expires_at" not found`) | `::warning::expires_at ausente ou inválido no state: o ambiente conta como vencido.`, `vencido=true`, marcações `1970-01-01T00:00:00Z` e `0000...`; destruído; verificação **APROVADA**, `fim=0` | destruído e verificação 0, com o aviso | **179 s** (destroy+verificação 2 min 50 s) |

Folga: o maior ciclo foi de 4 min no Floci (a verificação repete uma vez, com 10 s de espera; com os 90 s
reais, mais 80 s). Cabe folgado nos 30 min do cron e no `timeout-minutes: 60` do job. **Ressalva:** o RDS
da AWS real costuma levar vários minutos para ser apagado (estimativa minha, não medida nem conferida na
documentação), então o destroy real pode ser bem mais longo que o do Floci; provavelmente ainda cabe nos
60 min, mas um ciclo longo sobrepõe o cron seguinte
(grupo de concurrency `infra-aws-agendado`, `cancel-in-progress: false`: o seguinte espera), o que é
aceitável. Medir na AWS real é do P8.

Observação: no estado 4 o `image_sha` do state também caiu na marcação, mas isso é porque o `jq`
removeu só `expires_at`; o `image_sha` permaneceu no state e foi lido de lá (`0000...` coincide com a
marcação, então o teste não distingue): irrelevante para o resultado.

### QA11-13 — `apply` interrompido como um cancelamento do GitHub

**Resultado: OK com RISCO (R-A4, destroy de 20 min com órfão) e a observação de que o roteiro de recuperação
do README só chega a 0 com a ordem certa.** Aproximação local do I2 (continua pendente do P8: S3 real, lock
`.tflock`, runner morto de verdade). `terraform apply floci.tfplan` em segundo plano, sinal quando o alvo
aparece no Floci, depois a sequência do GitHub (INT, 7,5 s, TERM, 2,5 s, KILL), três variações:

| Variação | O que aconteceu | `floci.tfstate` e lock | Verificação logo depois | Recuperação |
|---|---|---|---|---|
| **A.** INT/TERM/KILL quando o RDS começou a ser criado (+36 s) | O Terraform trata o INT como parada graciosa: `apply=1`, erros `waiting for RDS DB Instance (renda-fixa-api) create: context canceled` e o do ALB; terminou antes do TERM | íntegro (51 KB, JSON válido), **27** endereços, RDS marcado `(tainted)`; sem `.terraform.tfstate.lock.info` | saída **1**, 20 achados (o ambiente quase inteiro) | `desce.sh` (marcações do `destruir`): `Destroy complete! Resources: 24 destroyed.`, verificação **0**. Recuperação: 3 min 14 s |
| **B.** mesma sequência durante a criação do ALB (+22 s) | `apply=1`; erro `creating RDS DB Instance (renda-fixa-api): ... canceled, context canceled`: o RDS foi criado no Floci e **não entrou no state** (órfão) | íntegro (46 KB), 26 endereços, sem lock | saída **1**, 20 achados, **inclui** `rds-instancia renda-fixa-api` | `desce.sh`: 23 destruídos, `desce=0` (o Floci apagou o DB subnet group com o RDS vivo); verificação saiu **2** (`ERRO rds describe-db-instances: DBSubnetGroupNotFoundFault`, `NAO CONCLUIDA (0 achado(s), 1 erro(s) de chamada)`): fail closed. Comportamento do emulador (a AWS recusaria apagar o subnet group em uso) |
| **C.** só `kill -KILL` (runner morto), durante a criação do RDS (+20 s) | `apply=137`, sem mensagem final | `floci.tfstate` íntegro (40 KB) com 24 endereços (**antes** de ALB, cluster e RDS serem gravados); sobrou `.floci.tfstate.lock.info` (199 bytes, só informação: o lock do backend local é do SO e foi liberado) | saída **1**, 19 achados, lista o ALB, o target group, o cluster, o RDS, o secret e os 12 `recurso-com-tag` | ver abaixo |

**Variação C, recuperação seguindo o README** ("Depois de cancelar um `subir` durante o `apply`"): (3)
`desce.sh` ficou **20 min 30 s** em `aws_subnet.public[*]: Still destroying...` (o Floci mantém as subnets
ocupadas pelo RDS órfão; o provider repete o `DeleteSubnet` por 20 min) e terminou com `desce=1`,
`Error: deleting EC2 Subnet ... DeleteSubnet ... StatusCode: 400`. (4) a verificação saiu 2 (o mesmo
`DBSubnetGroupNotFoundFault`) com 7 achados: cluster, ALB, 2 subnets, VPC e as tags. (5) removi os órfãos
pela CLI na ordem do README: `delete-load-balancer`, `delete-target-group`, `delete-cluster` (`INACTIVE`);
`delete-db-instance` também recebeu `DBSubnetGroupNotFoundFault` (no Floci, não é possível apagar esse RDS
pela CLI nesse instante; o Floci removeu o contêiner filho sozinho em seguida). (3 de novo) `desce.sh`:
`Destroy complete! Resources: 3 destroyed.` (6) verificação **APROVADA**, saída 0. Tempo total da
recuperação: cerca de 22 min, dos quais 20 min são o primeiro `destroy` preso.

Esperado (README "Riscos aceitos" e "Lock preso"): pode sobrar órfão; a verificação **lista** cada órfão e o
roteiro, na ordem, leva a 0. **Atingido nos três casos.** Nenhum órfão pago deixou de ser listado (em B e C
a verificação saiu 1 ou 2, nunca 0 com recurso vivo); `floci.tfstate` ficou íntegro nos três.

**RISCO R-A4 (baixo, ordem do roteiro de recuperação).** O README manda `destruir` (passo 3) **antes** de
remover os órfãos (passo 5). Com um órfão que ocupa subnet, o `destroy` do passo 3 pode gastar o tempo do
provider (20 min no Floci) antes de falhar, e no workflow o `destruir`/`limpar` têm `timeout-minutes: 60`:
cabe, mas o `destruir` termina vermelho e o operador precisa repetir o passo 3 depois do 5, o que o texto
só sugere implicitamente ("rode a verificação de novo até ela terminar com APROVADA"). Sugestão: dizer no
README que, depois de remover os órfãos, é preciso rodar `destruir` de novo para o que o state ainda
conhece (no QA ficaram 3 recursos). Na AWS real o `DeleteDBSubnetGroup` falharia na hora com o RDS em uso,
então a duração de 20 min é, em parte, efeito do emulador (não confirmado, P8).

Observação sobre o lock: o roteiro de "Lock preso" (`force-unlock`) não foi exercitado, porque o backend
local não deixa lock de verdade; só o S3 com `use_lockfile` (QA11-14, P2, fora do escopo; I2 no P8). O passo
do README tem problemas próprios de comando (Terraform no CloudShell e caminho do passo 4), registrados no
QA11-33 abaixo.

Dados criados: tudo no Floci A (removido).

### QA11-05 (parte A) — mudanças que impedem o `destroy`, ciclo no Floci A

**PARCIAL (instância encerrada por tempo, 20/11/2026).** V1 a V3 completos, V4 só até a verificação do
`destroy` (faltou apenas o "caminho do operador"), **V5 e V6 não executados**. Cada V rodou num Floci A
recriado (isolamento), cópia `/work/v`, `bash /passos/q05b.sh <V>`: `sobe.sh`, `desce.sh`,
`verificar-zerado.sh`, depois a correção (desfazer a mudança no código, `sobe.sh`, `desce.sh`, verificação).
A trava aprovou as quatro mudanças (`APROVADO 27 itens`), conforme a parte B da instância 2.

| Id | `sobe.sh` | `desce.sh` | Verificação | Conclusão |
|---|---|---|---|---|
| V1 `deletion_protection = true` (RDS) | 0 (114 s) | **1**, `Error: deleting RDS DB Instance (renda-fixa-api): DeleteDBInstance ... StatusCode: 400 ... InvalidParameter...`; ficaram 7 recursos no state | **1**, 7 achados (RDS, log group, SG, 2 subnets, VPC, serviço ECS) | **Floci reproduz a proteção.** Com a trava aprovando, o `destroy` deixa o banco e a rede. Correção: desfazer no código, `sobe.sh` (update no lugar), `desce.sh`: 27 destruídos e verificação 0. O RDS não se apaga por `terraform` sem antes mudar o código (o README não cobre isso). Evidência de **BUG** já classificada na parte B (trava não barra); aqui o efeito é confirmado no Floci |
| V2 `enable_deletion_protection = true` (ALB) | 0 | 0, 27 destruídos | 0 | **Floci não reproduz** (o destroy passa). Vai para o P8 |
| V3 `skip_final_snapshot = false` + `final_snapshot_identifier = "qa11-final"` | 0 (163 s) | 0, 27 destruídos | 0; `describe-db-snapshots --snapshot-type manual` vazio | **Floci não cria o snapshot final**: não dá para dizer se a verificação (`rds-snapshot-manual`) o listaria na AWS real. P8 |
| V4 `delete_automated_backups = false` | 0 (180 s) | 0, 27 destruídos | 0; `describe-db-instance-automated-backups` vazio | Floci não reproduz. P8 |
| V5 `lifecycle { prevent_destroy = true }` | NÃO EXECUTADO | | | a instância 2 já mostrou o efeito (BUG-3 na parte B, `destroy` sai 1 sem destruir nada) |
| V6 `recovery_window_in_days = 30` | NÃO EXECUTADO | | | |

Uma primeira tentativa (V1 a V4 em sequência sem isolar) foi descartada por contaminação: o RDS protegido do V1
ficou no Floci e fez os `sobe.sh` seguintes falharem; por isso refiz cada V num Floci novo.

### QA11-33 (leitura do README; PARCIAL, só a leitura, sem a parte B do Floci)

Achados por leitura do README contra o código e a documentação pública da AWS (CloudShell):

- **BUG média B-A2 (Terraform não vem no CloudShell).** A seção "Lock preso" manda rodar `terraform init ...`
  e `terraform force-unlock <LOCK_ID>` "no CloudShell", mas a lista de software pré-instalado do CloudShell
  (documentação pública da AWS, página "CloudShell compute environment: specifications and software") traz
  AWS CLI, git, jq, Docker, psql, make etc., e **não** Terraform; o README não diz para instalá-lo. Copiado
  literalmente, o comando falha com `terraform: command not found`. Esperado: o README dá o passo de
  instalação (binário 1.17.0 pinado, em `$HOME`, porque fora dele o CloudShell apaga os arquivos ao fim da
  sessão). Teste sugerido: `tests/readme/rodar.sh`, caso `readme_lock_preso_instala_terraform`.
- **BUG média B-A3 (caminho do passo 4).** O bloco do passo 1/2 deixa o operador em `renda-fixa-api/infra/aws`
  (`cd renda-fixa-api/infra/aws`), e o passo 4 manda `bash infra/aws/scripts/verificar-zerado.sh us-east-1`:
  daquela pasta o caminho não existe (`ls infra/aws/infra` dá `No such file or directory`); só serve a partir
  da raiz do clone. Esperado: `bash scripts/verificar-zerado.sh us-east-1` (a partir de `infra/aws`) ou um
  `cd` explícito. Teste sugerido: `tests/readme/rodar.sh`, caso
  `readme_comandos_existem_na_pasta_em_que_o_operador_esta`.
- **BUG baixa B-A4 ("em até 1 h").** README, passo 2 do `subir`: o provisório é de 1 h e o agendado "o destrói
  em até 1 h"; na verdade é 1 h mais até 30 min do cron mais a duração do ciclo e o atraso do GitHub
  (até cerca de 1 h 30 min ou mais).
- **RISCO.** O README não ensina a rodar o ciclo do Floci localmente nem avisa que `floci_override.tf` e
  `floci.tfstate` ficam na pasta (ambos estão no `.gitignore`, linhas 491 a 507).
- **OK.** A data de fechamento do plano continua `(preencher no P0 ...)` (conhecido); `voce@exemplo.com` passa
  na validação do `bootstrap.sh` (`^[A-Za-z0-9._%+-]+@...`); o bootstrap imprime `AWS_ROLE_ARN`, `AWS_REGION`
  e `TF_STATE_BUCKET` (mais a execution role e o budget); `AWS_ROLE_ARN` é `vars.` de repositório e
  `AWS_REGION`/`TF_STATE_BUCKET` do environment `aws`, como no `infra-aws.yml`; `horas` `08` passa (`10#`);
  prazo de 15 min do alvo ALB = `SECONDS + 900`; cron `7,37 * * * *`; os scripts do README usam `bash`.

### Resumo da trilha A (instância 1)

| Roteiro | Resultado |
|---|---|
| QA11-07 | OK (RISCO R-A1: `vpc_cidr` recria VPC sem reprovação) |
| QA11-11 | OK |
| QA11-12 | OK (ressalva de emulador no item c) |
| QA11-13 | OK (RISCO R-A4) |
| QA11-23 | OK (RISCO R-A2, state de 0 bytes) |
| QA11-25 | **BUG média B-A1** (a2), RISCO R-A3, (b) OK |
| QA11-26 | OK |
| QA11-05 (parte A) | PARCIAL (V1 a V4; V5 e V6 não executados) |
| QA11-33 | PARCIAL (só leitura): BUG média B-A2, B-A3; BUG baixa B-A4 |

Limpeza: **não feita pela instância 1** (decisão do coordenador); ver o relatório final para a lista do que existe.


## Trilhas B, C, D (instância 2)

_Instância 2, 20/11/2026 (retomada após interrupção por limite de uso; nada foi perdido: os roteiros desta
seção são escritos aqui assim que terminam). Ordem de execução: C, B, QA11-29, D. Floci B (porta 4567) e
contêineres `qa11b`/`qa11c`/`qa11d`. Scripts do QA (não da fase), todos em `$QA/passos/`: `t18.sh`
(matriz do QA11-18), `t22.sh` (matriz do QA11-22), `fixtures/*.json` (QA11-20). Eles chamam os passos
extraídos do YAML, sem alteração. Locale: o runner só tem `C` e `C.UTF-8` (não há `en_US.UTF-8` nem
`pt_BR.UTF-8`: o `setlocale` avisa e cai em `C`)._

_A tabela final (roteiro x resultado) fica no fim desta seção._

### QA11-01 (parte C) — `bootstrap.sh` sem rede e sem credencial

**Resultado: RISCO** (rastro cru da CLI em inglês e código 253/255 no `bootstrap.sh` sem rede ou sem
credencial; o resto está OK). Runner `c` (sem rede), pasta `infra/aws` da cópia. Esperado (QA11-01 t e u;
cabeçalho do script, `Uso: bootstrap.sh --email voce@exemplo.com`): saída 1 com `ERRO ...` em português,
antes de qualquer chamada à AWS.

| Comando (`rc 'cd /work/repo/infra/aws; <cmd>'`) | Saída | Código |
|---|---|---|
| `bash bootstrap/bootstrap.sh` | `ERRO informe o e-mail dos alertas do budget com --email ou BUDGET_EMAIL (ele não fica no script)` | 1 |
| `--help` / `-h` | `ERRO argumento desconhecido: --help` (sem uso, sem ajuda) | 1 |
| `--Email x@y.com` | `ERRO argumento desconhecido: --Email` | 1 |
| `--email` (sem valor) | `ERRO --email exige um endereço` | 1 |
| `--email x`, `--email a@b`, `--email " "` | a mesma mensagem de e-mail ausente (não diz que o valor é inválido) | 1 |
| `--email x@y.com extra` | `ERRO argumento desconhecido: extra` | 1 |
| `BUDGET_EMAIL= bash bootstrap/bootstrap.sh` | mensagem de e-mail ausente | 1 |
| `--imprimir-policy-ci` | JSON da policy (conta fictícia `123456789012`), sem chamar a AWS | 0 |
| `--email voce@exemplo.com` (endpoint `localhost:4566` sem Floci) | `aws: [ERROR]: Could not connect to the endpoint URL: "http://localhost:4566/"` | **255** |
| idem com `env -u AWS_ENDPOINT_URL` (nome da AWS real, sem rede) | `aws: [ERROR]: Could not connect to the endpoint URL: "https://sts.us-east-1.amazonaws.com/"` | **255** |
| idem sem credencial e sem endpoint | `aws: [ERROR]: An error occurred (NoCredentials): Unable to locate credentials. You can configure credentials by running "aws login".` | **253** |

- **RISCO (baixa a média, sem efeito em custo):** com rede ou credencial faltando, o primeiro `aws sts
  get-caller-identity` (dentro de `$(...)` com `set -e`) aborta com a mensagem da própria CLI, em inglês,
  sem o prefixo `ERRO` e com código 253/255, não 1. A mensagem diz o que houve, mas não o que fazer, e é o
  caminho do operador no CloudShell com a sessão expirada. Se o coordenador aplicar literalmente o
  critério do QA11-03 ("só o rastro cru da CLI em inglês no bootstrap, sem o `ERRO ...` do script" = BUG
  média), sobe para BUG média. Teste sugerido: `tests/bootstrap/rodar.sh`, caso "sem credencial sai 1 com
  ERRO em português" (com um `aws` falso que falha em `sts get-caller-identity`).
- **RISCO baixa:** `--help` e `-h` não imprimem uso e saem 1; e-mail malformado recebe a mensagem de "informe".
- Nenhuma chamada à AWS antes da validação dos argumentos: os casos de argumento terminam em menos de
  10 ms, num runner sem rede, sem mensagem da CLI.

### QA11-01 (parte B) — entradas, códigos de saída e mensagens dos scripts

**Resultado: RISCO** (nenhum fail open; só mensagens e comportamentos que surpreendem). Runner `b`, Floci B
vazio, `init.sh` feito. Reproduzir: `source "$QA/lib.sh"; rb 'bash /passos/t01b.sh'` (a-s, saída com código, tempo,
`stdout` e `stderr` separados) e os comandos soltos abaixo. `grep -c 'Password=' /tmp/captura-t01.txt` = **0**.

| # | Comando | Obtido | Esperado / fonte | Resultado |
|---|---|---|---|---|
| a | `env -u AWS_REGION -u AWS_DEFAULT_REGION bash scripts/verificar-zerado.sh` | 2; `stderr`: `ERRO região ausente ou inválida: informe o argumento ou AWS_REGION` | 2 (cabeçalho) | OK |
| b | `US-EAST-1`, `"us-east-1 "`, `us_east_1` | 2 nos três, mesma mensagem | 2 | OK |
| c | `verificar-zerado.sh sa-east-1` | 0 em 46 s; `Verificação de zerado: APROVADA (nada sobrando na região sa-east-1)` | cita a região | OK |
| d | `verificar-zerado.sh xx-qualquer-9` | **2** em 1 s: `ERRO sts get-caller-identity: InvalidSignatureException` e `NAO CONCLUIDA (0 resíduos, 1 erro)` | registrar | OK (fail closed; o Floci não aceita a região inexistente) |
| e | `AWS_REGION=us-east-2 ... verificar-zerado.sh us-east-1` | 0; cita `us-east-1` | argumento prevalece | OK |
| f | `verificar-zerado.sh us-east-1 sobra` | 0; `APROVADA ... us-east-1` | ignorar é aceitável | OK (argumento a mais ignorado em silêncio) |
| g | `PATH` sem `jq` (diretório de links para tudo de `/usr/bin` menos `jq`, mais `aws`; `command -v jq` = ausente) | 2; `ERRO ferramenta ausente: jq` | 2 | OK (nota: com `PATH=/usr/bin:/bin` a ferramenta que falta é `aws`, que fica em `/usr/local/bin`: `ERRO ferramenta ausente: aws`, 2) |
| h | `VERIFICAR_ESPERA_SEGUNDOS=abc`, `-1`, `99999` em `verificar-zerado-apos-destroy.sh` | 2 nos três: `ERRO VERIFICAR_ESPERA_SEGUNDOS inválida: <valor>`; 0 s (nenhuma chamada à AWS) | 2 | OK |
| h' | `VERIFICAR_ESPERA_SEGUNDOS=` (vazio) | **0**, rodou a verificação inteira (69 s): vazio vira o padrão 90 (`${VAR:-90}`) | roteiro esperava 2 | RISCO baixa (o cabeçalho diz "padrão 90"; vazio = padrão é defensável, só difere do roteiro). Também: `9999` passa na regex `^[0-9]{1,4}$` = até 2 h 46 min de `sleep` se houver resíduo (o job tem `timeout-minutes`) |
| i | `VERIFICAR_ESPERA_SEGUNDOS=0 bash scripts/verificar-zerado-apos-destroy.sh` (Floci vazio) | 0 em 52 s, sem repetição | 0 | OK |
| j | `bash scripts/trava-custo.sh < /dev/null` e `bloquear-recriacao.sh < /dev/null` | 1 nos dois: `REPROVADO entrada inválida: esperado exatamente um documento JSON` | 1, entrada vazia | OK (fail closed) |
| k | `terraform show -json \| bash scripts/trava-custo.sh` (state vazio, sem plano) | 1: `REPROVADO resource_changes ausente ou inválido: sem ele o plano não pode ser verificado` | 1, deixar claro que faltou o plano | OK (entendível) |
| l | `terraform show floci.tfplan \| ...trava-custo.sh` (plano em texto) | 1: `REPROVADO entrada inválida: o plano não é um JSON legível` (e `bloquear-recriacao.sh` igual) | 1 | OK |
| m | `terraform show -json nao-existe.tfplan \| bash scripts/trava-custo.sh`, sem e com `pipefail` | `terraform` sai 1; a trava recebe vazio e sai 1 (`entrada inválida`); com `pipefail` o pipeline sai 1 | diferente de 0 | OK |
| n | `bash scripts/trava-custo.sh` sem pipe, num terminal | **não executado com terminal real** (este ambiente não tem TTY). Por leitura: o script não tem `[ -t 0 ]` nem aviso; num terminal o `jq` espera a entrada em silêncio até Ctrl+D | RISCO baixa se esperar sem avisar | RISCO baixa (inferido por leitura) |
| o | `bash scripts/parar-migracao.sh` com Floci vazio | 0 em 2 s: `Cluster renda-fixa-api não existe: nenhuma tarefa de migração a parar.` | 0 | OK |
| p | `parar-migracao.sh "qa11 com espaco"` e `''` | 0 (`Cluster qa11 com espaco não existe...`; vazio usa o padrão `renda-fixa-api`) | não pode dizer que parou algo | OK |
| q | `env -u AWS_REGION -u AWS_DEFAULT_REGION bash scripts/parar-migracao.sh` | 1; `stderr`: `ERRO ecs list-tasks: NoRegion` | 1, citando a região | RISCO baixa: mostra só o código `NoRegion`, sem frase. Ver QA11-02(e) para o caso pior (código vazio) |
| r | `gerar-floci-override.sh http://localhost:4566/`, `localhost:4566`, `'http://a;b'` | 1 nas três: `URL do Floci inválida: <valor>` | 1 | OK |
| s | `cd /tmp && bash /work/tmp-ovr/scripts/gerar-floci-override.sh http://localhost:4566` | 0; `Gerado: /work/tmp-ovr/floci_override.tf (endpoint http://localhost:4566)`; nada criado em `/tmp` | arquivo vai para a pasta do script e a mensagem diz o caminho | OK |

Observações:
- **RISCO baixa:** `bloquear-recriacao.sh` com entrada inválida (vazia, texto, plano inexistente) imprime, depois do
  `REPROVADO entrada inválida`, a mensagem "O ambiente não pode ser atualizado com recriação do banco ou do secret:
  a senha e a connection string ficariam dessincronizadas. Rode o workflow com acao = destruir...". O texto é
  sobre recriação, mas a causa foi entrada ilegível: pode levar o operador a destruir um ambiente que não precisa.
- Controle: plano bom (27 itens), `trava-custo.sh` `APROVADO 27 itens`, `bloquear-recriacao.sh` `nenhum RDS nem versão de secret será recriado`, ambos 0.
- Os casos de argumento (a, b, h, j) saem em 0 s: nenhuma chamada à AWS antes da validação.

### QA11-02 — Endpoint lento ou parando no meio, e o tempo da verificação

**Resultado: BUG** (média, `parar-migracao.sh`: falha sem causa e, pelo YAML, impede o `destroy`; ver BUG-1
abaixo). A verificação de zerado e o `apos-destroy` se comportaram bem (fail closed, tempo limitado).
Runner `b`, Floci B (`docker pause` / `unpause`). Drivers: `$QA/passos/t02a.sh`; os demais são os comandos abaixo.

| Caso | Comando | Obtido | Esperado (cabeçalho, spec) |
|---|---|---|---|
| a. vazio | `rb 'time bash scripts/verificar-zerado.sh; echo saida=$?'` | 0 em **46 s**, `APROVADA` | 0 |
| a. 20 secrets + 10 log groups `qa11` | `rb 'bash /passos/t02a.sh'` | 1 em **41 s**; 30 linhas `RESIDUO` (20 `secret`, 10 `log-group`); `REPROVADA (30 achado(s); o mesmo recurso pode aparecer por mais de uma via)` | 1, 30 resíduos. OK |
| b. Floci congelado do começo ao fim | `docker pause qa11b-floci; rb 'time timeout 1200 bash scripts/verificar-zerado.sh; echo saida=$?'` | **2** em **3 min 03 s**: `ERRO sts get-caller-identity: sem credencial válida ou sem rede` e `NAO CONCLUIDA (0 resíduos, 1 erro)` | 2, nunca 0. OK (o limite é o do `aws`: `--cli-read-timeout 60` x 3 tentativas) |
| c. congelado no meio (90 s) | verificação em segundo plano; `pause` 12 s depois, `unpause` 90 s depois (03:46:56 a 03:48:27) | **1** em **2 min 18 s**, os mesmos 30 resíduos, nenhum `ERRO`: as retentativas da CLI absorveram o congelamento | 2 ou 1, nunca 0. OK (nem perdeu resíduo nem aprovou) |
| d1. `parar-migracao.sh`, Floci congelado | `docker pause ...; rb 'time timeout 900 bash scripts/parar-migracao.sh; echo saida=$?'` | **1** em **3 min 03 s**, `stderr`: **`ERRO ecs list-tasks: `** (nada depois dos dois pontos) | 1 com a causa |
| d2. `verificar-zerado-apos-destroy.sh` (espera 5), Floci congelado | `... env VERIFICAR_ESPERA_SEGUNDOS=5 bash scripts/verificar-zerado-apos-destroy.sh` | **2** em 3 min 03 s, **sem repetição** (nenhum `AVISO resíduo logo depois do destroy`) | 2, sem repetir. OK |
| e1. porta fechada, `verificar-zerado.sh` | `rb 'AWS_ENDPOINT_URL=http://localhost:4599 bash scripts/verificar-zerado.sh'` | 2 em 1 s, `ERRO sts get-caller-identity: sem credencial válida ou sem rede` | 2 rápido. OK |
| e2. porta fechada, `parar-migracao.sh` | `rb 'AWS_ENDPOINT_URL=http://localhost:4599 bash scripts/parar-migracao.sh'` | **1** em 1 s, **`ERRO ecs list-tasks: `** (vazio) | 1 com a causa |
| e3. porta fechada, `apos-destroy` | idem com `VERIFICAR_ESPERA_SEGUNDOS=5` | 2 em 1 s, sem repetição | OK |

Limpeza: os 20 secrets (`--force-delete-without-recovery`) e os 10 log groups foram removidos; verificação final 0.

Tempo (a pergunta do roteiro): com o Floci congelado a verificação chega ao 2 em **3 min**, muito abaixo
dos 30 min do agendado; o `parar-migracao` também termina em 3 min (as tentativas padrão da CLI limitam),
então a preocupação de "agendados acumulados" não se confirma.

#### BUG-1 (média) — `parar-migracao.sh` falha sem dizer o motivo, e a falha impede o `terraform destroy`

- **Mensagem vazia.** O script só extrai o código de `An error occurred (<Codigo>)`. Rede fora, timeout e
  endpoint inalcançável não têm esse padrão (a CLI v2 escreve `aws: [ERROR]: Could not connect to the
  endpoint URL...`), então sai `ERRO ecs list-tasks: ` e nada mais (casos d1 e e2). Em `NoRegion` (QA11-01 q)
  mostra só o código. A `verificar-zerado.sh`, ao lado, já trata isso (`${codigo:-falha sem código da AWS
  (rede, credencial, timeout ou API inexistente)}`). Fonte da expectativa: QA11-01 q do roteiro ("BUG média: o
  `destruir` fica vermelho sem dizer por quê") e a regra do agente (vermelho que ninguém entende).
- **Impede o destroy (custo).** Em `.github/workflows/infra-aws.yml` o passo `Parar tarefas de migração` é um
  `run: bash scripts/parar-migracao.sh` simples, no `destruir` (linha 656), no `limpar` (739) e, no
  agendado, `Ambiente vencido - parar tarefas de migração` (911). Os passos seguintes (`Ler expires_at...`,
  `Terraform destroy`, `Ambiente vencido - terraform destroy`) só têm condição de entrada sem `always()`, ou
  seja, rodam só se o anterior deu certo (`success()` implícito do Actions). Qualquer falha desse script
  (erro transitório que a CLI não absorveu, throttling, `AccessDenied` por permissão faltando) **pula o
  `terraform destroy`** do ciclo: o `destruir` manual e o `limpar` ficam vermelhos com o ambiente ligado, e o
  agendado repete 30 min depois (e de novo falha, se a causa for permanente). Contraria a premissa de custo da
  spec ("nada pode ficar ligado esquecido") e a decisão 2 (impedir o `destroy` = BUG).
- Reproduzir a parte do script: `AWS_ENDPOINT_URL=http://localhost:4599 bash scripts/parar-migracao.sh; echo
  saida=$?` (1, `ERRO ecs list-tasks: `). A parte do YAML é leitura; a confirmação com o Actions é do P8.
- **Esperado:** (1) a mensagem diz a causa (`${codigo:-falha sem código da AWS (rede, credencial, timeout)}`);
  (2) uma falha em parar tarefas não pode impedir o `destroy`: o passo deve rodar com `continue-on-error: true`
  (e o job ficar vermelho no fim se o `destroy` ou a verificação também falharem), ou o `destroy` com
  `if: always() && ...`. O `destroy` já falha sozinho se uma tarefa solta segurar o cluster, e a verificação de
  zerado final acusa o resto.
- **Testes sugeridos:** `tests/parar-migracao/rodar.sh` (não existe hoje; seguir o estilo de
  `tests/verificar-zerado/rodar.sh`, com `aws` falso): `mensagem_cita_a_causa_quando_a_chamada_falha_sem_codigo_da_aws`
  e `sai_1_com_regiao_ausente_citando_a_regiao`; e um teste do workflow (`actionlint`/`yq` no job `validar`) que
  confirme que o passo `Parar tarefas de migração` tem `continue-on-error: true` (ou que o `terraform destroy`
  roda com `always()`) nos três jobs.

Outros achados deste roteiro:
- **RISCO baixa:** com o endpoint congelado ou inalcançável a `verificar-zerado.sh` diz `sem credencial válida
  ou sem rede` e nunca menciona timeout (o caso congelado é timeout). O operador pode caçar credencial à toa.
- `parar-migracao.sh` não tem `--cli-connect-timeout`/`--cli-read-timeout`, mas o limite padrão da CLI o fecha
  em 3 min; não há pendurada longa.

### QA11-04 — Mudanças de operador num plano real, contra a trava

**Resultado: RISCO** (nenhum BUG). Tudo o que a spec manda a trava limitar foi reprovado, com mensagem
clara. A trava **não** olha cinco atributos pagos (M08, M10, M11, M18, M20); dois deles (M08, M11) a spec
manda desligar e o `terraform test` do job `validar` os reprova, os outros três não são barrados por nada.
Runner `b`, plano real contra o Floci B vazio (tudo `create`, 27 itens), JSON do plano direto no pipe da
trava, nada impresso. `grep -c 'Password=' /tmp/plan-*.log` = 0 nas 19 mutações.

Reproduzir (cada mutação altera só a **cópia** `/work/m/<id>`; `$QA/passos/mutacoes.sh` tem a função de cada
uma): `source "$QA/lib.sh"; rb 'bash /passos/t04.sh M02 M03 ...'` (usa `/passos/mut.sh`: copia a pasta base,
aplica `m_<id>`, faz o plano e passa por `trava-custo.sh` e `bloquear-recriacao.sh`). Para a segunda coluna:
`rb 'bash /passos/tt.sh M08 M10 ...'` (`terraform test` na cópia mutada).

| Id | Mudança (na cópia) | `trava-custo.sh` | `terraform test` (job `validar`) | Veredito |
|---|---|---|---|---|
| M01 | `instance_class` `db.t4g.small` | 1: `REPROVADO aws_db_instance module.database.aws_db_instance.this: instance_class deve ser db.t4g.micro` | (não rodado) | OK |
| M02 | `db.t3.micro` | 1, a mesma mensagem (diz o valor aceito) | | OK |
| M03 | `allocated_storage = 30` | 1: `allocated_storage deve ser no maximo 20` | | OK |
| M04 | `storage_type` removido | 1: `storage_type deve ser gp3` | | OK |
| M05 | `backup_retention_period = 1` | 1: `backup_retention_period deve ser 0` | | OK |
| M06 | `publicly_accessible = true` | 1: `publicly_accessible deve ser false` | | OK |
| M07 | `performance_insights_enabled = true` | 1: `performance_insights_enabled deve ser false` | | OK |
| M08 | `enabled_cloudwatch_logs_exports = ["postgresql"]` | **0**, `APROVADO 27 itens` | **falha** (`banco_nao_deixa_residuo_ao_ser_destruido`), 53 passam e 1 falha | RISCO |
| M09 | CPU 512 na API, memória 1024 na migração | 1: `...api: cpu deve ser 256` e `...migrator: memory deve ser 512` (os dois endereços) | | OK |
| M10 | `ephemeral_storage { size_in_gib = 100 }` na API | **0**, `APROVADO 27 itens` | passa (54 de 54) | RISCO |
| M11 | Container Insights `enabled` | **0**, `APROVADO 27 itens` | **falha** (`cluster_tem_container_insights_desligado_e_a_tag_de_expiracao`) | RISCO |
| M12 | `desired_count = 2` literal na chamada do módulo | o `plan` falha antes (`desired_count deve ser 0 ou 1`): a `validation` do módulo vale também para literal | | OK |
| M12b | M12 e a `validation` do módulo trocada por `var.desired_count >= 0` | 1: `aws_ecs_service module.app.aws_ecs_service.this: desired_count deve ser no maximo 1` | | OK |
| M13 | `aws_appautoscaling_target` e `_policy` | 1, as duas linhas `tipo fora da lista positiva` | | OK |
| M14 | `aws_cloudwatch_metric_alarm` | 1: `tipo fora da lista positiva` (não diz onde mudar a lista) | | OK (RISCO baixa de texto) |
| M15 | `aws_eip` e `aws_nat_gateway` | 1, **as duas** linhas | | OK |
| M16 | segundo `module "app2"` | 1: `aws_ecs_service: 2 recursos no plano, no máximo 1`; `aws_ecs_task_definition: 4 ..., no máximo 2`; `aws_lb: 2 ..., no máximo 1` | | OK |
| M17 | `retention_in_days = 1` | 1: `retention_in_days deve ser 3` (deixa claro que é exatamente 3) | | OK |
| M18 | 10 `aws_secretsmanager_secret` extras (`count = 10`) | **0**, `APROVADO 37 itens` | passa | RISCO |
| M19 | `engine_version = "17"` | 0, `APROVADO 27 itens` | | informativo (sem limite de versão, não muda o custo) |
| M20 | `storage_throughput = 500` com `gp3` | **0**, `APROVADO 27 itens` | passa | RISCO |

`bloquear-recriacao.sh` saiu 0 (`nenhum RDS nem versão de secret será recriado`) em todas, como esperado para
um plano contra state vazio. Todas as reprovações citam tipo, endereço e atributo, sem o valor.

Por que RISCO e não BUG (decisão 2):
- **M08 e M11** (a spec proíbe: linhas 89 e 235 de `docs/SPEC-F11-EXECUCAO.md`; a lista de checagens da trava, no
  P5, linha 679, não os inclui): a trava aprova, mas o `terraform test` do job `validar` reprova, então a mudança
  é barrada **se o PR passar por esse job** e se ele for exigido antes do merge (configuração do repositório,
  P8). Se escapar, ambos criam log groups fora do state que o `destroy` não apaga
  (`/aws/rds/instance/renda-fixa-api/postgresql`, `/aws/ecs/containerinsights/renda-fixa-api/performance`, na
  AWS real; o Floci não reproduz o segundo) e a verificação de zerado os acusa depois. Recomendação: a trava
  checar `enabled_cloudwatch_logs_exports` e a `setting` `containerInsights` (teste sugerido em
  `tests/trava-custo/`: `reprova-rds-exporta-logs.json`, `reprova-cluster-container-insights-ligado.json`).
- **M10, M18, M20** (a spec não prevê, nada barra): disco efêmero acima de 20 GiB, quantidade de secrets e
  throughput do gp3. Tudo isso está no state e some no `destroy`, então o custo fica limitado ao TTL (no máximo
  12 h): por isso RISCO.

### QA11-05 (parte B) — mudanças que impedem o `destroy`, contra a trava

**Resultado: BUG** (alta, `BUG-3`: nada barra `lifecycle { prevent_destroy = true }`). A parte A (ciclo
`apply`/`destroy` no Floci A com cada mudança) é da instância 1. Aqui: a trava e o `terraform test` contra as
seis mudanças. Runner `b`, mesmo método do QA11-04 (`mut.sh`, `tt.sh`, funções `m_V1` a `m_V6`).

| Id | Mudança (na cópia) | `trava-custo.sh` | `terraform test` (job `validar`) |
|---|---|---|---|
| V1 | `deletion_protection = true` no RDS | **0**, `APROVADO 27 itens` | falha (`banco_nao_deixa_residuo_ao_ser_destruido`) |
| V2 | `enable_deletion_protection = true` no ALB | **0** | falha (`alb_e_publico_sem_protecao_contra_exclusao_nas_duas_subnets`) |
| V3 | `skip_final_snapshot = false` e `final_snapshot_identifier = "qa11-final"` | **0** | falha (`banco_nao_deixa_residuo...`) |
| V4 | `delete_automated_backups = false` | **0** | falha (`banco_nao_deixa_residuo...`) |
| V5 | `lifecycle { prevent_destroy = true }` no RDS | **0** | **passa (54 de 54)** |
| V6 | `recovery_window_in_days = 30` no secret | **0** | falha (`secret_some_na_hora_e_a_connection_string_e_de_escrita_unica`) |

`bloquear-recriacao.sh` saiu 0 nos seis. A trava de custo aprova **todas**: os atributos que a spec manda
(`deletion_protection = false`, `enable_deletion_protection = false`, `skip_final_snapshot = true`,
`delete_automated_backups = true`, `recovery_window_in_days = 0`) não estão na lista dela. O que barra V1 a V4 e V6
é só o `terraform test` do job `validar` (PR): defesa em profundidade que depende de o `validar` ser exigido
antes do merge (P8) e de o `subir` rodar da `main`. Por isso V1, V2, V3, V4 e V6 ficam como **RISCO** (barrados
na PR, não no `subir`).

#### BUG-3 (alta) — `prevent_destroy` no Terraform não é barrado por nenhuma camada e aborta o `destroy` inteiro

- **Passos:** (1) copiar `infra/aws`; (2) em `modules/database/main.tf` adicionar `lifecycle { prevent_destroy = true }`
  no `aws_db_instance.this`; (3) `terraform test` (54 de 54 passam) e o plano na trava (`APROVADO 27 itens`).
  Reprodução do efeito do `destroy` no Floci B com dois recursos (um com `prevent_destroy`), no estilo do job
  `destruir`: `terraform apply -auto-approve` e depois `terraform destroy -auto-approve -input=false`:

  ```
  Plan: 0 to add, 0 to change, 2 to destroy.
  Error: Instance cannot be destroyed
  Resource aws_cloudwatch_log_group.protegido has lifecycle.prevent_destroy set, but the plan calls for this
  resource to be destroyed. ...
  saida=1
  aws logs describe-log-groups  ->  /ecs/renda-fixa-api-qa11-comum   /ecs/renda-fixa-api-qa11-protegido
  ```

  O `destroy` sai 1 **sem destruir nada** (o recurso comum também ficou: o erro é no plano do `destroy`).
- **Esperado:** premissa de custo da spec ("nada pode ficar ligado esquecido") e a decisão 2 (impedir o
  `destroy` é BUG). Uma mudança dessas devia ser barrada antes do `subir`.
- **Obtido:** nada barra (nem a trava, que só vê o JSON do plano, onde `lifecycle` não aparece, nem o
  `terraform test`). Com `prevent_destroy` no RDS, o `destruir`, o `limpar` e o agendado ficam vermelhos a cada
  rodada, com ALB, RDS e a API ligados, até alguém editar o código e fazer merge. Pior que V1/V2: não há
  `-var` nem passo do workflow que contorne (só `-target` ou remover o código).
- **Teste sugerido:** `terraform test` não enxerga `lifecycle`; o teste fica no `validar` como verificação do
  código: `tests/lifecycle/rodar.sh` (ou um passo `grep`) que falha se houver `prevent_destroy` em qualquer
  `infra/aws/**/*.tf`, nome `nenhum_recurso_tem_prevent_destroy`.
- **Operador:** a mensagem que ele vê é a do Terraform acima, sem pista de que o cluster, o RDS e o ALB seguem
  ligados; o README ("Lock preso" e "Destruir") não cobre esse caso.

**Ligação com a parte A:** a verificação de zerado depois de cada `destroy` (parte A, instância 1) decide se V1 a V4 e V6
deixariam recurso no Floci; o Floci pode não reproduzir as proteções (nesse caso vai para o P8).

### QA11-18 — "Validar entradas" (`horas` e `sha`)

**Resultado: OK.** (runner `c`, sem rede; passo extraído do `infra-aws.yml` linhas 294 a 304, rodado com
`bash --noprofile --norc -eo pipefail`, `GITHUB_OUTPUT` em arquivo.)

Reproduzir: `source "$QA/lib.sh"; sobe_runner c; rc 'bash /passos/t18.sh'` (matriz completa, 37 linhas).

| Entrada | Obtido |
|---|---|
| `HORAS` `4`, `1`, `12` | 0; `GITHUB_OUTPUT` = `horas=4`, `horas=1`, `horas=12` |
| `08`, `09` | 0; `horas=8`, `horas=9` (não octal) |
| `00`, `0`, `13`, `099`, `-1`, `+4`, `4.0`, `1e1`, `" 4"`, `"4 "`, vazio | 1, `::error::horas deve ser um inteiro de 1 a 12`; `GITHUB_OUTPUT` vazio |
| `$'4\n'`, `$'4\nhoras=12'` | 1; `GITHUB_OUTPUT` vazio (sem injeção) |
| `٤` (árabe) e `４` (largura total), em `C` e `C.UTF-8` | 1 em todos. `en_US.UTF-8` e `pt_BR.UTF-8` **não existem** no runner (cai em `C`): não testado nesses dois locales |
| `SHA_ENTRADA` com 40 hex minúsculos | 0 |
| 40 maiúsculos, 39, 41, 40 + espaço, `$(id)`, `<40 hex>\nsha=x`, `sha=x\n<40 hex>`, só espaço | 1, `::error::sha deve ter exatamente 40 hexadecimais minúsculos`; `GITHUB_OUTPUT` vazio |

Esperado: spec "Pipeline" e README. Nenhum valor fora da regra aceito e nenhuma linha extra no
`GITHUB_OUTPUT`. O YAML entrega `inputs.horas`/`inputs.sha` ao passo só por `env:` (linha 287: "nunca
interpolados no script"), então `$(id)` não executa.

### QA11-20 — "Resolver o SHA da imagem" e "Conferir as imagens no GHCR"

**Resultado: OK quanto à segurança** (todos os casos ruins saem diferente de 0, nenhum SHA inválido passa,
o token só aparece no `::add-mask::`); **2 achados de mensagem, `RISCO` baixa.** Trilha `c`, shims do
`docker` e do `curl`; as fixtures do roteiro foram escritas em `$QA/passos/fixtures/`.

Reproduzir: `wf c "PATH=/passos/shims/docker:/usr/local/bin:/usr/bin:/bin FALSO_IMAGETOOLS=<f>.json SHA_ENTRADA=''" wf-resolver-sha.sh /work`.

| Caso | Código | Saída |
|---|---|---|
| `simples`, `com-atestado` | 0 | `sha=1111...` (40 `1`) no `GITHUB_OUTPUT` |
| `divergente` (duas plataformas com SHA diferente), `sem-rotulo`, `invalido` (maiúsculo + 41), `vazio` | 1 | `::error::Não foi possível resolver um SHA válido (40 hexadecimais) para a imagem` |
| `nao-json` (`nao e json`) | **5** | só `jq: parse error: Invalid numeric literal at line 1, column 4`, **sem** o `::error::` do script |
| `FALSO_DOCKER_FALHA=1` | 1 | só `ERROR: ghcr.io/...:latest: not found` do docker, **sem** `::error::` do script |
| `SHA_ENTRADA=<40 hex>` | 0 | usa o valor dado, o docker não é chamado (o shim sairia 99) |

Todos saem não zero (fail closed). A ressalva: nos dois casos "sem `::error::`" o `set -e` com `pipefail`
aborta na atribuição, antes do `if`, e o operador vê a mensagem crua do `docker`/`jq`, sem anotação em
português dizendo que é a resolução do SHA. **RISCO baixa** (texto).

GHCR (`wf c "PATH=/passos/shims/ghcr:/usr/local/bin:/usr/bin:/bin SHA=<40 uns> <var do shim>" wf-ghcr.sh /work`):

| Caso | Código | Saída |
|---|---|---|
| padrão | 0 | `::add-mask::tok` e duas linhas `Tag ... encontrada no GHCR.` |
| `FALSO_TOKEN_JSON='{"erro":1}'`, `VAZIO` | 1 | `::error::O GHCR não devolveu o token anônimo de pull` |
| `FALSO_TOKEN_FALHA=1` (DNS) | **6** | só `curl: (6) Could not resolve host: ghcr.io`, **sem** `::error::` do script (mesmo padrão do `set -e`) |
| `FALSO_HTTP_API=404`, `401` | 1 | `::error::A tag <sha> não está disponível sem login no GHCR (HTTP 404): não existe ou o pacote não é público...` |
| `FALSO_HTTP_MIGRATOR=404` | 1 | primeira tag "encontrada", depois `::error::A tag <sha>-migrator ... (HTTP 404)` |
| `FALSO_HTTP_API=000` (timeout), `429`, `500` | 1 | **a mesma mensagem** "não existe ou o pacote não é público", com `(HTTP 000/429/500)` entre parênteses |

- **RISCO baixa:** timeout (000), limite de taxa (429) e 5xx recebem a mensagem "não existe ou o pacote
  não é público". O código HTTP vem entre parênteses, mas a explicação culpa o pacote. É o caso que o
  roteiro previa ("se só disser isso para um timeout, BUG baixa"); como o `subir` é manual e a falha é
  vermelha (fail closed), nada fica ligado, então registro como RISCO baixa e deixo a triagem ao coordenador.
- O token aparece só em `::add-mask::tok`; nenhuma outra linha o contém.

### QA11-22 — "Ler expires_at e image_sha do state" e "checar o TTL"

**Resultado: OK.** Trilha `c`, `terraform` falso que só responde `output -raw`; passos "Ler ..." do
`destruir`/`limpar` e "Ambiente ativo - checar o TTL" do agendado, extraídos do YAML. Nenhum caso
ausente ou inválido virou `vencido=false`; nenhuma linha extra no `GITHUB_OUTPUT`.

Reproduzir: `source "$QA/lib.sh"; rc 'bash /passos/t22.sh'` (30 casos, cada um nos dois passos).

| `FALSO_EXPIRES` | "Ler" (destruir/limpar) | "TTL" |
|---|---|---|
| ausente (e sha ausente) | marcações `1970-01-01T00:00:00Z` e 40 zeros | `vencido=true`, `::warning::expires_at ausente ou inválido no state: o ambiente conta como vencido.` |
| +3 h, sha válido | os dois valores | `vencido=false`, resumo "restam 3h0min", log "restam 180 min" |
| +50 s | os dois | `vencido=false`, resumo **"restam 0h0min"** e log "restam 0 min" |
| agora exato; 1 h atrás | os dois | `vencido=true` nos dois |
| `2026-11-19T10:00:00.5Z` (passado) | passa | `vencido=true` |
| `2030-11-19T10:00:00+03:00` / `2026-11-19T10:00:00+03:00` | passam | `vencido=false` / `vencido=true` (o fuso é respeitado) |
| `2026-02-30T10:00:00Z`, `2026-11-19T25:00:00Z`, `2026-11-19T1Z`, `0000-00-00T00:00:00Z`, `...T:Z`, `...T...Z`, `...T:::Z`, `+99:99`, `+25:00`, `T24:00:00Z`, `T10:60:00Z`, `T10:00:61Z`, `T1.5:.Z`, sem fuso | **passam pela regex** ("Ler" devolve o valor como veio) | `date` recusa todos: `vencido=true`, aviso "ausente ou inválido", `expires_at` volta à marcação 1970 |
| `20300-11-19T10:00:00Z` | a regex recusa: marcação | `vencido=true` |
| `9999-12-31T23:59:59Z`, `2100-01-01T00:00:00Z` | passam | `vencido=false` ("restam 69890876h29min"): só por edição manual do state, informativo |
| `lixo`, vazio | marcação | `vencido=true` |
| `2030-11-19T10:00:00Z\nimage_sha=evil` | marcação `1970` (a regex recusa a quebra de linha) | `vencido=true`; `GITHUB_OUTPUT` só com as três chaves esperadas |
| sha maiúsculo, 39, `111\nexpires_at=evil` | `image_sha` = 40 zeros | idem; sem linha extra |

Observações:
- **RISCO baixa (informativo):** "Ler" repassa ao `destroy` um `expires_at` que passa pela regex mas é uma
  data impossível (`2026-02-30T10:00:00Z`), enquanto o "TTL" já sabe que `date` a recusa. Só acontece com
  state editado à mão (o `apply` valida o formato antes de gravar). Sem efeito em custo no caminho normal.
- **RISCO baixa:** com menos de 1 min restando, o resumo diz "restam 0h0min" e o log "restam 0 min", com
  `vencido=false`. Não confunde a decisão (o próximo agendado destrói), só a leitura.
- Fail-safe confirmado: qualquer falha de `terraform output` (ausente, vazio, erro) vira vencido, como a
  spec manda ("ausente, inválido ou vencido conta como vencido"). O QA11-23 (instância 1) cobre se o passo
  "Ler o state" impede que um erro transitório de leitura do state destrua um ambiente vivo.

### QA11-08 — Resíduos que a spec cita

**Resultado: OK.** Os 17 tipos da lista foram criados à mão no Floci B (fora do state, sem a tag do projeto,
menos o último que só tem a tag) e todos foram achados com `RESIDUO <tipo> <id>` e saída 1; depois de
removidos, a verificação saiu 0. Runner `b`. Criação: `rb 'bash /passos/t08-cria.sh'` (60 s, todos os
comandos do roteiro aceitos pelo Floci); limpeza: `bash /passos/t08-limpa.sh`.

**Desvio de método (justificado):** em vez de criar, verificar e apagar um tipo por vez (17 x 2 verificações de
cerca de 45 s), criei todos de uma vez, verifiquei, parei a tarefa e agendei a exclusão da KMS, verifiquei de
novo, apaguei tudo e verifiquei pela terceira vez. Cada tipo tem a sua linha própria na saída, então não há
mascaramento; o que o desvio não prova é a ausência de falso negativo de um tipo por interferência do outro.

| Resíduo (criado) | Linha obtida (`verificar-zerado.sh`, saída 1) |
|---|---|
| cluster ECS `qa11-vazio` | `RESIDUO ecs-cluster arn:aws:ecs:us-east-1:000000000000:cluster/qa11-vazio` |
| serviço `qa11-svc` (`desired-count 0`) | `RESIDUO ecs-servico arn:...:service/qa11-vazio/qa11-svc` |
| tarefa em execução (`run-task` da `qa11-td`) | `RESIDUO ecs-tarefa arn:...:task/qa11-vazio/1d636f47...`; **some** depois de `stop-task` (`STOPPED`): 20 linhas em vez de 22 |
| ALB `qa11-alb` | `RESIDUO load-balancer arn:...:loadbalancer/app/qa11-alb/...` |
| target group `qa11-tg` | `RESIDUO target-group arn:...:targetgroup/qa11-tg/...` |
| instância RDS `qa11-db` | `RESIDUO rds-instancia qa11-db` |
| snapshot manual `qa11-snap` | `RESIDUO rds-snapshot-manual qa11-snap` |
| backup automático retido (`qa11-db2`, retenção 1, apagada com `--no-delete-automated-backups`) | `RESIDUO rds-backup-automatico arn:...:auto-backup:ab-...` (duas linhas: o retido e o ativo de `qa11-db`) |
| 3 Elastic IPs | `RESIDUO elastic-ip eipalloc-...` x 3 |
| NAT gateway | `RESIDUO nat-gateway nat-154909a3d525fe6c1` |
| ENI com IPv4 público (`create-network-interface` + `associate-address`) | `RESIDUO eni-ipv4-publico eni-15ad4c76...` (mais a ENI da instância EC2) |
| instância EC2 (`run-instances`, AMI do Floci) | `RESIDUO ec2-instancia i-d4c0af4c...` |
| VPC endpoint (S3, gateway) | `RESIDUO vpc-endpoint vpce-9f7e0b14...` |
| secret `qa11/qualquer` | `RESIDUO secret qa11/qualquer` |
| secret agendado (`delete-secret --recovery-window-in-days 7`) | `RESIDUO secret qa11/agendado (agendado para exclusao)` |
| log group `/ecs/renda-fixa-api-qa11` | `RESIDUO log-group /ecs/renda-fixa-api-qa11` |
| chave KMS de cliente | `RESIDUO kms-chave-cliente 70e1378e-...`; depois de `schedule-key-deletion`, **só** `AVISO kms-chave-agendada-para-exclusao 70e1378e-...` (sem `RESIDUO`) |
| só a tag (VPC `qa11-tag` com `project=renda-fixa-api`) | `RESIDUO recurso-com-tag arn:...:vpc/vpc-75e99ce8` |

Tempos: com 22 resíduos a verificação levou **1 min 08 s** (a 2ª, 20 resíduos, 56 s; a 3ª, com o ambiente
vazio, 1 min 20 s, rodando junto com outras cargas da máquina; o vazio sozinho foi 41 a 46 s, QA11-00 e
QA11-01). Resumo final: `REPROVADA (22 achado(s); o mesmo recurso pode aparecer por mais de uma via)`.

Verificação final (tudo removido; a KMS fica `PendingDeletion` porque a AWS e o Floci não a apagam na hora):
`AVISO kms-chave-agendada-para-exclusao 70e1378e-...` e `Verificação de zerado: APROVADA (nada sobrando na
região us-east-1)`, saída 0.

Esperado: spec "Verificação de zerado" e README "Resíduos". Nenhum item da lista passou despercebido.
Observação (não é bug): o rótulo é `ecs-cluster`, enquanto o roteiro cita "cluster".

### QA11-09 — Resíduos que a spec não cita, ou quase iguais

**Resultado: RISCO** (média). A verificação é de **uma região** e de uma **lista fechada de tipos**, como a spec
diz, mas o README promete mais ("tudo o que sobrou na conta"). Nada fora da lista nem de outra região é
achado, com recurso pago presente, e ela aprova (saída 0). Os casos que a spec cobre se comportaram como
esperado. Runner `b`, Floci B. Criação: `rb 'bash /passos/t09-cria.sh'` (todos os comandos aceitos pelo
Floci); depois, `rb 'bash scripts/verificar-zerado.sh [região]'`.

| Caso | Criado | Obtido | Esperado / leitura |
|---|---|---|---|
| log group parecido | `/ecs/renda-fixa`, `/ecs/Renda-Fixa-Api`, `/aws/lambda/qa11` | **não achados** | OK: só o que contém `renda-fixa-api`, minúsculo |
| log group que contém o nome no meio | `/ecs/xrenda-fixa-apix` | `RESIDUO log-group /ecs/xrenda-fixa-apix` (saída 1) | a spec diz "prefixo do projeto", o script usa `contains` (é mais estrito, fail closed). RISCO baixa: texto da spec e do script divergem (o Container Insights `/aws/ecs/containerinsights/renda-fixa-api/...` só é achado graças ao `contains`) |
| tag parecida | VPC com `Project=renda-fixa-api`, VPC com `project=Renda-Fixa-Api`, VPC com `project="renda-fixa-api "` | **nenhuma achada** | OK: a busca por tag diferencia maiúsculas e espaços |
| bucket S3 com a tag do projeto | `qa11-bucket` + `put-bucket-tagging project=renda-fixa-api` | `AVISO 1 recurso(s) com a tag do projeto em s3/iam/budgets ignorado(s): são do bootstrap, que não deveria usar a tag project`; sem `RESIDUO` | OK (contrato com o bootstrap) |
| ELB clássico | `aws elb create-load-balancer --load-balancer-name qa11-classico ...`, sem tag | **não achado** | RISCO: `elbv2 describe-load-balancers` não lista o clássico, que cobra por hora |
| volume EBS | `ec2 create-volume --size 1 --availability-zone us-east-1a`, sem tag | **não achado** | RISCO: fora da lista; cobra |
| cluster Aurora sem instância | `rds create-db-cluster --engine aurora-postgresql ...` | **não achado** | RISCO: `describe-db-instances` não lista cluster; cobra armazenamento |
| hosted zone, alarme, repositório ECR | `route53 create-hosted-zone`, `cloudwatch put-metric-alarm`, `ecr create-repository` | **não achados** | RISCO baixa: custo pequeno |
| **outra região** | secret `qa11/outra-regiao`, EIP e cluster `qa11-ohio` em `us-east-2` | `verificar-zerado.sh us-east-1`: **APROVADA** (exit 0, 1 min 13 s, com volume, ELB clássico, Aurora, zona, alarme, ECR e os recursos de `us-east-2` presentes). `... us-east-2`: `REPROVADA (3 achado(s))`: `ecs-cluster`, `elastic-ip`, `secret` | a verificação é de uma região só (spec: "na região") |
| muitos secrets | 60 secrets `qa11/pag-NN` | 60 linhas `RESIDUO secret ...`, saída 1, 49 s | OK (a CLI pagina) |

Limpeza: tudo removido (secrets, EIP e cluster de `us-east-2`, log groups, VPCs de tag, bucket, ELB, volumes,
cluster Aurora, zona, alarme, ECR). A KMS do QA11-08 ficou `PendingDeletion` (só `AVISO`).

#### RISCO (média) — o README promete "tudo o que sobrou na conta" e a verificação olha uma região e uma lista fechada

- **Fonte da expectativa:** README linha 780 a 781: "No CloudShell, rode `bash infra/aws/scripts/verificar-zerado.sh
  us-east-1` ... Ele lista, por tipo e identificador, **tudo o que sobrou na conta**, inclusive o que o state não
  conhece". O README não diz que a verificação é de uma região (o CloudShell abre na região do console: o
  operador que esquecer o argumento ou rodar com `AWS_REGION` de outra região recebe `APROVADA` sem ter olhado
  `us-east-1`; o `AWS_REGION` do CloudShell é a região do console) nem que a lista de tipos é fechada.
- **Alcance real do risco:** o que o `destroy` do Terraform pode deixar (ECS, ALB, RDS, ENIs, EIP, secret, log
  groups, snapshots) está na lista, e a role do CI só escreve em `us-east-1` (`aws:RequestedRegion`, QA11-29),
  então os casos acima só aparecem por criação manual fora do Terraform. Por isso RISCO e não BUG.
- **Sugestão:** ajustar o texto do README ("só `us-east-1`, só estes tipos") ou fazer o script avisar quando a
  região difere de `us-east-1`; considerar `elb`, `ec2 describe-volumes` e `rds describe-db-clusters` na lista.

### QA11-29 — A policy da role do CI contra o que os jobs e os scripts chamam

**Resultado: OK** quanto à cobertura (toda chamada dos scripts, do workflow e do Terraform tem ação
correspondente na policy, e o Deny bate com a spec). Dois **RISCO** baixa (cabeçalho do script e margem do
limite de 10 KiB) e uma lista de ações permitidas com potencial de custo (RISCO). O que só a AWS real
confirma está no fim, para o P8.

Reproduzir: `cd "$QA/arvore" && bash infra/aws/bootstrap/bootstrap.sh --imprimir-policy-ci > "$QA/saidas/policy-ci.json"`
(saída 0, 8 918 bytes, JSON válido; conta fictícia `123456789012`), lida com o `jq` do runner `c`
(`docker exec -i qa11c-runner jq -r '.Statement[] | ...' < policy-ci.json`). Além do cruzamento estático,
coletei as **operações reais** do provider `aws` 6.73.0 num ciclo completo no Floci B
(`TF_LOG=DEBUG TF_LOG_PATH=/tmp/tf-sobe.log bash /passos/sobe.sh` e o mesmo para `desce.sh`; `grep -o
'Action=[A-Za-z]*'` nas operações query EC2/ELB/RDS e o log do Floci nas operações JSON de ECS, Logs e Secrets).
Ciclo: `apply` 2 min 48 s, `destroy` 2 min 10 s, 27 recursos.

Estrutura da policy: 21 statements, 18 `Allow` e 3 `Deny`. Não há `iam:Create*`, `iam:Put*`, `iam:Attach*`,
`budgets:*` nem `*:*`; a única ação `iam:` é `iam:PassRole` (ARN da execution role, `iam:PassedToService =
ecs-tasks.amazonaws.com`). Escrita de EC2, ELB, ECS e RDS: `aws:RequestedRegion = us-east-1`; logs, secrets, S3,
`RunTask` e `StopTask` são limitados por ARN de `us-east-1`.

**Chamada x ação IAM x coberta?**

| Quem chama | Chamada | Ação IAM exigida | Statement | Coberta |
|---|---|---|---|---|
| `verificar-zerado.sh` | `ecs list-clusters`, `describe-clusters`, `list-services`, `list-tasks`, `describe-tasks` | `ecs:ListClusters`, `DescribeClusters`, `ListServices`, `ListTasks`, `DescribeTasks` | `EcsLeituraEVerificacao` | sim (as duas últimas "novas", S4 e I1, estão) |
| idem | `elbv2 describe-load-balancers`, `describe-target-groups` | `elasticloadbalancing:Describe...` | `ElbLeituraEVerificacao` | sim |
| idem | `rds describe-db-instances`, `describe-db-snapshots`, `describe-db-instance-automated-backups` | `rds:Describe...` | `RdsLeituraEVerificacao` | sim |
| idem | `ec2 describe-nat-gateways`, `-addresses`, `-network-interfaces`, `-instances`, `-vpc-endpoints` | `ec2:Describe*` | `Ec2LeituraEVerificacao` | sim |
| idem | `secretsmanager list-secrets --include-planned-deletion` | `secretsmanager:ListSecrets` | `SecretsSemRecursoEVerificacao` | sim |
| idem | `logs describe-log-groups` | `logs:DescribeLogGroups` | `LogsListarGrupos` | sim |
| idem | `kms list-keys`, `describe-key` | `kms:ListKeys`, `kms:DescribeKey` | `VerificacaoKmsEtags` | sim |
| idem | `resourcegroupstaggingapi get-resources` | `tag:GetResources` | `VerificacaoKmsEtags` | sim |
| idem | `sts get-caller-identity` | (sem permissão) | n/a | sim |
| `parar-migracao.sh` | `ecs list-tasks`, `ecs stop-task` | `ecs:ListTasks`; `ecs:StopTask` em `task/renda-fixa-api/*` | `EcsLeituraEVerificacao`; `EcsStopTaskSoDoCluster` | sim |
| workflow `subir` | `ecs run-task ... --propagate-tags TASK_DEFINITION` | `ecs:RunTask` (task definition `renda-fixa-migrator:*`, `ecs:cluster`), `ecs:TagResource`, `iam:PassRole` | `EcsRunTaskSoDaMigracao`, `EcsEscrita`, `PassarSoAExecutionRole` | sim (a família sem revisão resolve para `:<rev>`: confirmar no P8) |
| workflow | `ecs describe-tasks`, `describe-services`, `ecs wait services-stable` | `ecs:DescribeTasks`, `DescribeServices` | `EcsLeituraEVerificacao` | sim |
| workflow | `elbv2 describe-target-health` | `elasticloadbalancing:DescribeTargetHealth` | `ElbLeituraEVerificacao` | sim |
| workflow | `logs tail ... --log-stream-name-prefix` | `logs:FilterLogEvents` (e `GetLogEvents`) no log group `/ecs/renda-fixa-api` | `LogsDoProjeto` (`...:/ecs/renda-fixa-api` e `...:*`) | sim |
| Terraform (state) | init/plan/apply com `use_lockfile` | `s3:ListBucket`; `Get/Put/DeleteObject` em `aws.tfstate` e `aws.tfstate.tflock` | `StateListarBucket`, `StateObjetos` | sim |
| Terraform `apply` | operações observadas: `CreateVpc`, `ModifyVpcAttribute`, `CreateSubnet`, `CreateInternetGateway`, `AttachInternetGateway`, `CreateRouteTable`, `CreateRoute`, `AssociateRouteTable`, `CreateSecurityGroup`, `AuthorizeSecurityGroupIngress/Egress`, `RevokeSecurityGroupEgress`, `CreateDBSubnetGroup`, `CreateDBInstance`, `CreateLoadBalancer`, `ModifyLoadBalancerAttributes`, `CreateTargetGroup`, `ModifyTargetGroupAttributes`, `CreateListener`, ECS (`CreateCluster`, `RegisterTaskDefinition` x2, `CreateService`), `CreateLogGroup`, `PutRetentionPolicy`, `CreateSecret`, `PutSecretValue`, `GetRandomPassword` e as leituras (`DescribeVpcAttribute`, `DescribeNetworkAcls`, `DescribeCapacityReservation`, `DescribeListenerAttributes`, `DescribeTags`, `ListTagsForResource`, `GetResourcePolicy`, `ListSecretVersionIds`...) | todas nas listas de escrita e leitura | `Ec2RedeEscrita`, `Ec2Leitura...`, `ElbEscrita`, `ElbLeitura...`, `EcsEscrita`, `EcsLeitura...`, `RdsEscrita`, `RdsLeitura...`, `LogsDoProjeto`, `SecretsDoProjeto`, `SecretsSemRecurso...` | sim, **nenhuma operação sem ação correspondente** |
| Terraform `destroy` | `UpdateService`, `DeleteService`, `DeleteCluster`, `DeregisterTaskDefinition` x2, `DeleteListener`, `DeleteLoadBalancer`, `DeleteTargetGroup`, `DeleteDBInstance`, `DeleteDBSubnetGroup`, `DeleteLogGroup`, `DeleteSecret`, `DeleteSecurityGroup`, `RevokeSecurityGroupIngress/Egress`, `DisassociateRouteTable`, `DeleteRouteTable`, `DetachInternetGateway`, `DeleteInternetGateway`, `DeleteSubnet`, `DeleteVpc` | todas | os mesmos statements de escrita | sim (o provider 6.73.0 desregistra a task definition com `DeregisterTaskDefinition`, coberta) |

**Deny x spec (linha 317 em diante da spec):** `ec2:CreateNatGateway`, `ec2:AllocateAddress`, `ec2:RunInstances`,
`ec2:CreateVpcEndpoint`, `ec2:CreateTransitGateway`, `kms:CreateKey` e `route53:CreateHostedZone` estão em
`NegarServicosCarosEmQualquerRegiao`, sem condição de região (qualquer região). `rds:CreateDBInstance` com
`rds:DatabaseClass` diferente de `db.t4g.micro` (`StringNotEquals` + `Null` falso) e com `rds:MultiAz` verdadeiro
estão em dois statements, estendidos a `ModifyDBInstance`, `RestoreDBInstance*` e `CreateDBInstanceReadReplica`
(mais que a spec, no bom sentido). Tudo o que a spec lista está lá.

**RISCO baixa (cabeçalho):** o cabeçalho de `verificar-zerado.sh` ("Permissões de leitura que a role do CI precisa")
lista só `ecs:ListClusters/ListServices/ListTasks`; o script também chama `ecs:DescribeClusters` e
`ecs:DescribeTasks` (a policy as tem). Quem montar a role só pelo cabeçalho teria a verificação saindo 2.

**RISCO baixa (margem):** o limite de 10 240 caracteres (sem espaço) das policies inline da role é conferido
pelo `bootstrap.sh` (`LIMITE_POLICY_INLINE`); a policy usa **7 155** (saída do bootstrap: `Policy renda-fixa-gha:
7155 de 10240 caracteres`), sobra cerca de 30%. Correções do P8 que acrescentem muitas ações cabem, mas a margem
não é grande; a execution role usa 393.

**RISCO (ações permitidas que poderiam criar algo pago fora do desenho; cruzado com o QA11-09):**
- `elasticloadbalancing:CreateLoadBalancer` em `*`: o nome e o prefixo da ação IAM são os mesmos do ELB clássico,
  do NLB e do GWLB, então o statement permite criar qualquer tipo e quantos forem. Só a trava (QA11-04, M16)
  limita o número e o tipo.
- `rds:CreateDBInstance` e `ModifyDBInstance`: o Deny limita classe e Multi-AZ, mas não armazenamento,
  IOPS, retenção de backup, Performance Insights nem exportação de logs (só a trava e o `terraform test`).
- `ecs:UpdateClusterSettings`/`UpdateCluster` (liga Container Insights), `ecs:CreateService` e `UpdateService`
  (`desired_count` sem teto), `RegisterTaskDefinition` (CPU e memória livres), `secretsmanager:CreateSecret` em
  `renda-fixa-api/*` (sem teto): idem, só a trava limita.
- `ec2:CreateTags`/`DeleteTags` em `*` (na região): permite apagar a tag `project` de qualquer recurso e
  esconder um órfão da busca por tag.
- `secretsmanager:GetSecretValue` e `s3:DeleteObject` no state: riscos aceitos no README.
Nenhuma delas é alcançável sem alguém com acesso ao `environment: aws` da `main`; por isso só RISCO.

**Vai para o P8 (só a AWS confirma):** (1) a autorização de `ecs:RunTask` quando `--task-definition` é só a
família, contra `renda-fixa-migrator:*` e a condição `ecs:cluster`; (2) se a criação do RDS com a chave
gerenciada `aws/rds` e a criação de um ALB pedem alguma ação a mais (por exemplo `ec2:Describe*` extra ou
`kms:CreateGrant`) na identidade da role, que o Floci não exige; (3) se a leitura de `aws_cloudwatch_log_group` e
de `aws_ecs_*` pede `logs:ListTagsForResource` com o ARN com e sem `:*` e `ecs:ListTagsForResource` (a
policy cobre as duas formas de ARN do log group); (4) se o destroy do ambiente real chama só
`DeregisterTaskDefinition` ou também `DeleteTaskDefinitions`, que não está na policy; (5) o `AccessDenied` de
qualquer item acima.

### QA11-28 — `bootstrap.sh` contra o Floci

**Resultado: RISCO** (sem BUG). O bootstrap faz o que o README e o cabeçalho prometem: cria tudo, é
idempotente (a segunda execução diz "já existe"/"atualizada"), imprime só identificadores, a verificação de
zerado aprova depois dele. Um item é **limitação do Floci** (o `iam update-role` quebra a CLI na 2ª execução) e
registrado como tal. Os riscos são de entrada e de aviso (abaixo). Floci B e runner `b` **recriados** (vazios,
sem a execution role do `init.sh`; o filho `floci-aws-ec2-volume-helper` do B foi removido junto). Drivers:
`$QA/passos/t28-emails.sh`, `shims/aws-floci-updaterole`, `shims/aws-sem-budgets`.

**1ª execução** (`rb 'time bash bootstrap/bootstrap.sh --email voce@exemplo.com'`): saída 0, **37 s**, `stderr`
vazio. Saída (só identificadores, e-mail ausente: `grep -c exemplo.com` = 0 nas quatro capturas):
`Policy renda-fixa-gha: 7155 de 10240 caracteres.`, `Policy renda-fixa-api-execution: 393 de 10240 caracteres.`,
`Bucket do state criado`, `Provedor OIDC do GitHub criado.`, `Role criada: renda-fixa-gha`, `Role criada:
renda-fixa-api-execution`, três `Service-linked role criada`, `Budget criado: renda-fixa-api-mensal` e o bloco
`AWS_ROLE_ARN`, `AWS_REGION`, `TF_STATE_BUCKET`.

**2ª execução, sem alteração:** saída **255**, 20 s, `aws: [ERROR]: 'UpdateRoleResult'`, parando em `Role atualizada`.
Causa: o Floci aplica o `iam update-role` (conferido: `MaxSessionDuration` e descrição mudaram) mas devolve o XML sem
`<UpdateRoleResult/>`, e a CLI v2 aborta ao analisar a resposta. Reproduzir direto: `aws iam update-role --role-name
renda-fixa-gha --max-session-duration 7200` (255). **É do emulador, não do script**: o que a AWS real devolve só se
confirma no P8. Para exercitar o resto da idempotência embrulhei o `aws` num shim que ignora só esse erro de
parse (`/passos/shims/aws-floci-updaterole`; todas as outras chamadas passam direto).

**2ª execução (com o shim):** saída 0, 46 s, `stderr` vazio: `Bucket do state já existe`, `Provedor OIDC do GitHub já
existe.`, `Role atualizada` x2, `Service-linked role já existe` x3, `Budget atualizado: renda-fixa-api-mensal`. Sem erro
no que já existia. Resultado final conferido (`get-bucket-*`, `iam`, `budgets`):

| Item | Obtido | Esperado (README, spec) |
|---|---|---|
| versionamento | `{"Status":"Enabled"}` | habilitado |
| expiração de versões antigas | regra `expirar-versoes-antigas-do-state`, `NoncurrentDays: 7` | 7 dias |
| Block Public Access | os quatro `true` | sim |
| bucket policy | `NegarSemTls` (`aws:SecureTransport = false`) e `NegarTlsAntigo` (`s3:TlsVersion < 1.2`), ambas `Deny` | negação sem TLS |
| criptografia | `AES256` (SSE-S3) | SSE-S3 |
| role `renda-fixa-gha` | `MaxSessionDuration 7200`; só a inline `renda-fixa-gha-ambiente-efemero`; trust com `aud = sts.amazonaws.com` e `sub = repo:matheuscardosolazzarini/renda-fixa-api:environment:aws` | 7200 s, trust só do environment `aws` |
| OIDC | 1 provedor `token.actions.githubusercontent.com` | sim |
| budget | `renda-fixa-api-mensal`, US$ 5, `MONTHLY`, `IncludeCredit: false` | US$ 5 sem créditos |
| região do bucket | `LocationConstraint: null` (= `us-east-1`) | `us-east-1` |
| `verificar-zerado.sh` depois do bootstrap | **APROVADA**, saída 0 (42 s), nenhum `AVISO` de s3 (o bootstrap usa `stack=bootstrap`) | aprova (contrato do bootstrap) |

**Passo 1, policy gerenciada anexada à mão** (`iam create-policy qa11-extra` e `attach-role-policy`; bootstrap de
novo): saída **0**, `stderr`: `AVISO a role renda-fixa-gha tem policy gerenciada anexada: remova, a policy dela deve
ser só a inline`; a policy continua anexada e o bootstrap termina com "Bootstrap concluído". **RISCO baixa**
(decisão 5): só avisa, como o cabeçalho promete; o aviso sai em `stderr` e antes do bloco final.

**Passo 2, e-mails** (`rb 'bash /passos/t28-emails.sh'`: `aws` falso que aborta na 1ª chamada, para ver só a validação):

| Entrada | Obtido |
|---|---|
| vazio, `a@b`, `a b@c.com`, `a"b@c.com`, `a@b.c`, `álvaro@x.com`, `a@.com`, `x@y.com,z@w.com`, `x@y.com\n` | rejeitados, saída 1, `ERRO informe o e-mail dos alertas do budget com --email ou BUDGET_EMAIL (ele não fica no script)` |
| `voce@exemplo.com` (o exemplo do README) | **aceito** |
| 290 caracteres antes do `@` | aceito (o limite do Budgets é do P8) |
| `a@b..com` | aceito (ponto duplo) |
| `BUDGET_EMAIL=x@y.com` com `--email a@b` | rejeitado: `--email` prevalece (e o inválido derruba) |
| `BUDGET_EMAIL=a@b` com `--email z@w.com` | aceito: `--email` vale |

- **RISCO baixa a média:** `voce@exemplo.com`, copiado literalmente do README, é aceito e o alerta de orçamento
  (camada 5, a rede de segurança de custo) iria para um domínio de exemplo sem ninguém perceber. Sugestão: rejeitar
  `exemplo.com`/`example.*` ou confirmar o e-mail digitado.
- **RISCO baixa (texto):** e-mail inválido recebe a mensagem "informe o e-mail...", que não diz que o valor dado é
  inválido; `--help` e `-h` não existem (QA11-01 t).

**Passo 3, região** (bucket apagado antes; `AWS_REGION=sa-east-1 AWS_DEFAULT_REGION=sa-east-1 bash bootstrap/bootstrap.sh
--email ...`): saída 0, `Bucket do state criado`, `get-bucket-location` = `null` (`us-east-1`). O script força
`us-east-1` (`export AWS_DEFAULT_REGION="$REGION" AWS_REGION="$REGION"`) e ignora o do CloudShell. OK.

**Passo 4, falha no meio** (`budgets` falhando, shim `aws-sem-budgets`, budget apagado antes): saída **254**,
`stderr`: `aws: [ERROR]: An error occurred (AccessDeniedException) when calling the create-budget operation: ...` (sem
`ERRO ...` do script). Bucket, OIDC, roles e service-linked roles ficaram criados; a execução seguinte (sem o
shim) fez `Budget criado` e o resto "já existe"/"atualizada", sem quebrar. **RISCO baixa:** o erro do último passo
sai cru da CLI, como no QA11-01 (u).

Limpeza: `qa11-extra` desanexada e apagada; o Floci B vai embora com a trilha.

### QA11-30 — Bundle e imagens (preparação da trilha D)

**Resultado: OK.** Tudo na cópia (`$QA/arvore`), nunca no repositório. Nomes com prefixo `qa11d`. Como a
imagem da API só entra no QA11-32 (P2, fora do escopo), **não foi montada**; registro só que o `Dockerfile` da
API não mudou na F11 (`git status --short Dockerfile` vazio; último commit dele: `14337a7 F6: Dockerfile, compose
completo, CI e cobertura confiavel`).

- **Bundle** (mesmo comando do `release.yml`, num contêiner do SDK com `--memory 2g` para proteger a
  instância 1, que fazia `apply` ao mesmo tempo): `docker run --rm --name qa11d-sdk --memory 2g -v
  "$WQA\arvore:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 bash -c 'dotnet tool restore && dotnet ef migrations
  bundle --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api --configuration Release
  --self-contained --target-runtime linux-x64 --output artifacts/efbundle --force && ls -la artifacts'`.
  Saída 0 em cerca de **2 min 30 s** (restore do NuGet incluído; pico amostrado do contêiner 551 MiB aos 90 s, sem
  OOM nem evento de morte nos outros contêineres). `artifacts/efbundle`: 125 134 659 bytes (cerca de 119 MiB,
  `-rwxr-xr-x`). Aviso do próprio `dotnet ef`: "copy appsettings.json alongside your bundle" (a imagem não leva
  `appsettings.json`: a connection string vem só da variável, como a spec manda).
- **Imagem:** `docker build -f arvore/Dockerfile.migrator -t qa11d-migrator:tmp arvore/artifacts` (a base
  `mcr.microsoft.com/dotnet/runtime-deps:8.0` estava no cache da preparação). `docker image inspect
  qa11d-migrator:tmp --format 'user={{.Config.User}} entry={{json .Config.Entrypoint}} size={{.Size}}'` =
  `user=app entry=["/app/efbundle"] size=100867524`: usuário sem privilégio `app`, entrypoint do bundle. O
  `--chmod=0755` do `COPY` preservou o bit de execução (o bundle roda).
- Rede e bancos: rede `qa11d-net` (172.19.0.0/16), PostgreSQL `qa11d-pg` e `qa11d-pg-tls` (`postgres:16@sha256:f9a7...`,
  sem porta publicada), senha sentinela de 32 alfanuméricos. Nada tocou `fixedincome-api`, `fixedincome-db` nem as
  portas 5432 e 8080.

### QA11-31 — Migração fora do caminho feliz

**Resultado: RISCO** (sem BUG; dois RISCO baixa). Todo caso de falha saiu diferente de 0 com mensagem legível e
**nenhuma** das 20 capturas contém a senha sentinela, a senha errada nem a connection string (`grep -c "$SENHA"` = 0 e
nenhum `Host=`/`Username=`; as duas linhas com "password" são o texto `password authentication failed for user
"fixedincome"` do PostgreSQL e o resumo do próprio QA). Drivers: `bash "$QA/passos/t31.sh" <caso>` e
`t31b.sh interrompida|a-frente`; saídas em `$QA/saidas/31-*.txt`. Imagem `qa11d-migrator:tmp`, rede `qa11d-net`,
`SENHA` (a sentinela de 32 alfanuméricos do roteiro).

| Caso | Montagem | Saída | Tempo | Mensagem / observação |
|---|---|---|---|---|
| caminho feliz (controle) | `-e ConnectionStrings__DefaultConnection=...` | 0 | 5 s | 3 `Applying migration ...`, `Done.`; 5 tabelas, `__EFMigrationsHistory` com 3 linhas |
| variável ausente | sem a variável | **1** | 3 s | `Connection string do banco não configurada. Defina a variável de ambiente ConnectionStrings__DefaultConnection ...` (cita a variável) |
| só `--connection` | `qa11d-migrator:tmp --connection "<conn>"` | **1** | 2 s | a mesma mensagem: o argumento é ignorado, a variável é obrigatória (spec: a fábrica exige a variável) |
| host inexistente | `Host=qa11d-nao-existe` | **1** | 5 s | `SocketException: Name or service not known` |
| IP da rede sem ninguém | `Host=172.19.77.77` | **1** | 6 s | `No route to host` |
| IP que descarta pacotes | `Host=192.0.2.1` | **1** | **18 s** | `System.TimeoutException: Timeout during connection attempt` (o limite padrão do Npgsql, 15 s, mais a partida: bem abaixo dos 10 min do `subir`) |
| porta fechada | `Host=qa11d-semporta` (contêiner sem nada escutando; parar o `qa11d-pg` só faz o nome sumir do DNS) | **1** | 3 s | `Connection refused` |
| senha errada | senha `QA11errada...` com o servidor no ar | **1** | 5 s | `28P01: password authentication failed for user "fixedincome"`; a senha não aparece |
| banco inexistente | `Database=qa11_nao_existe` | **0** | 4 s | **cria o banco** e aplica as 3 migrations (`bancos: postgres,fixedincome,qa11_nao_existe`). No RDS o banco existe; fica registrado: erro de digitação no nome do banco não falha, cria outro |
| usuário sem privilégio | role `qa11_semdireito` (`LOGIN`, sem `CREATE` no `public`), banco novo | **1** | 3 s | `42501: permission denied for schema public` ao criar `__EFMigrationsHistory`; o log traz o SQL, não segredo |
| TLS obrigatório | `qa11d-pg-tls` (`hostnossl ... reject`), connection string **sem** `SSL Mode` | **0** | 4 s | 3 migrations aplicadas: o Npgsql negocia TLS por padrão. Controle com `SSL Mode=Disable`: saída **1**, `28000: pg_hba.conf rejects connection for host "172.19.0.5", user "fixedincome", database "fixedincome", no encryption` |
| interrompida | migração terminando em 2 s: kill no meio por `docker pause qa11d-pg` 1,8 s depois do início e `kill -s KILL` do migrador (saída 137) | 137 | n/a | **1 migration aplicada, 3 tabelas** (estado parcial, transação por migration); a **segunda execução** termina com saída 0, 5 tabelas e 3 linhas no histórico. Os cortes mais cedo (1,0 e 1,4 s, e `docker stop -t 1` aos 0,6 a 1,95 s, saída 143) pegaram o bundle antes de qualquer escrita: banco vazio e a 2ª execução também termina |
| banco à frente | linha extra `29991231000000_Futura` em `__EFMigrationsHistory`, depois rodar o bundle | **0** | 2 s | `No migrations were applied. The database is already up to date.` (a linha desconhecida é ignorada em silêncio) |

Esperado: spec D3 e P8 ("ausência de segredo nos logs, inclusive no `aws logs tail` da falha da migração"): todo
caso de falha diferente de 0, mensagem legível, em tempo compatível com os 10 min do `subir`, sem segredo. Atendido.

**RISCO baixa:** banco à frente. Prorrogar um ambiente no ar com um `sha` antigo (caso citado no roteiro) roda o
bundle antigo num banco que já tem uma migration mais nova: sai 0 e diz "up to date", e a API antiga sobe contra
um esquema do futuro. Como o ambiente é efêmero e o banco nasce novo a cada `subir`, só afeta uma prorrogação com
troca de SHA; vale um aviso ou uma verificação no workflow. (Não conferi o que o README diz sobre isso.)

**RISCO baixa:** banco inexistente é criado em silêncio (comportamento do `Migrate()`); na AWS o `db_name` do RDS é
criado pelo Terraform, então só aparece se `Database=` for editado à mão.

Notas: a saída das falhas é um stack trace do .NET de 25 a 37 linhas em inglês (a mensagem de configuração, em
português, vem na primeira linha). O bundle só despeja o log ao terminar (um `docker logs` durante a execução vinha
vazio): na falha do `subir` o `aws logs tail` só mostra algo depois que a tarefa para. A migração das 3 migrations leva
cerca de 5 s no total.

### Resumo da instância 2 (trilhas B, C, D e Git Bash)

| Roteiro | Resultado | Onde |
|---|---|---|
| QA11-01 parte C (`bootstrap.sh` sem rede e sem credencial) | RISCO | seção "QA11-01 (parte C)" |
| QA11-01 parte B (entradas e códigos de saída dos scripts) | RISCO | seção "QA11-01 (parte B)" |
| QA11-02 (endpoint lento ou parando, tempo da verificação) | **BUG-1** (média) | seção "QA11-02" |
| QA11-04 (mudanças de operador contra a trava) | RISCO (M08, M10, M11, M18, M20) | seção "QA11-04" |
| QA11-05 parte B (mudanças que impedem o `destroy`, contra a trava) | **BUG-3** (alta) | seção "QA11-05 (parte B)" |
| QA11-08 (resíduos que a spec cita) | OK | seção "QA11-08" |
| QA11-09 (resíduos que a spec não cita, outra região) | RISCO (média) | seção "QA11-09" |
| QA11-18 (Validar entradas) | OK | seção "QA11-18" |
| QA11-20 (Resolver SHA, GHCR) | OK (RISCO baixa de texto) | seção "QA11-20" |
| QA11-22 (Ler state, TTL) | OK | seção "QA11-22" |
| QA11-28 (`bootstrap.sh` no Floci) | RISCO (a 2ª execução só passa com shim: limitação do Floci) | seção "QA11-28" |
| QA11-29 (policy da role do CI) | OK (RISCO baixa e lista de ações com potencial de custo) | seção "QA11-29" |
| QA11-30 (bundle e imagens) | OK | seção "QA11-30" |
| QA11-31 (migração fora do caminho feliz) | RISCO (baixa) | seção "QA11-31" |
| QA11-05 parte A, 07, 11, 12, 13, 23, 25, 26, 33 | **NÃO EXECUTADO por esta instância** (são da instância 1, por divisão do coordenador; o QA11-33 também) | n/a |

**Bugs (ordem de severidade):**
1. **BUG-3 (alta)**: `lifecycle { prevent_destroy = true }` (por exemplo no RDS) não é barrado pela trava nem pelo `terraform test`;
   o `terraform destroy` aborta no plano sem destruir nada (reproduzido no Floci com dois recursos), então o
   `destruir`, o `limpar` e o agendado ficam vermelhos com o ambiente ligado. Teste sugerido:
   `nenhum_recurso_tem_prevent_destroy` (passo `grep` ou `tests/lifecycle/rodar.sh` no job `validar`).
2. **BUG-1 (média)**: `scripts/parar-migracao.sh` sai 1 com `ERRO ecs list-tasks: ` vazio quando a falha não é um
   erro com código da AWS (rede, timeout, região), e, como o passo `Parar tarefas de migração` não tem
   `continue-on-error` nem `always()`, essa falha **pula o `terraform destroy`** em `destruir`, `limpar` e no
   agendado. Testes sugeridos: `tests/parar-migracao/rodar.sh` (`mensagem_cita_a_causa_quando_a_chamada_falha_sem_codigo_da_aws`,
   `sai_1_com_regiao_ausente_citando_a_regiao`) e a checagem do workflow (`continue-on-error: true` ou `always()`).

**Riscos relevantes** (sem BUG pela regra da decisão 2, detalhes nas seções): a trava não olha
`enabled_cloudwatch_logs_exports`, Container Insights, disco efêmero, número de secrets nem `storage_throughput`
(os dois primeiros e `deletion_protection`, `skip_final_snapshot`, `delete_automated_backups`,
`recovery_window_in_days` são barrados só pelo `terraform test` do job `validar`); a verificação de zerado é de uma
região e de uma lista fechada de tipos e o README diz "tudo o que sobrou na conta"; `voce@exemplo.com`, o exemplo do
README, é aceito como e-mail do orçamento; `bootstrap.sh` sem rede ou credencial sai 253/255 com a mensagem crua da
CLI; mensagens enganosas de timeout ("não existe ou o pacote não é público", "sem credencial válida ou sem rede");
banco à frente aprovado como "up to date".

**Defeitos do roteiro e do ambiente (não são da fase):**
1. O runner `qa11-runner:tmp` não tem `diff` nem `tar` (QA11-28 e o controle de mutação usam `diff`): usei `cksum` e
   `find -newer`. Só tem os locales `C` e `C.UTF-8`: o QA11-18 não cobre `en_US.UTF-8`. Sem TTY neste ambiente: o
   QA11-01 (n) ficou por leitura.
2. O `docker exec ... bash /passos/...` com `MSYS_NO_PATHCONV=1` deve ser usado em todo comando (já está no `lib.sh`);
   `node` precisa de `MSYS_NO_PATHCONV` desligado para ler caminhos do Windows.
3. `aws iam update-role` no Floci aplica a mudança mas a CLI v2 aborta (`'UpdateRoleResult'`): a 2ª execução do
   bootstrap sai 255 no emulador. Para o P8, confirmar que na AWS real passa.
4. O Floci cria contêineres auxiliares **globais** que o roteiro não lista (`floci-aws-ec2-volume-helper` e o volume
   `floci-aws-ec2-volumes`, depois de `ec2 run-instances`/`create-volume`; imagens `registry:2` e
   `public.ecr.aws/amazonlinux/amazonlinux:2`, depois de `ecr create-repository`/`run-instances`). A limpeza do
   roteiro precisa citá-los.
5. Um passo do roteiro (QA11-31, "porta fechada" com `docker stop qa11-pg`) não testa porta fechada: com a rede
   definida pelo usuário o nome some do DNS. Usei um contêiner `sleep` na mesma rede.
6. Dois `docker run --name qa11d-mig` em paralelo se destroem (o `mig()` remove o contêiner anterior): rodar os
   casos da trilha D em sequência.

**Tempo gasto:** cerca de 1 h 40 min de relógio na sessão retomada (mais a leitura inicial do roteiro, antes da
interrupção). Pico de memória observado: cerca de 3 GiB somados com a instância 1 durante os planos paralelos; sem OOM.

**Limpeza:** Floci B, runners `qa11b` e `qa11c`, `floci-aws-ec2-volume-helper`, volumes `qa11b-work`, `qa11c-work`,
`qa11c-plugins`, `floci-aws-ec2-volumes`, contêineres `qa11d-*`, rede `qa11d-net`, imagem `qa11d-migrator:tmp`, pasta
`arvore/artifacts` do rascunho e as imagens `registry:2` e `amazonlinux:2` (baixadas pelo Floci) removidos.
**Mantidos de propósito:** `qa11b-plugins` (cache do provider, 772 MB, o prompt da preparação os mantém) e
`qa11-runner:tmp`. Nada da instância 1 foi tocado (`qa11a-*`, `floci-aws-rds-db-*` do Floci A). `fixedincome-api` e
`fixedincome-db` seguem no ar, repositório sem `floci_override.tf`/`floci.tfstate`; `git status` só difere do inicial
por `docs/QA-F11.md`.

## Escopo, bugs e veredito (coordenador)

Preenchido pelo coordenador em 20/11/2026, a partir das duas seções acima e da triagem abaixo. A instância 1
foi encerrada por decisão do Matheus antes de terminar o QA11-05 (parte A) e o QA11-33; o que ela já tinha
de resultado está na seção dela.

### Escopo

**Executados (P1):**
- Trilha A: QA11-07, 11, 12, 13, 23, 25 e 26 completos; QA11-05 (parte A) e QA11-33 parciais.
- Trilhas B, C e D: QA11-01 (B e C), 02, 04, 05 (parte B), 08, 09, 18, 20, 22, 28, 29, 30 e 31. Além deles,
  o QA11-00 da preparação.

**P1 da trilha A que não rodaram: `NÃO EXECUTADO (instância encerrada por tempo, 20/11/2026)`:**

| Roteiro | O que não rodou | Cobertura que existe |
|---|---|---|
| QA11-05 (parte A), V4 | o "caminho do operador" depois do `destroy` | V4 rodou até a verificação, que deu 0. O Floci não reproduz o efeito, então vai para o P8 |
| QA11-05 (parte A), V5 (`prevent_destroy`) | o ciclo `apply`/`destroy` no Floci A | o efeito foi mostrado pela instância 2 (BUG-3) e reproduzido na triagem |
| QA11-05 (parte A), V6 (`recovery_window_in_days = 30`) | o ciclo `apply`/`destroy` no Floci A | só a parte B: a trava aprova e o `terraform test` reprova |
| QA11-33, parte B | o README seguido ao pé da letra contra o Floci | só a leitura do README (B-A2, B-A3 e B-A4) |

**P2 e P3: `NÃO EXECUTADO (fora do escopo aprovado)`:** QA11-03, QA11-06, QA11-10, QA11-14, QA11-15,
QA11-16, QA11-17, QA11-19, QA11-21, QA11-24, QA11-27, QA11-32, QA11-34 e QA11-35. A limpeza final foi feita
mesmo assim; ver "Limpeza" abaixo.

### Triagem

O coordenador repetiu cada `BUG` num contêiner descartável (`qa11-runner:tmp`) ou no Floci A antes de
removê-lo. Os scripts estão em `$QA/triagem/`. Todos se reproduziram, então nenhum virou `RISCO`.

| Bug | Reprodução na triagem | Resultado |
|---|---|---|
| BUG-1 | `AWS_ENDPOINT_URL=http://localhost:4599 bash scripts/parar-migracao.sh` dá `ERRO ecs list-tasks: ` (vazio), saída 1. Sem região, dá `ERRO ecs list-tasks: NoRegion`, saída 1. No `infra-aws.yml`, o `Terraform destroy` (linha 674) e o do agendado (linha 915) não têm `always()`, e o passo das linhas 656, 739 e 911 não tem `continue-on-error` | reproduzido |
| BUG-3 | Cópia de `infra/aws` com `lifecycle { prevent_destroy = true }` no `aws_db_instance.this`: `terraform test` dá `54 passed, 0 failed`. Numa config com dois `terraform_data`, um deles protegido, `terraform show -json` do plano não traz `prevent_destroy` (0 ocorrências); o `destroy` dá `Error: Instance cannot be destroyed`, saída 1, e os dois recursos continuam no state | reproduzido |
| B-A1 | Floci A. `sobe.sh 2026-11-20T00:00:00Z 0` (30 itens no state). Às 06:53:20 roda o `subir` que prorroga para `+1 h` em segundo plano. Um segundo depois, `wf-ttl.sh` dá `vencido=true` com `expires_at=2026-11-20T00:00:00Z`. Em seguida `parar-migracao.sh` e `terraform destroy -lock-timeout=5m`: `Destroy complete! Resources: 27 destroyed.` às 06:55:33. O `subir` passou pela trava e terminou com `Error: Saved plan is stale`, `sobe-saida=1`; o state ficou com 0 recursos | reproduzido |
| B-A2 | A documentação pública da AWS ("CloudShell compute environment", lista de software pré-instalado) traz AWS CLI, Git, jq, Docker, psql e make, mas não Terraform. A seção "Lock preso" manda `terraform init` e `terraform force-unlock` no CloudShell sem passo de instalação | confirmado por leitura |
| B-A3 | README "Lock preso": `cd renda-fixa-api/infra/aws`, e no passo 4 `bash infra/aws/scripts/verificar-zerado.sh us-east-1`. Esse caminho não existe a partir de `infra/aws` | confirmado por leitura |
| B-A4 | README, passo 2 do `subir`: "o agendado o destrói em até 1 h". São 1 h do prazo provisório, mais até 30 min até o próximo agendado (`7,37 * * * *`), mais a duração do ciclo | confirmado por leitura |

### Bugs por severidade

Nenhum bug de severidade alta. A escala é a do `qa-fase`, com a decisão 2 do Matheus: o que afeta custo é
`BUG` média ou alta.

**Média**

1. **BUG-1: `parar-migracao.sh` falha sem dizer a causa, e essa falha pula o `terraform destroy`.**
   - Por que é média: afeta custo, porque o `destruir`, o `limpar` e o agendado deixam o ambiente ligado
     enquanto a falha persistir.
   - Não é alta porque não envolve valor financeiro nem perda de dados. Uma falha transitória é repetida
     pelo agendado 30 min depois.
   - Reproduzir: `AWS_ENDPOINT_URL=http://localhost:4599 bash infra/aws/scripts/parar-migracao.sh; echo $?`.
   - Correção proposta:
     - A mensagem cai num texto padrão quando não há código da AWS:
       `${codigo:-falha sem código da AWS (rede, credencial, timeout ou região)}`, como na
       `verificar-zerado.sh`.
     - O passo "Parar tarefas de migração" ganha `continue-on-error: true` no `destruir`, no `limpar` e no
       agendado. O `destroy` e a verificação de zerado continuam decidindo a cor do job.
   - Testes antes:
     - `tests/parar-migracao/rodar.sh`, com `aws` falso e casos em português no estilo de
       `tests/verificar-zerado/rodar.sh`: falha sem código da AWS cita a causa; região ausente cita a
       região; nenhuma tarefa sai 0; tarefas em execução são paradas.
     - Uma checagem do workflow que reprova se algum dos três passos não tiver `continue-on-error: true`.

2. **BUG-3: `lifecycle { prevent_destroy = true }` não é barrado por nenhuma camada, e o `destroy` aborta
   sem destruir nada.**
   - A instância 2 propôs alta; a triagem rebaixou para média. Afeta custo: tudo fica ligado até alguém
     mudar o código.
   - Exige um commit que passe pelo PR, e o agendado fica vermelho a cada 30 min. Não é perda silenciosa
     nem valor errado.
   - Reproduzir: os passos da seção "QA11-05 (parte B)".
   - Correção proposta: um verificador no job `validar` que reprova qualquer `prevent_destroy` em
     `infra/aws/**/*.tf`. O `terraform test` e a trava não enxergam o `lifecycle`.
   - Teste antes: `tests/lifecycle/rodar.sh`, com um caso que reprova uma pasta com `prevent_destroy` e
     outro que aprova a árvore atual. O script entra no `validar` e na lista do `shellcheck`.

3. **B-A1: o agendado destrói o ambiente que um `subir` está prorrogando.**
   - Por que é média: é uma corrida que destrói o ambiente (decisão 2), sem custo extra.
   - Reproduzir: os passos da seção "QA11-25" e a triagem acima.
   - Correção proposta, para o destroy do agendado:
     - Fazer `terraform plan -destroy -out` e checar o TTL no `expires_at` do `prior_state` desse plano,
       ou seja, no mesmo snapshot que vai ser destruído.
     - Repetir a guarda da fila manual imediatamente antes de aplicar.
     - Aplicar o plano salvo. Se um `subir` gravar o state no meio, o Terraform recusa o plano
       ("Saved plan is stale") e nada é destruído.
     - A lógica sai do YAML para um script testável, por exemplo `scripts/destruir-se-vencido.sh`.
   - Janela que sobra: o agendado aplicar o destroy antes de o `subir` gravar qualquer coisa. Nesse caso o
     ambiente estava vencido e intocado. Fica registrada como risco no README.
   - Teste antes: `tests/agendado/rodar.sh`, com `terraform` e `gh` falsos, e estes casos:
     - plano com `expires_at` futuro não aplica;
     - plano vencido aplica o arquivo do plano;
     - plano obsoleto sai diferente de 0 sem destruir, com mensagem clara;
     - plano sem `expires_at` conta como vencido;
     - fila manual não vazia antes do `apply` não aplica.

4. **B-A2: o roteiro "Lock preso" usa `terraform` no CloudShell, que não o tem.**
   - Por que é média: é o único caminho manual para destravar o state, e sem ele o `destroy` não roda.
   - Reproduzir: os comandos do README num CloudShell, ou a leitura da documentação da AWS citada acima.
   - Correção proposta: destravar só com a AWS CLI, que vem no CloudShell. O backend usa `use_lockfile`,
     então o lock é o objeto `<key>.tflock` no bucket do state.
     - Ver o lock: `aws s3 cp s3://<bucket>/<key>.tflock -`, que mostra `ID`, `Operation`, `Who` e
       `Created`.
     - Remover o lock: `aws s3 rm`.
     - Alternativa: instalar o Terraform 1.17.0 em `$HOME` com o checksum conferido. A escolha fica com o
       `implementador`, que deve justificá-la.
   - Teste antes: `tests/readme/rodar.sh`. Os blocos da seção "Lock preso" só usam `bash`, `aws`, `cd` e
     `git`.

**Baixa**

Estes dois bugs entram na mesma rodada, porque estão no trecho do README que a correção do B-A2 reescreve.

5. **B-A3: o caminho do passo 4 do roteiro de recuperação não existe na pasta em que o operador está.**
   - Correção: `bash scripts/verificar-zerado.sh us-east-1` a partir de `infra/aws`, ou um `cd` explícito.
   - Teste antes: em `tests/readme/rodar.sh`, todo `bash <caminho>` da seção "Nuvem na AWS" existe a partir
     da pasta do `cd` anterior.
6. **B-A4: "o agendado o destrói em até 1 h" é falso.**
   - Correção: escrever "em até cerca de 1 h 30 min (1 h do prazo provisório, mais até 30 min até o próximo
     agendado, mais a duração do ciclo)".

### Riscos

Os riscos não entram na rodada de correção; ficam para decisão do Matheus. Os detalhes estão nas seções de
cada roteiro.

| Id | Risco | Severidade | Sugestão |
|---|---|---|---|
| R-B1 | A trava não olha `enabled_cloudwatch_logs_exports` nem Container Insights (M08, M11), que a spec manda desligar, nem `deletion_protection`, `enable_deletion_protection`, `skip_final_snapshot`, `delete_automated_backups` e `recovery_window_in_days` (V1 a V4, V6). Todos são barrados só pelo `terraform test` do `validar`, no PR. O `subir` só roda da `main` (`github.ref == 'refs/heads/main'`), então a barreira vale se a proteção da `main` exigir o `validar` (P8). No Floci, o V1 deixou RDS e rede ligados | média | Pôr esses atributos na trava, que roda em todo `subir`, com fixtures `reprova-*` |
| R-B2 | Disco efêmero da tarefa, quantidade de secrets e `storage_throughput` (M10, M18, M20) não são barrados por nada. O custo fica limitado ao TTL | baixa | Limites na trava |
| R-B3 | `verificar-zerado.sh` olha uma região e uma lista fechada de tipos (EBS, ELB clássico, Aurora e `us-east-2` passam), e o README diz "tudo o que sobrou na conta" | média | Corrigir o texto do README. Ampliar só se o Matheus quiser |
| R-B4 | `voce@exemplo.com`, o exemplo do README, é aceito como e-mail do budget | baixa | Rejeitar domínios de exemplo |
| R-B5 | `bootstrap.sh` sem rede ou sem credencial sai 253/254/255 com a mensagem crua da CLI. Não existe `--help` | baixa | Mensagem própria |
| R-B6 | Mensagens enganosas: timeout ou 5xx do GHCR dizem "não existe ou não é público"; endpoint congelado diz "sem credencial válida ou sem rede"; `bloquear-recriacao.sh` com entrada ilegível fala de recriação | baixa | Ajustar os textos |
| R-B7 | Migração: banco à frente sai 0 com "up to date"; banco inexistente é criado em silêncio | baixa | Aviso no README |
| R-B8 | Policy do CI: ELB de qualquer tipo, RDS sem teto de disco e `desired_count` sem teto só são limitados pela trava. O cabeçalho da `verificar-zerado.sh` omite `ecs:DescribeClusters` e `DescribeTasks` | baixa | Acertar o cabeçalho |
| R-A1 | Mudar o `vpc_cidr` recria VPC, subnets, security groups e target group com o RDS e o ALB no ar. Nem a trava nem o `bloquear-recriacao.sh` reprovam | baixa | Estender o `bloquear-recriacao.sh` |
| R-A2 | State de 0 bytes é lido como "sem ambiente". A verificação de zerado pega o resíduo | baixa | n/a |
| R-A3 | Agendado vermelho falso no começo de um `subir`: o state ainda tem 0 recursos e a conta já tem algum | baixa | n/a |
| R-A4 | O roteiro de recuperação manda `destruir` antes de remover os órfãos. Com órfão ocupando subnet, o primeiro `destroy` fica preso, até 20 min no Floci | baixa | Dizer no README que o `destruir` roda de novo depois do passo 5 |
| R-A5 | O README não ensina o ciclo local no Floci | baixa | n/a |

**Pontos para o P8**, que o Floci não reproduz:
- as proteções do V2 a V4;
- o `iam update-role` da 2ª execução do bootstrap (`'UpdateRoleResult'`);
- a autorização de `ecs:RunTask` só pela família;
- ações extras que a AWS real exige: chave `aws/rds` e ALB;
- se o destroy usa `DeleteTaskDefinitions`;
- o lock `.tflock` do S3 com runner morto (I2);
- a largura real da janela do B-A1.

### Veredito

**COM BUGS: quatro de severidade média, dois de baixa, nenhum de alta.**

- Nenhum cenário executado terminou com recurso pago vivo sem que alguma camada acusasse: a verificação de
  zerado saiu 1 ou 2, nunca 0 com resíduo. Nenhum segredo apareceu em saída.
- Mas há três caminhos em que o `destroy` deixa de rodar ou destrói o ambiente errado (BUG-1, BUG-3 e B-A1),
  e o roteiro manual de destravar o state não funciona como está escrito (B-A2).
- A fase não fecha antes da rodada de correção (etapa 5 do `executar-fase`, com o teste antes) e da nova
  execução dos roteiros afetados:
  - QA11-02, casos d e e;
  - QA11-05, V5 nas partes A e B;
  - QA11-25 (a2);
  - QA11-33, leitura da seção "Lock preso".

### Limpeza (coordenador, 20/11/2026)

- **Removidos:**
  - os contêineres `qa11a-floci` e `qa11a-runner`, com os volumes deles;
  - os volumes dos filhos RDS do Floci `floci-aws-rds-db-20887B665E2643E2A17AB630-5ff41a` e
    `floci-aws-rds-db-55757002260045A391B14704-ee26c3`;
  - os volumes `qa11a-work`, `qa11a-plugins` e `qa11b-plugins`;
  - 12 volumes anônimos criados durante o QA, de 20/11/2026 00:18 a 06:47 UTC: dados do Floci em `/app/data`
    e dados PostgreSQL dos filhos RDS.
  - A instância 2 já tinha removido o que era dela; ver a seção dela.
- **Conferido depois:** nenhum contêiner `qa11*` ou `floci-*`, nenhuma rede `qa11*` ou `floci-*`, nenhum
  volume do QA. `fixedincome-api` e `fixedincome-db` seguem no ar (`/health` = 200), com o volume
  `renda-fixa-api_fixedincome-db-data` intocado. O repositório não tem `floci_override.tf` nem
  `floci.tfstate`.
- **Não removidos, de propósito:**
  - nove volumes anônimos de antes do QA: dois de 09/11/2026 (PostgreSQL, origem desconhecida) e sete de
    19/11/2026 entre 19:13 e 23:54 UTC (dados do Floci e um PostgreSQL de filho RDS, das rodadas de
    implementação e verificação da F11). O Matheus decide se saem.
  - as imagens (`qa11-runner:tmp`, `floci/floci`, `postgres`, SDK e runtime do .NET), que serão reusadas na
    nova execução dos roteiros afetados.
  - o rascunho `$QA`, fora do repositório.

### Depois da correção (coordenador, 20/11/2026)

**Rodadas.** A rodada 2, descrita na spec em "Rodada 2 de correção", corrigiu BUG-1, BUG-3, B-A1, B-A2,
B-A3 e B-A4, cada um com o teste escrito antes e visto falhar. Também aplicou a decisão do critério 8.

- A revisão de arquitetura (Opus) reprovou o B-A1 com um bloqueante, reproduzido pelo coordenador. O TTL
  era lido do output do `plan -destroy`, que o Terraform recalcula a partir da `-var`.
- Uma segunda rodada passou a ler a tag `expires_at` do cluster no `prior_state`, com casos de Terraform
  real em `tests/agendado`.
- A revisão final saiu **APROVADA COM RESSALVAS**: 0 bloqueantes, 1 importante e 3 sugestões.
- Foram as duas rodadas que a etapa 5 do `executar-fase` permite. O importante e as sugestões ficam para
  decisão do Matheus (abaixo).

**Reexecução dos roteiros afetados**, na seção "Reexecução pós-correção" abaixo: nenhum bug novo.

| Roteiro | Bug | Resultado |
|---|---|---|
| QA11-02 (d, e) | BUG-1 | OK. A mensagem cita a causa, e o `destroy` roda mesmo com o passo de parar falhando (simulação dos jobs `destruir` e `limpar`) |
| QA11-05 V5 (B e A) | BUG-3 | OK para as formas que um operador escreveria. RISCO baixa para quatro formas artificiais que passam pelo verificador; a mais plausível é um `floci_override.tf` dentro de um módulo |
| QA11-25 (a2) | B-A1 | OK em seis ordens da corrida, inclusive o caso do bloqueante (o `subir` termina antes do `plan -destroy`): o ambiente prorrogado nunca foi destruído. Os controles também passam: fila manual e ambiente vencido sem concorrência |
| QA11-33 "Lock preso" | B-A2, B-A3 | OK. Lock preso com `kill -KILL` no backend S3 do Floci, removido com os comandos do README; o `plan` seguinte pega o lock. Passo 4 a partir de `~/renda-fixa-api` |

**Para decisão do Matheus:**

1. **Importante (revisão), RISCO de custo confirmado no QA11-25.** Uma tag `expires_at` do cluster editada
   fora do Terraform para uma data distante faz o agendado "prorrogar" a cada ciclo. Ele fica verde, sem
   destroy e sem verificação de zerado; só o budget avisa.
   - Correção proposta: tratar como inválida, e portanto vencida, uma tag acima de `agora + 12 h` (com uma
     folga), que é o mesmo limite da `validation`.
   - Outra opção: usar o menor valor entre a tag e o output.
   - Exige uma rodada 3 de correção, fora do limite da etapa 5.
2. **Sugestões da revisão:**
   - um teste que ligue o endereço `module.app.aws_ecs_cluster.this` à configuração real (um renome
     faria o agendado destruir todo ambiente ativo);
   - falhar em vez de pular os casos de Terraform real com `CI=true`;
   - acertar o texto do README sobre `tests/agendado`;
   - reescrever a pendência do P8 sobre a tag, que já foi confirmada no Floci.
3. **RISCO baixa do verificador de `prevent_destroy`:** restringir a exceção ao `floci_override.tf` da raiz.

**Veredito do QA depois da correção: APROVADO COM RISCOS.** Os seis bugs estão corrigidos e confirmados na
reexecução. Restam os riscos da tabela "Riscos" e os três itens acima, todos para decisão do Matheus.

### Rodada 3 e decisões do Matheus (coordenador, 20/11/2026)

O Matheus aprovou em 20/11/2026 uma terceira rodada de correção curta, com o teste antes de cada correção e sem QA
novo. Ela fechou os itens 1 a 3 de "Para decisão do Matheus" acima e os riscos R-B1 e R-B3.

| Item | Correção | Teste escrito antes |
|---|---|---|
| Importante da revisão (tag editada) | Uma tag `expires_at` do cluster acima de `agora + 12 h + 10 min` conta como vencida, com aviso de que pode ter sido editada fora do Terraform. O limite é o da `validation` | `tests/agendado`: tag em 2099 destrói; agora + 11 h e o limite menos 1 min não destroem; o limite mais 1 min destrói |
| Sugestão: endereço | Output `cluster_expires_at` no módulo `app` | `run` novo no `terraform test`, mais casos que leem o endereço padrão do próprio script e o procuram na configuração |
| Sugestão: `CI=true` | Os casos de Terraform real falham em vez de serem pulados | `sem_terraform_com_ci_true_falha`, `sem_terraform_sem_ci_pula_com_aviso` |
| Sugestão: README de `tests/agendado` | O texto cita os casos com Terraform real | n/a (só texto) |
| RISCO baixa: `floci_override.tf` | O verificador ignora só o `floci_override.tf` da raiz | `floci_override_em_subpasta_com_prevent_destroy_reprova` (o caso 15 da reexecução) |
| R-B1 | A trava de custo exige, no RDS, `deletion_protection = false`, `skip_final_snapshot = true`, `delete_automated_backups = true` e nenhum log exportado; no ALB, `enable_deletion_protection = false`; no secret, `recovery_window_in_days = 0`; no cluster, `containerInsights` presente e `disabled`. Valor `after_unknown` em qualquer um deles reprova | 14 fixtures `reprova-*` novas, cada uma vista falhando antes. As `aprova-*` foram ajustadas, e `reprova-multi-az.json` agora isola o Multi-AZ |
| R-B3 | O README descreve o alcance real da verificação de zerado: uma região, a lista de tipos do script e a tag `project`. O que fica fora tem o budget como rede de segurança | `tests/readme`, regra (e): três casos |

A conferência independente do coordenador está em `docs/VALIDACAO-F11.md`, seção "Rodada 3":
- `terraform test` passou com 55 runs;
- as suítes de shell passaram com 201 casos, todas com `CI=true` e nenhum caso pulado;
- o plano real do Floci foi aprovado pela trava já com as checagens novas, e o ciclo do `validar` fechou com a
  verificação de zerado aprovada.

**Riscos que continuam abertos, todos de severidade baixa:** R-B2, R-B4 a R-B8 e R-A1 a R-A5 da tabela "Riscos", mais
as quatro formas artificiais de `prevent_destroy` que ainda passam pelo verificador. Uma delas, o
`floci_override.tf` em subpasta, foi fechada nesta rodada. **Veredito do QA:** continua **APROVADO COM RISCOS**,
agora sem nenhum risco de severidade média em aberto.

## Riscos aceitos do Trivy e do Checkov (coordenador, 23/11/2026)

**Origem.** O Matheus rodou o Trivy em `infra/aws` antes da correção de segurança: 12 achados, em `trivy.txt` na raiz,
removido depois de transcrito. A imagem foi `aquasec/trivy` 0.76.2
(`sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb`), posterior ao incidente de março de
2026. Dois achados foram corrigidos (spec, "Correção de segurança (Trivy e Checkov)"):
- AWS-0052, com `drop_invalid_header_fields`;
- AWS-0104, com a saída restrita a 443 e 5432.

Do Checkov, a correção tratou o CKV_AWS_131 (o mesmo do AWS-0052) e o CKV2_AWS_12 (o SG padrão sem regras).

O coordenador rodou de novo a mesma imagem do Trivy, sem rede e com `--skip-check-update`, depois da correção. O
AWS-0052 sumiu. Restam os 10 achados abaixo, aceitos com o motivo de cada um. O AWS-0104 aparece de novo porque a
regra que sobra (`task_https`) ainda sai para `0.0.0.0/0`, só que restrita a TCP 443.

| Achado | Severidade (Trivy) | Onde | Motivo da aceitação |
|---|---|---|---|
| AWS-0104, saída para `0.0.0.0/0` | crítica | `modules/network`, `task_https` | **Reduzido, não eliminado.** A saída passou de todo protocolo para TCP 443. Sem NAT nem VPC endpoints (fora de escopo pela spec, porque cobram por hora e quebrariam a premissa de custo), a tarefa precisa sair para a internet: o GHCR e a API do SGS (`api.bcb.gov.br`) não têm IP fixo, e Secrets Manager, CloudWatch Logs e ECS são alcançados pelo endpoint público. O ambiente é efêmero, sem entrada direta: a tarefa só recebe tráfego do SG do ALB. A prova ao vivo de que a saída restrita basta está pendente do P8 |
| AWS-0054, listener HTTP | crítica | `modules/app`, `aws_lb_listener.http` | **Fora de escopo pela spec:** sem domínio, HTTPS nem certificado; a demo usa o DNS HTTP do ALB. A API não tem autenticação e o banco é destruído no fim, risco já aceito e registrado no README. Não trafega segredo pelo ALB |
| AWS-0053, ALB público | alta | `modules/app`, `aws_lb.this` | É o objetivo: expor a demo pela internet durante a janela do TTL. A entrada é só a porta 80 no SG do ALB, e o ALB só encaminha para a tarefa na 8080 |
| AWS-0177, RDS sem proteção contra exclusão | média | `modules/database` | **Intencional e obrigatório:** a proteção impede o `destroy` automático (premissa "nada fica ligado esquecido"). A trava de custo exige `deletion_protection = false` (rodada 3, R-B1), e o QA mostrou o efeito no Floci (QA11-05, V1) |
| AWS-0077, sem retenção de backup | média | `modules/database` | **Fora de escopo pela spec** (sem backup nem snapshot). O banco nasce vazio e é destruído a cada ciclo, e backup retido sobraria como resíduo e custo. A trava exige `backup_retention_period = 0` |
| AWS-0176, sem autenticação IAM no RDS | média | `modules/database` | A API usa a connection string com senha, entregue pelo Secrets Manager (spec, segredos). A autenticação IAM exigiria mudar o código .NET, fora de escopo da F11 (a spec proíbe mexer em .NET). A senha é gerada efêmera e nunca vai para o state |
| AWS-0178, VPC sem Flow Logs | média | `modules/network`, `aws_vpc.this` | Custo de ingestão no CloudWatch e um log group a mais para a verificação de zerado, para uma VPC que vive horas. A spec não pede Flow Logs |
| AWS-0017, log group sem chave KMS própria | baixa | `modules/app`, log group `/ecs/...` | O log group já é criptografado pela chave gerenciada pela AWS. Uma chave KMS própria custa por mês e não é efêmera: só sai depois de 7 a 30 dias em `PendingDeletion`. Isso conflita com a premissa de custo, e `aws_kms_key` está fora da lista positiva da trava (fixture `reprova-kms-key.json`). Retenção de 3 dias |
| AWS-0098, secret sem chave KMS própria | baixa | `modules/app`, secret da connection string | O mesmo motivo do AWS-0017: o secret usa a chave gerenciada `aws/secretsmanager`. Uma chave própria cobraria e ficaria pendente depois do `destroy` |
| AWS-0034, cluster sem Container Insights | baixa | `modules/app`, `aws_ecs_cluster.this` | **A spec manda desligar:** tem custo e cria um log group fora do state que o `destroy` não apaga. A trava exige `containerInsights = disabled` (rodada 3, R-B1) |
| AWS-0133, RDS sem Performance Insights | baixa | `modules/database` | Custo e fora de escopo. A trava exige `performance_insights_enabled = false` |

**Checkov.** Não havia relatório do Checkov no repositório, só os dois IDs citados no pedido (CKV_AWS_131 e
CKV2_AWS_12, ambos corrigidos). O coordenador não rodou o Checkov. Os achados que ele tiver e que correspondem aos
da tabela acima (HTTPS, ALB público, proteção contra exclusão, backup, IAM no RDS, Flow Logs, KMS, Container
Insights, Performance Insights) têm o mesmo motivo de aceitação. Qualquer outro ID do Checkov fica para ser
transcrito aqui quando o relatório dele for anexado.

## Reexecução pós-correção (20/11/2026)

Reexecução dos roteiros afetados pela rodada de correção ("Rodada 2" de `docs/SPEC-F11-EXECUCAO.md`).

- **Momento da cópia:** `arvore/` do rascunho recriada a partir de `git ls-files -co --exclude-standard` (316
  arquivos, sem CRLF), na manhã de 20/11/2026 (relógio dos contêineres 07:2x UTC), **antes** das duas edições
  que o implementador fez depois (`destruir-se-vencido.sh`, com o novo `expires-at-do-cluster.jq`, e
  `verificar-sem-prevent-destroy.sh`, que passou a reprovar também `removed { lifecycle { destroy = false } }`).
  Conferido no fim (`cmp` arquivo a arquivo contra o repositório): diferem da cópia `.github/workflows/infra-aws.yml`,
  `docs/SPEC-F11-EXECUCAO.md`, `scripts/destruir-se-vencido.sh`, `scripts/expires-at-do-cluster.jq`,
  `scripts/verificar-sem-prevent-destroy.sh`, `tests/agendado/rodar.sh`, `tests/lifecycle/rodar.sh` e
  `README.md`. A seção "Lock preso" do README é **idêntica** à da cópia (`diff` vazio) e `parar-migracao.sh` não
  mudou. Os resultados do QA11-02 e do QA11-33 valem, portanto, para a árvore atual nos trechos testados; os do
  QA11-05 valem para o verificador da cópia, e há uma repetição informativa contra o verificador novo. As
  simulações do YAML (QA11-02) usam os passos dos jobs `destruir` e `limpar` da cópia; o `git diff` do YAML
  posterior não foi reavaliado.
- **Segunda cópia (QA11-25):** `arvore/` atualizada de novo em 20/11/2026 22:33 UTC, já com a correção final do B-A1
  (`destruir-se-vencido.sh` lendo a tag do cluster, `expires-at-do-cluster.jq`, `infra-aws.yml`, `README.md`, verificador novo); `cmp`
  com o repositório bateu. O QA11-02, o QA11-05 e o QA11-33 abaixo foram rodados com a primeira cópia.
- **Ambiente:** um Floci só (`floci/floci@sha256:a0fbf944d7f338cc0856f1ef83831ea749ff2be71956eb7a7fce08fc21458e19`),
  `qa11a-floci`, e o runner `qa11a-runner` (`qa11-runner:tmp`). Provider `hashicorp/aws` 6.73.0 baixado de novo no
  `terraform init` (volume `qa11a-plugins`, autorizado). Drivers novos em `$QA/passos/r2/` (`sim.sh`,
  `corrida.sh`, `q05a.sh`, `q33-*.sh`, `gh-corrida`), `$QA/passos/r2-*.sh` e `$QA/extrai.js`; saídas em
  `$QA/saidas/r2-*.txt`.
- **Adaptações do QA, só na cópia:** nos passos extraídos do YAML, `environments/aws.tfvars` virou
  `environments/floci.tfvars`; entre o `destroy` e a verificação roda o `artefatos.sh` (o passo "Floci: remover
  artefatos que o emulador não apaga" do `validar`), e a verificação após destroy usa `VERIFICAR_ESPERA_SEGUNDOS=5`.

### QA11-02 (casos d e e) — `parar-migracao.sh` e o `destroy` que não pode parar (BUG-1)

**Resultado: OK.** A mensagem cita a causa nos três cenários e, pela simulação do YAML, a falha do passo não
impede o `terraform destroy` nos jobs `destruir` e `limpar`. O `agendado` foi simulado no QA11-25 (a2), abaixo;
pelo YAML o passo dele tem o mesmo `continue-on-error: true` (linha 939).

| Caso | Comando | Obtido | Esperado | Resultado |
|---|---|---|---|---|
| d1. Floci congelado | `docker pause qa11a-floci; ra 'time timeout 900 bash scripts/parar-migracao.sh'` | saída **1** em **3 min 04 s**: `ERRO ecs list-tasks: falha sem código da AWS (rede, credencial, timeout ou região)` | ≠ 0 com a causa | OK (antes: `ERRO ecs list-tasks: ` vazio) |
| e2. porta fechada | `AWS_ENDPOINT_URL=http://localhost:4599 bash scripts/parar-migracao.sh` | saída **1** em 4 s, a mesma mensagem | ≠ 0 com a causa | OK |
| sem região | `env -u AWS_REGION -u AWS_DEFAULT_REGION bash scripts/parar-migracao.sh` | saída **1** em 13 s: `ERRO ecs list-tasks: região não definida (NoRegion): defina AWS_REGION ou AWS_DEFAULT_REGION` | ≠ 0 com a causa | OK |
| credencial vazia (extra) | `AWS_ACCESS_KEY_ID= AWS_SECRET_ACCESS_KEY= bash scripts/parar-migracao.sh` | saída 1: `ERRO ecs list-tasks: NoCredentials` | ≠ 0 com a causa | OK |
| controle: Floci no ar, sem cluster | `bash scripts/parar-migracao.sh` | saída 0: `Cluster renda-fixa-api não existe: nenhuma tarefa de migração a parar.` | 0 | OK |

**Leitura do YAML.** Nos jobs `destruir` (`infra-aws.yml`, linha 679), `limpar` (764) e `agendado` (939) o passo que
roda `parar-migracao.sh` tem `continue-on-error: true`. Os passos seguintes (`Ler expires_at...`, `Terraform
destroy`, `Verificação de zerado`; no agendado, `destruir-se-vencido.sh`) não têm `always()` e rodam porque o
passo com `continue-on-error` conta como sucesso para o `success()` implícito.

**Simulação com `bash -e`** (`$QA/passos/r2/sim.sh`, blocos `run:` extraídos do YAML por `$QA/extrai.js`; cada
passo com o próprio `GITHUB_OUTPUT`, `bash --noprofile --norc -eo pipefail`, passos sem `always()` pulados depois
de uma falha, `continue-on-error` respeitado). O passo "Parar tarefas de migração" falha por porta fechada
(`AWS_ENDPOINT_URL=http://localhost:4599` só nele; o Terraform segue falando com o Floci pelos endpoints do
override). Ambiente de 30 itens no state criado por `sobe.sh` antes de cada simulação:

```
== (mutante) como o YAML ANTIGO: sem continue-on-error
>>> Parar tarefas de migração   ERRO ecs list-tasks: falha sem código da AWS (...)   exit=1
PULADO: Ler expires_at e image_sha do state / Terraform destroy / Verificação de zerado
JOB=failure      state depois do mutante: 30 recursos         <- o comportamento do BUG-1
== YAML NOVO
>>> Parar tarefas de migração   exit=1   (continue-on-error: o passo é marcado como falho, o job segue)
>>> Ler expires_at e image_sha do state   exit=0   expires_at=2026-11-20T09:33:03Z image_sha=000...
>>> Terraform destroy   Plan: 0 to add, 0 to change, 27 to destroy.  Destroy complete! Resources: 27 destroyed.  exit=0
>>> Verificação de zerado   APROVADA (nada sobrando na região us-east-1)   exit=0
JOB=success      state depois: 0 recursos
```

Repetido para `limpar` (os blocos `run:` dos passos "Parar", "Ler", "Destroy" e "Verificação" de `destruir` e `limpar` são
idênticos byte a byte, `cmp`): mesmo resultado, `Destroy complete! Resources: 27 destroyed.`, `JOB=success`, state 0,
verificação 0. O mutante (sem `continue-on-error`) reproduz o BUG-1 antigo, o que mostra que a simulação distingue os
dois YAML. Saídas: `$QA/saidas/r2-qa02-*.txt`.

Observação (não é bug): com `continue-on-error: true` o job termina **verde** quando só o passo de parar falha e o
`destroy` funciona. É a decisão registrada na spec da rodada 2; a falha fica no log como anotação do passo.

### QA11-05 V5 (partes B e A) — `prevent_destroy` e variações

**Resultado: BUG-3 corrigido para as formas que um operador escreveria (OK). RISCO baixa: quatro formas
artificiais passam pelo verificador, uma delas com o efeito comprovado no ciclo do Floci (parte A).**

**Parte B: o que barra agora.** `bash scripts/verificar-sem-prevent-destroy.sh` (o passo "Sem prevent_destroy" do
`validar`) numa cópia limpa de `infra/aws` por variação (`$QA/passos/r2-verif.sh`, saída em
`$QA/saidas/r2-verif.txt`). Árvore atual sem mudança: `APROVADO ... 15 arquivo(s)`, saída 0. A trava de custo e o
`terraform test` continuam sem enxergar o atributo (BUG-3 original); o que barra agora é só o verificador.

| # | Variação | Saída | Observação |
|---|---|---|---|
| 1 | bloco `lifecycle {` multilinha no `aws_db_instance.this` | **1** | `REPROVADO prevent_destroy: modules/database/main.tf:12` |
| 2 | `prevent_destroy=true` sem espaços | **1** | |
| 3 | `lifecycle { prevent_destroy = true }` numa linha | **1** | |
| 4 | em outro módulo (`modules/network`) | **1** | |
| 5 | `= false` | **1** | reprova também o `false`, como a spec diz |
| 6 | `extra.tf.json` na raiz | **1** | |
| 7 | `.tf.json` num módulo | **1** | |
| 8, 9 | só comentário `#` ou `//` | 0 | correto: o Terraform ignora |
| 10 | `/* ... */` com o atributo dentro | **1** | falso positivo, documentado ("`/* */` não é tratado") |
| 11, 12 | código + comentário no fim, ou `/* x */` antes do código | **1** | |
| 13 | `.tf` numa subpasta de módulo | **1** | |
| 14 | `override.tf` na raiz | **1** | |
| 18 | arquivo com CRLF | **1** | |
| 19 | `PREVENT_DESTROY` em maiúsculas | 0 | inválido no Terraform (argumento desconhecido), não é evasão |
| pasta inexistente / sem `.tf` | `ERRO ... não existe` / `nenhum arquivo .tf` | **2** | falha fechada |

Passam (saída 0), e o Terraform **honra** o `prevent_destroy` em todas (teste com `terraform_data`,
`$QA/passos/r2-tiny.sh` e `r2-unicode.sh`: `apply=0`, `destroy=1`, `Error: Instance cannot be destroyed`, recurso
fica no state):

| # | Forma que passa | Por que passa | Chega ao `main`? |
|---|---|---|---|
| 15 | `modules/database/floci_override.tf` com `lifecycle { prevent_destroy = true }` (arquivo de override do Terraform) | o `find` ignora o nome `floci_override.tf` em qualquer profundidade; só o da raiz é gerado | sim (o `.gitignore` cobre só `infra/aws/floci_override.tf`) |
| 16 | módulo numa pasta chamada `.terraform` (`source = "./m/.terraform"`) | o `find` poda qualquer `.terraform` | não pelo Git (`.terraform/` está no `.gitignore`) |
| 17 | `.tf.json` com `"prevent_destroy"` (escape JSON na chave) | o `grep` não decodifica JSON | sim |
| 20 | `.tf` atrás de um link simbólico para pasta | o `find` não segue links | só com link simbólico no Git |

Nenhuma é algo que um operador escreveria por engano, então não reabre o BUG-3. Fica como **RISCO baixa**: o
verificador é uma busca textual. Sugestão (não aplicada): restringir a exceção ao `floci_override.tf` da raiz
(`-path ./floci_override.tf`) e acrescentar o caso 15 a `tests/lifecycle/rodar.sh`.

**Parte A (ciclo no Floci com uma variação que o verificador deixa passar).** Caso 15 no `aws_db_instance.this`,
cópia `/work/v/aws` (`$QA/passos/r2/q05a.sh`, saída `$QA/saidas/r2-q05a.txt`):

```
verificador: APROVADO: nenhum prevent_destroy em 15 arquivo(s) .tf.   saída=0
sobe.sh: 0 (221 s)   Trava de custo: APROVADO 27 itens do plano dentro da lista positiva e dos limites
desce.sh: 1 (46 s)  Error: Instance cannot be destroyed
recursos no state depois do destroy: 30   (nada foi destruído)
verificar-zerado: 1, REPROVADA (22 achados): ecs-cluster, ecs-servico, load-balancer, target-group, rds-instancia, secret, log-group x2, ...
caminho do operador: apagar o arquivo, desce.sh de novo: Destroy complete! Resources: 27 destroyed.; verificação APROVADA (0)
```

Confirma o efeito do BUG-3 original para esta forma: a trava aprova, o `destroy` aborta sem destruir nada, a
verificação de zerado acusa tudo (a camada final funciona) e o ambiente fica de pé até alguém editar o código. As
formas 1 a 14 não chegam a este ponto porque o verificador as barra.

**Repetição informativa contra o verificador novo** (o implementador alterou o script depois da minha cópia; saída em
`$QA/saidas/r2-verif-nova.txt`; **não** é o objeto desta rodada): as variações 1 a 14 continuam como acima, a mensagem
passou a `REPROVADO proteção contra destroy: ...`, e o caso novo `removed { from = ...; lifecycle { destroy = false } }`
dá saída 1. Os casos 15, 16, 17 e 20 continuam passando.

### QA11-25 (a2) — contra a correção final do B-A1

**Resultado: OK.** Em nenhuma das seis ordens com `subir` concorrente o ambiente prorrogado foi destruído depois de
o `subir` gravar o prazo novo; o controle destrói e a verificação dá 0; com `gh` respondendo `in_progress` nada é
aplicado. A janela aceita no README (destroy antes de o `subir` gravar qualquer coisa) **não ocorreu** em nenhuma
execução. Um **RISCO conhecido** (tag editada por fora) foi registrado no cenário 4. Nenhum bug novo (`BUG-R*`).

**Momento e árvore.** Cópia `arvore/` atualizada em 20/11/2026 22:33 UTC com `git ls-files -co --exclude-standard`;
`cmp` com o repositório bateu em `destruir-se-vencido.sh`, `expires-at-do-cluster.jq`, `infra-aws.yml` e
`README.md` (e em todos os demais arquivos). Floci `qa11a-floci` e runner `qa11a-runner` novos, provider
`hashicorp/aws` 6.73.0 baixado de novo (autorizado).

**Como o agendado foi simulado** (`$QA/passos/r2/sim.sh agendado`, passos extraídos do YAML atual por
`$QA/extrai.js`, na ordem do job): "Guarda" (`gh run list`), "Ler o state", "Ambiente ativo - checar o TTL" (pelo
output, como o YAML), "Ambiente vencido - parar tarefas de migração" (`continue-on-error`), "Ambiente vencido -
destruir (plano salvo, se continuar vencido)" (`bash scripts/destruir-se-vencido.sh`, com `GH_TOKEN=x`,
`REPOSITORIO` e o `gh` falso no PATH), e "verificação de zerado" só se `destruiu=true`. `gh` falso
(`$QA/passos/r2/gh-corrida`, registra cada chamada): `vazia` responde 0 sempre; `manual_depois` responde 0 às 5
primeiras consultas (a guarda do começo) e `in_progress=1` depois (um `subir` manual entrou depois da guarda);
`manual_sempre` responde `in_progress=1` desde a primeira. Adaptação do QA, só na cópia: `aws.tfvars` virou
`floci.tfvars` (nos passos e no `destruir-se-vencido.sh`).
Cada ordem parte de um ambiente vencido novo (`sobe.sh 2026-11-20T00:00:00Z 0`, 30 recursos, output e tag do cluster
`expires_at=2026-11-20T00:00:00Z`) e de um `subir` que prorroga para `+1 h` (`sobe.sh <agora+1h> 0`, trava de custo e
bloqueio de recriação incluídos). Drivers: `$QA/passos/r2/corrida.sh`, `ctrl.sh`; saídas em `$QA/saidas/r2b-*.txt`.
A tag do cluster foi lida com `aws ecs describe-clusters --include TAGS` (o Floci devolve a tag `expires_at`, que é
de onde o script novo lê o prazo; se não devolvesse, o script a trataria como vencida).

| Ordem | Agendado | `subir` | Recursos / `expires_at` final (output e tag) | Veredito |
|---|---|---|---|---|
| (a) agendado 1 s depois do `subir` (22:39:27; `subir` 22:39:26 a 22:39:54) | lê state (30) e TTL (`vencido=true`), `parar-migracao` ok, `plan -destroy` feito antes de o `subir` gravar, `terraform apply` do plano depois de o `subir` gravar: `Error: Saved plan is stale` -> `ERRO: o state foi alterado depois do plano ... nada foi destruído`, `destruiu=false`, saída 1 (job vermelho), verificação pulada | `sobe: ok`, saída 0 | 30 recursos; `2026-11-20T23:39:26Z` / `2026-11-20T23:39:26Z` | OK: nada destruído; o plano obsoleto foi recusado |
| (b) o agendado lê o TTL vencido, **espera o `subir` terminar por inteiro** (22:43:20 a 22:43:43) e só então roda `parar-migracao` e `destruir-se-vencido.sh` (o caso do bloqueante) | TTL lido às 22:43:25 como vencido; espera até 22:43:44; `plan -destroy` lê a tag do cluster do state novo: `O ambiente foi prorrogado depois da leitura (expira em 2026-11-20T23:43:20Z, restam 59 min): nada será destruído.` `destruiu=false`, saída 0, verificação pulada | `sobe: ok`, saída 0 | 30 recursos; `2026-11-20T23:43:20Z` / `2026-11-20T23:43:20Z` | **OK**: é a ordem que o bloqueante quebraria (o output recalculado voltaria o prazo velho); com a tag do cluster o prazo novo é visto |
| (c) agendado ~20 s depois (22:41:04; o `subir` terminou 22:41:06) | lê state e TTL: `Ambiente ativo e dentro do prazo: restam 59 min` (`vencido=false`), job verde, nada mais roda | `sobe: ok` | 30; `2026-11-20T23:40:44Z` / idem | OK (o `subir` já tinha gravado) |
| (d) agendado ~60 s depois (22:42:41; o `subir` terminou 22:42:04) | `restam 58 min`, `vencido=false`, nada mais roda | `sobe: ok` | 30; `2026-11-20T23:41:41Z` / idem | OK (idem) |
| (extra e1) agendado 8 s depois (22:45:41; `subir` 22:45:33 a 22:46:09) | TTL vencido, `plan -destroy`, `apply` do plano: `Saved plan is stale`, nada destruído, saída 1 | `sobe: ok` | 30; `2026-11-20T23:45:33Z` / idem | OK (mesmo desfecho do (a)) |
| (extra e2) agendado 14 s depois (22:47:03; `subir` 22:46:49 a 22:47:11) | TTL vencido às 22:47:06; o `plan -destroy` (22:47:08) leu o state já com o prazo do `subir`: tag nova, `prorrogado ... nada será destruído`, saída 0 | `sobe: ok` | 30; `2026-11-20T23:46:49Z` / idem | OK (pega pela tag, não pelo plano obsoleto) |

Como o `subir` neste Floci é uma atualização no lugar (cerca de 25 s) e não uma criação, as ordens (c) e (d), com 20 s e
60 s, já caíram **depois** de o `subir` terminar; por isso acrescentei 8 s e 14 s, que caem dentro do `apply`. As quatro
ordens que realmente atravessam o `subir` ((a), (b), e1, e2) deram três desfechos seguros diferentes (plano obsoleto
recusado; prazo novo visto pela tag; ambos no mesmo ciclo). O ambiente nunca foi destruído depois do prazo novo.

Observação (não é bug): quando o plano é recusado por obsoleto ((a), e1), o job do **agendado** termina vermelho
(saída 1) mesmo sendo o resultado correto; a mensagem em português diz o motivo e o README o descreve como risco
aceito. Só afeta o ícone/e-mail do ciclo.

**Caso `gh` falso respondendo `in_progress`** (ambiente vencido, sem `subir` concorrente):

| Modo do `gh` | Obtido | Esperado | Resultado |
|---|---|---|---|
| `manual_depois` (guarda do começo passa; a repetida no script vê `in_progress=1`) | `plan -destroy` (`Plan: 0 to add, 0 to change, 27 to destroy.`), depois `::warning::Há 1 execução(ões) manual(is) do infra-aws em andamento ou na fila: este ciclo não destrói nada.`, `destruiu=false`, saída 0, verificação pulada; state com 30 recursos e tag intacta | não aplica | OK |
| `manual_sempre` (a guarda do começo já vê `in_progress=1`) | `pular=true`, "este ciclo não mexe em nada", o resto do ciclo é pulado, 30 recursos | não mexe em nada | OK |

**Controle (vencido, sem `subir` concorrente, `gh` vazio):** `vencido=true`, `plan -destroy` (27 a destruir), `apply`
do plano: `destruiu=true` (2 min 08 s), verificação de zerado roda e dá `APROVADA (nada sobrando na região
us-east-1)`, saída 0, `JOB=success`; state com 0 recursos, sem output e `tag_cluster=None`. **OK.**

**Cenário 4, achado importante da revisão (RISCO conhecido, decisão do Matheus): tag editada por fora.** Ambiente
vencido pelo **output** (`expires_at=2026-11-20T00:00:00Z`) e, por fora do Terraform, `aws ecs tag-resource` no
cluster com `expires_at=2099-01-01T00:00:00Z` (`tag-resource=0`, a tag vira 2099 e o output continua vencido). O
agendado rodou **duas vezes** (22:50:38 e 22:51:38), com o mesmo resultado:

```
Ambiente ativo - checar o TTL: Ambiente vencido: será destruído.   (vencido=true, pelo output)
parar tarefas de migração: ok
destruir (plano salvo): Plan: 0 to add, 0 to change, 27 to destroy.
O ambiente foi prorrogado depois da leitura (expira em 2099-01-01T00:00:00Z, restam 37928228 min): nada será destruído.
destruiu=false, saída 0; verificação de zerado PULADA; JOB=success
estado: recursos=30 output_expires_at=2026-11-20T00:00:00Z tag_cluster=2099-01-01T00:00:00Z
```

Confirma o esperado pela revisão: o agendado sai 0 ("prorrogado") **a cada ciclo**, sem destroy e sem verificação,
com o ambiente de pé (30 recursos), e o job fica verde (nada alerta). Quem editar a tag do cluster por fora (ou um
estado em que a tag diverge do output) mantém o ambiente vivo indefinidamente até um `destruir` manual ou um `subir`
que regrave o prazo. Como a edição precisa de `ecs:TagResource` na conta e o ambiente é de uso do próprio
Matheus, é RISCO (baixa a média, custo de um ambiente de demonstração ligado). Decisão pendente com o Matheus; não
reabre o B-A1. Sugestões (não aplicadas): comparar a tag com o output e tratar divergência como vencido (o menor
dos dois), ou fazer o `destruir-se-vencido.sh` falhar (vermelho) quando a tag e o output divergirem.

### QA11-33 (só "Lock preso" e o roteiro de recuperação) — B-A2, B-A3

**Resultado: OK para B-A2 e B-A3.** Comandos do README ao pé da letra, contra o Floci, num runner onde o operador só
usa `aws` e `bash` (o `terraform` do runner serviu só para criar o lock real). O bucket é versionado, como o do
bootstrap, com o nome do padrão `renda-fixa-api-tfstate-000000000000`.

**1. Só a AWS CLI** (`$QA/passos/r2/q33-cli.sh`, `$QA/saidas/r2-q33-cli.txt`). Objeto
`renda-fixa-api/aws.tfstate.tflock` com um JSON plausível (`ID`, `Operation`, `Info`, `Who`, `Version`, `Created`, `Path`):

| Passo do README | Comando | Obtido |
|---|---|---|
| sem lock (caso 404) | `aws s3 cp "s3://$BUCKET/renda-fixa-api/aws.tfstate.tflock" -` | saída 1: `download failed: ... An error occurred (404) when calling the HeadObject operation: Not Found`. O README diz "falhar com `404`": bate |
| ler o lock | o mesmo comando, com o objeto | saída 0, imprime o JSON com `ID`, `Operation`, `Who` e `Created` (os campos que o README cita) |
| remover | `aws s3 rm "s3://$BUCKET/renda-fixa-api/aws.tfstate.tflock"` | `delete: s3://...`, saída 0 |
| ler de novo (versionado) | `aws s3 cp ... -` | 404: o delete marker esconde o objeto |
| `rm` sem lock | `aws s3 rm ...` | saída 0 e mais um delete marker (inofensivo) |

**2. Backend S3 real do Floci com `use_lockfile`** (`$QA/passos/r2/q33-real.sh`, `$QA/saidas/r2-q33-real.txt`).
`terraform init` com `-backend-config=infra/aws/environments/aws.backend.hcl` (a chave e o `use_lockfile = true`
reais), `bucket=` e um arquivo só do Floci (endpoint, `use_path_style`, `skip_*`); um `terraform_data` com
`local-exec` `sleep 300`:

```
apply em segundo plano -> aparece renda-fixa-api/aws.tfstate.tflock (248 bytes)
lock do terraform: {"ID":"4644d157-...","Operation":"OperationTypeApply","Info":"","Who":"root@48bbabc27bf8","Version":"1.17.0","Created":"2026-11-20T22:18:24.573209141Z","Path":"renda-fixa-api-tfstate-000000000000/renda-fixa-api/aws.tfstate"}
kill -KILL no terraform apply -> o lock continua no bucket
terraform plan -lock-timeout=3s -> Error acquiring the state lock ... Lock Info: ID 4644d157-..., Operation: OperationTypeApply, Who: root@48bbabc27bf8, Created ...; saída 1
README: aws s3 cp ... -  -> o mesmo JSON (ID igual ao da mensagem do Terraform)
README: aws s3 rm ...    -> delete: ..., saída 0
terraform plan seguinte  -> "Plan: 1 to add, 0 to change, 0 to destroy.", saída 0 (pegou o lock e rodou)
versões do .tflock: 3 versões e 4 delete markers
```

Confirmado no Floci: a chave do README é a do backend (o `kill -KILL` deixa exatamente esse objeto), o conteúdo traz
os campos que o README manda conferir, as mesmas informações aparecem na mensagem de erro do Terraform (como o README
diz) e, depois do `aws s3 rm` num bucket **versionado**, o próximo `plan` pega o lock sem `force-unlock`. Isso fecha o
ponto "delete marker" que a spec deixou para o P8 **apenas para o Floci**; no S3 real continua a conferir.

**3. Passo 4 a partir de um clone simulado em `~`** (`$QA/passos/r2/q33-passo4.sh`): cópia do repositório para
`~/renda-fixa-api` (sem `.terraform` nem override), `cd ~/renda-fixa-api/infra/aws` e `bash
scripts/verificar-zerado.sh us-east-1`: `APROVADA (nada sobrando na região us-east-1)`, saída 0 (B-A3 corrigido). A
forma antiga, `bash infra/aws/scripts/verificar-zerado.sh us-east-1` a partir de `infra/aws`, dá `No such file or
directory`, o que mostra que o defeito existia e que o bloco novo (com `cd` explícito) o evita.

Observações (não são bug): o roteiro manda remover o lock (passo 2) antes do `destruir` (passo 3), como a triagem
(R-A4) já registrou; e o `Who` de um runner do GitHub vem como `runner@<host>` e no Floci como `root@<id>`, o que o
texto do README ("o runner do job que morreu") cobre.

### Limpeza (20/11/2026)

- **Removidos:** `qa11a-floci` e `qa11a-runner` (`docker rm -f -v`, que leva o volume anônimo `/app/data` do Floci),
  e os volumes `qa11a-work` e `qa11a-plugins`. Os filhos `floci-aws-*` já tinham sido apagados pelos `destroy` (nenhum
  sobrou; conferido antes de remover o Floci). Nenhuma rede foi criada (os contêineres usaram a rede do Floci).
- **Conferido depois:** `docker ps -a` mostra só `fixedincome-api`, `fixedincome-db` (intocados, `/health` = 200) e
  `amazing_nobel`, que **não é meu** (é do revisor, imagem `qa11-runner:tmp` com o volume `rev-f11-cache3`; não
  toquei). `docker network ls`: `bridge`, `host`, `none` e `renda-fixa-api_default`, como no início. `docker volume
  ls`: os 9 volumes anônimos de antes do QA, `renda-fixa-api_fixedincome-db-data` (intocado) e `rev-f11-cache3` (do
  revisor). As imagens foram mantidas. O repositório não tem `floci_override.tf` nem `floci.tfstate`.
- **Rodada do QA11-25 (a2), 20/11/2026 ~23:00 UTC:** como a limpeza acima já tinha sido feita, recriei `qa11a-floci` e
  `qa11a-runner` e os removi de novo ao fim (`docker rm -f -v`, que leva o volume anônimo `/app/data` do Floci), mais os
  volumes `qa11a-work` e `qa11a-plugins`. Nenhum filho `floci-aws-*` foi criado nesta rodada (`service_desired_count = 0`
  e RDS destruído em cada ciclo; conferido antes de remover o Floci). Conferido depois: `docker ps -a` só com
  `fixedincome-api` e `fixedincome-db` (intocados, `/health` = 200); `docker network ls` com `bridge`, `host`, `none` e
  `renda-fixa-api_default`; `docker volume ls` com os 9 volumes anônimos de antes do QA e
  `renda-fixa-api_fixedincome-db-data` (o contêiner `amazing_nobel` e o volume `rev-f11-cache3`, do revisor, já não
  existem). Imagens mantidas. O repositório não tem `floci_override.tf` nem `floci.tfstate`.
- O rascunho `$QA` (fora do repositório) fica, com `arvore/`, `passos/` e `saidas/`.
