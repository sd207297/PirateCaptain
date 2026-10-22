using System;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 绑定配置实现
    /// </summary>
    internal class BindingConfiguration : IBindingConfiguration
    {
        private readonly BindingInfo _bindingInfo;

        public BindingConfiguration(BindingInfo bindingInfo)
        {
            _bindingInfo = bindingInfo;
        }

        public IBindingConfiguration AsSingleton()
        {
            _bindingInfo.Lifecycle = LifecycleType.Singleton;
            return this;
        }

        public IBindingConfiguration AsTransient()
        {
            _bindingInfo.Lifecycle = LifecycleType.Transient;
            return this;
        }

        public IBindingConfiguration AsScoped()
        {
            _bindingInfo.Lifecycle = LifecycleType.Scoped;
            return this;
        }

        public IBindingConfiguration Named(string name)
        {
            _bindingInfo.Name = name ?? string.Empty;
            return this;
        }

        public IBindingConfiguration When(Func<bool> condition)
        {
            _bindingInfo.Condition = condition;
            return this;
        }
    }
}