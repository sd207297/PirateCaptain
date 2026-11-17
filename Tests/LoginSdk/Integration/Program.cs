using Nakama;
using PirateCaptain.Login;

var key = Environment.GetEnvironmentVariable("NAKAMA_SERVER_KEY");
if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("NAKAMA_SERVER_KEY is required.");
var endpoint = new Uri(Environment.GetEnvironmentVariable("NAKAMA_TEST_URL") ?? "http://127.0.0.1:17350");
var options = new LoginOptions { Scheme = endpoint.Scheme, Host = endpoint.Host, Port = endpoint.Port,
    ServerKey = key, TimeoutSeconds = 10, MaxReconnectAttempts = 2 };
using var http = new HttpClient();
var native = new Client(options.Scheme, options.Host, options.Port, key, new HttpRequestAdapter(http), false);
native.Timeout = 10;
var transport = new NakamaLoginTransport(native, () => Nakama.Socket.From(native));
var store = new MemoryCredentialStore();
var sdk = new PirateLoginClient(options, transport, store);
var email = "sdk-" + Guid.NewGuid().ToString("N") + "@example.com";
var guestEmail = "sdk-guest-" + Guid.NewGuid().ToString("N") + "@example.com";
var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
var cleanup = new List<Func<Task<ISession>>>();
var results = new List<object>();
string guestId = null;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    results.Add(new { name, passed = true });
    Console.WriteLine("PASS " + name);
}
async Task ExpectError(Func<Task> action, LoginError expected, string name)
{
    try { await action(); }
    catch (LoginException error) { Check(error.Code == expected, name); return; }
    throw new InvalidOperationException(name);
}
try
{
    await ExpectError(() => sdk.LoginEmailAsync(email, password), LoginError.NotFound, "Unknown login does not create an account");
    var created = await sdk.RegisterEmailAsync(email, password);
    cleanup.Add(() => native.AuthenticateEmailAsync(email, password, create: false));
    Check(created.Created && !string.IsNullOrEmpty(created.RefreshToken), "Registration returns both tokens and created=true");
    var userId = created.UserId;
    Check((await sdk.GetAccountAsync()).User.Id == userId, "Authenticated account matches registration");
    Check(!(await sdk.RegisterEmailAsync(email, password)).Created, "Repeated registration uses the same account");
    await ExpectError(() => sdk.LoginEmailAsync(email, "incorrect-test-password"), LoginError.InvalidCredentials, "Wrong password is rejected");
    Check((await sdk.LoginEmailAsync(email, password)).UserId == userId, "Email login retains identity");
    await sdk.ConnectAsync();
    Check(sdk.IsConnected && sdk.State == LoginState.Connected, "Authenticated WebSocket connects");
    await sdk.Socket.UpdateStatusAsync("SDK integration check");
    Check(true, "WebSocket request receives server acknowledgement");
    await Task.Delay(TimeSpan.FromSeconds(17));
    await sdk.Socket.UpdateStatusAsync("SDK heartbeat check");
    Check(sdk.IsConnected, "Connection survives the server heartbeat interval");
    Check((await sdk.RefreshSessionAsync()).UserId == userId, "Refresh preserves identity");
    await sdk.ReconnectAsync();
    Check(sdk.IsConnected, "Explicit reconnect succeeds");
    await native.SessionLogoutAsync(sdk.Session);
    await sdk.DisconnectAsync();
    await ExpectError(() => sdk.ConnectAsync(), LoginError.InvalidCredentials, "Revoked session cannot reconnect");
    Check(sdk.Session == null && store.LoadSession() == null, "Revoked session clears local credentials");
    await sdk.LoginEmailAsync(email, password);
    var current = sdk.Session;
    await sdk.ShutdownAsync();
    sdk = new PirateLoginClient(options, transport, store);
    Check(await sdk.RestoreSessionAsync(), "Saved session restores after server verification");
    await sdk.ConnectAsync();
    current = sdk.Session;
    await sdk.LogoutAsync();
    Check(!sdk.IsConnected && sdk.Session == null && store.LoadSession() == null, "Logout closes socket and clears local credentials");
    try { await native.GetAccountAsync(current); throw new InvalidOperationException("Revoked token was accepted"); }
    catch (ApiResponseException error) { Check(error.StatusCode == 401, "Server rejects the logged-out access token"); }
    await sdk.LoginGuestAsync();
    guestId = store.GetOrCreateDeviceId();
    cleanup.Add(() => native.AuthenticateDeviceAsync(guestId, create: false));
    var guestUserId = sdk.Session.UserId;
    Check((await sdk.LoginGuestAsync(false)).UserId == guestUserId, "Guest re-login retains identity");
    await sdk.BindEmailAsync(guestEmail, password);
    Check((await sdk.GetAccountAsync()).User.Id == guestUserId, "Binding email preserves guest identity");
    await sdk.LogoutAsync();
    Check((await sdk.LoginAndConnectAsync(guestEmail, password)).UserId == guestUserId && sdk.IsConnected,
        "Bound email login connects to the same account");
}
catch (Exception error)
{
    // Never print upstream exception bodies or session objects.
    results.Add(new { name = "Integration", passed = false, error = LoginException.From(error).Code.ToString() });
    Console.WriteLine("FAIL Integration: " + LoginException.From(error).Code);
    Environment.ExitCode = 1;
}
finally
{
    await sdk.ShutdownAsync();
    foreach (var authenticate in cleanup)
    {
        try
        {
            var temporary = await authenticate();
            await native.DeleteAccountAsync(temporary);
            Check(true, "Temporary SDK test account deleted");
        }
        catch
        {
            results.Add(new { name = "Temporary account cleanup", passed = false });
            Console.WriteLine("FAIL Temporary account cleanup");
            Environment.ExitCode = 1;
        }
    }
    await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "integration-results.json"),
        System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
}
