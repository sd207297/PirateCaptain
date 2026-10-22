using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 生命周期管理器实现（线程安全）
    /// </summary>
    public class LifecycleManager : ILifecycleManager
    {
        // ⭐ 线程安全的单例字典
        private readonly ConcurrentDictionary<string, object> _singletons = new ConcurrentDictionary<string, object>();
        private readonly ThreadLocal<IScope> _currentScope = new ThreadLocal<IScope>();

        public object GetInstance(Type type, string name, LifecycleType lifecycle, Func<object> factory)
        {
            var key = GetKey(type, name);

            switch (lifecycle)
            {
                case LifecycleType.Singleton:
                    return GetSingleton(key, factory);

                case LifecycleType.Scoped:
                    return GetScoped(key, factory);

                case LifecycleType.Transient:
                default:
                    return factory();
            }
        }

        public IDisposable CreateScope()
        {
            var scope = new Scope();
            _currentScope.Value = scope;
            return scope;
        }

        public IScope GetCurrentScope()
        {
            return _currentScope.Value;
        }

        public void ClearSingletons()
        {
            // ⭐ 线程安全：ConcurrentDictionary 的 Clear 是线程安全的
            _singletons.Clear();
        }

        private object GetSingleton(string key, Func<object> factory)
        {
            // ⭐ 线程安全：GetOrAdd 确保即使多线程同时访问，也只会创建一次实例
            // 这是双重检查锁定模式的线程安全版本
            return _singletons.GetOrAdd(key, _ => factory());
        }

        private object GetScoped(string key, Func<object> factory)
        {
            var scope = GetCurrentScope();
            if (scope == null)
            {
                throw new InvalidOperationException("No active scope found for scoped dependency");
            }

            if (scope.ContainsInstance(key))
            {
                return scope.GetInstance(key);
            }

            var instance = factory();
            scope.SetInstance(key, instance);
            return instance;
        }

        private string GetKey(Type type, string name)
        {
            return $"{type.FullName}:{name}";
        }
    }
}