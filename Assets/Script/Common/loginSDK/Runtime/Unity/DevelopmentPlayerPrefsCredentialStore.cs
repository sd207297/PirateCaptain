using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PirateCaptain.Login
{
    /// <summary>
    /// 仅限开发环境的明文持久化示例。生产环境请实现 ICredentialStore 并接入平台安全存储。
    /// 不保存邮箱或密码；游客 ID 与令牌仍属于凭据，不应上传或记录到日志。
    /// </summary>
    public sealed class DevelopmentPlayerPrefsCredentialStore : ICredentialStore
    {
        private readonly string _prefix = null;

        public DevelopmentPlayerPrefsCredentialStore(string environmentNamespace, bool allowPlaintextStorage)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            throw new NotSupportedException("PlayerPrefs 凭据存储仅允许在编辑器和 Development Build 中使用。");
#else
            if (!allowPlaintextStorage)
                throw new ArgumentException("开发环境必须显式同意明文保存令牌和游客 ID。", nameof(allowPlaintextStorage));
            if (string.IsNullOrWhiteSpace(environmentNamespace))
                throw new ArgumentException("必须为不同服务器和账号槽指定独立存储名称。", nameof(environmentNamespace));
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(environmentNamespace));
                _prefix = "PirateCaptain.Login.Dev." + BitConverter.ToString(bytes).Replace("-", "") + ".";
            }
#endif
        }

        public SavedSession LoadSession()
        {
            var json = PlayerPrefs.GetString(_prefix + "Session", "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<SessionData>(json);
                if (data == null || string.IsNullOrEmpty(data.authToken) || string.IsNullOrEmpty(data.refreshToken))
                {
                    ClearSession();
                    return null;
                }
                return new SavedSession(data.authToken, data.refreshToken);
            }
            catch (ArgumentException)
            {
                ClearSession();
                return null;
            }
        }

        public void SaveSession(SavedSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            // One record prevents a process interruption from mixing tokens from different sessions.
            PlayerPrefs.SetString(_prefix + "Session", JsonUtility.ToJson(new SessionData
            {
                authToken = session.AuthToken,
                refreshToken = session.RefreshToken
            }));
            PlayerPrefs.Save();
        }

        public void ClearSession()
        {
            PlayerPrefs.DeleteKey(_prefix + "Session");
            PlayerPrefs.Save();
        }

        public string GetOrCreateDeviceId()
        {
            var key = _prefix + "DeviceId";
            var id = PlayerPrefs.GetString(key, "");
            if (Guid.TryParseExact(id, "N", out _)) return id;
            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key, id);
            PlayerPrefs.Save();
            return id;
        }

        [Serializable]
        private sealed class SessionData
        {
            public string authToken;
            public string refreshToken;
        }
    }
}
