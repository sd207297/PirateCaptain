using System;
using System.Collections.Generic;
using PirateCaptain.Framework;
using UnityEngine;

namespace PirateCaptain.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        private readonly List<ModuleBase> _modules = new List<ModuleBase>();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            try
            {
                RegisterModules();
            }
            catch
            {
                ShutdownModules();
                Destroy(gameObject);
                throw;
            }
        }

        private void RegisterModules()
        {
            // 在此按依赖顺序注册。可将 Register 返回的实例传给后续模块的构造函数。
            Register(new MessageBus());
        }

        private T Register<T>(T module) where T : ModuleBase
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            var type = module.GetType();
            if (_modules.Exists(existing => existing.GetType() == type))
                throw new InvalidOperationException($"模块已注册：{type.FullName}");

            // 先记录再初始化，以便失败时也能释放当前模块已取得的资源。
            _modules.Add(module);
            module.OnInitialize();
            return module;
        }

        private void ShutdownModules()
        {
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try { _modules[i].OnShutdown(); }
                catch (Exception error) { Debug.LogException(error, this); }
            }
            _modules.Clear();
        }

        private void OnDestroy()
        {
            ShutdownModules();
        }
    }
}
