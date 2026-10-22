using System;
using UnityEngine;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 基础MonoBehaviour容器 - 用户需要继承此类实现自己的容器
    /// </summary>
    public abstract class BaseMonoContainer : MonoBehaviour
    {
        private IDependencyContainer _container;

        /// <summary>
        /// 容器实例
        /// </summary>
        public IDependencyContainer Container
        {
            get
            {
                if (_container == null)
                {
                    InitializeContainer();
                }
                return _container;
            }
        }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _container != null;

        protected virtual void Awake()
        {
            DontDestroyOnLoad(gameObject);
            InitializeContainer();
            OnContainerInitialized();

            // 注入到 MonoBehaviour 本身，而不是 GameObject
            Container.InjectInto(this);
            OnAwake();

           // EventCenter.Invoke(EventNames.ContainerCreate, gameObject.name);
        }

        protected virtual void OnAwake()
        {
            RegisterEvents();
            Init();
        }

        /// <summary>
        /// 注册事件 - 子类重写此方法注册需要监听的消息
        /// </summary>
        protected virtual void RegisterEvents()
        {
        }

        /// <summary>
        /// 容器初始化结束后的业务初始化 - 子类重写此方法初始化业务内容（依赖已注入、事件已注册）
        /// </summary>
        protected virtual void Init()
        {
        }

        /// <summary>
        /// 注销事件 - 子类重写此方法注销在 RegisterEvents 中注册的监听
        /// </summary>
        protected virtual void UnregisterEvents()
        {
        }

        private void InitializeContainer()
        {
            if (_container == null)
            {
                _container = new DependencyContainer();
                Debug.Log($"[{GetType().Name}] IoC Container initialized");
            }
        }

        /// <summary>
        /// 容器初始化完成后的回调 - 子类重写此方法进行绑定
        /// </summary>
        protected virtual void OnContainerInitialized()
        {
            // 子类重写此方法进行具体的依赖绑定
            BindDependencies();
        }

        /// <summary>
        /// 绑定依赖 - 子类重写此方法实现具体的绑定逻辑
        /// </summary>
        protected abstract void BindDependencies();

        /// <summary>
        /// 重置容器
        /// </summary>
        public void Reset()
        {
            _container?.Clear();
            _container = null;
        }

        /// <summary>
        /// 设置自定义容器
        /// </summary>
        /// <param name="container">容器</param>
        public void SetContainer(IDependencyContainer container)
        {
            _container = container;
        }

        private void OnDestroy()
        {
            UnregisterEvents();
            _container?.Clear();
            _container = null;
        }
    }
}