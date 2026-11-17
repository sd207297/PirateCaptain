# 登录 SDK 验证

在 Unity 工程根目录运行（需要 .NET 9）：

```powershell
dotnet run --project Tests/LoginSdk/Unit/PirateCaptain.Login.Tests.csproj
```

自动测试不访问外部服务器，覆盖 31 项生命周期与 HTTP 协议约定。

真实联调需要先开启现有 SSH 通道，在本机环境变量设置 `NAKAMA_SERVER_KEY`。
可选 `NAKAMA_TEST_URL`，默认 `http://127.0.0.1:17350`。不要将实际密钥写入脚本或提交。

```powershell
dotnet run --project Tests/LoginSdk/Integration/PirateLogin.Integration.csproj
```

联调会创建唯一临时账号，校验 HTTP + WebSocket，并在 finally 中删除这些账号；不会重启服务。
如进程被强制终止，应检查服务器是否留下 sdk- 前缀测试账号。输出中不打印密码、令牌或客户端访问键。
联调结果文件生成在该项目输出目录的 integration-results.json。

接口及 Unity 接入文档见 `Assets/Script/Common/loginSDK/README.md`。
