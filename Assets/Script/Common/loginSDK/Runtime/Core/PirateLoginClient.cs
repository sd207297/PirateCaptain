using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nakama;

namespace PirateCaptain.Login
{
    public enum LoginState { SignedOut, Authenticated, Connecting, Connected, Reconnecting }

    /// <summary>Own one instance per server. In Unity create and call it on the main thread.</summary>
    public sealed class PirateLoginClient
    {
        private readonly LoginOptions _options;
        private readonly ILoginTransport _transport;
        private readonly ICredentialStore _store;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly SynchronizationContext _context;
        private ILoginSocket _socket;
        private Action<string> _closedHandler;
        private Action<Exception> _errorHandler;
        private CancellationTokenSource _connectionAttempt;
        private CancellationTokenSource _reconnect;
        private bool _wantConnection;
        private bool _shutdown;

        public ISession Session { get; private set; }
        public LoginState State { get; private set; } = LoginState.SignedOut;
        public bool IsConnected => _socket != null && _socket.IsConnected;
        public ISocket Socket => _socket?.NativeSocket;
        public IClient Client => _transport.Client;
        public event Action<LoginState> StateChanged;
        public event Action<LoginException> Error;

        public PirateLoginClient(LoginOptions options, ILoginTransport transport, ICredentialStore store = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.Validate();
            // Snapshot configuration: changes made by callers must not change a live session's environment.
            _options = new LoginOptions
            {
                Scheme = options.Scheme, Host = options.Host, Port = options.Port, ServerKey = options.ServerKey,
                TimeoutSeconds = options.TimeoutSeconds, RefreshBeforeExpirySeconds = options.RefreshBeforeExpirySeconds,
                MaxReconnectAttempts = options.MaxReconnectAttempts,
                ReconnectBaseDelayMilliseconds = options.ReconnectBaseDelayMilliseconds,
                AppearOnline = options.AppearOnline, Language = options.Language, AllowInsecureHttp = options.AllowInsecureHttp
            };
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _store = store ?? new MemoryCredentialStore();
            _context = SynchronizationContext.Current;
        }

        /// <summary>Nakama registration also signs in an existing account when credentials match. Inspect Created.</summary>
        public Task<ISession> RegisterEmailAsync(string email, string password, string username = null,
            CancellationToken cancellationToken = default) =>
            AuthenticateEmailAsync(email, password, username, true, cancellationToken);

        public Task<ISession> LoginEmailAsync(string email, string password, string username = null,
            CancellationToken cancellationToken = default) =>
            AuthenticateEmailAsync(email, password, username, false, cancellationToken);

        private Task<ISession> AuthenticateEmailAsync(string email, string password, string username, bool create,
            CancellationToken cancellationToken)
        {
            ValidateEmail(email, password);
            StopConnections();
            return RunAsync(async ct =>
            {
                var session = await RequestAsync(t => _transport.AuthenticateEmailAsync(email, password, username, create, t), ct);
                ct.ThrowIfCancellationRequested();
                await CloseSocketAsync();
                SaveSession(session);
                SetState(LoginState.Authenticated);
                return session;
            }, cancellationToken);
        }

        public Task<ISession> LoginGuestAsync(bool create = true, string username = null,
            CancellationToken cancellationToken = default)
        {
            StopConnections();
            return RunAsync(async ct =>
            {
                string deviceId;
                try { deviceId = _store.GetOrCreateDeviceId(); }
                catch { throw new LoginException(LoginError.Storage, "无法读取游客凭据。"); }
                if (string.IsNullOrWhiteSpace(deviceId))
                    throw new LoginException(LoginError.Storage, "游客凭据为空。");
                var session = await RequestAsync(t => _transport.AuthenticateDeviceAsync(deviceId, username, create, t), ct);
                ct.ThrowIfCancellationRequested();
                await CloseSocketAsync();
                SaveSession(session);
                SetState(LoginState.Authenticated);
                return session;
            }, cancellationToken);
        }

        /// <summary>Always checks saved credentials with the server. Offline errors preserve saved credentials.</summary>
        public Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default) => RunAsync(async ct =>
        {
            if (Session != null)
            {
                await EnsureSessionAsync(ct);
                await VerifyAccountAsync(ct);
                return true;
            }
            SavedSession saved;
            try { saved = _store.LoadSession(); }
            catch { throw new LoginException(LoginError.Storage, "无法读取已保存的会话。"); }
            if (saved == null) return false;
            ISession restored;
            try
            {
                restored = Nakama.Session.Restore(saved.AuthToken, saved.RefreshToken);
                if (restored == null || string.IsNullOrEmpty(restored.UserId)) throw new FormatException();
            }
            catch { ClearSavedSession(); return false; }
            try
            {
                if (restored.HasExpired(DateTime.UtcNow.AddSeconds(_options.RefreshBeforeExpirySeconds)))
                {
                    if (restored.IsRefreshExpired || string.IsNullOrEmpty(restored.RefreshToken))
                    { ClearSavedSession(); return false; }
                    var previousId = restored.UserId;
                    restored = await RequestAsync(t => _transport.RefreshAsync(restored, t), ct);
                    if (restored.UserId != previousId)
                        throw new LoginException(LoginError.SessionExpired, "会话账号不匹配，请重新登录。");
                }
                // A decoded JWT alone is never proof that an account/session is still valid.
                var account = await RequestAsync(t => _transport.GetAccountAsync(restored, t), ct);
                if (account?.User?.Id != restored.UserId)
                    throw new LoginException(LoginError.SessionExpired, "会话账号不匹配，请重新登录。");
                ct.ThrowIfCancellationRequested();
                SaveSession(restored);
                SetState(LoginState.Authenticated);
                return true;
            }
            catch (Exception exception) when (IsInvalidSession(exception))
            { await ClearSessionAsync(); return false; }
        }, cancellationToken);

        public Task<ISession> RefreshSessionAsync(CancellationToken cancellationToken = default) =>
            RunAsync(ct => RefreshCoreAsync(ct), cancellationToken);

        public Task<IApiAccount> GetAccountAsync(CancellationToken cancellationToken = default) => RunAsync(async ct =>
        {
            await EnsureSessionAsync(ct);
            return await VerifyAccountAsync(ct);
        }, cancellationToken);

        public Task BindEmailAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            ValidateEmail(email, password);
            return RunAsync(async ct =>
            {
                await EnsureSessionAsync(ct);
                try { await RequestAsync(t => _transport.LinkEmailAsync(Session, email, password, t), ct); }
                catch (Exception exception) when (IsInvalidSession(exception) && !IsNotFound(exception))
                { await ClearSessionAsync(); throw; }
            }, cancellationToken);
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => RunAsync(async ct =>
        {
            _wantConnection = true;
            await ConnectCoreAsync(ct, false);
        }, cancellationToken);

        public async Task<ISession> LoginAndConnectAsync(string email, string password,
            CancellationToken cancellationToken = default)
        {
            // The gate is kept for both steps so another login cannot change the identity in between.
            ValidateEmail(email, password);
            StopConnections();
            return await RunAsync(async ct =>
            {
                var session = await RequestAsync(t => _transport.AuthenticateEmailAsync(email, password, null, false, t), ct);
                ct.ThrowIfCancellationRequested();
                await CloseSocketAsync();
                SaveSession(session);
                SetState(LoginState.Authenticated);
                _wantConnection = true;
                await ConnectCoreAsync(ct, false);
                return session;
            }, cancellationToken);
        }

        /// <summary>At most MaxReconnectAttempts; no password retries. Successful reconnect does not rejoin rooms.</summary>
        public Task ReconnectAsync(CancellationToken cancellationToken = default)
        {
            StopConnections();
            _wantConnection = true;
            _reconnect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            return ReconnectLoopAsync(_reconnect);
        }

        private async Task ReconnectLoopAsync(CancellationTokenSource owner)
        {
            try
            {
                await RunAsync(async ct =>
                {
                    if (Session == null) throw new LoginException(LoginError.NotAuthenticated, "请先登录。");
                    await CloseSocketAsync();
                    Exception last = null;
                    for (var attempt = 0; attempt < _options.MaxReconnectAttempts; attempt++)
                    {
                        ct.ThrowIfCancellationRequested();
                        SetState(LoginState.Reconnecting);
                        if (attempt > 0)
                        {
                            var delay = Math.Min(10000, (long)_options.ReconnectBaseDelayMilliseconds << Math.Min(attempt - 1, 5));
                            await Task.Delay((int)delay, ct);
                        }
                        try { await ConnectCoreAsync(ct, true); return; }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception exception)
                        {
                            last = exception;
                            var code = LoginException.From(exception).Code;
                            if (code != LoginError.Network && code != LoginError.Timeout && code != LoginError.Server)
                                throw;
                        }
                    }
                    SetState(Session == null ? LoginState.SignedOut : LoginState.Authenticated);
                    throw last ?? new LoginException(LoginError.Network, "已禁用自动重连。");
                }, owner.Token);
            }
            finally
            {
                if (ReferenceEquals(_reconnect, owner))
                {
                    _reconnect = null;
                    if (!IsConnected) SetState(Session == null ? LoginState.SignedOut : LoginState.Authenticated);
                }
                owner.Dispose();
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            StopConnections();
            return RunAsync(async ct =>
            {
                await CloseSocketAsync();
                SetState(Session == null ? LoginState.SignedOut : LoginState.Authenticated);
            }, cancellationToken);
        }

        /// <summary>Always removes local credentials and closes the socket, even if remote revocation fails.</summary>
        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            StopConnections();
            // Do not cancel waiting for the gate: local sign-out is mandatory even with an already canceled token.
            return RunAsync(async ignored =>
            {
                try
                {
                    if (Session != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await EnsureSessionAsync(cancellationToken);
                        await RequestAsync(t => _transport.LogoutAsync(Session, t), cancellationToken);
                    }
                }
                finally { await ClearSessionAsync(); }
            }, CancellationToken.None);
        }

        /// <summary>Release networking while keeping saved credentials for the next application run.</summary>
        public async Task ShutdownAsync()
        {
            if (_shutdown) return;
            _shutdown = true;
            StopConnections();
            _lifetime.Cancel();
            await _gate.WaitAsync();
            try { await CloseSocketAsync(); Session = null; SetState(LoginState.SignedOut); }
            finally { _gate.Release(); }
        }

        private async Task EnsureSessionAsync(CancellationToken ct)
        {
            if (Session == null) throw new LoginException(LoginError.NotAuthenticated, "请先登录。");
            if (Session.HasExpired(DateTime.UtcNow.AddSeconds(_options.RefreshBeforeExpirySeconds)))
                await RefreshCoreAsync(ct);
        }

        private async Task<ISession> RefreshCoreAsync(CancellationToken ct)
        {
            if (Session == null) throw new LoginException(LoginError.NotAuthenticated, "请先登录。");
            if (Session.IsRefreshExpired || string.IsNullOrEmpty(Session.RefreshToken))
            {
                await ClearSessionAsync();
                throw new LoginException(LoginError.SessionExpired, "登录已过期，请重新登录。");
            }
            try
            {
                var previousId = Session.UserId;
                var refreshed = await RequestAsync(t => _transport.RefreshAsync(Session, t), ct);
                if (refreshed.UserId != previousId)
                    throw new LoginException(LoginError.SessionExpired, "会话账号不匹配，请重新登录。");
                ct.ThrowIfCancellationRequested();
                SaveSession(refreshed);
                return refreshed;
            }
            catch (Exception exception) when (IsInvalidSession(exception))
            { await ClearSessionAsync(); throw; }
        }

        private async Task<IApiAccount> VerifyAccountAsync(CancellationToken ct)
        {
            try { return await RequestAsync(t => _transport.GetAccountAsync(Session, t), ct); }
            catch (Exception exception) when (IsInvalidSession(exception))
            { await ClearSessionAsync(); throw; }
        }

        private async Task ConnectCoreAsync(CancellationToken ct, bool reconnecting)
        {
            await EnsureSessionAsync(ct);
            if (IsConnected) return;
            await CloseSocketAsync();
            // A locally unexpired token may have been revoked or its account disabled.
            // HTTP gives a precise auth error before the less descriptive WebSocket handshake.
            await VerifyAccountAsync(ct);
            var socket = _transport.CreateSocket();
            SetState(reconnecting ? LoginState.Reconnecting : LoginState.Connecting);
            _socket = socket;
            _closedHandler = reason => Dispatch(() => { _ = HandleClosedAsync(socket); });
            _errorHandler = exception => Dispatch(() =>
            {
                if (ReferenceEquals(_socket, socket)) NotifyError(LoginException.From(exception));
            });
            socket.Closed += _closedHandler;
            socket.Error += _errorHandler;
            using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token))
            {
                _connectionAttempt = attempt;
                Task connect = null;
                try
                {
                    connect = socket.ConnectAsync(Session, _options.AppearOnline, _options.TimeoutSeconds, _options.Language);
                    await AwaitBoundedAsync(connect, attempt.Token);
                    attempt.Token.ThrowIfCancellationRequested();
                    if (!socket.IsConnected) throw new LoginException(LoginError.Network, "服务器连接已断开。");
                    SetState(LoginState.Connected);
                }
                catch
                {
                    await CloseSocketAsync();
                    // Upstream ConnectAsync has no CancellationToken. Close a late completion as well.
                    if (connect != null) _ = CloseAfterCompletionAsync(connect, socket);
                    SetState(Session == null ? LoginState.SignedOut : LoginState.Authenticated);
                    throw;
                }
                finally { if (ReferenceEquals(_connectionAttempt, attempt)) _connectionAttempt = null; }
            }
        }

        private async Task HandleClosedAsync(ILoginSocket source)
        {
            try
            {
                await _gate.WaitAsync(_lifetime.Token);
                var shouldReconnect = false;
                try
                {
                    if (!ReferenceEquals(_socket, source)) return;
                    DetachSocket();
                    SetState(Session == null ? LoginState.SignedOut : LoginState.Authenticated);
                    shouldReconnect = _wantConnection && Session != null && _options.MaxReconnectAttempts > 0 && _reconnect == null;
                }
                finally { _gate.Release(); }
                if (shouldReconnect && !_shutdown) await ReconnectAsync();
            }
            catch (OperationCanceledException) { }
            catch (LoginException) { /* RunAsync already publishes the sanitized error. */ }
            catch (Exception exception) { NotifyError(LoginException.From(exception)); }
        }

        private void StopConnections()
        {
            _wantConnection = false;
            _reconnect?.Cancel();
            _connectionAttempt?.Cancel();
        }

        private ILoginSocket DetachSocket()
        {
            var socket = _socket;
            if (socket == null) return null;
            _socket = null;
            socket.Closed -= _closedHandler;
            socket.Error -= _errorHandler;
            _closedHandler = null;
            _errorHandler = null;
            return socket;
        }

        private async Task CloseSocketAsync()
        {
            var socket = DetachSocket();
            if (socket == null) return;
            try { await AwaitBoundedAsync(socket.CloseAsync(), CancellationToken.None); }
            catch (Exception exception) { NotifyError(LoginException.From(exception)); }
            finally { SetState(Session == null ? LoginState.SignedOut : LoginState.Authenticated); }
        }

        private async Task CloseAfterCompletionAsync(Task connect, ILoginSocket socket)
        {
            try { await connect; }
            catch { }
            try { await AwaitBoundedAsync(socket.CloseAsync(), CancellationToken.None); }
            catch { }
        }

        private void SaveSession(ISession session)
        {
            if (session == null || string.IsNullOrEmpty(session.AuthToken) || string.IsNullOrEmpty(session.RefreshToken))
                throw new LoginException(LoginError.Server, "服务器没有返回完整会话。");
            try { _store.SaveSession(new SavedSession(session.AuthToken, session.RefreshToken)); }
            catch { throw new LoginException(LoginError.Storage, "无法保存登录会话。"); }
            Session = session;
        }

        private void ClearSavedSession()
        {
            try { _store.ClearSession(); }
            catch { throw new LoginException(LoginError.Storage, "无法清除本地会话，请检查凭据存储。"); }
        }

        private async Task ClearSessionAsync()
        {
            StopConnections();
            await CloseSocketAsync();
            Session = null;
            SetState(LoginState.SignedOut);
            ClearSavedSession();
        }

        private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(PirateLoginClient));
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token))
            {
                await _gate.WaitAsync(linked.Token);
                try { linked.Token.ThrowIfCancellationRequested(); return await action(linked.Token); }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception)
                {
                    var error = LoginException.From(exception);
                    NotifyError(error);
                    throw error;
                }
                finally { _gate.Release(); }
            }
        }

        private Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct) =>
            RunAsync(async token => { await action(token); return true; }, ct);

        private async Task<T> RequestAsync<T>(Func<CancellationToken, Task<T>> request, CancellationToken ct)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
                try
                {
                    var task = request(timeout.Token);
                    await AwaitBoundedAsync(task, timeout.Token);
                    timeout.Token.ThrowIfCancellationRequested();
                    return await task;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && !_lifetime.IsCancellationRequested)
                { throw new TimeoutException(); }
            }
        }

        private Task RequestAsync(Func<CancellationToken, Task> request, CancellationToken ct) =>
            RequestAsync(async token => { await request(token); return true; }, ct);

        private async Task AwaitBoundedAsync(Task task, CancellationToken ct)
        {
            using (var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var delay = Task.Delay(TimeSpan.FromSeconds(_options.TimeoutSeconds), delayCancellation.Token);
                if (await Task.WhenAny(task, delay) != task)
                {
                    _ = ObserveAsync(task);
                    ct.ThrowIfCancellationRequested();
                    throw new TimeoutException();
                }
                delayCancellation.Cancel();
                await task;
                ct.ThrowIfCancellationRequested();
            }
        }

        private static async Task ObserveAsync(Task task) { try { await task; } catch { } }

        private void SetState(LoginState state)
        {
            if (State == state) return;
            State = state;
            Dispatch(() => InvokeSafely(StateChanged, state));
        }

        private void NotifyError(LoginException error) => Dispatch(() => InvokeSafely(Error, error));
        private void Dispatch(Action action)
        {
            if (_context != null && SynchronizationContext.Current != _context) _context.Post(_ => action(), null);
            else action();
        }

        private static void InvokeSafely<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch { /* A UI callback must not invalidate a successful login. */ }
        }

        private static bool IsInvalidSession(Exception exception)
        {
            var code = LoginException.From(exception).Code;
            return code == LoginError.InvalidCredentials || code == LoginError.SessionExpired ||
                code == LoginError.Forbidden || code == LoginError.NotFound;
        }
        private static bool IsNotFound(Exception exception) => LoginException.From(exception).Code == LoginError.NotFound;

        private static void ValidateEmail(string email, string password)
        {
            var emailBytes = email == null ? 0 : Encoding.UTF8.GetByteCount(email);
            var passwordBytes = password == null ? 0 : Encoding.UTF8.GetByteCount(password);
            if (emailBytes < 10 || emailBytes > 255 || email.IndexOf('@') <= 0 ||
                email.IndexOf(' ') >= 0 || email != email.Trim() || passwordBytes < 8 || passwordBytes > 72)
                throw new LoginException(LoginError.InvalidInput, "邮箱或密码格式不正确；密码需为 8–72 个 UTF-8 字节。");
        }
    }
}
