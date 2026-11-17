using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using Nakama;

namespace PirateCaptain.Login
{
    public enum LoginError
    {
        InvalidInput,
        InvalidCredentials,
        NotFound,
        Conflict,
        Forbidden,
        SessionExpired,
        NotAuthenticated,
        Network,
        Timeout,
        Server,
        Storage,
        Unknown
    }

    /// <summary>A public error with safe text; original response bodies are never retained.</summary>
    public sealed class LoginException : Exception
    {
        public LoginError Code { get; }
        public int? HttpStatusCode { get; }

        public LoginException(LoginError code, string message = null, int? httpStatusCode = null)
            : base(message ?? MessageFor(code))
        {
            Code = code;
            HttpStatusCode = httpStatusCode;
        }

        public static LoginException From(Exception exception)
        {
            if (exception == null) return new LoginException(LoginError.Unknown);
            var known = exception as LoginException;
            if (known != null) return known;
            var aggregate = exception as AggregateException;
            if (aggregate != null && aggregate.InnerExceptions.Count == 1)
                return From(aggregate.InnerExceptions[0]);

            var response = exception as ApiResponseException;
            if (response != null)
            {
                var status = response.StatusCode > 0 && response.StatusCode <= int.MaxValue
                    ? (int?)response.StatusCode : null;
                LoginError code;
                switch (status)
                {
                    case 400: code = LoginError.InvalidInput; break;
                    case 401: code = LoginError.InvalidCredentials; break;
                    case 403: code = LoginError.Forbidden; break;
                    case 404: code = LoginError.NotFound; break;
                    case 409: code = LoginError.Conflict; break;
                    case 408: case 504: code = LoginError.Timeout; break;
                    case 429: code = LoginError.Server; break;
                    default:
                        if (status >= 500) code = LoginError.Server;
                        else if (status == null) code = FromGrpcCode(response.GrpcStatusCode);
                        else code = LoginError.Unknown;
                        break;
                }
                return new LoginException(code, httpStatusCode: status);
            }

            if (exception is TimeoutException || exception is OperationCanceledException)
                return new LoginException(LoginError.Timeout);
            var web = exception as WebException;
            if (web != null && web.Status == WebExceptionStatus.Timeout)
                return new LoginException(LoginError.Timeout);
            if (exception is HttpRequestException || exception is SocketException ||
                exception is WebSocketException || exception is WebException)
                return new LoginException(LoginError.Network);
            if (exception is ArgumentException || exception is FormatException)
                return new LoginException(LoginError.InvalidInput);
            return new LoginException(LoginError.Unknown);
        }

        private static LoginError FromGrpcCode(int code)
        {
            switch (code)
            {
                case 3: return LoginError.InvalidInput;
                case 4: return LoginError.Timeout;
                case 5: return LoginError.NotFound;
                case 6: return LoginError.Conflict;
                case 7: return LoginError.Forbidden;
                case 8: case 13: return LoginError.Server;
                case 16: return LoginError.InvalidCredentials;
                default: return LoginError.Network;
            }
        }

        private static string MessageFor(LoginError code)
        {
            switch (code)
            {
                case LoginError.InvalidInput: return "输入或连接配置无效，请检查后重试。";
                case LoginError.InvalidCredentials: return "登录凭据无效，请检查账号和密码。";
                case LoginError.NotFound: return "账号不存在。";
                case LoginError.Conflict: return "该用户名或邮箱已被使用。";
                case LoginError.Forbidden: return "当前账号无法访问此服务。";
                case LoginError.SessionExpired: return "登录会话已失效，请重新登录。";
                case LoginError.NotAuthenticated: return "请先登录。";
                case LoginError.Network: return "无法连接服务器，请检查网络后重试。";
                case LoginError.Timeout: return "连接超时，请稍后重试。";
                case LoginError.Server: return "服务器暂时无法处理请求，请稍后重试。";
                case LoginError.Storage: return "无法读取或保存登录信息。";
                default: return "登录服务发生错误，请稍后重试。";
            }
        }
    }
}
