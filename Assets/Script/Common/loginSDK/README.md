# loginSDK

面向现有 Nakama 原生服务端的 Unity 底层账号模块。游戏界面通过 `PirateLoginClient` 完成注册、登录、游客升级、恢复会话和实时连接；HTTP、WebSocket 和令牌格式由官方 Nakama 客户端处理。

SDK 本身不创建登录界面，不修改服务端，也不实现服务端尚未提供的邮件验证码、邮箱验证和找回密码流程。

## 文件与依赖

- `Runtime/Core/`：登录、会话、连接状态与存储接口，不依赖 Unity 场景。
- `Runtime/Nakama/`：对接官方 Nakama 客户端。
- `Runtime/Unity/`：Unity 工厂、服务器配置和可选的开发持久化示例。
- `Samples/BasicLogin/`：随项目编译的调用示例。
- 依赖官方 `com.heroiclabs.nakama-unity` **3.23.0**，来源提交 `704f5da244a49d942f383965f5549420522eb8e0`。

loginSDK 是项目内的普通源码模块，位于 `Assets/Script/Common/loginSDK/`，不使用自定义 UPM package。`Runtime/` 包含底层实现，`Samples/BasicLogin/` 提供调用示例。官方 Nakama 依赖由 `Packages/manifest.json` 引用，Unity 将包解析到 `Library/PackageCache/`，保留原始许可证和平台适配文件。最低支持 Unity 2022.3，当前项目为 Unity 6。

## 连接当前开发服务器

公网入口为 `https://43.138.240.10`，使用可信 IP 证书；客户端无需 SSH 转发。云端安全组需允许 TCP 443。

| 配置项 | 当前开发值 |
| --- | --- |
| `Scheme` | `https` |
| `Host` | `43.138.240.10` |
| `Port` | `443` |
| `ServerKey` | 由开发配置传入服务器 `NAKAMA_SERVER_KEY` 的值 |

HTTP API 和 WebSocket 共用 443 端口，SDK 自动使用 `wss`。无需启用 `AllowInsecureHttp` 或绕过证书校验。新建配置资源默认使用此地址；已有配置资源需要在 Inspector 中改为上述值。

`NAKAMA_SERVER_KEY` 是客户端 API 访问键，会随客户端分发，不能承担玩家身份认证。不要向客户端传入数据库密码、Console 密码、`NAKAMA_SESSION_KEY`、`NAKAMA_REFRESH_KEY`、HTTP 管理键或 SSH 私钥。配置资源与示例中没有内置任何实际密钥。

IPv6 可选入口已配置：`https://[2402:4e00:c013:7500:b426:6f12:2e2:0]:443`。
使用时设置 `Scheme = "https"`、`Host = "2402:4e00:c013:7500:b426:6f12:2e2:0"`、`Port = 443`。
完整 URL 需要方括号；Host 字段仅填地址。客户端网络必须具备 IPv6 路由；当前开发电脑没有该路由，因此默认暂时保留 IPv4。IPv6 的客户端 HTTPS/WSS 联调仍待可用网络验证。

## 最小接入

在 Unity 主线程创建一个全游戏共享的 SDK 实例，例如由启动模块持有。下面的 `clientServerKey` 来自项目的环境配置，`email/password` 来自用户提交。

```csharp
using PirateCaptain.Login;

var login = UnityLoginFactory.Create(new LoginOptions
{
    Scheme = "https",
    Host = "43.138.240.10",
    Port = 443,
    ServerKey = clientServerKey
});

// 普通登录明确禁止创建新账号，账号不存在时返回 NotFound。
await login.LoginAndConnectAsync(email, password, cancellationToken);

// 连接成功后，可以通过官方 Socket 接入匹配、聊天、房间等后续模块。
var account = await login.GetAccountAsync(cancellationToken);
var userId = account.User.Id;
```

也可以通过 `Assets > Create > Pirate Captain > Login Server Config` 创建服务器配置资源，再调用 `config.CreateClient(clientServerKey, store)`。

注册与游客流程：

```csharp
// 邮箱注册成功已经取得会话，无需再调用一次登录。
var registered = await login.RegisterEmailAsync(email, password, username, cancellationToken);
await login.ConnectAsync(cancellationToken);
bool createdNow = registered.Created;

// 游客身份来自 ICredentialStore。默认允许首次创建，也允许同一游客重入。
await login.LoginGuestAsync(create: true, cancellationToken: cancellationToken);
await login.ConnectAsync(cancellationToken);

// 使用当前游客会话绑定邮箱，保留用户 ID 和已有游戏数据。
await login.BindEmailAsync(email, password, cancellationToken);

// 显式退出会撤销当前令牌、关闭实时连接，并清空本地会话。
await login.LogoutAsync(cancellationToken);
```

Nakama 的 `create=true` 表示“不存在时注册，存在且凭据正确时登录”。因此 `RegisterEmailAsync` 的重复调用可以成功，`Created` 为 `false`，不是强制报“邮箱已注册”。绑定邮箱同样沿用原生语义：也可以更改当前账号已有的邮箱和密码，不需要创建另一个账号。

## SDK API

以下方法除 `ShutdownAsync` 外均支持可选 `CancellationToken`。取消本地等待不代表已经抵达服务器的注册或绑定操作被回滚；重复提交前应先确认账号状态。

| API | 返回 | 作用 |
| --- | --- | --- |
| `RegisterEmailAsync(email, password, username = null, cancellationToken)` | `Task<ISession>` | 邮箱注册或已有账号重入，`create=true` |
| `LoginEmailAsync(email, password, username = null, cancellationToken)` | `Task<ISession>` | 只登录，`create=false` |
| `LoginAndConnectAsync(email, password, cancellationToken)` | `Task<ISession>` | 邮箱登录后连接实时服务器 |
| `LoginGuestAsync(create = true, username = null, cancellationToken)` | `Task<ISession>` | 使用存储中的随机游客 ID 注册或登录 |
| `BindEmailAsync(email, password, cancellationToken)` | `Task` | 给当前账号绑定或更新邮箱与密码 |
| `RestoreSessionAsync(cancellationToken)` | `Task<bool>` | 尝试恢复保存的会话，并由服务器验证；不能恢复时回到登录流程 |
| `RefreshSessionAsync(cancellationToken)` | `Task<ISession>` | 使用刷新令牌取得可用会话 |
| `GetAccountAsync(cancellationToken)` | `Task<IApiAccount>` | 获取当前服务器账号资料 |
| `ConnectAsync(cancellationToken)` | `Task` | 建立已认证的实时连接；已连接时可重复调用 |
| `ReconnectAsync(cancellationToken)` | `Task` | 执行有次数上限的重连 |
| `DisconnectAsync(cancellationToken)` | `Task` | 关闭实时连接，保留登录会话 |
| `LogoutAsync(cancellationToken)` | `Task` | 退出当前会话；失败时仍清空本地登录状态 |
| `ShutdownAsync()` | `Task` | 关闭 SDK 生命周期；供应用退出或所有者销毁时调用 |

| 属性或事件 | 用途 |
| --- | --- |
| `Session` | 当前 Nakama 会话；未登录时为 `null` |
| `State` | `SignedOut / Authenticated / Connecting / Connected / Reconnecting` |
| `IsConnected` | 当前实时连接是否可用 |
| `StateChanged` | 监听登录与连接状态变化 |
| `Error` | 监听 SDK 操作与后台重连错误；主动调用也应捕获异常 |
| `Client` | 后续账号、存储、排行榜等模块使用的官方 `IClient` |
| `Socket` | 当前连接的官方 `ISocket`；连接重建后需重新获取与订阅 |

`Authenticated` 仅表示有登录会话。进入依赖实时服务的场景前应等待 `ConnectAsync` 成功或 `Connected` 状态。意外断线会自动触发有限次数的重连；主动 `DisconnectAsync`、`LogoutAsync`、新登录或 `ShutdownAsync` 会停止旧连接的重连。连接断开后的房间重入、匹配票据恢复、聊天订阅等属于业务流程，应在重新连接后自行恢复。

所有 SDK 调用从 Unity 主线程发起。SDK 的状态和错误事件回到创建实例时的同步上下文。直接订阅 `Socket` 的 Nakama 原生消息事件时，其线程行为仍由底层传输决定；在回调中修改 Unity 对象前，应调度回主线程。不要从业务模块直接调用 `Client` 的认证、刷新或退出接口，也不要直接管理 `Socket` 的连接生命周期，以免绕过 SDK 的存储与状态管理。

## 会话与游客存储

默认 `MemoryCredentialStore` 只在当前进程中保留会话与随机游客 ID，不写磁盘、不保存密码。它适合不需要自动登录的接入和测试。**如果游客需要跨启动保留账号，必须在首次游客登录前注入持久化存储**；重启后生成新 ID 会产生另一个游客账号。

`ICredentialStore` 是同步、可替换接口：

```csharp
SavedSession LoadSession();
void SaveSession(SavedSession session);
void ClearSession();
string GetOrCreateDeviceId();
```

生产应用应把令牌和游客 ID 保存到平台安全存储，例如 iOS Keychain、Android Keystore 支持的加密存储或 Windows 受保护凭据存储。使用服务器环境与账号槽共同隔离存储；`ClearSession` 只清除本次会话，保留游客 ID，避免退出后游客丢失账号。保存 access/refresh token 时应整体写入，避免令牌配对错乱。不持久化用户密码。

开发环境可以显式启用以下存储进行联调，它在正式非 Development Build 中会拒绝创建：

```csharp
var store = new DevelopmentPlayerPrefsCredentialStore(
    "https://43.138.240.10:443/default",
    allowPlaintextStorage: true);
var login = UnityLoginFactory.Create(options, store);

if (await login.RestoreSessionAsync(cancellationToken))
    await login.ConnectAsync(cancellationToken);
else
    ShowLoginPanel();
```

PlayerPrefs 示例会明文保存令牌与游客 ID，只用于开发。它不应进入正式发布，也不能被视为平台安全存储。离线或服务器暂时不可用时，请按错误处理网络失败，不要自动创建一个新游客覆盖原身份。

## 错误处理

```csharp
try
{
    await login.LoginAndConnectAsync(email, password, cancellationToken);
}
catch (OperationCanceledException)
{
    // 用户取消或界面生命周期结束。
}
catch (LoginException error)
{
    ShowLoginError(error.Code, error.Message);
}
```

`LoginException.Code` 提供稳定分类：`InvalidInput`、`InvalidCredentials`、`NotFound`、`Conflict`、`Forbidden`、`SessionExpired`、`NotAuthenticated`、`Network`、`Timeout`、`Server`、`Storage`、`Unknown`；`HttpStatusCode` 提供可用时的 HTTP 状态码。SDK 使用固定错误消息，不把原始服务端响应或认证令牌拼入消息。

退出失败时，本地仍退出并关闭连接，但可能未完成服务端令牌撤销；调用方应保留错误提示。取消、刷新失败和断线处理都不能通过自动注册来“修复”账号。不要记录 `Session`、token、密码或完整连接 URL；不要启用会输出请求正文的上游调试日志。

## 服务端接口对应关系

| 功能 | HTTP 请求 | 鉴权 |
| --- | --- | --- |
| 邮箱注册 | `POST /v2/account/authenticate/email?create=true` | Basic：客户端访问键 |
| 邮箱登录 | `POST /v2/account/authenticate/email?create=false` | Basic：客户端访问键 |
| 游客注册或登录 | `POST /v2/account/authenticate/device?create=true/false` | Basic：客户端访问键 |
| 绑定邮箱 | `POST /v2/account/link/email` | Bearer：access token |
| 获取账号 | `GET /v2/account` | Bearer：access token |
| 刷新会话 | `POST /v2/account/session/refresh` | Basic：客户端访问键 |
| 退出登录 | `POST /v2/session/logout` | Bearer：access token |
| 实时连接 | `GET /ws?format=json&status=true&lang=zh&token=...` | access token，官方客户端负责握手 |

邮箱体为 `{"email":"captain@example.com","password":"..."}`，游客体为 `{"id":"随机游客ID"}`，刷新体为 `{"token":"refresh token"}`。退出会同时提交当前 access token 和 refresh token，不发送可能导致全账号会话撤销的空请求体。HTTP 成功登录和 WebSocket 成功握手是两个步骤，后者成功即完成实时认证，无额外自定义登录消息。

## 可调参数

默认请求超时 10 秒、会话过期前 60 秒准备刷新、重连最多 3 次、重连初始间隔 500 毫秒、语言 `zh`、连接时在线状态为 `true`。调用账号 API、连接或重连前按需检查会话并刷新，不启动每帧轮询刷新任务，也不会仅因为 access token 到期而定时拆掉已建立的 WebSocket。`MaxReconnectAttempts`、`ReconnectBaseDelayMilliseconds` 可按项目需要配置；重连不是无限后台循环。保持 SDK 生命周期与应用或账号模块一致，切换界面时共享同一实例。销毁前调用 `ShutdownAsync`，它保留已保存凭据供下次启动恢复；需要撤销账号会话时明确调用 `LogoutAsync`。

默认仅允许回环地址使用 HTTP；局域网开发可显式配置 `AllowInsecureHttp`，公网应配置 HTTPS。在 Unity 中进行 HTTP 联调还可能需要在 Player Settings 为 Development Builds 允许 HTTP 下载。WebGL 部署需同时满足浏览器的 HTTPS、混合内容和跨域规则。SDK 提供平台适配路径，平台发布包仍需实际构建验证。

## 验证边界

纯 C# 生命周期测试与实际服务器联调结果由项目的验证记录提供。API 表和示例不代表已经完成 Unity Play Mode、IL2CPP、Android、iOS 或 WebGL 真机构建验证；发布前应在目标平台验证网络、前后台切换和安全存储实现。
