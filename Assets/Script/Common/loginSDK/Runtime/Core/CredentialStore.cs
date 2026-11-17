using System;

namespace PirateCaptain.Login
{
    /// <summary>Bearer credentials. Do not log or serialize these into ordinary preferences.</summary>
    public sealed class SavedSession
    {
        public string AuthToken { get; }
        public string RefreshToken { get; }

        public SavedSession(string authToken, string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(refreshToken))
                throw new LoginException(LoginError.InvalidInput, "保存的会话必须包含完整令牌。");
            AuthToken = authToken;
            RefreshToken = refreshToken;
        }
    }

    /// <summary>Implement with platform secure storage to retain login across app launches.</summary>
    public interface ICredentialStore
    {
        SavedSession LoadSession();
        void SaveSession(SavedSession session);
        void ClearSession();
        string GetOrCreateDeviceId();
    }

    /// <summary>Process-only defaults. No password or token is written to disk.</summary>
    public sealed class MemoryCredentialStore : ICredentialStore
    {
        private readonly object _sync = new object();
        private SavedSession _session;
        private string _deviceId;

        public SavedSession LoadSession()
        {
            lock (_sync) return _session;
        }

        public void SaveSession(SavedSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            lock (_sync) _session = session;
        }

        public void ClearSession()
        {
            lock (_sync) _session = null;
        }

        public string GetOrCreateDeviceId()
        {
            lock (_sync)
            {
                if (_deviceId == null) _deviceId = Guid.NewGuid().ToString("D");
                return _deviceId;
            }
        }
    }
}
