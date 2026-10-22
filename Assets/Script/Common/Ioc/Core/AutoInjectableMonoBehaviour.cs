using System;
using UnityEngine;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 自动注入的MonoBehaviour基类
    /// </summary>
    /// <remarks>
    /// [已过时] 请使用 BaseView 替代此类
    /// 
    /// 迁移指南：
    /// - 如果您的类需要 View 功能（自动注册到 container），请继承 BaseView
    /// - 如果您的类不需要 View 功能，但仍需要依赖注入，可以考虑直接使用 BaseView（它提供了所有功能）
    /// 
    /// 示例：
    /// // 旧代码
    /// public class MyComponent : AutoInjectableMonoBehaviour { }
    /// 
    /// // 新代码（推荐）
    /// public interface IMyComponent : IView { }
    /// public class MyComponent : BaseView, IMyComponent { }
    /// 
    /// 注意：此类将在未来版本中移除，请尽快迁移到 BaseView
    /// </remarks>
    [Obsolete("AutoInjectableMonoBehaviour 已过时，请使用 BaseView 替代。BaseView 提供了所有功能并支持 View 自动注册机制。", false)]
    public abstract class AutoInjectableMonoBehaviour : MonoBehaviour
    {
        [Header("自动注入配置")]
        [SerializeField] private bool _enableAutoInject = true;
        [SerializeField] private string _containerName = "";

        /// <summary>
        /// 是否启用自动注入
        /// </summary>
        public bool EnableAutoInject => _enableAutoInject;

        /// <summary>
        /// 容器名称
        /// </summary>
        public string ContainerName => _containerName;

        protected virtual void Awake()
        {
            if (_enableAutoInject)
            {
                PerformAutoInjection();
            }
            OnAwake();
        }

        protected virtual void OnAwake()
        {

        }

        /// <summary>
        /// 执行自动注入
        /// </summary>
        public void PerformAutoInjection()
        {
            try
            {
                // 获取目标容器
                var container = GetTargetContainer();

                if (container == null)
                {
                    return;
                }

                // 执行注入
                container.Container.InjectInto(this);

                // 调用注入完成回调
                OnInjectionCompleted();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{name}] 自动注入失败: {ex.Message}");
                OnInjectionFailed(ex);
            }
        }

        /// <summary>
        /// 获取目标容器
        /// </summary>
        /// <returns>目标BaseMonoContainer</returns>
        protected virtual BaseMonoContainer GetTargetContainer()
        {
            // 如果指定了容器名称，使用指定容器
            if (!string.IsNullOrEmpty(_containerName))
            {
                return ContainerManager.Instance.GetContainer(_containerName);
            }

            // 否则使用默认容器
            return null;
        }

        /// <summary>
        /// 注入完成回调
        /// </summary>
        protected virtual void OnInjectionCompleted()
        {
            // 子类可以重写此方法
        }

        /// <summary>
        /// 注入失败回调
        /// </summary>
        /// <param name="exception">异常信息</param>
        protected virtual void OnInjectionFailed(System.Exception exception)
        {
            // 子类可以重写此方法处理注入失败
        }

        /// <summary>
        /// 手动执行注入
        /// </summary>
        /// <param name="containerName">容器名称</param>
        public void ManualInject(string containerName = null)
        {
            try
            {
                BaseMonoContainer container;

                if (!string.IsNullOrEmpty(containerName))
                {
                    container = ContainerManager.Instance.GetContainer(containerName);
                }
                else
                {
                    container = GetTargetContainer();
                }

                if (container != null)
                {
                    container.Container.InjectInto(this);
                }
                else
                {
                    Debug.LogWarning($"[{name}] 未找到容器: {containerName ?? "默认容器"}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{name}] 手动注入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否已注入
        /// </summary>
        /// <returns>是否已注入</returns>
        public virtual bool IsInjected()
        {
            // 子类可以重写此方法实现自定义的注入检查逻辑
            return true;
        }

        /// <summary>
        /// 设置容器名称
        /// </summary>
        /// <param name="containerName">容器名称</param>
        public void SetContainerName(string containerName)
        {
            _containerName = containerName;
        }

        /// <summary>
        /// 启用/禁用自动注入
        /// </summary>
        /// <param name="enable">是否启用</param>
        public void SetAutoInject(bool enable)
        {
            _enableAutoInject = enable;
        }
    }
}