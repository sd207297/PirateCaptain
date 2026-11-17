using UnityEngine;

namespace PirateCaptain.Login
{
    /// <summary>可提交到项目的服务器地址配置；客户端访问键由启动配置传入。</summary>
    [CreateAssetMenu(fileName = "LoginServerConfig", menuName = "Pirate Captain/Login Server Config")]
    public sealed class LoginServerConfig : ScriptableObject
    {
        [SerializeField] private string scheme = "http";
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 17350;
        [SerializeField] private int timeoutSeconds = 10;
        [SerializeField] private int refreshBeforeExpirySeconds = 60;
        [SerializeField] private int maxReconnectAttempts = 3;
        [SerializeField] private int reconnectBaseDelayMilliseconds = 500;
        [SerializeField] private bool appearOnline = true;
        [SerializeField] private string language = "zh";
        [Tooltip("仅供明确配置的局域网开发环境；公网应使用 HTTPS。")]
        [SerializeField] private bool allowInsecureHttp = false;

        public LoginOptions CreateOptions(string clientServerKey)
        {
            var options = new LoginOptions
            {
                Scheme = scheme,
                Host = host,
                Port = port,
                ServerKey = clientServerKey,
                TimeoutSeconds = timeoutSeconds,
                RefreshBeforeExpirySeconds = refreshBeforeExpirySeconds,
                MaxReconnectAttempts = maxReconnectAttempts,
                ReconnectBaseDelayMilliseconds = reconnectBaseDelayMilliseconds,
                AppearOnline = appearOnline,
                Language = language,
                AllowInsecureHttp = allowInsecureHttp
            };
            options.Validate();
            return options;
        }

        public PirateLoginClient CreateClient(string clientServerKey, ICredentialStore store = null)
        {
            return UnityLoginFactory.Create(CreateOptions(clientServerKey), store);
        }
    }
}
