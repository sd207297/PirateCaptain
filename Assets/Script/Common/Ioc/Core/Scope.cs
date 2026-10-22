using System;
using System.Collections.Concurrent;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 作用域实现（线程安全）
    /// </summary>
    public class Scope : IScope
    {
        // ⭐ 线程安全：作用域内的实例字典
        // 注意：虽然作用域通常是单线程使用，但为了安全起见，使用线程安全集合
        private readonly ConcurrentDictionary<string, object> _instances = new ConcurrentDictionary<string, object>();
        private bool _disposed = false;

        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsDisposed => _disposed;

        public object GetInstance(string key)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Scope));
            }

            // ⭐ 线程安全：TryGetValue 是线程安全的
            return _instances.TryGetValue(key, out var instance) ? instance : null;
        }

        public void SetInstance(string key, object instance)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Scope));
            }

            // ⭐ 线程安全：索引器操作是线程安全的
            _instances[key] = instance;
        }

        public bool ContainsInstance(string key)
        {
            if (_disposed)
            {
                return false;
            }

            // ⭐ 线程安全：ContainsKey 是线程安全的
            return _instances.ContainsKey(key);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // 释放作用域内的所有实例
            foreach (var instance in _instances.Values)
            {
                if (instance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"Error disposing instance {instance.GetType().Name}: {ex.Message}");
                    }
                }
            }

            _instances.Clear();
            _disposed = true;
        }
    }
}