using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ForeverNine.IoC
{
    /// <summary>
    /// 反射辅助实现（带缓存优化，线程安全）
    /// </summary>
    public class ReflectionHelper : IReflectionHelper
    {
        // ⭐ 缓存字段信息（按类型缓存需要注入的字段）- 线程安全
        private static readonly ConcurrentDictionary<Type, List<FieldInfo>> _injectableFieldsCache = 
            new ConcurrentDictionary<Type, List<FieldInfo>>();
        
        // ⭐ 缓存属性信息（按类型缓存需要注入的属性）- 线程安全
        private static readonly ConcurrentDictionary<Type, List<PropertyInfo>> _injectablePropertiesCache = 
            new ConcurrentDictionary<Type, List<PropertyInfo>>();
        
        // ⭐ 缓存方法信息（按类型缓存需要注入的方法）- 线程安全
        private static readonly ConcurrentDictionary<Type, List<MethodInfo>> _injectableMethodsCache = 
            new ConcurrentDictionary<Type, List<MethodInfo>>();
        
        // ⭐ 缓存特性信息（避免重复查找）- MemberInfo（字段、属性、方法）- 线程安全
        private static readonly ConcurrentDictionary<MemberInfo, InjectAttribute> _attributeCache = 
            new ConcurrentDictionary<MemberInfo, InjectAttribute>();
        
        // ⭐ 缓存参数特性信息（ParameterInfo 不是 MemberInfo 的子类）- 线程安全
        private static readonly ConcurrentDictionary<ParameterInfo, InjectAttribute> _parameterAttributeCache = 
            new ConcurrentDictionary<ParameterInfo, InjectAttribute>();
        
        /// <summary>
        /// 清理所有缓存
        /// </summary>
        public static void ClearCache()
        {
            _injectableFieldsCache.Clear();
            _injectablePropertiesCache.Clear();
            _injectableMethodsCache.Clear();
            _attributeCache.Clear();
            _parameterAttributeCache.Clear();
        }
        
        public void InjectFields(object target, IDependencyContainer container)
        {
            if (target == null || container == null) return;

            var type = target.GetType();
            
            // ⭐ 从缓存获取字段信息（线程安全）
            // GetOrAdd 确保即使多线程同时访问，也只会创建一次
            var fields = _injectableFieldsCache.GetOrAdd(type, t =>
            {
                // 第一次：查找所有字段并过滤出需要注入的字段
                return t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => GetInjectAttribute(f) != null)
                    .ToList();
            });

            foreach (var field in fields)
            {
                var injectAttr = GetInjectAttribute(field);
                if (injectAttr == null) continue;

                try
                {
                    var fieldType = field.FieldType;
                    var instance = container.Resolve(fieldType, injectAttr.Name);
                    field.SetValue(target, instance);
                }
                catch (Exception ex)
                {
                    if (injectAttr.Required)
                    {
                        Debug.LogError($"Failed to inject field {field.Name} in {type.Name}: {ex.Message}");
                        throw;
                    }
                    else
                    {
                        Debug.LogWarning($"Optional field {field.Name} in {type.Name} could not be injected: {ex.Message}");
                    }
                }
            }
        }

        public void InjectProperties(object target, IDependencyContainer container)
        {
            if (target == null || container == null) return;

            var type = target.GetType();
            
            // ⭐ 从缓存获取属性信息（线程安全）
            var properties = _injectablePropertiesCache.GetOrAdd(type, t =>
            {
                // 第一次：查找所有属性并过滤出需要注入的属性
                return t.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && GetInjectAttribute(p) != null)
                    .ToList();
            });

            foreach (var property in properties)
            {
                var injectAttr = GetInjectAttribute(property);
                if (injectAttr == null) continue;

                try
                {
                    var propertyType = property.PropertyType;
                    var instance = container.Resolve(propertyType, injectAttr.Name);
                    property.SetValue(target, instance);
                }
                catch (Exception ex)
                {
                    if (injectAttr.Required)
                    {
                        Debug.LogError($"Failed to inject property {property.Name} in {type.Name}: {ex.Message}");
                        throw;
                    }
                    else
                    {
                        Debug.LogWarning($"Optional property {property.Name} in {type.Name} could not be injected: {ex.Message}");
                    }
                }
            }
        }

        public void InjectMethods(object target, IDependencyContainer container)
        {
            if (target == null || container == null) return;

            var type = target.GetType();
            
            // ⭐ 从缓存获取方法信息（线程安全）
            var methods = _injectableMethodsCache.GetOrAdd(type, t =>
            {
                // 第一次：查找所有方法并过滤出需要注入的方法
                return t.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => GetInjectAttribute(m) != null)
                    .ToList();
            });

            foreach (var method in methods)
            {
                var injectAttr = GetInjectAttribute(method);
                if (injectAttr == null) continue;

                try
                {
                    // ⭐ 方法参数信息也可以缓存，但通常方法参数数量较少，缓存收益不大
                    // 如果需要进一步优化，可以添加参数缓存
                    var parameters = method.GetParameters();
                    var args = new object[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        var paramInjectAttr = GetInjectAttributeForParameter(parameters[i]);
                        var name = paramInjectAttr?.Name ?? injectAttr.Name;

                        try
                        {
                            args[i] = container.Resolve(paramType, name);
                        }
                        catch (Exception ex)
                        {
                            if (paramInjectAttr?.Required != false)
                            {
                                Debug.LogError($"Failed to resolve parameter {parameters[i].Name} for method {method.Name} in {type.Name}: {ex.Message}");
                                throw;
                            }
                            else
                            {
                                args[i] = GetDefaultValue(paramType);
                            }
                        }
                    }

                    method.Invoke(target, args);
                }
                catch (Exception ex)
                {
                    if (injectAttr.Required)
                    {
                        Debug.LogError($"Failed to inject method {method.Name} in {type.Name}: {ex.Message}");
                        throw;
                    }
                    else
                    {
                        Debug.LogWarning($"Optional method {method.Name} in {type.Name} could not be injected: {ex.Message}");
                    }
                }
            }
        }

        private object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        
        /// <summary>
        /// 获取注入特性（带缓存，线程安全）- 用于 MemberInfo（字段、属性、方法）
        /// </summary>
        private InjectAttribute GetInjectAttribute(MemberInfo member)
        {
            // GetOrAdd 确保线程安全，即使多线程同时访问也只会查找一次
            return _attributeCache.GetOrAdd(member, m => m.GetCustomAttribute<InjectAttribute>());
        }
        
        /// <summary>
        /// 获取注入特性（带缓存，线程安全）- 用于 ParameterInfo（方法参数）
        /// </summary>
        private InjectAttribute GetInjectAttributeForParameter(ParameterInfo parameter)
        {
            // GetOrAdd 确保线程安全，即使多线程同时访问也只会查找一次
            return _parameterAttributeCache.GetOrAdd(parameter, p => p.GetCustomAttribute<InjectAttribute>());
        }
    }
}