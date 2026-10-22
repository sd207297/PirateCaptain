using System;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 绑定信息
    /// </summary>
    internal class BindingInfo
    {
        /// <summary>
        /// 接口类型
        /// </summary>
        public Type InterfaceType { get; set; }

        /// <summary>
        /// 实现类型
        /// </summary>
        public Type ImplementationType { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 生命周期
        /// </summary>
        public LifecycleType Lifecycle { get; set; }

        /// <summary>
        /// 工厂方法
        /// </summary>
        public Func<object> Factory { get; set; }

        /// <summary>
        /// 条件
        /// </summary>
        public Func<bool> Condition { get; set; }

        /// <summary>
        /// 是否满足条件
        /// </summary>
        public bool IsConditionMet => Condition?.Invoke() ?? true;
    }
}