using System;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 依赖注入属性标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false)]
    public class InjectAttribute : Attribute
    {
        /// <summary>
        /// 命名绑定
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否必需
        /// </summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        public InjectAttribute()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">命名绑定</param>
        public InjectAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">命名绑定</param>
        /// <param name="required">是否必需</param>
        public InjectAttribute(string name, bool required)
        {
            Name = name;
            Required = required;
        }
    }
}