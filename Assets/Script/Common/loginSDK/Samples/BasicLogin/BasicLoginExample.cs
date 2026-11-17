using System;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using PirateCaptain.Login;
using UnityEngine;

namespace PirateCaptain.Samples
{
    /// <summary>
    /// 启动配置就绪后由主线程调用 Initialize，界面提交时 await 相应方法。
    /// 此示例不自动创建账号，也不将密码写入场景、Inspector 或日志。
    /// </summary>
    public sealed class BasicLoginExample : MonoBehaviour
    {
        [SerializeField] private LoginServerConfig serverConfig = null;
        [SerializeField] private bool rememberForDevelopment = false;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        public PirateLoginClient Login { get; private set; }
        public event Action<LoginState> LoginStateChanged;

        public void Initialize(string clientServerKey)
        {
            if (Login != null) throw new InvalidOperationException("登录 SDK 已初始化。");
            if (serverConfig == null) throw new InvalidOperationException("请指定 LoginServerConfig。");
            var options = serverConfig.CreateOptions(clientServerKey);
            ICredentialStore store = new MemoryCredentialStore();
            if (rememberForDevelopment)
            {
                var environment = options.Scheme + "://" + options.Host + ":" + options.Port + "/default";
                store = new DevelopmentPlayerPrefsCredentialStore(environment, allowPlaintextStorage: true);
            }
            Login = UnityLoginFactory.Create(options, store);
            Login.StateChanged += HandleStateChanged;
        }

        public async Task<ISession> RegisterAndConnectAsync(string email, string password, string username = null)
        {
            RequireInitialized();
            var session = await Login.RegisterEmailAsync(email, password, username, _lifetime.Token);
            await Login.ConnectAsync(_lifetime.Token);
            return session;
        }

        public Task<ISession> LoginAndConnectAsync(string email, string password)
        {
            RequireInitialized();
            return Login.LoginAndConnectAsync(email, password, _lifetime.Token);
        }

        public async Task LoginGuestAndConnectAsync()
        {
            RequireInitialized();
            await Login.LoginGuestAsync(true, null, _lifetime.Token);
            await Login.ConnectAsync(_lifetime.Token);
        }

        public async Task<bool> ResumeAndConnectAsync()
        {
            RequireInitialized();
            if (!await Login.RestoreSessionAsync(_lifetime.Token)) return false;
            await Login.ConnectAsync(_lifetime.Token);
            return true;
        }

        public Task BindEmailAsync(string email, string password)
        {
            RequireInitialized();
            return Login.BindEmailAsync(email, password, _lifetime.Token);
        }

        public Task LogoutAsync()
        {
            RequireInitialized();
            return Login.LogoutAsync(_lifetime.Token);
        }

        private void HandleStateChanged(LoginState state) => LoginStateChanged?.Invoke(state);

        private void RequireInitialized()
        {
            if (Login == null) throw new InvalidOperationException("请先从启动配置调用 Initialize。");
        }

        private async void OnDestroy()
        {
            _lifetime.Cancel();
            if (Login != null)
            {
                Login.StateChanged -= HandleStateChanged;
                try { await Login.ShutdownAsync(); }
                catch (LoginException error) { Debug.LogWarning("关闭登录服务失败：" + error.Code); }
            }
            _lifetime.Dispose();
        }
    }
}
