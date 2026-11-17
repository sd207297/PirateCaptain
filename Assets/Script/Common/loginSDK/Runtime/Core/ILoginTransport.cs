using System;
using System.Threading;
using System.Threading.Tasks;
using Nakama;

namespace PirateCaptain.Login
{
    /// <summary>Injectable transport boundary for authentication and realtime connections.</summary>
    public interface ILoginTransport
    {
        IClient Client { get; }
        Task<ISession> AuthenticateEmailAsync(string email, string password, string username, bool create, CancellationToken ct);
        Task<ISession> AuthenticateDeviceAsync(string deviceId, string username, bool create, CancellationToken ct);
        Task<ISession> RefreshAsync(ISession session, CancellationToken ct);
        Task<IApiAccount> GetAccountAsync(ISession session, CancellationToken ct);
        Task LinkEmailAsync(ISession session, string email, string password, CancellationToken ct);
        Task LogoutAsync(ISession session, CancellationToken ct);
        ILoginSocket CreateSocket();
    }

    public interface ILoginSocket
    {
        bool IsConnected { get; }
        event Action<string> Closed;
        event Action<Exception> Error;
        Task ConnectAsync(ISession session, bool appearOnline, int timeoutSeconds, string language);
        Task CloseAsync();
        ISocket NativeSocket { get; }
    }
}
