using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("PHANTOM_BROTHERS_URL") ?? "http://localhost:5080");
builder.Services.AddSingleton<SessionStore>();

var app = builder.Build();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(20)
});

app.MapGet("/health", () => Results.Ok(new
{
    name = "Phantom Brothers Session Server",
    version = "0.1.0",
    status = "ready"
}));

app.MapPost("/api/sessions", (CreateSessionRequest request, SessionStore store) =>
{
    if (!TryNormalizeName(request.DisplayName, out var displayName))
        return Results.BadRequest(new { error = "displayName deve ter entre 1 e 24 caracteres." });

    var player = store.Create(displayName);
    return Results.Created($"/api/sessions/{player.Code}", ToPlayerResponse(player));
});

app.MapPost("/api/sessions/{code}/join", (string code, JoinSessionRequest request, SessionStore store) =>
{
    if (!TryNormalizeName(request.DisplayName, out var displayName))
        return Results.BadRequest(new { error = "displayName deve ter entre 1 e 24 caracteres." });

    var result = store.Join(code, displayName);
    return result switch
    {
        JoinResult.Joined joined => Results.Created($"/api/sessions/{joined.Player.Code}", ToPlayerResponse(joined.Player)),
        JoinResult.NotFound => Results.NotFound(new { error = "Sala não encontrada." }),
        JoinResult.Full => Results.Conflict(new { error = "Sala cheia." }),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
});

app.MapGet("/api/sessions/{code}", (string code, SessionStore store) =>
{
    var room = store.GetSnapshot(code);
    return room is null ? Results.NotFound(new { error = "Sala não encontrada." }) : Results.Ok(room);
});

app.Map("/api/sessions/{code}/ws", HandleWebSocketAsync);

app.Run();

static async Task HandleWebSocketAsync(HttpContext context)
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var store = context.RequestServices.GetRequiredService<SessionStore>();
    var code = context.Request.RouteValues["code"]?.ToString() ?? string.Empty;
    var playerId = context.Request.Query["playerId"].ToString();
    var auth = context.Request.Headers.Authorization.ToString();
    var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..].Trim() : string.Empty;
    var attach = store.Attach(code, playerId, token);
    if (attach is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    attach.Peer.SetSocket(socket);
    await SendJsonAsync(attach.Peer, new
    {
        type = "room_snapshot",
        players = attach.Snapshot
    }, context.RequestAborted);
    await store.BroadcastAsync(code, new { type = attach.WasConnected ? "player_reconnected" : "player_joined", playerId, displayName = attach.DisplayName }, playerId);

    try
    {
        await ReceiveLoopAsync(context, store, code, playerId, attach.Peer);
    }
    catch (WebSocketException)
    {
        // A dropped network connection is expected; the player remains reconnectable.
    }
    finally
    {
        var detached = store.Detach(code, playerId, attach.Peer);
        if (detached)
            await store.BroadcastAsync(code, new { type = "player_left", playerId });
    }
}

static async Task ReceiveLoopAsync(HttpContext context, SessionStore store, string code, string playerId, Peer peer)
{
    var socket = peer.Socket;
    var buffer = new byte[1024];
    using var message = new MemoryStream();

    while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
    {
        message.SetLength(0);
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
                return;
            if (result.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Somente mensagens de texto são aceitas.", CancellationToken.None);
                return;
            }
            if (message.Length + result.Count > 4096)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Mensagem acima de 4 KiB.", CancellationToken.None);
                return;
            }
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
        if (!TryParseClientMessage(json, out var clientMessage, out var error))
        {
            await SendJsonAsync(peer, new { type = "error", message = error }, context.RequestAborted);
            continue;
        }

        if (clientMessage!.Type == "ping")
        {
            await SendJsonAsync(peer, new { type = "pong", serverTime = DateTimeOffset.UtcNow }, context.RequestAborted);
            continue;
        }

        if (!store.SetState(code, playerId, peer, clientMessage))
            return;
        await store.BroadcastAsync(code, new
        {
            type = "player_state",
            playerId,
            displayName = store.GetDisplayName(code, playerId),
            state = clientMessage
        }, playerId);
    }
}

static bool TryParseClientMessage(string json, out ClientMessage? message, out string error)
{
    message = null;
    error = "JSON inválido.";
    try
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            return false;

        var type = typeElement.GetString();
        if (type == "ping")
        {
            message = new ClientMessage("ping", 0, new Position(0, 0, 0), 0);
            return true;
        }
        if (type != "state")
        {
            error = "Tipo de mensagem não suportado.";
            return false;
        }

        if (!root.TryGetProperty("sequence", out var seqElement) || !seqElement.TryGetInt64(out var sequence) ||
            !root.TryGetProperty("position", out var posElement) || posElement.ValueKind != JsonValueKind.Object ||
            !TryReadFinite(posElement, "x", out var x) || !TryReadFinite(posElement, "y", out var y) ||
            !TryReadFinite(posElement, "z", out var z) || !TryReadFinite(root, "rotationY", out var rotationY))
        {
            error = "Estado inválido: informe sequence, position (x/y/z) e rotationY numéricos e finitos.";
            return false;
        }

        message = new ClientMessage("state", sequence, new Position(x, y, z), rotationY);
        return true;
    }
    catch (JsonException)
    {
        return false;
    }
}

static bool TryReadFinite(JsonElement element, string property, out float value)
{
    value = 0;
    return element.TryGetProperty(property, out var number) && number.TryGetSingle(out value) && float.IsFinite(value);
}

static bool TryNormalizeName(string? input, out string value)
{
    value = input?.Trim() ?? string.Empty;
    return value.Length is >= 1 and <= 24 && !value.Any(char.IsControl);
}

static PlayerResponse ToPlayerResponse(PlayerCredentials player) => new(
    player.Code,
    player.PlayerId,
    player.DisplayName,
    player.AccessToken,
    $"/api/sessions/{player.Code}/ws?playerId={Uri.EscapeDataString(player.PlayerId)}");

static Task SendJsonAsync(Peer peer, object value, CancellationToken cancellationToken) =>
    peer.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value, ProtocolJson.Options), cancellationToken);

public sealed record CreateSessionRequest(string? DisplayName);
public sealed record JoinSessionRequest(string? DisplayName);
public sealed record PlayerResponse(string Code, string PlayerId, string DisplayName, string AccessToken, string WebSocketPath);
public sealed record Position(float X, float Y, float Z);
public sealed record ClientMessage(string Type, long Sequence, Position Position, float RotationY);
public sealed record SnapshotPlayer(string PlayerId, string DisplayName, ClientMessage? State, bool Connected);
public sealed record SessionSnapshot(string Code, int PlayerCount, IReadOnlyList<SnapshotPlayer> Players);
public sealed record PlayerCredentials(string Code, string PlayerId, string DisplayName, string AccessToken);
public sealed record AttachResult(Peer Peer, string DisplayName, bool WasConnected, IReadOnlyList<SnapshotPlayer> Snapshot);

public static class ProtocolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public abstract record JoinResult
{
    public sealed record Joined(PlayerCredentials Player) : JoinResult;
    public sealed record NotFound : JoinResult;
    public sealed record Full : JoinResult;
}

public sealed class Peer
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private WebSocket? _socket;

    public WebSocket Socket => _socket ?? throw new InvalidOperationException("WebSocket ainda não conectado.");
    public void SetSocket(WebSocket socket) => _socket = socket;
    public void Abort() => _socket?.Abort();

    public async Task SendAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var socket = _socket;
        if (socket?.State != WebSocketState.Open)
            return;

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (WebSocketException)
        {
            // O cliente pode ter desconectado entre a checagem e o envio.
        }
        finally
        {
            _sendLock.Release();
        }
    }
}

public sealed class SessionStore
{
    private const int MaxPlayers = 8;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public PlayerCredentials Create(string displayName)
    {
        string code;
        Room room;
        do
        {
            code = CreateCode();
            room = new Room(code);
        } while (!_rooms.TryAdd(code, room));

        var player = NewPlayer(code, displayName);
        room.Members.TryAdd(player.PlayerId, new Member(player));
        return player;
    }

    public JoinResult Join(string code, string displayName)
    {
        if (!_rooms.TryGetValue(code, out var room))
            return new JoinResult.NotFound();

        lock (room.Gate)
        {
            if (room.Members.Count >= MaxPlayers)
                return new JoinResult.Full();

            var player = NewPlayer(room.Code, displayName);
            room.Members.TryAdd(player.PlayerId, new Member(player));
            return new JoinResult.Joined(player);
        }
    }

    public AttachResult? Attach(string code, string playerId, string token)
    {
        if (!_rooms.TryGetValue(code, out var room) || !room.Members.TryGetValue(playerId, out var member) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(member.AccessToken), Encoding.UTF8.GetBytes(token)))
            return null;

        lock (room.Gate)
        {
            var wasConnected = member.Connection is not null;
            var previousConnection = member.Connection;
            member.Connection = new Peer();
            var snapshot = room.Members.Values
                .Where(other => other.PlayerId != playerId)
                .Select(other => new SnapshotPlayer(other.PlayerId, other.DisplayName, other.State, other.Connection is not null))
                .ToArray();
            previousConnection?.Abort();
            return new AttachResult(member.Connection, member.DisplayName, wasConnected, snapshot);
        }
    }

    public bool Detach(string code, string playerId, Peer peer)
    {
        if (!_rooms.TryGetValue(code, out var room) || !room.Members.TryGetValue(playerId, out var member))
            return false;
        lock (room.Gate)
        {
            if (!ReferenceEquals(member.Connection, peer))
                return false;
            member.Connection = null;
            return true;
        }
    }

    public bool SetState(string code, string playerId, Peer peer, ClientMessage state)
    {
        if (!_rooms.TryGetValue(code, out var room) || !room.Members.TryGetValue(playerId, out var member))
            return false;
        lock (room.Gate)
        {
            if (!ReferenceEquals(member.Connection, peer))
                return false;
            member.State = state;
            return true;
        }
    }

    public string GetDisplayName(string code, string playerId) =>
        _rooms.TryGetValue(code, out var room) && room.Members.TryGetValue(playerId, out var member)
            ? member.DisplayName
            : string.Empty;

    public async Task BroadcastAsync(string code, object message, string? excludePlayerId = null)
    {
        if (!_rooms.TryGetValue(code, out var room))
            return;
        Peer[] peers;
        lock (room.Gate)
            peers = room.Members.Values.Where(member => member.PlayerId != excludePlayerId && member.Connection is not null)
                .Select(member => member.Connection!).ToArray();

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Options);
        await Task.WhenAll(peers.Select(peer => peer.SendAsync(payload, CancellationToken.None)));
    }

    public SessionSnapshot? GetSnapshot(string code)
    {
        if (!_rooms.TryGetValue(code, out var room))
            return null;
        lock (room.Gate)
            return new SessionSnapshot(room.Code, room.Members.Count,
                room.Members.Values.Select(member => new SnapshotPlayer(member.PlayerId, member.DisplayName, member.State, member.Connection is not null)).ToArray());
    }

    private static PlayerCredentials NewPlayer(string code, string displayName) =>
        new(code, Convert.ToHexString(RandomNumberGenerator.GetBytes(8)), displayName,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    private static string CreateCode()
    {
        Span<char> chars = stackalloc char[6];
        for (var index = 0; index < chars.Length; index++)
            chars[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    private sealed class Room(string code)
    {
        public string Code { get; } = code;
        public object Gate { get; } = new();
        public ConcurrentDictionary<string, Member> Members { get; } = new(StringComparer.Ordinal);
    }

    private sealed class Member(PlayerCredentials player)
    {
        public string PlayerId { get; } = player.PlayerId;
        public string DisplayName { get; } = player.DisplayName;
        public string AccessToken { get; } = player.AccessToken;
        public Peer? Connection { get; set; }
        public ClientMessage? State { get; set; }
    }
}
