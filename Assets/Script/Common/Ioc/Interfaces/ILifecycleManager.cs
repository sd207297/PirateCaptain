using System;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 生命周期类型
    /// </summary>
    public enum LifecycleType
    {
        /// <summary>
        /// 瞬态 - 每次解析都创建新实例
        /// </summary>
        Transient,
        
        /// <summary>
        /// 作用域 - 在同一作用域内共享实例
        /// </summary>
        Scoped,
        
        /// <summary>
        /// 单例 - 全局唯一实例
        /// </summary>
        Singleton
    }

    /// <summary>
    /// 生命周期管理器接口
    /// </summary>
    public interface ILifecycleManager
    {
        /// <summary>
        /// 获取实例
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="name">命名</param>
        /// <param name="lifecycle">生命周期</param>
        /// <param name="factory">工厂方法</param>
        /// <returns>实例</returns>
        object GetInstance(Type type, string name, LifecycleType lifecycle, Func<object> factory);

        /// <summary>
        /// 创建作用域
        /// </summary>
        /// <returns>作用域</returns>
        IDisposable CreateScope();

        /// <summary>
        /// 获取当前作用域
        /// </summary>
        /// <returns>当前作用域</returns>
        IScope GetCurrentScope();

        /// <summary>
        /// 清除单例实例
        /// </summary>
        void ClearSingletons();
    }
}