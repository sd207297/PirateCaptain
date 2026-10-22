using System;
using UnityEngine;

namespace ForeverNine.IoC
{
    /// <summary>
    /// View 基类 - 所有需要依赖注入的 MonoBehaviour 组件都应该继承此类
    /// 当 View 在场景中创建时，会自动检查所有 container 是否需要此 View，如果需要则自动注册并注入
    /// </summary>
    /// <remarks>
    /// 此类型替代了 AutoInjectableMonoBehaviour，提供了所有原有功能并增加了 View 自动注册机制。
    /// 
    /// 使用方式：
    /// 1. 创建 View 接口，继承 IView：public interface IMainView : IView { }
    /// 2. 创建 View 实现类，继承 BaseView 并实现接口：public class MainView : BaseView, IMainView { }
    /// 3. 在 BaseMonoContainer.BindDependencies() 中注册：Container.BindView&lt;IMainView, MainView&gt;();
    /// 4. 当 MainView 在场景中创建时，会自动注册到对应的 container 并执行依赖注入
    /// 
    /// 特性：
    /// - 自动发现：View 创建时自动检查所有 container
    /// - 自动注册：如果 container 注册了此 View 类型，自动注册到该 container
    /// - 自动注入：自动执行依赖注入，支持 [Inject] 特性
    /// - 向后兼容：完全兼容 AutoInjectableMonoBehaviour 的所有功能
    /// 
    /// 从 AutoInjectableMonoBehaviour 迁移：
    /// - 如果您的类需要 View 功能，请创建接口并实现 IView，然后使用 BindView 注册
    /// - 如果您的类不需要 View 功能，可以直接继承 BaseView，它会正常工作
    /// </remarks>
    public abstract class BaseView : MonoBehaviour, IView
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
            // 先检查并注册到 container
            CheckAndRegisterToContainers();
            
            // 执行自动注入
            if (_enableAutoInject)
            {
                PerformAutoInjection();
            }
            
            OnAwake();
        }

        protected virtual void OnAwake()
        {
            // 子类可以重写此方法
        }

        /// <summary>
        /// 检查所有 container 是否需要此 View，如果需要则注册并注入
        /// </summary>
        private void CheckAndRegisterToContainers()
        {
            var viewType = GetType();
            var allContainers = ContainerManager.Instance.GetAllContainers();

            foreach (var container in allContainers)
            {
                if (container == null || !container.IsInitialized)
                {
                    continue;
                }

                // 检查此 container 是否需要此 View 类型
                if (container.Container.IsViewTypeRegistered(viewType))
                {
                    // 将 View 实例绑定到 container 中
                    container.Container.BindViewInstance(viewType, this);
                    
                    // 设置容器名称（这样 PerformAutoInjection 就能找到正确的容器）
                    SetContainerName(container.gameObject.name);
                    
                    Debug.Log($"[{GetType().Name}] 自动注册到容器: {container.gameObject.name}");
                }
            }
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
            return ContainerManager.Instance.DefaultContainer;
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

