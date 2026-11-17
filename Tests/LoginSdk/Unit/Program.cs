using Nakama;
using PirateCaptain.Login;
using PirateCaptain.Login.Tests;
using System.Text;
using System.Text.Json;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Login explicitly disables account creation", LoginDoesNotRegister),
    ("Registration explicitly enables creation", RegistrationAllowsCreation),
    ("Unknown-account login never falls back to registration", UnknownAccountStaysUnknown),
    ("Guest identity remains stable across sessions", GuestIdentityIsStable),
    ("Binding preserves the authenticated account", BindingPreservesIdentity),
    ("Restore validates saved credentials with the server", RestoreValidatesCredentials),
    ("Restore clears rejected credentials", RestoreRejectsInvalidCredentials),
    ("Restore preserves credentials on a temporary network failure", RestorePreservesCredentialsOnNetworkFailure),
    ("Expired access token refreshes before authenticated calls", ExpiredAccessRefreshes),
    ("Concurrent authenticated calls share a single refresh", ConcurrentCallsShareRefresh),
    ("Connecting twice creates only one live connection", ConnectionIsIdempotent),
    ("Logout closes socket and clears local credentials", LogoutClearsAllLocalState),
    ("Failed server logout still clears local state", LogoutFailureClearsLocalState),
    ("Cancelled connect cannot revive a late socket", CancelledConnectCannotRevive),
    ("Timed-out connect cannot revive a late socket", TimedOutConnectCannotRevive),
    ("Reconnect stops at its configured attempt limit", ReconnectIsBounded),
    ("Shutdown closes transport without revoking the session", ShutdownDoesNotLogout),
    ("Pre-cancelled operations never contact the transport", PreCancelledDoesNotSend),
    ("Credential validation counts UTF-8 password bytes", PasswordValidationCountsBytes),
    ("Pre-cancelled logout still clears this device", CancelledLogoutClearsLocalState),
    ("A throwing observer cannot corrupt login state", ObserverFailuresAreIsolated),
    ("Unexpected socket closure triggers bounded reconnection", SocketClosureReconnects),
    ("Authentication arriving after shutdown cannot restore state", LateAuthenticationCannotRevive),
    ("Refresh refuses a different account identity", RefreshRejectsIdentitySwitch),
    ("Binding conflict keeps the authenticated account", BindingConflictKeepsSession),
    ("Official adapter emits the existing server's exact HTTP contract", OfficialTransportContract),
    ("Restore rejects a refresh token for a different account", RestoreRejectsIdentitySwitch),
    ("Closing a socket updates state even if the replacement session cannot be saved", ClosedSocketStateSurvivesStorageFailure),
    ("Cancelling reconnect during backoff restores authenticated state", CancelReconnectDuringBackoff),
    ("Socket factory failures never leave a connecting state", SocketFactoryFailureRestoresState),
    ("Official adapter reports transient failures as network errors without retrying", OfficialTransportPreservesTransientFailure)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine("PASS " + test.Name);
    }
    catch (Exception error)
    {
        failures.Add(test.Name);
        Console.WriteLine("FAIL " + test.Name + ": " + error.GetType().Name + " - " + error.Message);
    }
}
Console.WriteLine($"\n{tests.Length - failures.Count}/{tests.Length} passed");
return failures.Count == 0 ? 0 : 1;

static LoginOptions Options() => new()
{
    ServerKey = "test-client-key",
    TimeoutSeconds = 1,
    MaxReconnectAttempts = 3,
    ReconnectBaseDelayMilliseconds = 1
};

static PirateLoginClient Client(FakeTransport transport, FakeStore store = null) =>
    new(Options(), transport, store ?? new FakeStore());

static async Task Login(PirateLoginClient client) =>
    await client.LoginEmailAsync("captain@example.com", "test-password");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task<Exception> Fails(Func<Task> action)
{
    try { await action(); }
    catch (Exception error) { return error; }
    throw new InvalidOperationException("Expected operation to fail.");
}

static async Task LoginDoesNotRegister()
{
    var transport = new FakeTransport();
    var client = Client(transport);
    var session = await client.LoginEmailAsync("captain@example.com", "test-password");
    Check(!transport.LastCreate && transport.EmailCalls == 1, "Login must use create=false exactly once.");
    Check(session.UserId == "captain-1" && client.State == LoginState.Authenticated, "Login must retain its session.");
    await client.ShutdownAsync();
}

static async Task RegistrationAllowsCreation()
{
    var transport = new FakeTransport();
    var client = Client(transport);
    await client.RegisterEmailAsync("captain@example.com", "test-password");
    Check(transport.LastCreate && transport.EmailCalls == 1, "Register must use create=true.");
    await client.ShutdownAsync();
}

static async Task UnknownAccountStaysUnknown()
{
    var transport = new FakeTransport { AuthFailure = new ApiResponseException(404, "unknown", 5) };
    var client = Client(transport);
    var error = await Fails(() => Login(client));
    Check(error is LoginException loginError && loginError.Code == LoginError.NotFound, "404 should map to NotFound.");
    Check(transport.EmailCalls == 1 && !transport.LastCreate && client.Session == null,
        "Unknown-account login must not silently create an account.");
    await client.ShutdownAsync();
}

static async Task GuestIdentityIsStable()
{
    var store = new FakeStore();
    var transport = new FakeTransport();
    var first = Client(transport, store);
    await first.LoginGuestAsync();
    var deviceId = transport.LastDeviceId;
    Check(transport.LastCreate && Guid.TryParse(deviceId, out _), "Guest creation requires a random UUID.");
    await first.LogoutAsync();
    var second = Client(transport, store);
    await second.LoginGuestAsync(false);
    Check(transport.LastDeviceId == deviceId && !transport.LastCreate, "Returning guest must reuse its saved identifier.");
    await first.ShutdownAsync();
    await second.ShutdownAsync();
}

static async Task BindingPreservesIdentity()
{
    var transport = new FakeTransport();
    var client = Client(transport);
    await client.LoginGuestAsync();
    var userId = client.Session.UserId;
    await client.BindEmailAsync("captain@example.com", "test-password");
    Check(transport.LinkCalls == 1 && transport.LastLinkedSession.UserId == userId,
        "Binding must use the current account's session.");
    Check(transport.EmailCalls == 0 && client.Session.UserId == userId, "Binding must not authenticate a second account.");
    await client.ShutdownAsync();
}

static async Task RestoreValidatesCredentials()
{
    var session = Sessions.Create();
    var store = new FakeStore { Value = new SavedSession(session.AuthToken, session.RefreshToken) };
    var transport = new FakeTransport();
    var client = Client(transport, store);
    Check(await client.RestoreSessionAsync(), "A valid saved session should restore.");
    Check(transport.AccountCalls == 1, "Restore must check the account with the server; JWT parsing is insufficient.");
    Check(client.Session.UserId == session.UserId, "Restore changed user identity.");
    await client.ShutdownAsync();
}

static async Task RestoreRejectsInvalidCredentials()
{
    var session = Sessions.Create();
    var store = new FakeStore { Value = new SavedSession(session.AuthToken, session.RefreshToken) };
    var transport = new FakeTransport
    {
        AccountFailure = new ApiResponseException(401, "revoked", 16),
        RefreshFailure = new ApiResponseException(401, "revoked", 16)
    };
    var client = Client(transport, store);
    try { Check(!await client.RestoreSessionAsync(), "Rejected saved credentials must not restore."); }
    catch (LoginException error) { Check(error.Code is LoginError.InvalidCredentials or LoginError.SessionExpired, "Unexpected error for revoked session."); }
    Check(store.Value == null && client.Session == null && client.State == LoginState.SignedOut,
        "Rejected saved tokens must be removed.");
    await client.ShutdownAsync();
}

static async Task RestorePreservesCredentialsOnNetworkFailure()
{
    var session = Sessions.Create();
    var store = new FakeStore { Value = new SavedSession(session.AuthToken, session.RefreshToken) };
    var transport = new FakeTransport { AccountFailure = new System.Net.Http.HttpRequestException("offline") };
    var client = Client(transport, store);
    await Fails(() => client.RestoreSessionAsync());
    Check(store.Value != null, "A transient connection failure must not erase refresh credentials.");
    await client.ShutdownAsync();
}

static async Task ExpiredAccessRefreshes()
{
    var transport = new FakeTransport { AuthResult = Sessions.Create(expiresInSeconds: -60) };
    var store = new FakeStore();
    var client = Client(transport, store);
    await Login(client);
    var account = await client.GetAccountAsync();
    Check(transport.RefreshCalls == 1 && !client.Session.IsExpired, "Expired access token was not refreshed.");
    Check(account.User.Id == "captain-1" && store.Value.AuthToken == client.Session.AuthToken,
        "Refresh must preserve identity and persist the replacement token.");
    await client.ShutdownAsync();
}

static async Task ConcurrentCallsShareRefresh()
{
    var transport = new FakeTransport { AuthResult = Sessions.Create(expiresInSeconds: 10) };
    var client = Client(transport);
    await Login(client);
    await Task.WhenAll(client.GetAccountAsync(), client.GetAccountAsync(), client.GetAccountAsync());
    Check(transport.RefreshCalls == 1, "Concurrent calls triggered duplicate refreshes.");
    await client.ShutdownAsync();
}

static async Task ConnectionIsIdempotent()
{
    var transport = new FakeTransport();
    var client = Client(transport);
    await Login(client);
    await Task.WhenAll(client.ConnectAsync(), client.ConnectAsync());
    Check(transport.SocketCalls == 1 && client.IsConnected && client.State == LoginState.Connected,
        "Connect must reuse an existing connection.");
    await client.ShutdownAsync();
}

static async Task LogoutClearsAllLocalState()
{
    var transport = new FakeTransport();
    var store = new FakeStore();
    var client = Client(transport, store);
    await client.LoginAndConnectAsync("captain@example.com", "test-password");
    await client.LogoutAsync();
    Check(transport.LogoutCalls == 1 && !string.IsNullOrEmpty(transport.LastLogoutSession.AuthToken)
        && !string.IsNullOrEmpty(transport.LastLogoutSession.RefreshToken), "Logout must revoke the specific access and refresh pair.");
    Check(!client.IsConnected && client.Session == null && client.State == LoginState.SignedOut && store.Value == null,
        "Logout retained local session state.");
    Check(transport.Sockets[0].CloseCalls > 0 && !transport.Sockets[0].IsConnected, "Logout must close its realtime socket.");
    await client.ShutdownAsync();
}

static async Task LogoutFailureClearsLocalState()
{
    var transport = new FakeTransport { LogoutFailure = new System.Net.Http.HttpRequestException("offline") };
    var store = new FakeStore();
    var client = Client(transport, store);
    await client.LoginAndConnectAsync("captain@example.com", "test-password");
    await Fails(() => client.LogoutAsync());
    Check(client.Session == null && store.Value == null && !client.IsConnected && client.State == LoginState.SignedOut,
        "A server logout failure must still clear this device.");
    Check(transport.Sockets[0].CloseCalls > 0, "Failed logout left realtime connection open.");
    await client.ShutdownAsync();
}

static async Task CancelledConnectCannotRevive()
{
    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var transport = new FakeTransport { SocketFactory = () => new FakeSocket { ConnectCompletion = completion } };
    var client = Client(transport);
    await Login(client);
    using var cancellation = new CancellationTokenSource();
    var connect = client.ConnectAsync(cancellation.Token);
    await Task.Delay(20);
    cancellation.Cancel();
    var error = await Fails(() => connect);
    Check(error is OperationCanceledException, "Caller cancellation should remain distinguishable.");
    await client.LogoutAsync();
    completion.TrySetResult(true);
    await Task.Delay(75);
    Check(client.State == LoginState.SignedOut && !client.IsConnected && !transport.Sockets[0].IsConnected,
        "An obsolete connect resurrected after logout.");
    await client.ShutdownAsync();
}

static async Task TimedOutConnectCannotRevive()
{
    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var transport = new FakeTransport { SocketFactory = () => new FakeSocket { ConnectCompletion = completion } };
    var client = Client(transport);
    await Login(client);
    var error = await Fails(() => client.ConnectAsync());
    Check(error is LoginException loginError && loginError.Code == LoginError.Timeout, "Connection timeout must be reported.");
    completion.TrySetResult(true);
    await Task.Delay(75);
    Check(!client.IsConnected && !transport.Sockets[0].IsConnected, "Late timed-out connection must be closed.");
    await client.ShutdownAsync();
}

static async Task ReconnectIsBounded()
{
    var transport = new FakeTransport
    {
        SocketFactory = () => new FakeSocket { ConnectFailure = new System.Net.Http.HttpRequestException("offline") }
    };
    var client = Client(transport);
    await Login(client);
    await Fails(() => client.ReconnectAsync());
    Check(transport.SocketCalls == 3 && !client.IsConnected, "Reconnect must respect its exact configured attempt limit.");
    await client.ShutdownAsync();
}

static async Task ShutdownDoesNotLogout()
{
    var transport = new FakeTransport();
    var store = new FakeStore();
    var client = Client(transport, store);
    await client.LoginAndConnectAsync("captain@example.com", "test-password");
    await client.ShutdownAsync();
    Check(transport.LogoutCalls == 0 && store.Value != null, "Shutdown must preserve credentials for the next launch.");
    Check(!client.IsConnected && !transport.Sockets[0].IsConnected, "Shutdown left a socket open.");
}

static async Task PreCancelledDoesNotSend()
{
    var transport = new FakeTransport();
    var client = Client(transport);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var error = await Fails(() => client.LoginEmailAsync("captain@example.com", "test-password", null, cancellation.Token));
    Check(error is OperationCanceledException && transport.EmailCalls == 0, "Pre-cancelled request reached the server.");
    await client.ShutdownAsync();
}

static async Task PasswordValidationCountsBytes()
{
    var transport = new FakeTransport();
    var client = Client(transport);
    var tooLong = new string('\u8239', 25); // 75 UTF-8 bytes, beyond bcrypt's 72-byte limit.
    var error = await Fails(() => client.RegisterEmailAsync("captain@example.com", tooLong));
    Check(error is LoginException loginError && loginError.Code == LoginError.InvalidInput && transport.EmailCalls == 0,
        "Validation must reject passwords over 72 UTF-8 bytes before sending.");
    await client.ShutdownAsync();
}

static async Task CancelledLogoutClearsLocalState()
{
    var transport = new FakeTransport();
    var store = new FakeStore();
    var client = Client(transport, store);
    await client.LoginAndConnectAsync("captain@example.com", "test-password");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var error = await Fails(() => client.LogoutAsync(cancellation.Token));
    Check(error is OperationCanceledException && transport.LogoutCalls == 0, "Cancellation must avoid the remote logout call.");
    Check(client.Session == null && store.Value == null && !client.IsConnected && client.State == LoginState.SignedOut,
        "Pre-cancelled logout must still clear this device.");
    await client.ShutdownAsync();
}

static async Task ObserverFailuresAreIsolated()
{
    var transport = new FakeTransport();
    var client = Client(transport);
    var observedStates = 0;
    var observedErrors = 0;
    client.StateChanged += _ => throw new InvalidOperationException("broken state observer");
    client.StateChanged += _ => observedStates++;
    client.Error += _ => throw new InvalidOperationException("broken error observer");
    client.Error += _ => observedErrors++;
    await Login(client);
    Check(client.Session != null && client.State == LoginState.Authenticated && observedStates > 0,
        "State observers must not invalidate a completed login or block each other.");
    transport.AccountFailure = new System.Net.Http.HttpRequestException("offline");
    var error = await Fails(() => client.GetAccountAsync());
    Check(error is LoginException loginError && loginError.Code == LoginError.Network && observedErrors == 1,
        "Error observers must not replace the original operation error or block other observers.");
    await client.ShutdownAsync();
}

static async Task SocketClosureReconnects()
{
    var transport = new FakeTransport();
    var client = Client(transport);
    await client.LoginAndConnectAsync("captain@example.com", "test-password");
    transport.Sockets[0].Drop();
    for (var i = 0; i < 100 && (transport.SocketCalls < 2 || !client.IsConnected); i++) await Task.Delay(10);
    Check(transport.SocketCalls == 2 && client.IsConnected && client.State == LoginState.Connected,
        "Unexpected closure must reconnect when policy allows it.");
    await client.DisconnectAsync();
    await Task.Delay(30);
    Check(transport.SocketCalls == 2 && !client.IsConnected, "An intentional disconnect must not reconnect.");
    await client.ShutdownAsync();
}

static async Task LateAuthenticationCannotRevive()
{
    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var transport = new FakeTransport { AuthCompletion = completion };
    var store = new FakeStore();
    var client = Client(transport, store);
    var login = Login(client);
    await client.ShutdownAsync();
    var error = await Fails(() => login);
    Check(error is OperationCanceledException, "Shutdown should cancel outstanding authentication.");
    completion.TrySetResult(true);
    await Task.Delay(40);
    Check(client.Session == null && store.Value == null && client.State == LoginState.SignedOut,
        "A late authentication response restored a shut-down service.");
}

static async Task RefreshRejectsIdentitySwitch()
{
    var transport = new FakeTransport { RefreshResult = Sessions.Create("different-account") };
    var store = new FakeStore();
    var client = Client(transport, store);
    await Login(client);
    var error = await Fails(() => client.RefreshSessionAsync());
    Check(error is LoginException loginError && loginError.Code == LoginError.SessionExpired,
        "A refresh must not switch accounts.");
    Check(client.Session == null && store.Value == null, "Account-mismatched refresh must clear local state.");
    await client.ShutdownAsync();
}

static async Task BindingConflictKeepsSession()
{
    var transport = new FakeTransport { LinkFailure = new ApiResponseException(409, "occupied", 6) };
    var client = Client(transport);
    await Login(client);
    var error = await Fails(() => client.BindEmailAsync("captain@example.com", "test-password"));
    Check(error is LoginException loginError && loginError.Code == LoginError.Conflict,
        "An occupied binding must map to Conflict.");
    Check(client.Session?.UserId == "captain-1" && client.State == LoginState.Authenticated,
        "A binding conflict must not erase an otherwise valid account.");
    await client.ShutdownAsync();
}

static async Task OfficialTransportContract()
{
    var session = Sessions.Create();
    var http = new RecordingHttpAdapter();
    http.Respond = call => call.Uri.AbsolutePath.Contains("authenticate") || call.Uri.AbsolutePath.EndsWith("refresh")
        ? JsonSerializer.Serialize(new { token = session.AuthToken, refresh_token = session.RefreshToken, created = false })
        : call.Uri.AbsolutePath == "/v2/account"
            ? JsonSerializer.Serialize(new { user = new { id = session.UserId, username = "captain" } })
            : "{}";
    var native = new Nakama.Client("http", "127.0.0.1", 17350, "test-client-key", http, autoRefreshSession: false);
    var transport = new NakamaLoginTransport(native, () => null);
    await transport.AuthenticateEmailAsync("captain@example.com", "test-password", null, false, CancellationToken.None);
    await transport.AuthenticateEmailAsync("captain@example.com", "test-password", null, true, CancellationToken.None);
    await transport.AuthenticateDeviceAsync(Guid.NewGuid().ToString(), null, false, CancellationToken.None);
    await transport.RefreshAsync(session, CancellationToken.None);
    await transport.GetAccountAsync(session, CancellationToken.None);
    await transport.LinkEmailAsync(session, "captain@example.com", "test-password", CancellationToken.None);
    await transport.LogoutAsync(session, CancellationToken.None);
    Check(http.Calls.Count == 7, "Adapter emitted duplicate/retried requests.");
    var expected = new[]
    {
        ("POST", "/v2/account/authenticate/email"),
        ("POST", "/v2/account/authenticate/email"),
        ("POST", "/v2/account/authenticate/device"),
        ("POST", "/v2/account/session/refresh"),
        ("GET", "/v2/account"),
        ("POST", "/v2/account/link/email"),
        ("POST", "/v2/session/logout")
    };
    var basic = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-client-key:"));
    for (var i = 0; i < expected.Length; i++)
    {
        var call = http.Calls[i];
        Check(call.Method == expected[i].Item1 && call.Uri.AbsolutePath == expected[i].Item2,
            "Unexpected HTTP path or method at request " + i);
        Check(call.Headers["Authorization"] == (i <= 3 ? basic : "Bearer " + session.AuthToken),
            "Wrong authorization scheme at request " + i);
    }
    Check(http.Calls[0].Uri.Query.Contains("create=false") && http.Calls[1].Uri.Query.Contains("create=true")
        && http.Calls[2].Uri.Query.Contains("create=false"), "Creation flags must always be explicit.");
    using var refresh = JsonDocument.Parse(http.Calls[3].Body);
    Check(refresh.RootElement.GetProperty("token").GetString() == session.RefreshToken, "Refresh body contains wrong token.");
    using var logout = JsonDocument.Parse(http.Calls[6].Body);
    Check(logout.RootElement.GetProperty("token").GetString() == session.AuthToken
        && logout.RootElement.GetProperty("refresh_token").GetString() == session.RefreshToken,
        "Logout must target both current session tokens.");
}

static async Task RestoreRejectsIdentitySwitch()
{
    var expired = Sessions.Create("captain-1", expiresInSeconds: -60);
    var store = new FakeStore { Value = new SavedSession(expired.AuthToken, expired.RefreshToken) };
    var transport = new FakeTransport { RefreshResult = Sessions.Create("different-account") };
    var client = Client(transport, store);
    Check(!await client.RestoreSessionAsync(), "Restore accepted a replacement session for a different account.");
    Check(transport.RefreshCalls == 1 && transport.AccountCalls == 0,
        "Identity mismatch must be rejected before accepting the replacement account.");
    Check(store.Value == null && client.Session == null && client.State == LoginState.SignedOut,
        "Restore mismatch must clear credentials and remain signed out.");
    await client.ShutdownAsync();
}

static async Task ClosedSocketStateSurvivesStorageFailure()
{
    var transport = new FakeTransport();
    var store = new FakeStore();
    var client = Client(transport, store);
    await client.LoginAndConnectAsync("captain@example.com", "test-password");
    store.FailSave = true;
    transport.AuthResult = Sessions.Create("second-captain");
    var error = await Fails(() => client.LoginEmailAsync("second@example.com", "test-password"));
    Check(error is LoginException loginError && loginError.Code == LoginError.Storage,
        "Replacement session save failure must be reported.");
    Check(!client.IsConnected && !transport.Sockets[0].IsConnected && client.State == LoginState.Authenticated,
        "The old socket was closed, so State must not remain Connected even when saving the new session fails.");
    Check(client.Session?.UserId == "captain-1", "Failed save must not partially install the replacement account.");
    await client.ShutdownAsync();
}

static async Task CancelReconnectDuringBackoff()
{
    var transport = new FakeTransport
    {
        SocketFactory = () => new FakeSocket { ConnectFailure = new System.Net.Http.HttpRequestException("offline") }
    };
    var options = Options();
    options.ReconnectBaseDelayMilliseconds = 1000;
    var client = new PirateLoginClient(options, transport, new FakeStore());
    await Login(client);
    using var cancellation = new CancellationTokenSource();
    var reconnectStates = 0;
    client.StateChanged += state =>
    {
        if (state == LoginState.Reconnecting && ++reconnectStates == 2) cancellation.Cancel();
    };
    var error = await Fails(() => client.ReconnectAsync(cancellation.Token));
    Check(error is OperationCanceledException && transport.SocketCalls == 1,
        "Cancelling during backoff must prevent the second connection attempt.");
    Check(!client.IsConnected && client.State == LoginState.Authenticated,
        "Cancellation during reconnect backoff must not leave State at Reconnecting.");
    await client.ShutdownAsync();
}

static async Task SocketFactoryFailureRestoresState()
{
    var transport = new FakeTransport { SocketFactory = () => throw new InvalidOperationException("factory failed") };
    var client = Client(transport);
    await Login(client);
    await Fails(() => client.ConnectAsync());
    Check(!client.IsConnected && client.State == LoginState.Authenticated,
        "Socket factory failure during Connect must not leave State at Connecting.");
    await Fails(() => client.ReconnectAsync());
    Check(!client.IsConnected && client.State == LoginState.Authenticated,
        "Socket factory failure during Reconnect must not leave State at Reconnecting.");
    await client.ShutdownAsync();
}

static async Task OfficialTransportPreservesTransientFailure()
{
    var http = new RecordingHttpAdapter
    {
        IsTransient = _ => true,
        Failure = new System.Net.Http.HttpRequestException("test offline failure")
    };
    var native = new Nakama.Client("http", "127.0.0.1", 17350, "test-client-key", http, autoRefreshSession: false);
    var transport = new NakamaLoginTransport(native, () => null);
    var error = await Fails(() => transport.AuthenticateEmailAsync("captain@example.com", "test-password", null,
        false, CancellationToken.None));
    Check(error is LoginException loginError && loginError.Code == LoginError.Network,
        "Nakama's exhausted-transient wrapper must not look like caller cancellation.");
    Check(http.Calls.Count == 1, "Transient authentication failure must not trigger an implicit retry.");
}
