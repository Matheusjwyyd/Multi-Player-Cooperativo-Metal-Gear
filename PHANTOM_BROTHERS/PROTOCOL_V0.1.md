# Protocolo Phantom Brothers 0.1

> Este é um protótipo independente de salas e retransmissão JSON. O MGSV ainda não o consome. O protocolo de movimento definitivo deve usar as sessões Steam P2P e os mecanismos de sincronização da FOX Engine; este WebSocket não cria atores no jogo.

O protótipo expõe uma API HTTP e um canal WebSocket. Todas as mensagens são JSON UTF-8. O limite de mensagem do WebSocket é 4 KiB. Ele serve apenas para exercitar uma ideia de sala externa, não para provar sincronização dentro da campanha.

## Criar sala

`POST /api/sessions`

```json
{"displayName":"Snake"}
```

Resposta `201`:

```json
{
  "code":"7KQ2NP",
  "playerId":"…",
  "displayName":"Snake",
  "accessToken":"…",
  "webSocketPath":"/api/sessions/7KQ2NP/ws?playerId=…"
}
```

## Entrar em sala

`POST /api/sessions/{code}/join`

```json
{"displayName":"Miller"}
```

Resposta `201` com os mesmos campos. O limite atual é de oito participantes por sala.

## Conectar e reconectar

Abra o caminho `webSocketPath` devolvido pela API e envie o token no cabeçalho HTTP:

```text
Authorization: Bearer <accessToken>
```

O token permite reconectar o mesmo jogador enquanto o processo do servidor mantiver a sala em memória. Ele não é uma conta e deve ser tratado como segredo.

## Estado de movimento

Cliente para servidor:

```json
{
  "type":"state",
  "sequence":42,
  "position":{"x":10.0,"y":2.0,"z":-4.0},
  "rotationY":1.57
}
```

Servidor para os outros participantes:

```json
{
  "type":"player_state",
  "playerId":"…",
  "displayName":"Snake",
  "state":{
    "type":"state",
    "sequence":42,
    "position":{"x":10.0,"y":2.0,"z":-4.0},
    "rotationY":1.57
  }
}
```

O campo `sequence` ajuda o consumidor a descartar estados antigos. O servidor valida números finitos e retransmite o estado, mas não faz interpolação nem valida autoridade do anfitrião.

## Eventos do servidor

- `room_snapshot`: participantes presentes quando a conexão é aberta, incluindo o último estado conhecido.
- `player_joined`: participante conectado.
- `player_left`: conexão encerrada; o jogador continua reservado para reconexão.
- `player_state`: novo estado recebido de outro cliente.
- `pong`: resposta ao comando `{"type":"ping"}`.
- `error`: mensagem inválida ou tipo não suportado.

Salas e tokens existem apenas na memória. Reiniciar o servidor encerra todas as salas.
