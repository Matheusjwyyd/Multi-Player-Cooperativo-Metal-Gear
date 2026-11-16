# Revisão do Joint Operations site v1.7.1

Data: 2026-11-16  
Pasta revisada: `C:\Users\mathe\Downloads\Joint-Operations-site-v1.7.1\Joint-Operations-site-v1.7.1`

## Resumo

O download é um site estático, documentação de pesquisa e ferramentas administrativas do projeto Joint Operations. Não contém o mod multiplayer: não há DLL, EXE, código C++/Lua do jogo, projeto de launcher ou arquivos `.fox2`. O README do repositório público também informa que a primeira build jogável ainda está em desenvolvimento e que nada é jogável. [README público do Joint Operations](https://github.com/Hound-Protocol/Joint-Operations)

Inventário local: 238 arquivos, cerca de 2,78 MB. Há documentos Markdown, módulos JavaScript do site, scripts Python e shell, JSON/YAML, imagens, SVGs e fontes WOFF. Não há executáveis, bibliotecas nativas, arquivos de jogo, projetos `.csproj` ou links simbólicos no diretório. Os 13 arquivos JSON parseiam e todos os SVGs passam pelo parser XML.

## Como o projeto baixado funciona

1. **Site estático:** `site/index.html`, CSS e JavaScript local apresentam páginas como Briefing, Sessions, Docs, Handbook, Profile e Contracts. `tools/build_js.py` concatena `site/src/js/` em `site/assets/js/app.js`; os dados publicados ficam em JSON.
2. **Conteúdo:** o navegador lê arquivos públicos e releases do GitHub, documentos brutos do repositório e o contador público do widget do Discord. A página declara CSP que permite scripts somente do próprio site e limita destinos de rede ao GitHub, ao relay de login e ao Discord.
3. **Login opcional:** o navegador abre GitHub OAuth. O Cloudflare Worker troca o código OAuth por um token e o devolve à janela original com `postMessage` direcionado a `SITE_ORIGIN`. O token é usado pelo navegador para consultar GitHub; fica em `sessionStorage`, ou em `localStorage` por até 24 horas se a opção “manter conectado” for marcada.
4. **Convidados:** passes temporários usam códigos e uma chave ECDSA P-256 criada no navegador. O Worker guarda registros no Cloudflare KV e busca documentos privados usando um GitHub App. Isso protege o código da senha no armazenamento, mas continua dependendo da confiança no Worker e na conta GitHub App que o opera.
5. **Ferramentas administrativas:** scripts geram índices, PDFs, builds do site, registros assinados, issues/contratos e a estrutura do Discord. Workflows do GitHub executam verificações, publicam o site e criam releases.

Esse fluxo não cria salas dentro do MGSV, não inicializa uma sessão FOX/FOB, não injeta código no jogo e não sincroniza jogadores. A página “Sessions” é uma função do site e não um servidor multiplayer de campanha.

## Resultado da revisão estática

Foi feito inventário dos arquivos, busca de padrões de credenciais/comandos e leitura dos arquivos de login/relay, renderizador Markdown, CSP, ferramentas com rede ou efeitos de escrita, testes do relay e workflows. Comparei também `site/assets/js/app.js` com a concatenação ordenada dos 36 módulos fonte: o bundle corresponde exatamente. Nenhum código foi executado e nenhum arquivo da pasta baixada foi alterado.

**Não encontrei indicador claro de malware**: não há payload executável, download oculto, persistência disfarçada, ofuscação evidente ou destino de exfiltração escondido. O JavaScript entregue é legível e vem de módulos locais. A busca por credenciais reais encontrou somente a chave privada de teste gerada em memória por `tests/relay_test.mjs`; não encontrei uma chave privada ou token de produção no pacote.

Isso é uma revisão estática, não uma garantia de que todo arquivo seja seguro. Não executei antivírus nem comparei o diretório com o hash do arquivo original baixado. A pasta não contém `.git`, então não dá para vincular exatamente esse snapshot a um commit específico. O conteúdo e o README local são consistentes com o projeto público.

## Riscos e cuidados identificados

### Login e serviços remotos

- `site/data/config.json` aponta o login para `https://hp-signin.trueman-truetown.workers.dev`. Se alguém usar o login nesse site, o token OAuth passa por esse Worker. O código fonte descreve o fluxo, mas não prova que a implantação remota ainda corresponde a esse código ou quem controla hoje a conta Cloudflare.
- Para o Phantom Brothers, não reutilizar esse relay, token, conta GitHub App, KV ou URL. O mod não precisa deles para replicar estado de jogo. Só integrar login remoto depois de criar e controlar a própria infraestrutura.
- O projeto não carrega JavaScript de CDN e não encontrei analytics/tracker nos arquivos do site. As chamadas externas declaradas são GitHub, o Worker e o widget público do Discord.

### Endpoints do relay

- O Worker coloca `msg.error` no HTML de retorno do OAuth sem escapar texto. O valor vem da resposta de GitHub/configuração, não achei uma entrada direta do usuário que torne isso uma exploração confirmada, mas seria mais seguro renderizar como texto escapado.
- O código do passe e a chave de consulta de pedidos aparecem em query strings. Não vi logs de aplicação, mas URLs podem aparecer em telemetria de borda. A validação do passe também não mostra uma limitação robusta por endereço/cliente; o código de seis caracteres pode sofrer tentativa distribuída. Recomenda-se rate limit no Worker e tirar segredos de query strings.
- O limite de pedidos de passe é global e baseado em um registro KV compartilhado, não por visitante; tráfego automatizado pode ocupar a fila ou atrapalhar solicitações legítimas.
- A cópia local da página está protegida por CSP, mas a segurança de produção ainda depende da configuração e do código efetivamente implantado no Cloudflare.

### Ferramentas que fazem alterações

Essas ações são explícitas e não são malware, mas não devem ser executadas sem revisar o alvo:

- `tools/backup/install.sh` instala e ativa um timer persistente do `systemd` do usuário e pode habilitar `linger`. O script de backup usa `gh`, cria cópias, pode sincronizá-las ao destino `rclone` configurado e apaga cópias antigas conforme a retenção.
- `tools/discord/setup.py` usa `DISCORD_BOT_TOKEN` para criar/ajustar cargos, canais e permissões. Sem `--apply`, o modo padrão é dry run; com `--apply`, altera o servidor Discord informado.
- `tools/assemble_site.py OUT_DIR` apaga recursivamente o diretório passado em `OUT_DIR` antes de reconstruir o site. Use um caminho descartável e confira o destino antes de executá-lo.
- Workflows `pull_request_target` merecem revisão contínua. Os encontrados limitam permissões e usam o checkout da branch-base, sem executar o código da PR; ainda assim, ações externas estão referenciadas por tags de versão e alguns pacotes do CI não estão fixados em hash/versão exata, o que deixa risco de cadeia de suprimentos.

## O que aproveitar no Phantom Brothers

O valor está nas decisões e no processo de pesquisa, não em uma implementação multiplayer pronta:

- Priorizar a sessão nativa FOX/FOB e deixar netcode próprio com personagens substitutos como rota de contingência.
- Tratar o anfitrião como autoridade para IA, missão e mundo; cada cliente controla seu próprio movimento.
- Fazer a primeira prova para dois jogadores. Só aumentar a lotação após testar progressivamente 3, 4 e 8, pois a capacidade genérica da engine não prova que as rotinas de campanha aceitam esses jogadores.
- Separar eventos confiáveis, estado de movimento frequente e snapshots de entrada tardia, sem presumir que esses canais já estejam ligados ao jogo.
- Manter versionamento/hash do executável e recusar hooks não validados para a versão local.
- Dividir o trabalho em marcos que exijam evidência dentro do jogo; o site do Joint Operations também lista suas ideias de multiplayer como pesquisa e trabalho futuro, não como funcionalidade entregue.

Os detalhes incorporados à nossa direção técnica estão em [`ADAPTACAO_MULTIPLAYER.md`](./ADAPTACAO_MULTIPLAYER.md). Nenhum código, texto extenso, imagem, marca ou logo do download foi copiado para o Phantom Brothers.

## Licenças do pacote

O repositório declara o código sob GPL-3.0-only com permissão adicional; documentação sob CC BY-SA 4.0; e nome/logo Hound Protocol/Joint Operations reservados. Fontes possuem SIL Open Font License. Se no futuro código ou documentação forem copiados, será necessário cumprir a licença correspondente e manter atribuição; não reutilizar a identidade visual.
