# Phantom Brothers

Projeto de cooperativo para a campanha do MGSV: The Phantom Pain.

## Direção técnica

A campanha já contém transporte multiplayer usado pelas missões FOB. O mod Dynamite demonstrou que é possível reutilizar essa sessão Steam P2P em missões da campanha, com alterações nas scripts Lua, alocação do segundo jogador no FOX Engine e hooks nativos. O Phantom Brothers deve estender esse caminho; retransmitir posição por um servidor WebSocket separado não faz o jogo criar ou sincronizar um soldado.

## Estado atual

- Auditoria da instalação local e das opções de integração em [`AUDITORIA_MGSV.md`](./AUDITORIA_MGSV.md).
- Descobertas e plano de adaptação do multiplayer de campanha em [`ADAPTACAO_MULTIPLAYER.md`](./ADAPTACAO_MULTIPLAYER.md).
- Análise de segurança e arquitetura do projeto baixado Joint Operations em [`REVISAO_JO_SITE.md`](./REVISAO_JO_SITE.md).
- Análise do MGO3 Server Emulator em [`REVISAO_MGO3_EMULATOR.md`](./REVISAO_MGO3_EMULATOR.md).
- O serviço em `Server/` é somente um protótipo independente de sala e retransmissão. Não está ligado ao MGSV e não representa um mod carregável.
- Nenhum arquivo do jogo foi modificado.

O jogo local é 1.0.15.4. O Dynamite upstream ainda orienta voltar para a versão anterior; há uma portagem comunitária de 1.0.15.4, mas ela não registra teste concluído entre dois jogadores. Por isso, a integração nativa precisa começar validando os endereços e hooks desta build, sem aplicar arquivos experimentais ao jogo.

## Escopo de desenvolvimento

O primeiro marco será uma sessão de campanha para dois jogadores, usando a rede Steam P2P/FOB já existente. O objetivo de oito jogadores fica como expansão: a engine é descrita como tendo capacidade multiplayer maior, mas o autor do Dynamite relata que uma tentativa acima de dois jogadores nem iniciou o jogo.

Depois que a sessão de dois jogadores for reproduzida na versão 1.0.15.4, o trabalho avança para autoridade da IA, sincronização de missão, reconexão e um launcher próprio. A lista de limitações conhecidas está em [`ADAPTACAO_MULTIPLAYER.md`](./ADAPTACAO_MULTIPLAYER.md).

## Serviço experimental independente

O servidor WebSocket pode ser iniciado com .NET SDK 10.0:

```powershell
dotnet run --project .\PHANTOM_BROTHERS\Server\PhantomBrothers.Server.csproj
```

As rotas estão descritas em [`PROTOCOL_V0.1.md`](./PROTOCOL_V0.1.md). Esse processo ainda não descobre SteamIDs, não inicia a sessão FOX e não substitui a conexão de jogo.
