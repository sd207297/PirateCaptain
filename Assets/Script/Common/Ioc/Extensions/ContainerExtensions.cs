using System;
using UnityEngine;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 容器扩展方法
    /// </summary>
    public static class ContainerExtensions
    {
        /// <summary>
        /// 自动注入到MonoBehaviour
        /// </summary>
        /// <param name="container">容器</param>
        /// <param name="behaviour">MonoBehaviour</param>
        public static void InjectInto(this IDependencyContainer container, MonoBehaviour behaviour)
        {
            if (behaviour == null) return;

            container.InjectInto(behaviour);

            // 如果MonoBehaviour实现了IInjectable接口，调用注入完成回调
            if (behaviour is IInjectable injectable)
            {
                injectable.OnInjected();
            }
        }

        /// <summary>
        /// 自动注入到GameObject上的所有MonoBehaviour
        /// </summary>
        /// <param name="container">容器</param>
        /// <param name="gameObject">GameObject</param>
        public static void InjectInto(this IDependencyContainer container, GameObject gameObject)
        {
            if (gameObject == null) return;

            var behaviours = gameObject.GetComponents<MonoBehaviour>();
            foreach (var behaviour in behaviours)
            {
                container.InjectInto(behaviour);
            }
        }

        /// <summary>
        /// 自动注入到场景中的所有MonoBehaviour
        /// </summary>
        /// <param name="container">容器</param>
        public static void InjectIntoScene(this IDependencyContainer container)
        {
            var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var behaviour in behaviours)
            {
                container.InjectInto(behaviour);
            }
        }

        /// <summary>
        /// 绑定单例 - 用于绑定业务逻辑和服务（非 View 类型）
        /// 容器会创建并管理单例实例，整个应用生命周期中只有一个实例
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="container">容器</param>
        /// <param name="name">命名</param>
        /// <returns>容器</returns>
        /// <remarks>
        /// 使用场景：绑定服务、管理器、数据访问层等需要单例的业务逻辑组件
        /// </remarks>
        public static IDependencyContainer BindSingleton<TInterface, TImplementation>(this IDependencyContainer container, string name = null) 
            where TImplementation : class, TInterface
        {
            container.Bind<TInterface, TImplementation>(name).AsSingleton();
            return container;
        }

        /// <summary>
        /// 绑定瞬态 - 用于绑定业务逻辑和服务（非 View 类型）
        /// 每次解析时都会创建新实例
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="container">容器</param>
        /// <param name="name">命名</param>
        /// <returns>容器</returns>
        /// <remarks>
        /// 使用场景：绑定每次都需要新实例的业务逻辑组件
        /// </remarks>
        public static IDependencyContainer BindTransient<TInterface, TImplementation>(this IDependencyContainer container, string name = null) 
            where TImplementation : class, TInterface
        {
            container.Bind<TInterface, TImplementation>(name).AsTransient();
            return container;
        }

        /// <summary>
        /// 绑定作用域 - 用于绑定业务逻辑和服务（非 View 类型）
        /// 在同一个作用域内是单例，不同作用域创建不同实例
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="container">容器</param>
        /// <param name="name">命名</param>
        /// <returns>容器</returns>
        /// <remarks>
        /// 使用场景：绑定需要在特定作用域内共享实例的业务逻辑组件
        /// </remarks>
        public static IDependencyContainer BindScoped<TInterface, TImplementation>(this IDependencyContainer container, string name = null) 
            where TImplementation : class, TInterface
        {
            container.Bind<TInterface, TImplementation>(name).AsScoped();
            return container;
        }

        /// <summary>
        /// 绑定 View 类型 - 用于绑定 UI 视图组件
        /// 注册此 container 需要哪些 View 类型，当对应的 View 在场景中创建时，会自动注册并注入到此 container
        /// </summary>
        /// <typeparam name="TViewInterface">View 接口类型，应继承自 IView</typeparam>
        /// <typeparam name="TViewImplementation">View 实现类型，应继承自 BaseView 并实现 IView</typeparam>
        /// <param name="container">容器</param>
        /// <param name="name">命名</param>
        /// <returns>容器</returns>
        /// <remarks>
        /// 使用场景：
        /// - 在 BaseMonoContainer.BindDependencies() 中使用此方法注册需要的 View 类型
        /// - 当继承自 BaseView 的 View 在场景中创建时，会自动检查所有 container
        /// - 如果某个 container 注册了此 View 类型，View 会自动注册到该 container 并执行依赖注入
        /// 
        /// 示例：
        /// Container.BindView&lt;IMainView, MainView&gt;();
        /// </remarks>
        public static IDependencyContainer BindView<TViewInterface, TViewImplementation>(this IDependencyContainer container, string name = null) 
            where TViewImplementation : class, TViewInterface, IView
        {
            container.BindView<TViewInterface, TViewImplementation>(name);
            return container;
        }

        /// <summary>
        /// 声明本容器需要指定 View 类型（仅具体类型，不绑接口）。
        /// </summary>
        public static IDependencyContainer BindView<TView>(this IDependencyContainer container, string name = null)
            where TView : class, IView
        {
            container.BindView<TView>(name);
            return container;
        }
    }

    /// <summary>
    /// 可注入接口
    /// </summary>
    public interface IInjectable
    {
        /// <summary>
        /// 注入完成回调
        /// </summary>
        void OnInjected();
    }
}