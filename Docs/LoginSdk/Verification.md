# 登录 SDK 验证记录

最近验证日期：2026-11-18（Asia/Shanghai）。

项目已按要求移除 `Tests/LoginSdk/` 测试工程及生成文件；下列记录保留为此前已完成验证的证据。

- 2026-11-18：公网 HTTPS/WSS 联调 21/21 检查通过，包括注册登录、游客绑定、心跳、刷新、重连、会话恢复和退出失效，临时测试账号已清理。见 public-server-integration-results.json。
- 公网切换后的 SDK 生命周期回归 31/31 通过。

以下为 2026-11-17 的历史验证记录：

- SDK 自动回归：31/31 通过；0 编译警告。覆盖显式注册/登录、游客身份、会话恢复/刷新、有限重连、取消/超时后迟到响应、退出失败清理、凭据存储异常和官方客户端 HTTP 请求约定。
- 真实服务器：通过现有 SSH 通道调用腾讯云 Nakama 服务，注册、重复注册、错误密码、登录、WebSocket 握手和消息往返、17 秒心跳存活、刷新、重连、撤销会话拒绝、恢复、退出、游客重入和邮箱绑定均通过。两个临时账号已经删除。详见 server-integration-results.json。
- Unity C# 兼容编译：基于 Unity 2022.3.62f1 的实际托管程序集与 .NET Standard 2.1，编辑器和 WebGL 条件编译分支均 0 错误；有 4 个来自官方 Nakama/Satori 依赖的过时 API 警告。
- 没有使用不相关的当前编辑器项目做验证，没有运行本项目 Unity 6 Play Mode，也没有进行 IL2CPP 或移动/WebGL 平台发布构建。
- 默认使用内存凭据存储；开发环境提供显式启用的 PlayerPrefs 示例。正式跨启动自动登录和游客保留需要接入平台安全存储。
- 当时云服务仅通过 SSH 回环通道访问；现已配置公网 HTTPS，默认地址为 `https://43.138.240.10:443`，无需 SSH 转发。

## 依赖来源

官方 Nakama Unity SDK 3.23.0 固定提交 `704f5da244a49d942f383965f5549420522eb8e0`，来源 https://github.com/heroiclabs/nakama-unity 。依赖现由 Unity manifest 中的 Git 引用管理，原始源码、平台插件和 Apache-2.0 许可证均保留，未修改官方实现。

服务端协议依据现有 服务端仓库的 `NakamaNative` 源码核对。SDK 是客户端封装，无需新增认证服务端接口。
