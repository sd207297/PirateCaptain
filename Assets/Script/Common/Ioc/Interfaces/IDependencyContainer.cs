using System;
using System.Collections.Generic;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 依赖注入容器接口
    /// </summary>
    public interface IDependencyContainer
    {
        /// <summary>
        /// 绑定接口到实现类 - 用于绑定业务逻辑、服务、数据模型等
        /// 此方法用于绑定非 View 类型的依赖，如服务、管理器、数据访问层等
        /// 容器会负责创建和管理这些实例的生命周期
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="name">命名绑定</param>
        /// <returns>绑定配置</returns>
        /// <remarks>
        /// 使用场景：
        /// - 绑定服务接口到服务实现（如 IUserService -> UserService）
        /// - 绑定数据访问层（如 IRepository -> Repository）
        /// - 绑定业务逻辑管理器（如 IGameManager -> GameManager）
        /// 注意：不要使用此方法绑定 View，View 应使用 BindView 方法
        /// </remarks>
        IBindingConfiguration Bind<TInterface, TImplementation>(string name = null) where TImplementation : class, TInterface;

        /// <summary>
        /// 绑定接口到实例 - 用于绑定已存在的实例
        /// 此方法用于绑定已经创建好的实例，通常用于单例或预创建的实例
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <param name="instance">已存在的实例</param>
        /// <param name="name">命名绑定</param>
        /// <returns>绑定配置</returns>
        /// <remarks>
        /// 使用场景：
        /// - 绑定已创建的单例实例
        /// - 绑定 Unity MonoBehaviour 实例（非 View 类型）
        /// - 绑定预配置的实例
        /// </remarks>
        IBindingConfiguration BindInstance<TInterface>(TInterface instance, string name = null) where TInterface : class;

        /// <summary>
        /// 绑定类型到自身
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="name">命名绑定</param>
        /// <returns>绑定配置</returns>
        IBindingConfiguration BindSelf<T>(string name = null) where T : class;

        /// <summary>
        /// 解析依赖
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="name">命名绑定</param>
        /// <returns>实例</returns>
        T Resolve<T>(string name = null);

        /// <summary>
        /// 解析依赖
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="name">命名绑定</param>
        /// <returns>实例</returns>
        object Resolve(Type type, string name = null);

        /// <summary>
        /// 尝试解析依赖
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="instance">输出实例</param>
        /// <param name="name">命名绑定</param>
        /// <returns>是否解析成功</returns>
        bool TryResolve<T>(out T instance, string name = null);

        /// <summary>
        /// 注入依赖到对象
        /// </summary>
        /// <param name="target">目标对象</param>
        void InjectInto(object target);

        /// <summary>
        /// 检查是否已绑定
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="name">命名绑定</param>
        /// <returns>是否已绑定</returns>
        bool IsBound<T>(string name = null);

        /// <summary>
        /// 清除所有绑定
        /// </summary>
        void Clear();

        /// <summary>
        /// 绑定 View 类型 - 用于绑定 UI 视图组件
        /// 注册此 container 需要哪些 View 类型，当对应的 View 在场景中创建时，会自动注册并注入到此 container
        /// </summary>
        /// <typeparam name="TViewInterface">View 接口类型，应继承自 IView</typeparam>
        /// <typeparam name="TViewImplementation">View 实现类型，应继承自 BaseView 并实现 IView</typeparam>
        /// <param name="name">命名绑定</param>
        /// <returns>绑定配置</returns>
        /// <remarks>
        /// 使用场景：
        /// - 在 BaseMonoContainer.BindDependencies() 中注册需要的 View 类型
        /// - 当继承自 BaseView 的 View 在场景中创建时，会自动检查所有 container
        /// - 如果某个 container 注册了此 View 类型，View 会自动注册到该 container 并执行依赖注入
        /// 
        /// 示例：
        /// Container.BindView&lt;IMainView, MainView&gt;();
        /// </remarks>
        IBindingConfiguration BindView<TViewInterface, TViewImplementation>(string name = null) 
            where TViewImplementation : class, TViewInterface, IView;

        /// <summary>
        /// 声明本容器需要指定 View 类型（仅具体类型，不绑接口）。View 创建时会自动注册并注入。
        /// </summary>
        void BindView<TView>(string name = null) where TView : class, IView;

        /// <summary>
        /// 检查指定的 View 类型是否已注册
        /// </summary>
        /// <param name="viewType">View 类型</param>
        /// <returns>是否已注册</returns>
        bool IsViewTypeRegistered(System.Type viewType);

        /// <summary>
        /// 绑定 View 实例到容器（当 View 创建时调用）
        /// </summary>
        /// <param name="viewType">View 类型</param>
        /// <param name="viewInstance">View 实例</param>
        void BindViewInstance(System.Type viewType, object viewInstance);
    }
}