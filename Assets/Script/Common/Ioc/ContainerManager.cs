using System.Collections.Generic;
using UnityEngine;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 容器管理器 - 管理所有ActivityContainer实例
    /// </summary>
    public class ContainerManager : MonoBehaviour
    {
        private static ContainerManager _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        private bool _enableLogging = true;
        private bool _persistAcrossScenes = true;
        private bool _autoInjectOnCreate = true;
        
        private Dictionary<string, BaseMonoContainer> _containers = new Dictionary<string, BaseMonoContainer>();
        private BaseMonoContainer _defaultContainer;
        private const string DEFAULT_CONTAINER_NAME = "DefaultContainer";

        /// <summary>
        /// 单例实例
        /// </summary>
        public static ContainerManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[ContainerManager] 应用程序正在退出，返回 null");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<ContainerManager>();

                        if (_instance == null)
                        {
                            GameObject singleton = new GameObject(typeof(ContainerManager).Name);
                            _instance = singleton.AddComponent<ContainerManager>();
                            DontDestroyOnLoad(singleton);
                        }
                    }

                    return _instance;
                }
            }
        }

        /// <summary>
        /// 默认容器
        /// </summary>
        public BaseMonoContainer DefaultContainer => _defaultContainer;

        /// <summary>
        /// 所有容器数量
        /// </summary>
        public int ContainerCount => _containers.Count;

        /// <summary>
        /// 是否启用日志
        /// </summary>
        public bool EnableLogging => _enableLogging;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                
                if (_enableLogging)
                {
                    Debug.Log("ContainerManager 初始化");
                }
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[ContainerManager] 检测到重复实例，销毁 {gameObject.name}");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 创建默认容器
        /// </summary>
        //private void CreateDefaultContainer()
        //{
        //    if (_defaultContainer == null)
        //    {
        //        _defaultContainer = CreateContainer(DEFAULT_CONTAINER_NAME);
        //    }
        //}

        /// <summary>
        /// 创建或获取容器
        /// </summary>
        /// <param name="name">容器名称</param>
        /// <returns>BaseMonoContainer实例</returns>
        //public BaseMonoContainer GetOrCreateContainer(string name)
        //{
        //    if (string.IsNullOrEmpty(name))
        //    {
        //        name = DEFAULT_CONTAINER_NAME;
        //    }

        //    if (_containers.TryGetValue(name, out var existingContainer))
        //    {
        //        if (_enableLogging)
        //        {
        //            Debug.Log($"获取现有容器: {name}");
        //        }
        //        return existingContainer;
        //    }

        //    return CreateContainer(name);
        //}

        /// <summary>
        /// 创建新容器
        /// </summary>
        /// <param name="name">容器名称</param>
        /// <returns>BaseMonoContainer实例</returns>
        //public BaseMonoContainer CreateContainer(string name)
        //{
        //    if (string.IsNullOrEmpty(name))
        //    {
        //        name = DEFAULT_CONTAINER_NAME;
        //    }

        //    if (_containers.ContainsKey(name))
        //    {
        //        if (_enableLogging)
        //        {
        //            Debug.LogWarning($"容器已存在: {name}");
        //        }
        //        return _containers[name];
        //    }

        //    // 创建新的GameObject
        //    var containerGO = new GameObject($"BaseMonoContainer_{name}");

        //    // 添加BaseMonoContainer组件
        //    BaseMonoContainer baseContainer = containerGO.AddComponent<BaseMonoContainer>();
            
        //    // 添加到管理字典
        //    _containers[name] = baseContainer;
        //    if(baseContainer != null)
        //        baseContainer.name = name;
        //    if (_enableLogging)
        //    {
        //        Debug.Log($"创建新容器: {name}");
        //    }

        //    return baseContainer;
        //}

        /// <summary>
        /// 创建指定类型的容器
        /// </summary>
        /// <typeparam name="T">容器类型，必须继承自BaseMonoContainer</typeparam>
        /// <param name="name">容器名称</param>
        /// <returns>指定类型的容器实例</returns>
        public T CreateContainer<T>(string name) where T : BaseMonoContainer
        {
            if (string.IsNullOrEmpty(name))
            {
                name = typeof(T).Name;
            }

            if (_containers.ContainsKey(name))
            {
                if (_enableLogging)
                {
                    Debug.LogWarning($"容器已存在: {name}");
                }
                return _containers[name] as T;
            }

            // 创建新的GameObject
            var containerGO = new GameObject(name);
            
            // 添加指定类型的容器组件
            var container = containerGO.AddComponent<T>();
            // 添加到管理字典
            _containers[name] = container;
            
            if (_enableLogging)
            {
                Debug.Log($"创建新容器: {name} (类型: {typeof(T).Name})");
            }

            return container;
        }

        /// <summary>
        /// 创建或获取指定类型的容器
        /// </summary>
        /// <typeparam name="T">容器类型，必须继承自BaseMonoContainer</typeparam>
        /// <param name="name">容器名称</param>
        /// <returns>指定类型的容器实例</returns>
        public T GetOrCreateContainer<T>(string name) where T : BaseMonoContainer
        {
            if (string.IsNullOrEmpty(name))
            {
                name = typeof(T).Name;
            }

            if (_containers.TryGetValue(name, out var existingContainer))
            {
                if (_enableLogging)
                {
                    Debug.Log($"获取现有容器: {name}");
                }
                return existingContainer as T;
            }

            return CreateContainer<T>(name);
        }

        /// <summary>
        /// 获取容器
        /// </summary>
        /// <param name="name">容器名称</param>
        /// <returns>BaseMonoContainer实例，如果不存在返回null</returns>
        public BaseMonoContainer GetContainer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return _defaultContainer;
            }

            _containers.TryGetValue(name, out var container);
            return container;
        }

        /// <summary>
        /// 获取指定类型的容器
        /// </summary>
        /// <typeparam name="T">容器类型，必须继承自BaseMonoContainer</typeparam>
        /// <returns>指定类型的容器实例，如果不存在返回null</returns>
        public T GetContainer<T>() where T : BaseMonoContainer
        {
            var typeName = typeof(T).Name;
            
            // 首先尝试通过类型名称查找
            if (_containers.TryGetValue(typeName, out var container))
            {
                return container as T;
            }

            // 如果没找到，遍历所有容器查找匹配的类型
            foreach (var kvp in _containers)
            {
                if (kvp.Value is T)
                {
                    return kvp.Value as T;
                }
            }

            return null;
        }

        /// <summary>
        /// 移除容器
        /// </summary>
        /// <param name="name">容器名称</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveContainer(string name)
        {
            if (string.IsNullOrEmpty(name) || name == DEFAULT_CONTAINER_NAME)
            {
                if (_enableLogging)
                {
                    Debug.LogWarning("不能移除默认容器");
                }
                return false;
            }

            if (_containers.TryGetValue(name, out var container))
            {
                _containers.Remove(name);
                
                if (container != null && container.gameObject != null)
                {
                    Object.Destroy(container.gameObject);
                }

                if (_enableLogging)
                {
                    Debug.Log($"移除容器: {name}");
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取所有容器名称
        /// </summary>
        /// <returns>容器名称列表</returns>
        public List<string> GetAllContainerNames()
        {
            return new List<string>(_containers.Keys);
        }

        /// <summary>
        /// 获取所有容器
        /// </summary>
        /// <returns>所有BaseMonoContainer实例</returns>
        public List<BaseMonoContainer> GetAllContainers()
        {
            return new List<BaseMonoContainer>(_containers.Values);
        }

        /// <summary>
        /// 清理所有容器
        /// </summary>
        public void ClearAllContainers()
        {
            var containerNames = new List<string>(_containers.Keys);
            
            foreach (var name in containerNames)
            {
                if (name != DEFAULT_CONTAINER_NAME)
                {
                    RemoveContainer(name);
                }
            }

            if (_enableLogging)
            {
                Debug.Log("清理所有非默认容器");
            }
        }

        /// <summary>
        /// 检查容器是否存在
        /// </summary>
        /// <param name="name">容器名称</param>
        /// <returns>是否存在</returns>
        public bool HasContainer(string name)
        {
            return _containers.ContainsKey(name);
        }

        /// <summary>
        /// 注入到指定GameObject
        /// </summary>
        /// <param name="target">目标GameObject</param>
        /// <param name="containerName">容器名称，为空则使用默认容器</param>
        public static void InjectInto(GameObject target, string containerName = null)
        {
            var container = string.IsNullOrEmpty(containerName) 
                ? Instance._defaultContainer 
                : Instance.GetContainer(containerName);

            if (container != null && target != null)
            {
                container.Container.InjectInto(target);
            }
        }

        /// <summary>
        /// 注入到指定MonoBehaviour
        /// </summary>
        /// <param name="target">目标MonoBehaviour</param>
        /// <param name="containerName">容器名称，为空则使用默认容器</param>
        public static void InjectInto(MonoBehaviour target, string containerName = null)
        {
            var container = string.IsNullOrEmpty(containerName) 
                ? Instance._defaultContainer 
                : Instance.GetContainer(containerName);

            if (container != null && target != null)
            {
                container.Container.InjectInto(target);
            }
        }


        private void OnDestroy()
        {
            // 清理所有容器
            foreach (var container in _containers.Values)
            {
                if (container != null && container.gameObject != null)
                {
                    Object.Destroy(container.gameObject);
                }
            }
            
            _containers.Clear();
            _defaultContainer = null;

            if (_enableLogging)
            {
                Debug.Log("ContainerManager 销毁，清理所有容器");
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
    }
}