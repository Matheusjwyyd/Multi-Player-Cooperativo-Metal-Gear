# Phantom Brothers

Projeto de cooperativo para a campanha do MGSV: The Phantom Pain.

## Direção técnica

A campanha já contém transporte multiplayer usado pelas missões FOB. O mod Dynamite demonstrou que é possível reutilizar essa sessão Steam P2P em missões da campanha, com alterações nas scripts Lua, alocação do segundo jogador no FOX Engine e hooks nativos. O Phantom Brothers deve estender esse caminho; retransmitir posição por um servidor WebSocket separado não faz o jogo criar ou sincronizar um soldado.

## Estado atual

- Auditoria da instalação local e das opções de integração em [`AUDITORIA_MGSV.md`](./AUDITORIA_MGSV.md).
- Descobertas e plano de adaptação do multiplayer de campanha em [`ADAPTACAO_MULTIPLAYER.md`](./ADAPTACAO_MULTIPLAYER.md).
- Revisão estática da cópia Dynamite 0.6.0, incluindo comparação com o tag oficial, inventário de executáveis e compatibilidade em [`REVISAO_DYNAMITE_0.6.0.md`](./REVISAO_DYNAMITE_0.6.0.md).
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

### Testar a retransmissão entre dois PCs

O script [`Tools/Test-Relay.ps1`](./Tools/Test-Relay.ps1) cria ou entra em uma sala, conecta cada participante ao WebSocket e envia posições simuladas. Isso valida a rede e a retransmissão do servidor; não lê a posição do MGSV e não injeta jogadores no jogo.

No PC anfitrião, inicie o servidor em um terminal. `0.0.0.0` permite conexões pelas interfaces de rede; não exponha a porta diretamente à Internet:

```powershell
$env:PHANTOM_BROTHERS_URL = "http://0.0.0.0:5080"
dotnet run --project ".\PHANTOM_BROTHERS\Server\PhantomBrothers.Server.csproj"
```

Em outro PowerShell aberto na raiz do repositório, conecte o primeiro participante:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\PHANTOM_BROTHERS\Tools\Test-Relay.ps1 -ServerUrl http://localhost:5080 -Action Create -Name PC1
```

Copie o código mostrado. No segundo PC, execute o script da cópia do repositório, substituindo o endereço pelo IP alcançável do PC anfitrião e o código pelo código da sala:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\PHANTOM_BROTHERS\Tools\Test-Relay.ps1 -ServerUrl http://10.0.0.205:5080 -Action Join -Name PC2 -Code CODIGO_DA_SALA
```

Para testar de redes diferentes, conecte ambos os PCs à mesma rede privada do Tailscale e use o IP Tailscale do anfitrião no `-ServerUrl`. No PC anfitrião, permita a porta TCP 5080 apenas para o IP Tailscale do PC2, em um PowerShell como administrador:

```powershell
$ipDoPc2 = "100.x.y.z"
New-NetFirewallRule -DisplayName "Phantom Brothers teste remoto" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5080 -RemoteAddress $ipDoPc2 -Profile Any
```

Use o convite/compartilhamento de dispositivo do [Tailscale](https://tailscale.com/docs/features/sharing) para incluir o outro jogador. Não encaminhe essa porta diretamente para a Internet. Com os dois scripts conectados, cada terminal deve mostrar `room_snapshot`, `player_joined` e `player_state`, e a consulta `GET /api/sessions/{code}` deve listar os dois participantes com `connected = true`.
