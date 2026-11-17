using System;
using System.Net;

namespace PirateCaptain.Login
{
    /// <summary>Connection settings for the existing Nakama authentication service.</summary>
    public sealed class LoginOptions
    {
        public string Scheme { get; set; } = "http";
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 17350;
        public string ServerKey { get; set; }
        public int TimeoutSeconds { get; set; } = 10;
        public int RefreshBeforeExpirySeconds { get; set; } = 60;
        public int MaxReconnectAttempts { get; set; } = 3;
        public int ReconnectBaseDelayMilliseconds { get; set; } = 500;
        public bool AppearOnline { get; set; } = true;
        public string Language { get; set; } = "zh";

        /// <summary>Explicit development-only opt-in for HTTP beyond localhost.</summary>
        public bool AllowInsecureHttp { get; set; }

        public void Validate()
        {
            if (!string.Equals(Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Scheme, "https", StringComparison.OrdinalIgnoreCase))
                throw new LoginException(LoginError.InvalidInput, "连接协议必须是 http 或 https。");

            if (string.IsNullOrWhiteSpace(Host) || Host != Host.Trim() ||
                Uri.CheckHostName(Host) == UriHostNameType.Unknown)
                throw new LoginException(LoginError.InvalidInput, "服务器地址必须是有效的主机名或 IP，不包含协议、端口和路径。");

            if (Port < 1 || Port > 65535)
                throw new LoginException(LoginError.InvalidInput, "服务器端口必须在 1 到 65535 之间。");

            if (string.IsNullOrWhiteSpace(ServerKey) || ServerKey.IndexOf(':') >= 0 || HasControlCharacter(ServerKey))
                throw new LoginException(LoginError.InvalidInput, "请配置有效的服务器客户端连接密钥。");

            if (TimeoutSeconds < 1 || TimeoutSeconds > 300)
                throw new LoginException(LoginError.InvalidInput, "请求超时必须在 1 到 300 秒之间。");
            if (RefreshBeforeExpirySeconds < 0 || RefreshBeforeExpirySeconds > 86400)
                throw new LoginException(LoginError.InvalidInput, "会话提前刷新时间必须在 0 到 86400 秒之间。");
            if (MaxReconnectAttempts < 0 || MaxReconnectAttempts > 10)
                throw new LoginException(LoginError.InvalidInput, "自动重连次数必须在 0 到 10 次之间。");
            if (ReconnectBaseDelayMilliseconds < 1 || ReconnectBaseDelayMilliseconds > 60000)
                throw new LoginException(LoginError.InvalidInput, "重连基础间隔必须在 1 到 60000 毫秒之间。");
            if (string.IsNullOrWhiteSpace(Language) || Language.Length > 32 || HasControlCharacter(Language))
                throw new LoginException(LoginError.InvalidInput, "请配置有效的语言标识。");

            var loopback = string.Equals(Host, "localhost", StringComparison.OrdinalIgnoreCase);
            IPAddress address;
            if (IPAddress.TryParse(Host.Trim('[', ']'), out address))
                loopback = IPAddress.IsLoopback(address);
            if (string.Equals(Scheme, "http", StringComparison.OrdinalIgnoreCase) && !loopback && !AllowInsecureHttp)
                throw new LoginException(LoginError.InvalidInput, "远程服务器请使用 HTTPS；开发联调需显式启用 AllowInsecureHttp。");
        }

        private static bool HasControlCharacter(string value)
        {
            foreach (var character in value)
                if (char.IsControl(character)) return true;
            return false;
        }
    }
}
