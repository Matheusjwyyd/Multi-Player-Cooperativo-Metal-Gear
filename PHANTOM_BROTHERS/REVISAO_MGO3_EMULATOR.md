# Revisão do MGO3 Server Emulator

Data: 2026-11-16  
Pasta examinada: `C:\Users\mathe\Downloads\mgo3-server-emulator-main\mgo3-server-emulator-main`

## Resumo

Este projeto é um **emulador do backend HTTP de Metal Gear Online 3**. Não é o código de uma sessão cooperativa da campanha e não contém sincronização de personagens dentro da Fox Engine. Ele responde a mensagens de menu/conta de MGO3 com dados emulados, persiste contas e loadouts em MySQL e oferece um proxy experimental para encaminhar chamadas ao serviço Konami.

O README diz que o projeto foi refatorado de dois projetos anteriores, reconhece que ainda está incompleto e deixa o guia de patch do executável como trabalho futuro. O repositório público é AGPL-3.0, tem apenas três commits e não publica releases. [Repositório upstream](https://github.com/35mpded/mgo3-server-emulator)

## Inventário local

- 2.129 arquivos, cerca de 22,7 MB.
- O núcleo é Python: Flask, Hypercorn, PyMySQL, parser/encoder de mensagens, handlers `CMD_*` e camada MySQL.
- A pasta `packages/` contém um venv Python 3.10.12 inteiro, 908 arquivos `.pyc`, oito launchers `.exe` de setuptools, uma extensão binária MarkupSafe `.so`, o pacote `.deb` de repositório Percona e o bundle de certificados do certifi.
- `static_key.bin` contém 16 bytes usados pelo protocolo Blowfish. O README diz que essa chave vem do executável do jogo.
- `app.log` estava vazio quando inspecionei a pasta.
- Não encontrei DLL/EXE próprio do emulador, launcher do mod, código C++/Lua de gameplay, arquivos `.fox2` ou rotinas para sincronizar o mundo da campanha.

Os binários e o ambiente virtual foram inventariados e tiveram hashes calculados, mas não comparei esses hashes a uma publicação assinada ou a um manifesto upstream. A pasta não contém `.git`, então não há commit local para verificar. Também não fiz uma auditoria linha por linha das 2.000+ partes do ambiente virtual/bytecode. A presença do venv e do `.deb` dentro do download não permite verificar a origem deles apenas pelo diretório.

## Como o emulador funciona

1. `mgo3.py` expõe um endpoint HTTP Flask. Para POST, lê o campo `httpMsg`, decodifica a mensagem e chama `CommandProcessor`.
2. `Decoder.py` remove o envelope do protocolo, decifra Blowfish com a chave estática e, depois da autenticação, tenta usar a chave de sessão. O payload pode ser comprimido com zlib.
3. `CommandProcessor.py` lê `msgid`, carrega um handler `server.command.CMD_*`, cria a resposta e `Encoder.py` a cifra/comprime para o jogo.
4. `Database.py` grava usuários, IDs de sessão, chaves, personagens e loadouts em MySQL.
5. A maioria dos handlers devolve valores fixos ou exemplos; alguns persistem perfil/personagem. Há uma pasta `notimplemented` para comandos sem implementação.
6. `Proxy.py` foi pensado para encaminhar algumas chamadas ao Konami. A seleção do proxy está desativada no dispatcher atual por um `if False`, embora a implementação e o cliente HTTP ainda existam.

O `CMD_GET_SVRLIST` devolve uma lista vazia; `CMD_SEND_IPANDPORT` só confirma a mensagem e `CMD_UPDATE_SESSION` responde com valores padrão. Isso não implementa o tráfego de partida, a sessão P2P ou a simulação de gameplay.

## Revisão de segurança estática

**Não encontrei um payload de malware evidente**, como downloader oculto, persistência escondida ou código ofuscado no núcleo Python. Não executei o emulador, o ambiente vendorizado, o instalador `.deb` nem os binários. Esta revisão não substitui antivírus nem uma verificação de assinatura/hash do download.

Mesmo sem indicador claro de malware, **não é seguro expor ou executar esse servidor como descrito no README**. Há problemas de autenticação e operação importantes:

### Riscos altos para uso público

- **Banco de dados como `root` com senha `123`:** está fixo em `Database.py` e é repetido nas instruções de instalação.
- **Execução privilegiada:** o README recomenda iniciar Hypercorn com `sudo`; o `app.run()` alternativo escuta em `0.0.0.0:80`.
- **Autenticação de Steam simulada:** `CMD_AUTH_STEAMTICKET` não valida a assinatura do ticket Steam; deriva um identificador de uma fatia do texto recebido e cria um usuário. `CMD_REQAUTH_HTTPS` não verifica o hash de login e entrega a mesma `crypto_key` fixa para todas as sessões. Isso é comportamento de stub/emulador, não autenticação segura.
- **Logs com dados de autenticação:** `mgo3.py` registra request e resposta completos; `Logger.py` configura DEBUG em arquivo. O proxy experimental também registra dados enviados e recebidos. Pacotes podem conter tickets, IDs e material de sessão.
- **Sem proteção de borda aparente:** o POST aceita mensagens pela rede e não vi limite de tamanho, rate limit ou allowlist robusta de comandos.

### Riscos de implementação

- O `msgid` recebido do cliente é usado para importar dinamicamente um módulo `server.command.<msgid>`. O prefixo limita o import ao pacote de handlers que existe no projeto, então não confirmei execução arbitrária de Python; ainda assim falta uma tabela explícita de comandos permitidos e mensagens malformadas podem gerar exceções/DoS.
- `Database.create_avatar_upsert_sql()` incorpora nomes de campos JSON diretamente na string SQL. A chamada que alcançaria esse método aparece comentada no handler de personagem atual; é uma vulnerabilidade latente que precisa ser removida ou limitada a uma lista fixa antes de habilitar essa função.
- `session_id` é criado com `random.randint`, não um gerador criptográfico. A chave de sessão fixa também impede isolamento criptográfico entre usuários.
- O proxy encaminha requisições para o domínio Konami se for reativado; `HttpClient` registra corpo e resposta. Isso expõe dados de autenticação e contradiz a intenção de servidor independente.
- `db/mgsv_server.dump.sql` contém vários `DROP TABLE`; importar esse dump em um banco existente pode apagar dados. Use somente em um banco descartável recém-criado, se for estudar o formato.

### Cuidados com os arquivos empacotados

- O venv dentro do projeto é Linux/Python 3.10 e contém executáveis/bytecode compilado; não use `packages/mgo3-py-venv` como ambiente confiável. Crie um ambiente novo a partir de dependências verificadas.
- O `.deb` de Percona não vem acompanhado de checksum ou verificação de assinatura no repositório. Não instale o arquivo embutido; obtenha pacotes apenas do repositório oficial validado.
- `static_key.bin` e exemplos de sessão são dados de protocolo do jogo, não devem ser publicados em logs nem reaproveitados como credenciais de jogador.

## O que aproveitar no Phantom Brothers

Há valor como referência do **backend de MGO3**: envelopes de mensagens, IDs de comando, serialização, persistência de conta e respostas do menu. Esse modelo não resolve o objetivo da campanha. O Phantom Brothers precisa continuar investigando a sessão nativa FOB/FOX dentro de `mgsvtpp.exe`, o loader/hook e a representação dos jogadores no mundo.

Não copiei código deste repositório. Ele declara AGPL-3.0 e o README reconhece derivação de outros projetos; qualquer reutilização futura precisa respeitar a licença e verificar também a origem/licença dos componentes herdados. O plano de integração atualizado está em [`ADAPTACAO_MULTIPLAYER.md`](./ADAPTACAO_MULTIPLAYER.md).
