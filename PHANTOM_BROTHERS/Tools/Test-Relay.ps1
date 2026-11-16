[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerUrl,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Create', 'Join')]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [ValidateLength(1, 24)]
    [string]$Name,

    [string]$Code
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Name)) {
    throw 'Informe um nome com pelo menos um caractere.'
}

$baseUri = [Uri]$ServerUrl.TrimEnd('/')
if ($baseUri.Scheme -notin @('http', 'https')) {
    throw 'ServerUrl deve começar com http:// ou https://.'
}

$base = $baseUri.GetLeftPart([UriPartial]::Authority)
$requestBody = @{ displayName = $Name.Trim() } | ConvertTo-Json -Compress

if ($Action -eq 'Create') {
    $player = Invoke-RestMethod -Uri "$base/api/sessions" -Method Post -ContentType 'application/json' -Body $requestBody -TimeoutSec 15
    Write-Host "Sala criada: $($player.code)"
    Write-Host 'Envie esse codigo para o outro jogador.'
}
else {
    if ([string]::IsNullOrWhiteSpace($Code)) {
        throw 'Ao usar -Action Join, informe também -Code com o código da sala.'
    }

    $roomCode = [Uri]::EscapeDataString($Code.Trim())
    $player = Invoke-RestMethod -Uri "$base/api/sessions/$roomCode/join" -Method Post -ContentType 'application/json' -Body $requestBody -TimeoutSec 15
    Write-Host "Entrou na sala: $($player.code)"
}

if ([string]::IsNullOrWhiteSpace($player.accessToken) -or [string]::IsNullOrWhiteSpace($player.webSocketPath)) {
    throw 'A API não retornou as credenciais necessárias para o WebSocket.'
}

$webSocketScheme = if ($baseUri.Scheme -eq 'https') { 'wss' } else { 'ws' }
$webSocketUri = [Uri]::new(("{0}://{1}{2}" -f $webSocketScheme, $baseUri.Authority, $player.webSocketPath))
$socket = New-Object System.Net.WebSockets.ClientWebSocket
$socket.Options.SetRequestHeader('Authorization', "Bearer $($player.accessToken)")
$cancellationToken = [System.Threading.CancellationToken]::None

try {
    $null = $socket.ConnectAsync($webSocketUri, $cancellationToken).GetAwaiter().GetResult()
    Write-Host "WebSocket conectado a $($baseUri.Authority)."
    Write-Host 'Estados de posicao simulados serao enviados uma vez por segundo. Pressione Ctrl+C para sair.'

    $receiveBuffer = New-Object byte[] 4096
    $receiveSegment = [System.ArraySegment[byte]]::new($receiveBuffer)
    $sequence = 0
    $nextStateAt = [DateTime]::UtcNow.AddMilliseconds(100)

    while ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $messageBuilder = [System.Text.StringBuilder]::new()
        $receiveTask = $socket.ReceiveAsync($receiveSegment, $cancellationToken)
        $messageFinished = $false

        while (-not $messageFinished) {
            while (-not $receiveTask.IsCompleted) {
                if ([DateTime]::UtcNow -ge $nextStateAt) {
                    $state = [ordered]@{
                        type = 'state'
                        sequence = $sequence
                        position = [ordered]@{
                            x = [double]$sequence
                            y = 0.0
                            z = 0.0
                        }
                        rotationY = 0.0
                    }
                    $stateJson = $state | ConvertTo-Json -Compress -Depth 4
                    $stateBytes = [System.Text.Encoding]::UTF8.GetBytes($stateJson)
                    $sendSegment = [System.ArraySegment[byte]]::new($stateBytes)
                    $null = $socket.SendAsync($sendSegment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cancellationToken).GetAwaiter().GetResult()
                    Write-Host "> state #$sequence (posicao simulada)"
                    $sequence++
                    $nextStateAt = [DateTime]::UtcNow.AddSeconds(1)
                }

                [System.Threading.Thread]::Sleep(50)
            }

            $receiveResult = $receiveTask.GetAwaiter().GetResult()
            if ($receiveResult.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                $messageFinished = $true
                break
            }

            [void]$messageBuilder.Append([System.Text.Encoding]::UTF8.GetString($receiveBuffer, 0, $receiveResult.Count))
            $messageFinished = $receiveResult.EndOfMessage
            if (-not $messageFinished) {
                $receiveTask = $socket.ReceiveAsync($receiveSegment, $cancellationToken)
            }
        }

        if ($messageBuilder.Length -gt 0) {
            Write-Host "< $($messageBuilder.ToString())"
        }

        if ($receiveResult.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
            break
        }
    }
}
finally {
    if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        try {
            $null = $socket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Test ended', $cancellationToken).GetAwaiter().GetResult()
        }
        catch {
            # Ctrl+C pode cancelar a recepção antes do fechamento ordenado.
        }
    }

    $socket.Dispose()
}
