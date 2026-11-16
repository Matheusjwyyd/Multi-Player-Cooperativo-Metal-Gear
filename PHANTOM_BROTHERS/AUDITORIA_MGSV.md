# Auditoria local do MGSV

Data da inspeção: 2026-11-15.

## O que foi encontrado

Pasta examinada: `MGS_TPP/`.

| Item | Resultado |
| --- | --- |
| Executável da campanha | `mgsvtpp.exe`, versão de arquivo/produto **1.0.15.4**, 166,517,760 bytes |
| Executável de Metal Gear Online | `mgsvmgo.exe`, versão **1.1.2.9**, 155,560,448 bytes |
| Identificador Steam | `287700` |
| Identificação do pacote | `Tpp_steam_mst_en_day3900Mgo_patch_0707_1632` |
| Dados do jogo | Arquivos `.dat` em `master/` e `mgo/` |
| IHHook / SnakeBite / fontes do mod | Não encontrados na pasta examinada |
| Ferramentas locais | .NET SDK 10.0.404 disponível |

O diretório contém a instalação do jogo, mas não contém um projeto de mod anterior ou os arquivos de desenvolvimento do IHHook. Os arquivos `.dat` são conteúdo empacotado do jogo; esta entrega não os altera.

## Multiplayer que já existe no jogo

`mgsvmgo.exe` é o executável de Metal Gear Online; ele é um modo separado da campanha. Para cooperativo de campanha, o caminho técnico encontrado é a infraestrutura de sessão FOB dentro de `mgsvtpp.exe`: o Dynamite chama as funções de sessão FOB, usa Steam P2P para conectar host e cliente, remove verificações que limitavam o ator remoto às missões FOB e aumenta a alocação de instâncias de jogador no arquivo FOX correspondente. Portanto, o trabalho é adaptar a sessão/rotinas de FOB dentro da campanha; não reaproveitar `mgsvmgo.exe` como se fosse um modo de campanha.

O autor do Dynamite documenta também os limites: inimigos escolhem o host como alvo por padrão, a autoridade da IA precisa ser ajustada e pode dessincronizar; algumas missões, animações, veículos, buddies, triggers e interações ficam quebrados. Uma tentativa com mais de dois jogadores não iniciou o jogo. O relatório detalhado está em [`ADAPTACAO_MULTIPLAYER.md`](./ADAPTACAO_MULTIPLAYER.md).

## Compatibilidade

A especificação inicial fornecida cita 1.0.15.3 como versão de referência de ferramentas. O executável instalado reporta 1.0.15.4. O repositório upstream do Dynamite alerta que a atualização de junho de 2026 impede o jogo de iniciar com a versão então publicada e recomenda reverter para a anterior. O código upstream pesquisado contém tabela de endereços para 1.0.15.3. Uma portagem comunitária posterior adiciona tabelas para 1.0.15.4, mas a própria portagem lista o teste com dois jogadores como trabalho pendente. A compatibilidade precisa ser validada antes de carregar o hook; o número de versão sozinho não prova que os endereços ou patches funcionam.

## Consequências técnicas

- O jogo fornece a sessão multiplayer de FOB e o transporte Steam P2P que o cooperativo de campanha pode reutilizar.
- Uma sala WebSocket própria pode servir mais tarde para convites e coordenação, mas não substitui as rotinas nativas que criam jogadores, replicam atores e sincronizam a missão.
- A campanha precisa suportar múltiplas instâncias de jogador no FOX Engine e executar o fluxo de host/cliente de FOB sem as restrições de missão originais.
- `mgsvmgo.exe` é o executável de Metal Gear Online; as rotinas de MGO não são o caminho já demonstrado para cooperativo de campanha.
- A meta de oito jogadores excede o que foi demonstrado publicamente: o Dynamite funciona como projeto para dois jogadores e relata falha ao tentar mais de dois.

## Limite desta primeira entrega

Esta é uma auditoria dos arquivos locais e uma análise do caminho multiplayer de campanha. O servidor WebSocket criado aqui é apenas um experimento independente, não um mod carregável pelo MGSV. O próximo bloqueio técnico é reproduzir o hook/sessão nativos no executável 1.0.15.4 e provar uma sessão de dois jogadores antes de expandir o número de participantes.
