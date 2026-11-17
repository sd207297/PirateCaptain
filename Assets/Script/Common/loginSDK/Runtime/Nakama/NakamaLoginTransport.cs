using System;
using System.Threading;
using System.Threading.Tasks;
using Nakama;

namespace PirateCaptain.Login
{
    /// <summary>Uses the official Nakama SDK with explicit, non-retried authentication calls.</summary>
    public sealed class NakamaLoginTransport : ILoginTransport
    {
        private static readonly RetryConfiguration NoRetries = new RetryConfiguration(500, 0);
        private readonly Func<ISocket> _socketFactory;

        public IClient Client { get; }

        public NakamaLoginTransport(IClient client, Func<ISocket> socketFactory)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            _socketFactory = socketFactory ?? throw new ArgumentNullException(nameof(socketFactory));
            // The service owns refresh serialization and persists the resulting token pair.
            if (Client.AutoRefreshSession)
                throw new LoginException(LoginError.InvalidInput,
                    "请使用 autoRefreshSession: false 创建 Nakama Client，由登录服务统一管理会话刷新。");
        }

        public Task<ISession> AuthenticateEmailAsync(string email, string password, string username, bool create, CancellationToken ct)
        {
            return SendAsync(() => Client.AuthenticateEmailAsync(email, password, username, create, null, NoRetries, ct), ct);
        }

        public Task<ISession> AuthenticateDeviceAsync(string deviceId, string username, bool create, CancellationToken ct)
        {
            return SendAsync(() => Client.AuthenticateDeviceAsync(deviceId, username, create, null, NoRetries, ct), ct);
        }

        public Task<ISession> RefreshAsync(ISession session, CancellationToken ct)
        {
            return SendAsync(() => Client.SessionRefreshAsync(session, null, NoRetries, ct), ct);
        }

        public Task<IApiAccount> GetAccountAsync(ISession session, CancellationToken ct)
        {
            return SendAsync(() => Client.GetAccountAsync(session, NoRetries, ct), ct);
        }

        public Task LinkEmailAsync(ISession session, string email, string password, CancellationToken ct)
        {
            return SendAsync(() => Client.LinkEmailAsync(session, email, password, NoRetries, ct), ct);
        }

        public Task LogoutAsync(ISession session, CancellationToken ct)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.AuthToken) ||
                string.IsNullOrWhiteSpace(session.RefreshToken))
                throw new LoginException(LoginError.NotAuthenticated);
            // An empty logout body revokes every session. Always send this exact token pair.
            return SendAsync(() => Client.SessionLogoutAsync(session.AuthToken, session.RefreshToken, NoRetries, ct), ct);
        }

        private static async Task<T> SendAsync<T>(Func<Task<T>> request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try { return await request(); }
            catch (OperationCanceledException exception) when (!ct.IsCancellationRequested && exception.InnerException != null)
            {
                // Nakama wraps exhausted transient retries in TaskCanceledException, even at zero retries.
                // Preserve the real failure category instead of claiming the caller canceled the request.
                throw LoginException.From(exception.InnerException);
            }
        }

        private static async Task SendAsync(Func<Task> request, CancellationToken ct)
        {
            await SendAsync(async () => { await request(); return true; }, ct);
        }

        public ILoginSocket CreateSocket()
        {
            var socket = _socketFactory();
            if (socket == null) throw new LoginException(LoginError.Unknown, "未能创建服务器连接。");
            return new NakamaLoginSocket(socket);
        }

        private sealed class NakamaLoginSocket : ILoginSocket
        {
            public ISocket NativeSocket { get; }
            public bool IsConnected => NativeSocket.IsConnected;

            public event Action<string> Closed
            {
                add { NativeSocket.Closed += value; }
                remove { NativeSocket.Closed -= value; }
            }

            public event Action<Exception> Error
            {
                add { NativeSocket.ReceivedError += value; }
                remove { NativeSocket.ReceivedError -= value; }
            }

            public NakamaLoginSocket(ISocket socket) { NativeSocket = socket; }

            public Task ConnectAsync(ISession session, bool appearOnline, int timeoutSeconds, string language)
            {
                return NativeSocket.ConnectAsync(session, appearOnline, timeoutSeconds, language);
            }

            public Task CloseAsync() { return NativeSocket.CloseAsync(); }
        }
    }
}
