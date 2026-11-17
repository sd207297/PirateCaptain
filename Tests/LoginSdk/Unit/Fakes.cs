using System.Text;
using System.Text.Json;
using Nakama;
using PirateCaptain.Login;

namespace PirateCaptain.Login.Tests;

internal static class Sessions
{
    public static ISession Create(string id = "captain-1", int expiresInSeconds = 3600,
        int refreshExpiresInSeconds = 86400)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Session.Restore(Token(id, now + expiresInSeconds), Token(id, now + refreshExpiresInSeconds));
    }

    private static string Token(string id, long expiry)
    {
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = JsonSerializer.Serialize(new
        {
            uid = id,
            usn = "captain",
            exp = expiry,
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            tid = Guid.NewGuid().ToString("N"),
            vrs = new Dictionary<string, string>()
        });
        return Encode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}") + "." + Encode(payload) + ".test-signature";
    }
}

internal sealed class FakeTransport : ILoginTransport
{
    public IClient Client => null;
    public ISession AuthResult = Sessions.Create();
    public ISession RefreshResult;
    public Exception AuthFailure;
    public Exception RefreshFailure;
    public Exception AccountFailure;
    public Exception LogoutFailure;
    public Exception LinkFailure;
    public TaskCompletionSource<bool> AuthCompletion;
    public int EmailCalls;
    public int DeviceCalls;
    public int RefreshCalls;
    public int AccountCalls;
    public int LogoutCalls;
    public int LinkCalls;
    public int SocketCalls;
    public bool LastCreate;
    public string LastDeviceId;
    public ISession LastLinkedSession;
    public ISession LastLogoutSession;
    public Func<FakeSocket> SocketFactory = () => new FakeSocket();
    public readonly List<FakeSocket> Sockets = new();
    public readonly List<string> Calls = new();

    public async Task<ISession> AuthenticateEmailAsync(string email, string password, string username, bool create,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EmailCalls++;
        LastCreate = create;
        Calls.Add("email:" + create);
        if (AuthCompletion != null) await AuthCompletion.Task;
        if (AuthFailure != null) throw AuthFailure;
        return AuthResult;
    }

    public Task<ISession> AuthenticateDeviceAsync(string deviceId, string username, bool create, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        DeviceCalls++;
        LastCreate = create;
        LastDeviceId = deviceId;
        Calls.Add("device:" + create);
        return AuthFailure == null ? Task.FromResult(AuthResult) : Task.FromException<ISession>(AuthFailure);
    }

    public Task<ISession> RefreshAsync(ISession session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        RefreshCalls++;
        Calls.Add("refresh");
        return RefreshFailure == null
            ? Task.FromResult(RefreshResult ?? Sessions.Create(session.UserId))
            : Task.FromException<ISession>(RefreshFailure);
    }

    public Task<IApiAccount> GetAccountAsync(ISession session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AccountCalls++;
        Calls.Add("account");
        return AccountFailure == null
            ? Task.FromResult<IApiAccount>(new FakeAccount(session.UserId))
            : Task.FromException<IApiAccount>(AccountFailure);
    }

    public Task LinkEmailAsync(ISession session, string email, string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LinkCalls++;
        LastLinkedSession = session;
        Calls.Add("link");
        return LinkFailure == null ? Task.CompletedTask : Task.FromException(LinkFailure);
    }

    public Task LogoutAsync(ISession session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LogoutCalls++;
        LastLogoutSession = session;
        Calls.Add("logout");
        return LogoutFailure == null ? Task.CompletedTask : Task.FromException(LogoutFailure);
    }

    public ILoginSocket CreateSocket()
    {
        SocketCalls++;
        var socket = SocketFactory();
        Sockets.Add(socket);
        return socket;
    }
}

internal sealed class FakeSocket : ILoginSocket
{
    public bool IsConnected { get; private set; }
    public event Action<string> Closed;
    public event Action<Exception> Error;
    public ISocket NativeSocket => null;
    public int ConnectCalls;
    public int CloseCalls;
    public TaskCompletionSource<bool> ConnectCompletion;
    public Exception ConnectFailure;

    public async Task ConnectAsync(ISession session, bool appearOnline, int timeoutSeconds, string language)
    {
        ConnectCalls++;
        if (ConnectCompletion != null) await ConnectCompletion.Task;
        if (ConnectFailure != null) throw ConnectFailure;
        IsConnected = true;
    }

    public Task CloseAsync()
    {
        CloseCalls++;
        IsConnected = false;
        Closed?.Invoke("closed");
        return Task.CompletedTask;
    }

    public void Drop()
    {
        IsConnected = false;
        Closed?.Invoke("connection lost");
    }

    public void Fail(Exception error) => Error?.Invoke(error);
}

internal sealed class FakeStore : ICredentialStore
{
    public SavedSession Value;
    public readonly string DeviceId = Guid.NewGuid().ToString();
    public int Saves;
    public int Clears;
    public bool FailSave;
    public SavedSession LoadSession() => Value;
    public void SaveSession(SavedSession session)
    {
        if (FailSave) throw new IOException("storage unavailable");
        Value = session;
        Saves++;
    }
    public void ClearSession() { Value = null; Clears++; }
    public string GetOrCreateDeviceId() => DeviceId;
}

internal sealed class FakeAccount : IApiAccount
{
    public FakeAccount(string id) => User = new FakeUser(id);
    public string CustomId => null;
    public IEnumerable<IApiAccountDevice> Devices => Array.Empty<IApiAccountDevice>();
    public string DisableTime => null;
    public string Email => "captain@example.com";
    public IEnumerable<IApiAccountProviderIdentity> Providers => Array.Empty<IApiAccountProviderIdentity>();
    public IApiUser User { get; }
    public string VerifyTime => null;
    public string Wallet => "{}";
}

internal sealed class FakeUser : IApiUser
{
    public FakeUser(string id) => Id = id;
    public string Id { get; }
    public string AppleId => null;
    public string AvatarUrl => null;
    public string CreateTime => null;
    public string DisplayName => "Captain";
    public int EdgeCount => 0;
    public string FacebookId => null;
    public string FacebookInstantGameId => null;
    public string GamecenterId => null;
    public string GoogleId => null;
    public string LangTag => "zh";
    public string Location => null;
    public string Metadata => "{}";
    public bool Online => false;
    public string SteamId => null;
    public string Timezone => null;
    public string UpdateTime => null;
    public string Username => "captain";
}

internal sealed class RecordingHttpAdapter : IHttpAdapter
{
    public TransientExceptionDelegate IsTransient = _ => false;
    public TransientExceptionDelegate TransientExceptionDelegate => IsTransient;
    public ILogger Logger { get; set; }
    public readonly List<HttpCall> Calls = new();
    public Func<HttpCall, string> Respond;
    public Exception Failure;

    public Task<string> SendAsync(string method, Uri uri, IDictionary<string, string> headers, byte[] body,
        int timeoutSeconds, CancellationToken? cancellationToken)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        var call = new HttpCall(method, uri, new Dictionary<string, string>(headers),
            body == null ? "" : Encoding.UTF8.GetString(body));
        Calls.Add(call);
        if (Failure != null) return Task.FromException<string>(Failure);
        return Task.FromResult(Respond?.Invoke(call) ?? "{}");
    }
}

internal sealed record HttpCall(string Method, Uri Uri, Dictionary<string, string> Headers, string Body);
