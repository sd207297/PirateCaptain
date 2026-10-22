using UnityEngine;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 注入辅助工具类
    /// </summary>
    public static class InjectionHelper
    {
        /// <summary>
        /// 为GameObject及其子对象执行依赖注入
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <param name="containerName">容器名称，为空则使用默认容器</param>
        public static void InjectIntoGameObject(GameObject gameObject, string containerName = null)
        {
            if (gameObject == null) return;

            var container = GetContainer(containerName);
            if (container != null)
            {
                container.Container.InjectInto(gameObject);
                Debug.Log($"[InjectionHelper] 已为 {gameObject.name} 执行依赖注入");
            }
            else
            {
                Debug.LogWarning($"[InjectionHelper] 未找到容器: {containerName ?? "默认容器"}");
            }
        }

        /// <summary>
        /// 为MonoBehaviour执行依赖注入
        /// </summary>
        /// <param name="monoBehaviour">目标MonoBehaviour</param>
        /// <param name="containerName">容器名称，为空则使用默认容器</param>
        public static void InjectIntoMonoBehaviour(MonoBehaviour monoBehaviour, string containerName = null)
        {
            if (monoBehaviour == null) return;

            var container = GetContainer(containerName);
            if (container != null)
            {
                container.Container.InjectInto(monoBehaviour);
                Debug.Log($"[InjectionHelper] 已为 {monoBehaviour.name} 执行依赖注入");
            }
            else
            {
                Debug.LogWarning($"[InjectionHelper] 未找到容器: {containerName ?? "默认容器"}");
            }
        }

        /// <summary>
        /// 为场景中所有需要注入的对象执行依赖注入
        /// </summary>
        /// <param name="containerName">容器名称，为空则使用默认容器</param>
        public static void InjectIntoScene(string containerName = null)
        {
            var container = GetContainer(containerName);
            if (container == null)
            {
                Debug.LogWarning($"[InjectionHelper] 未找到容器: {containerName ?? "默认容器"}");
                return;
            }

            // 查找所有MonoBehaviour
            var allMonoBehaviours = Object.FindObjectsOfType<MonoBehaviour>();
            int injectedCount = 0;

            foreach (var mb in allMonoBehaviours)
            {
                if (mb != null && mb.gameObject != null)
                {
                    try
                    {
                        container.Container.InjectInto(mb);
                        injectedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[InjectionHelper] 注入失败: {mb.name}, 错误: {ex.Message}");
                    }
                }
            }

            Debug.Log($"[InjectionHelper] 场景注入完成，共注入 {injectedCount} 个对象");
        }

        /// <summary>
        /// 为指定类型的所有对象执行依赖注入
        /// </summary>
        /// <typeparam name="T">MonoBehaviour类型</typeparam>
        /// <param name="containerName">容器名称，为空则使用默认容器</param>
        public static void InjectIntoType<T>(string containerName = null) where T : MonoBehaviour
        {
            var container = GetContainer(containerName);
            if (container == null)
            {
                Debug.LogWarning($"[InjectionHelper] 未找到容器: {containerName ?? "默认容器"}");
                return;
            }

            var objects = Object.FindObjectsOfType<T>();
            int injectedCount = 0;

            foreach (var obj in objects)
            {
                if (obj != null)
                {
                    try
                    {
                        container.Container.InjectInto(obj);
                        injectedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[InjectionHelper] 注入失败: {obj.name}, 错误: {ex.Message}");
                    }
                }
            }

            Debug.Log($"[InjectionHelper] 类型 {typeof(T).Name} 注入完成，共注入 {injectedCount} 个对象");
        }

        /// <summary>
        /// 获取容器
        /// </summary>
        /// <param name="containerName">容器名称</param>
        /// <returns>容器实例</returns>
        private static BaseMonoContainer GetContainer(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
            {
                return ContainerManager.Instance.DefaultContainer;
            }
            else
            {
                return ContainerManager.Instance.GetContainer(containerName);
            }
        }
    }
}