using System;
using System.Threading;
using Nakama;

namespace PirateCaptain.Login
{
    /// <summary>从 Unity 主线程创建一个共享的登录服务实例。</summary>
    public static class UnityLoginFactory
    {
        public static PirateLoginClient Create(LoginOptions options, ICredentialStore store = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.Validate();
            if (SynchronizationContext.Current == null)
                throw new InvalidOperationException("请在 Unity 主线程的 Awake/Start 中创建登录服务。");

            // Session refresh belongs to PirateLoginClient so refreshed tokens are saved together.
            // Keep the HTTP logger unset: upstream debug logs may include credentials and tokens.
            var client = new Client(options.Scheme, options.Host, options.Port, options.ServerKey,
                UnityWebRequestAdapter.Instance, false);
            client.Timeout = options.TimeoutSeconds;
            var transport = new NakamaLoginTransport(client, () => CreateSocket(client));
            return new PirateLoginClient(options, transport, store ?? new MemoryCredentialStore());
        }

        private static ISocket CreateSocket(IClient client)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Nakama.Socket.From(client, new JsWebSocketAdapter());
#else
            return Nakama.Socket.From(client, new WebSocketStdlibAdapter());
#endif
        }
    }
}
