using System;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 绑定配置接口
    /// </summary>
    public interface IBindingConfiguration
    {
        /// <summary>
        /// 设置为单例
        /// </summary>
        /// <returns>绑定配置</returns>
        IBindingConfiguration AsSingleton();

        /// <summary>
        /// 设置为瞬态
        /// </summary>
        /// <returns>绑定配置</returns>
        IBindingConfiguration AsTransient();

        /// <summary>
        /// 设置为作用域
        /// </summary>
        /// <returns>绑定配置</returns>
        IBindingConfiguration AsScoped();

        /// <summary>
        /// 设置命名
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>绑定配置</returns>
        IBindingConfiguration Named(string name);

        /// <summary>
        /// 设置条件绑定
        /// </summary>
        /// <param name="condition">条件</param>
        /// <returns>绑定配置</returns>
        IBindingConfiguration When(Func<bool> condition);
    }
}