using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 循环依赖异常
    /// </summary>
    public class CircularDependencyException : InvalidOperationException
    {
        public CircularDependencyException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 依赖注入容器实现
    /// </summary>
    public class DependencyContainer : IDependencyContainer
    {
        // ⭐ 线程安全的绑定字典
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, BindingInfo>> _bindings = 
            new ConcurrentDictionary<Type, ConcurrentDictionary<string, BindingInfo>>();
        
        // ⭐ 线程安全的 View 类型集合
        private readonly ConcurrentDictionary<Type, bool> _registeredViewTypes = 
            new ConcurrentDictionary<Type, bool>(); // 记录注册的 View 类型
        
        // ⭐ 线程安全的 View 类型映射
        private readonly ConcurrentDictionary<Type, Type> _viewTypeMapping = 
            new ConcurrentDictionary<Type, Type>(); // 记录 View 实现类型到接口类型的映射
        private readonly ILifecycleManager _lifecycleManager;
        private readonly IReflectionHelper _reflectionHelper;
        
        // 循环依赖检测：使用 ThreadLocal 支持多线程环境
        private readonly ThreadLocal<Stack<Type>> _resolvingStack = new ThreadLocal<Stack<Type>>(() => new Stack<Type>());
        
        // ⭐ 缓存构造函数信息（按类型缓存构造函数列表）- 线程安全
        private static readonly ConcurrentDictionary<Type, ConstructorInfo[]> _constructorCache = 
            new ConcurrentDictionary<Type, ConstructorInfo[]>();
        
        // ⭐ 缓存参数信息（按构造函数缓存参数列表）- 线程安全
        private static readonly ConcurrentDictionary<ConstructorInfo, ParameterInfo[]> _parameterCache = 
            new ConcurrentDictionary<ConstructorInfo, ParameterInfo[]>();

        public DependencyContainer()
        {
            _lifecycleManager = new LifecycleManager();
            _reflectionHelper = new ReflectionHelper();
        }

        public IBindingConfiguration Bind<TInterface, TImplementation>(string name = null) where TImplementation : class, TInterface
        {
            var interfaceType = typeof(TInterface);
            var implementationType = typeof(TImplementation);
            name = name ?? string.Empty;

            // ⭐ 线程安全：获取或创建内部字典
            var nameBindings = _bindings.GetOrAdd(interfaceType, _ => new ConcurrentDictionary<string, BindingInfo>());

            var bindingInfo = new BindingInfo
            {
                InterfaceType = interfaceType,
                ImplementationType = implementationType,
                Name = name,
                Lifecycle = LifecycleType.Singleton,
                Factory = () => CreateInstance(implementationType)
            };

            nameBindings[name] = bindingInfo;

            return new BindingConfiguration(bindingInfo);
        }

        public IBindingConfiguration BindInstance<TInterface>(TInterface instance, string name = null) where TInterface : class
        {
            var interfaceType = typeof(TInterface);
            name = name ?? string.Empty;

            // ⭐ 线程安全：获取或创建内部字典
            var nameBindings = _bindings.GetOrAdd(interfaceType, _ => new ConcurrentDictionary<string, BindingInfo>());

            var bindingInfo = new BindingInfo
            {
                InterfaceType = interfaceType,
                ImplementationType = instance.GetType(),
                Name = name,
                Lifecycle = LifecycleType.Singleton,
                Factory = () => instance
            };

            nameBindings[name] = bindingInfo;

            return new BindingConfiguration(bindingInfo);
        }

        public IBindingConfiguration BindSelf<T>(string name = null) where T : class
        {
            return Bind<T, T>(name);
        }

        public T Resolve<T>(string name = null)
        {
            return (T)Resolve(typeof(T), name);
        }

        public object Resolve(Type type, string name = null)
        {
            name = name ?? string.Empty;

            // ⭐ 线程安全：使用 TryGetValue
            if (!_bindings.TryGetValue(type, out var nameBindings) || 
                !nameBindings.TryGetValue(name, out var binding))
            {
                throw new InvalidOperationException($"No binding found for type {type.Name} with name '{name}'");
            }

            // 循环依赖检测
            var resolvingStack = _resolvingStack.Value;
            if (resolvingStack.Contains(type))
            {
                // 构建循环依赖路径信息
                var path = new List<Type>(resolvingStack);
                path.Add(type); // 添加当前类型以显示完整循环
                
                var pathString = string.Join(" -> ", path.Select(t => t.Name));
                throw new CircularDependencyException(
                    $"检测到循环依赖！\n" +
                    $"依赖链: {pathString}\n" +
                    $"类型: {type.Name} (name: '{name}')");
            }

            // 将当前类型压入栈
            resolvingStack.Push(type);

            try
            {
                return _lifecycleManager.GetInstance(type, name, binding.Lifecycle, binding.Factory);
            }
            finally
            {
                // 解析完成后从栈中弹出
                resolvingStack.Pop();
            }
        }

        public bool TryResolve<T>(out T instance, string name = null)
        {
            try
            {
                instance = Resolve<T>(name);
                return true;
            }
            catch
            {
                instance = default(T);
                return false;
            }
        }

        public void InjectInto(object target)
        {
            if (target == null) return;

            var type = target.GetType();
            _reflectionHelper.InjectFields(target, this);
            _reflectionHelper.InjectProperties(target, this);
            _reflectionHelper.InjectMethods(target, this);
        }

        public bool IsBound<T>(string name = null)
        {
            name = name ?? string.Empty;
            // ⭐ 线程安全：使用 TryGetValue
            return _bindings.TryGetValue(typeof(T), out var nameBindings) && 
                   nameBindings.ContainsKey(name);
        }

        public void Clear()
        {
            _bindings.Clear();
            _registeredViewTypes.Clear();
            _viewTypeMapping.Clear();
            _lifecycleManager.ClearSingletons();
            
            // 清理循环依赖检测栈
            if (_resolvingStack.IsValueCreated)
            {
                _resolvingStack.Value.Clear();
            }
        }
        
        /// <summary>
        /// 清理所有反射缓存（包括构造函数、字段、属性、方法缓存）
        /// </summary>
        public static void ClearAllCaches()
        {
            _constructorCache.Clear();
            _parameterCache.Clear();
            ReflectionHelper.ClearCache();
        }

        /// <summary>
        /// 绑定 View 类型 - 用于绑定 UI 视图组件
        /// 注册此 container 需要哪些 View 类型，当对应的 View 在场景中创建时，会自动注册并注入到此 container
        /// </summary>
        public IBindingConfiguration BindView<TViewInterface, TViewImplementation>(string name = null) 
            where TViewImplementation : class, TViewInterface, IView
        {
            var viewInterfaceType = typeof(TViewInterface);
            var viewImplementationType = typeof(TViewImplementation);
            
            // 验证接口类型也应该实现 IView（可选，但建议）
            if (!typeof(IView).IsAssignableFrom(viewInterfaceType))
            {
                Debug.LogWarning($"View 接口 {viewInterfaceType.Name} 应该继承自 IView，建议添加 IView 约束");
            }
            
            // ⭐ 线程安全：注册 View 类型（记录此 container 需要此 View 类型）
            // 同时注册接口类型和实现类型，以便后续检查
            _registeredViewTypes.TryAdd(viewInterfaceType, true);
            _registeredViewTypes.TryAdd(viewImplementationType, true);
            
            // ⭐ 线程安全：记录实现类型到接口类型的映射
            _viewTypeMapping.TryAdd(viewImplementationType, viewInterfaceType);
            
            // 同时进行正常的绑定（当 View 实例被注册后，可以通过接口解析）
            return Bind<TViewInterface, TViewImplementation>(name);
        }

        /// <summary>
        /// 声明本容器需要指定 View 类型（仅具体类型，不绑接口）。
        /// </summary>
        public void BindView<TView>(string name = null) where TView : class, IView
        {
            _registeredViewTypes.TryAdd(typeof(TView), true);
        }

        public bool IsViewTypeRegistered(Type viewType)
        {
            if (viewType == null)
            {
                return false;
            }

            // ⭐ 线程安全：检查是否直接注册了此类型
            if (_registeredViewTypes.ContainsKey(viewType))
            {
                return true;
            }

            // ⭐ 线程安全：检查 viewType 是否实现了已注册的接口类型
            // 或者 viewType 是否是已注册的实现类型的子类
            foreach (var registeredType in _registeredViewTypes.Keys)
            {
                // 如果注册的是接口，检查 viewType 是否实现了此接口
                if (registeredType.IsInterface && registeredType.IsAssignableFrom(viewType))
                {
                    return true;
                }
                
                // 如果注册的是类，检查 viewType 是否是此类的子类或相同类型
                if (!registeredType.IsInterface && (viewType == registeredType || viewType.IsSubclassOf(registeredType)))
                {
                    return true;
                }
            }

            return false;
        }

        public void BindViewInstance(Type viewType, object viewInstance)
        {
            if (viewType == null || viewInstance == null)
            {
                return;
            }

            // 查找对应的接口类型（如果注册时使用的是接口）
            Type interfaceType = null;
            if (_viewTypeMapping.TryGetValue(viewType, out interfaceType))
            {
                // 使用接口类型绑定
                var method = typeof(IDependencyContainer).GetMethod("BindInstance");
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(interfaceType);
                    genericMethod.Invoke(this, new object[] { viewInstance, null });
                }
            }
            else
            {
                // 如果没有找到接口类型，直接使用 viewType 绑定
                var method = typeof(IDependencyContainer).GetMethod("BindInstance");
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(viewType);
                    genericMethod.Invoke(this, new object[] { viewInstance, null });
                }
            }
        }

        /// <summary>
        /// 创建作用域
        /// </summary>
        /// <returns>作用域</returns>
        public IDisposable CreateScope()
        {
            return _lifecycleManager.CreateScope();
        }

        private object CreateInstance(Type type)
        {
            try
            {
                // ⭐ 从缓存获取构造函数信息（线程安全）
                // GetOrAdd 确保即使多线程同时访问，也只会创建一次
                var constructors = _constructorCache.GetOrAdd(type, t =>
                {
                    // 第一次：查找所有构造函数并按参数数量排序
                    return t.GetConstructors()
                        .OrderByDescending(c => c.GetParameters().Length)
                        .ToArray();
                });

                foreach (var constructor in constructors)
                {
                    if (TryCreateInstanceWithConstructor(constructor, out var instance))
                    {
                        InjectInto(instance);
                        return instance;
                    }
                }

                // 如果没有找到合适的构造函数，尝试无参构造函数
                var instance2 = Activator.CreateInstance(type);
                InjectInto(instance2);
                return instance2;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create instance of type {type.Name}: {ex.Message}");
                throw;
            }
        }

        private bool TryCreateInstanceWithConstructor(ConstructorInfo constructor, out object instance)
        {
            instance = null;
            
            // ⭐ 从缓存获取参数信息（线程安全）
            var parameters = _parameterCache.GetOrAdd(constructor, c => c.GetParameters());
            
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var injectAttr = parameters[i].GetCustomAttribute<InjectAttribute>();
                var name = injectAttr?.Name;

                try
                {
                    args[i] = Resolve(paramType, name);
                }
                catch
                {
                    if (injectAttr?.Required != false)
                    {
                        return false;
                    }
                    args[i] = GetDefaultValue(paramType);
                }
            }

            try
            {
                instance = constructor.Invoke(args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}