using System;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 反射辅助接口
    /// </summary>
    public interface IReflectionHelper
    {
        /// <summary>
        /// 注入字段
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="container">容器</param>
        void InjectFields(object target, IDependencyContainer container);

        /// <summary>
        /// 注入属性
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="container">容器</param>
        void InjectProperties(object target, IDependencyContainer container);

        /// <summary>
        /// 注入方法
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="container">容器</param>
        void InjectMethods(object target, IDependencyContainer container);
    }
}