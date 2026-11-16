# Adaptação do multiplayer de campanha

## O que o jogo já oferece

Metal Gear Online usa `mgsvmgo.exe` e é um modo separado. A campanha roda em `mgsvtpp.exe`, que já contém uma sessão multiplayer interna usada pelo modo FOB. A adaptação de cooperativo deve reutilizar a sessão FOB/Steam P2P dentro do executável da campanha.

O projeto aberto Dynamite documenta uma prova prática desse caminho:

1. O anfitrião cria a sessão com as rotinas de host de FOB e aceita participantes.
2. O cliente aponta para o SteamID do anfitrião e inicia a rotina de sessão de cliente de FOB.
3. O mod remove verificações que restringem criação/representação de jogador às missões FOB.
4. O arquivo de definição FOX precisa alocar uma segunda instância de jogador em missões single-player.
5. Scripts de missão recebem mudanças para permitir o fluxo cooperativo.
6. Hooks nativos corrigem a lógica de alvo da IA e outros comportamentos que assumiam somente um jogador.

Isso é diferente de transmitir `{position, rotation}` por um servidor web: a engine precisa registrar os jogadores como entidades próprias da sessão para aplicar animação, câmera, dano e sincronização de missão.

## Decisões úteis do Joint Operations

O download `Joint-Operations-site-v1.7.1` não contém o mod, mas seus documentos organizam uma rota técnica útil:

- **Rota preferida:** reutilizar a sessão do jogo e estender os sistemas single-player que assumem um jogador.
- **Fallback:** personagens substitutos e netcode próprio, apenas se a sessão nativa não puder ser ampliada.
- **Autoridade:** o anfitrião controla IA, mundo e missão; cada cliente controla seu próprio movimento.
- **Transporte adicional:** o projeto propõe estudar `ISteamNetworkingSockets`, com mensagens confiáveis para eventos, mensagens não confiáveis para estados frequentes e um canal de maior volume para snapshots. Isso é uma proposta ainda sem teste dentro do jogo; a sessão/mensageria nativa deve ser investigada primeiro.
- **Marcos:** provar dois jogadores, depois testar três antes de escalar para quatro e oito. O número “16” da capacidade genérica da engine não é prova de que a campanha suporte essa lotação.

Essas ideias foram reescritas para o Phantom Brothers. O código do site ou do relay não será usado como implementação do mod. A avaliação completa do download, incluindo fluxo de dados e ressalvas de segurança, está em [`REVISAO_JO_SITE.md`](./REVISAO_JO_SITE.md).

## O que o MGO3 Server Emulator faz — e não faz

O segundo projeto baixado emula endpoints de backend de Metal Gear Online 3: autenticação de menu, dados de usuário, personagens, loadouts e respostas de serviço. Ele não hospeda a partida, não replica posição dos jogadores e não injeta gameplay na campanha. O próprio README declara o foco em manter o MGO3 online e deixa o patch do executável como trabalho futuro.

Não integrar seu banco, chaves Blowfish, autenticação de tickets ou código AGPL ao Phantom Brothers. Além de ser outro modo de jogo, o pacote analisado usa autenticação de teste, `root/123`, logs de pacotes e um proxy Konami desativado. A análise completa está em [`REVISAO_MGO3_EMULATOR.md`](./REVISAO_MGO3_EMULATOR.md). Para o cooperativo da campanha, a prioridade permanece a sessão FOX/FOB nativa e os hooks validados para `mgsvtpp.exe`.

## Modelo de sessão e ordem de sincronização

Este é o modelo-alvo para orientar o primeiro protótipo; cada item ainda precisa ser confirmado dentro do jogo:

- **Anfitrião:** autoridade para IA inimiga, fases dos postos de comando, objetivos, tempo/clima e estado do mundo.
- **Cliente:** autoridade inicial sobre seu próprio input e movimento; o anfitrião confirma interações, dano e ações que alteram o mundo.
- **Atualizações rápidas:** posição, direção, postura e estado de movimento são estado frequente e substituível; estados atrasados podem ser descartados.
- **Eventos importantes:** tiro/hit, morte/respawn, item coletado, objetivo, alerta e extração exigem confirmação e ordem consistente.
- **Entrada tardia:** um jogador que entra durante a missão precisa receber um snapshot do estado atual antes de receber atualizações incrementais.

Ordem sugerida para o primeiro ciclo de desenvolvimento:

1. **P0 — provar que há duas entidades de jogador:** conexão, identidade, posição, orientação, animação básica e desconexão.
2. **P1 — tornar uma missão cooperativa:** estado dos inimigos, dano, alerta por posto de comando, objetivos e extração sob autoridade do anfitrião.
3. **P2 — interações:** marcações, objetos do mundo, Fulton, veículos e buddies.
4. **P3 — escala e acabamento:** entrada tardia, voz, navegador de salas e testes progressivos de 3, 4 e 8 participantes.

Não devemos implementar replicação completa de inimigos por um relay externo antes de saber o que a sessão nativa já replica. A pesquisa do site é uma proposta de arquitetura feita em mesa; seus próprios contratos deixam os testes no jogo como pendentes.

## O que impede oito jogadores hoje

A documentação do Dynamite afirma que a FOX Engine tem sessões capazes de até 16 jogadores, mas relata que uma tentativa de iniciar com mais de dois falhou. Capacidade genérica da engine não prova que as rotinas FOB e as missões de campanha aceitem mais participantes.

O limite de oito do Phantom Brothers deve ficar desativado até validar, nesta ordem:

1. Mais de duas instâncias de jogador são alocadas e renderizadas em uma missão pequena.
2. Todos os clientes entram e recebem índices/identidades únicos.
3. O host mantém uma única autoridade de IA; guardas reagem corretamente aos participantes.
4. Morte, respawn, exfiltração e fim de missão são iguais em todas as máquinas.
5. A sincronização continua estável ao aumentar gradualmente de 2 para 3, 4 e 8 jogadores.

O primeiro alvo de implementação deve continuar sendo dois jogadores na missão 5, “Over the Fence”, que foi usada como cenário inicial de teste do Dynamite; oito é uma fase posterior que depende dessas provas.

## Compatibilidade com a instalação local

O `mgsvtpp.exe` desta pasta reporta 1.0.15.4. A versão pública upstream do Dynamite ainda mostra um aviso para voltar à versão anterior à atualização de junho de 2026. O problema upstream foi aberto em 9 de junho de 2026; um commit comunitário de 20 de outubro de 2026 adicionou tabelas de endereços e patches para 1.0.15.4, mas o próprio port lista o teste de dois jogadores nessa versão como pendente. Não usar uma tabela experimental como se estivesse validada.

Além dos endereços nativos, a instalação precisa tratar arquivos `.dat` e scripts modificados. Qualquer instalação futura deve trabalhar numa cópia/backup e verificar os hashes antes de alterar o jogo.

## Sequência de adaptação

### A. Identificar a build

- Guardar versão e SHA-256 do executável e de `00.dat` usados nos testes.
- Comparar com as tabelas e patches do hook para a versão correspondente.
- Recusar iniciar se a versão não tiver endereços validados.

### B. Reproduzir a conexão de dois jogadores

- Adaptar o fluxo de host/cliente FOB na campanha.
- Manter Steam P2P para dados de jogo.
- Separar o estado de sessão/invite do transporte dos atores.
- Confirmar o segundo player instance no `.fox2` e em scripts de missão.

### C. Tornar a missão cooperativa

- Corrigir atribuição de alvo da IA para qualquer jogador sem rodar duas IAs divergentes.
- Sincronizar estados de objetivo, resgate, dano, morte, respawn e extração.
- Criar uma lista de missões que passam por teste; não habilitar a campanha inteira por padrão.

### D. Expandir participantes

- Encontrar e corrigir arrays, limites e pressupostos de player index nas rotinas FOX/FOB.
- Aumentar a contagem somente quando o cliente adicional completar uma missão de ponta a ponta.

## Base de pesquisa e licença

- [unknown321/dynamite](https://github.com/unknown321/dynamite): fonte do mod de cooperativo de dois jogadores.
- [Making of do Dynamite](https://github.com/unknown321/dynamite/blob/master/docs/MAKING_OF.md): descreve as rotinas FOB/Steam P2P, alocação FOX, problemas de IA e o experimento com mais de dois participantes.
- [Documentação de uso](https://github.com/unknown321/dynamite/blob/master/webui/docs/docs.md): fluxo de anfitrião/cliente, requisito Steam e lista de limitações.
- [Issue sobre a atualização do jogo](https://github.com/unknown321/dynamite/issues/136): recomenda reversão para a versão anterior na publicação disponível.
- [Portagem comunitária para 1.0.15.4](https://github.com/slimy-bro/dynamite-vibecoded/commit/42806d1f1a182763b8809bccf3cbca37a8045e51): referência não oficial; o teste multiplayer em .4 aparece como pendente.

O Dynamite declara GPLv3 com Commons Clause. Se o Phantom Brothers incorporar código desse projeto, deverá manter os avisos/licença e as restrições da Commons Clause; esta base não pode ser redistribuída como produto pago.
