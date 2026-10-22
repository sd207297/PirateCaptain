using System;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 作用域接口
    /// </summary>
    public interface IScope : IDisposable
    {
        /// <summary>
        /// 作用域ID
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 是否已释放
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// 获取作用域内的实例
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>实例</returns>
        object GetInstance(string key);

        /// <summary>
        /// 设置作用域内的实例
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="instance">实例</param>
        void SetInstance(string key, object instance);

        /// <summary>
        /// 检查是否包含实例
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>是否包含</returns>
        bool ContainsInstance(string key);
    }
}