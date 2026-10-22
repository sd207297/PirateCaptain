using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>可单独生成的脚本类型（与完整生成中的文件一一对应）。</summary>
public enum ModuleScriptKind
{
    IModel,
    IService,
    IController,
    Data,
    Model,
    Service,
    View,
    Controller,
    Container,
    Module,
}

/// <summary>
/// 根据 Project 窗口右键选中的目录名称，生成 IoC + MVC 模块代码（Interface/Model/Service/View/Controller/Container）。
/// </summary>
public static class ModuleCodeGenerator
{
    private const int MenuPriority = 21;

    /// <summary>按约定子目录检测模块内脚本是否已存在，用于生成时省略未声明类型的 [Inject] / 绑定行。</summary>
    private readonly struct ModuleLayerPresence
    {
        public readonly bool IModelSource;
        public readonly bool IServiceSource;
        public readonly bool IControllerSource;
        public readonly bool ModelClassSource;
        public readonly bool ServiceClassSource;
        public readonly bool ViewClassSource;
        public readonly bool ControllerClassSource;

        public ModuleLayerPresence(
            bool iModelSource,
            bool iServiceSource,
            bool iControllerSource,
            bool modelClassSource,
            bool serviceClassSource,
            bool viewClassSource,
            bool controllerClassSource)
        {
            IModelSource = iModelSource;
            IServiceSource = iServiceSource;
            IControllerSource = iControllerSource;
            ModelClassSource = modelClassSource;
            ServiceClassSource = serviceClassSource;
            ViewClassSource = viewClassSource;
            ControllerClassSource = controllerClassSource;
        }
    }

    private static ModuleLayerPresence DetectModuleLayerPresence(string diskModuleRoot, string module)
    {
        string iface = Path.Combine(diskModuleRoot, "Interface");
        string modelDir = Path.Combine(diskModuleRoot, "Model");
        string serviceDir = Path.Combine(diskModuleRoot, "Service");
        string viewDir = Path.Combine(diskModuleRoot, "View");
        string controllerDir = Path.Combine(diskModuleRoot, "Controller");
        return new ModuleLayerPresence(
            File.Exists(Path.Combine(iface, "I" + module + "Model.cs")),
            File.Exists(Path.Combine(iface, "I" + module + "Service.cs")),
            File.Exists(Path.Combine(iface, "I" + module + "Controller.cs")),
            File.Exists(Path.Combine(modelDir, module + "Model.cs")),
            File.Exists(Path.Combine(serviceDir, module + "Service.cs")),
            File.Exists(Path.Combine(viewDir, module + "View.cs")),
            File.Exists(Path.Combine(controllerDir, module + "Controller.cs")));
    }

    [MenuItem("Assets/IOC工具/一键生成完整模块 (IoC MVC)", false, MenuPriority)]
    public static void GenerateModuleCode()
    {
        if (!TryGenerateFullModule(out int created, out string msg))
        {
            EditorUtility.DisplayDialog("Generate Module Code", msg, "确定");
            return;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Generate Module Code", msg, "确定");
    }

    [MenuItem("Assets/IOC工具/一键生成完整模块 (IoC MVC)", true, MenuPriority)]
    public static bool ValidateGenerateModuleCode()
    {
        return GetSelectedFolderAssetPath() != null;
    }

    /// <summary>与菜单校验一致：是否恰好选中一个文件夹。</summary>
    public static string GetSelectedFolderAssetPath()
    {
        if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length != 1)
            return null;
        string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
        if (string.IsNullOrEmpty(path))
            return null;
        string full = Path.Combine(Application.dataPath, "..", path);
        full = Path.GetFullPath(full);
        return Directory.Exists(full) ? path : null;
    }

    /// <summary>解析当前选中目录为磁盘路径与 Pascal 模块名（来自文件夹名）。</summary>
    public static bool TryGetSelectedModuleFolder(out string assetPath, out string diskFullPath, out string folderPascalModuleName)
    {
        assetPath = GetSelectedFolderAssetPath();
        diskFullPath = null;
        folderPascalModuleName = null;
        if (string.IsNullOrEmpty(assetPath))
            return false;

        return TryResolveModuleFolderFromAssetPath(assetPath, out diskFullPath, out folderPascalModuleName);
    }

    /// <summary>根据 Project 内资源路径（如 Assets/Scripts/.../MyModule）解析模块根目录；须为已存在的文件夹。</summary>
    public static bool TryResolveModuleFolderFromAssetPath(string assetPath, out string diskFullPath, out string folderPascalModuleName)
    {
        diskFullPath = null;
        folderPascalModuleName = null;
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string normalized = assetPath.Trim().Replace('\\', '/');
        while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
            normalized = normalized.TrimEnd('/');

        if (string.IsNullOrEmpty(normalized))
            return false;

        string folderName = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(folderName))
            return false;

        string combined = Path.Combine(Application.dataPath, "..", normalized);
        diskFullPath = Path.GetFullPath(combined);
        if (!Directory.Exists(diskFullPath))
            return false;

        folderPascalModuleName = ToPascalCase(folderName);
        return true;
    }

    /// <summary>一键生成完整模块；失败时 message 为错误说明。</summary>
    public static bool TryGenerateFullModule(out int createdFiles, out string message)
    {
        createdFiles = 0;
        if (!TryGetSelectedModuleFolder(out string ap, out string full, out string mod))
        {
            message = "请先右键选中一个文件夹。";
            return false;
        }

        string ns = "ForeverNine." + mod;
        createdFiles = GenerateFullModuleFiles(full, mod, ns);
        message = $"已在 {ap} 下生成 {createdFiles} 个文件，模块名：{mod}，命名空间：{ns}。";
        return true;
    }

    /// <summary>在指定模块根目录下按勾选类型生成脚本；已存在文件不覆盖。</summary>
    public static int GeneratePartialScripts(string diskModuleRootFullPath, string classPrefixPascal, string ns, IReadOnlyList<ModuleScriptKind> kinds)
    {
        if (string.IsNullOrEmpty(diskModuleRootFullPath) || !Directory.Exists(diskModuleRootFullPath))
            return 0;
        if (string.IsNullOrEmpty(classPrefixPascal) || string.IsNullOrEmpty(ns) || kinds == null || kinds.Count == 0)
            return 0;

        int created = 0;
        string m = classPrefixPascal;
        for (int i = 0; i < kinds.Count; i++)
        {
            ModuleScriptKind k = kinds[i];
            switch (k)
            {
                case ModuleScriptKind.IModel:
                    created += WriteFile(diskModuleRootFullPath, "Interface", "I" + m + "Model.cs", GetIModelContent(ns, m));
                    break;
                case ModuleScriptKind.IService:
                    created += WriteFile(diskModuleRootFullPath, "Interface", "I" + m + "Service.cs", GetIServiceContent(ns, m));
                    break;
                case ModuleScriptKind.IController:
                    created += WriteFile(diskModuleRootFullPath, "Interface", "I" + m + "Controller.cs", GetIControllerContent(ns, m));
                    break;
                case ModuleScriptKind.Data:
                    created += WriteFile(diskModuleRootFullPath, "Model", m + "Data.cs", GetDataContent(ns, m));
                    break;
                case ModuleScriptKind.Model:
                    created += WriteFile(diskModuleRootFullPath, "Model", m + "Model.cs", GetModelContent(ns, m, DetectModuleLayerPresence(diskModuleRootFullPath, m)));
                    break;
                case ModuleScriptKind.Service:
                    created += WriteFile(diskModuleRootFullPath, "Service", m + "Service.cs", GetServiceContent(ns, m, DetectModuleLayerPresence(diskModuleRootFullPath, m)));
                    break;
                case ModuleScriptKind.View:
                    created += WriteFile(diskModuleRootFullPath, "View", m + "View.cs", GetViewContent(ns, m, DetectModuleLayerPresence(diskModuleRootFullPath, m)));
                    break;
                case ModuleScriptKind.Controller:
                    created += WriteFile(diskModuleRootFullPath, "Controller", m + "Controller.cs", GetControllerContent(ns, m, DetectModuleLayerPresence(diskModuleRootFullPath, m)));
                    break;
                case ModuleScriptKind.Container:
                    created += WriteFile(diskModuleRootFullPath, "", m + "Container.cs", GetContainerContent(ns, m, DetectModuleLayerPresence(diskModuleRootFullPath, m)));
                    break;
                case ModuleScriptKind.Module:
                    created += WriteFile(diskModuleRootFullPath, "", m + "Module.cs", GetModuleContent(ns, m));
                    break;
            }
        }

        return created;
    }

    public static string GetScriptKindLabel(ModuleScriptKind k)
    {
        switch (k)
        {
            case ModuleScriptKind.IModel: return "I…Model（Interface）";
            case ModuleScriptKind.IService: return "I…Service（Interface）";
            case ModuleScriptKind.IController: return "I…Controller（Interface）";
            case ModuleScriptKind.Data: return "…Data（Model 数据结构占位）";
            case ModuleScriptKind.Model: return "…Model";
            case ModuleScriptKind.Service: return "…Service";
            case ModuleScriptKind.View: return "…View";
            case ModuleScriptKind.Controller: return "…Controller";
            case ModuleScriptKind.Container: return "…Container";
            case ModuleScriptKind.Module: return "…Module（独立模块 / CFNG Module）";
            default: return k.ToString();
        }
    }

    public static bool IsValidCSharpTypeName(string s)
    {
        if (string.IsNullOrEmpty(s))
            return false;
        if (!char.IsLetter(s[0]) && s[0] != '_')
            return false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        return true;
    }

    private static int GenerateFullModuleFiles(string fullPath, string moduleName, string ns)
    {
        int created = 0;
        created += WriteFile(fullPath, "Interface", "I" + moduleName + "Model.cs", GetIModelContent(ns, moduleName));
        created += WriteFile(fullPath, "Interface", "I" + moduleName + "Service.cs", GetIServiceContent(ns, moduleName));
        created += WriteFile(fullPath, "Interface", "I" + moduleName + "Controller.cs", GetIControllerContent(ns, moduleName));
        created += WriteFile(fullPath, "Model", moduleName + "Data.cs", GetDataContent(ns, moduleName));
        created += WriteFile(fullPath, "Model", moduleName + "Model.cs", GetModelContent(ns, moduleName, DetectModuleLayerPresence(fullPath, moduleName)));
        created += WriteFile(fullPath, "Service", moduleName + "Service.cs", GetServiceContent(ns, moduleName, DetectModuleLayerPresence(fullPath, moduleName)));
        created += WriteFile(fullPath, "View", moduleName + "View.cs", GetViewContent(ns, moduleName, DetectModuleLayerPresence(fullPath, moduleName)));
        created += WriteFile(fullPath, "Controller", moduleName + "Controller.cs", GetControllerContent(ns, moduleName, DetectModuleLayerPresence(fullPath, moduleName)));
        created += WriteFile(fullPath, "", moduleName + "Container.cs", GetContainerContent(ns, moduleName, DetectModuleLayerPresence(fullPath, moduleName)));
        created += WriteFile(fullPath, "", moduleName + "Module.cs", GetModuleContent(ns, moduleName));
        return created;
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        string s = name.Trim();
        if (s.Length == 0) return s;
        if (s.Length == 1) return s.ToUpperInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }

    private static int WriteFile(string baseDir, string subDir, string fileName, string content)
    {
        string dir = string.IsNullOrEmpty(subDir) ? baseDir : Path.Combine(baseDir, subDir);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        if (File.Exists(path))
            return 0;
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return 1;
    }

    private static string GetIModelContent(string ns, string module)
    {
        return $@"namespace {ns}
{{
    /// <summary>
    /// {module} 数据模型接口 - 持有 UI 状态与展示用数据。
    /// </summary>
    public interface I{module}Model
    {{
        // TODO: 当前分类、选中项、列表数据等
    }}
}}
";
    }

    private static string GetIServiceContent(string ns, string module)
    {
        return $@"namespace {ns}
{{
    /// <summary>
    /// {module} 服务接口 - 负责业务数据读写与查询。
    /// </summary>
    public interface I{module}Service
    {{
        // TODO: 具体接口方法
    }}
}}
";
    }

    private static string GetIControllerContent(string ns, string module)
    {
        return $@"namespace {ns}
{{
    /// <summary>
    /// {module} 控制器接口 - 协调 View 与 Model/Service，处理用户操作与业务逻辑。
    /// </summary>
    public interface I{module}Controller
    {{
        // TODO: 打开/关闭、刷新、点击项等
    }}
}}
";
    }

    private static string GetDataContent(string ns, string module)
    {
        return $@"namespace {ns}
{{
    /// <summary>
    /// {module} 模块所有数据结构的声明。
    /// </summary>
    // 在此添加 struct/class 等数据结构，例如：
    // public struct XxxItem {{ public long Id; public long Count; }}
}}
";
    }

    private static string GetModelContent(string ns, string module, ModuleLayerPresence p)
    {
        string impl = p.IModelSource ? $" : I{module}Model" : "";
        string note = p.IModelSource
            ? ""
            : $@"
    // TODO: 未检测到 Interface/I{module}Model.cs，未实现接口；生成该接口后可改为 : I{module}Model";
        return $@"namespace {ns}
{{
    /// <summary>
    /// {module} 数据模型{(p.IModelSource ? " - 实现 I" + module + "Model。" : "。")}
    /// </summary>
    public class {module}Model{impl}
    {{{note}
    }}
}}
";
    }

    private static string GetServiceContent(string ns, string module, ModuleLayerPresence p)
    {
        string impl = p.IServiceSource ? $" : I{module}Service" : "";
        string note = p.IServiceSource
            ? ""
            : $@"
    // TODO: 未检测到 Interface/I{module}Service.cs，未实现接口；生成该接口后可改为 : I{module}Service";
        return $@"namespace {ns}
{{
    /// <summary>
    /// {module} 服务实现{(p.IServiceSource ? " - 实现 I" + module + "Service。" : "。")}
    /// </summary>
    public class {module}Service{impl}
    {{{note}
    }}
}}
";
    }

    private static string GetViewContent(string ns, string module, ModuleLayerPresence p)
    {
        string injectBlock = p.IControllerSource
            ? $@"
        [Inject] private I{module}Controller _controller;

        protected override void OnAwake()
        {{
            // TODO: 绑定按钮、列表等 UI 到 _controller 的调用
        }}"
            : $@"

        protected override void OnAwake()
        {{
            // TODO: 未检测到 Interface/I{module}Controller.cs，未注入 I{module}Controller；生成接口后可添加 [Inject] private I{module}Controller _controller;
        }}";
        return $@"using ForeverNine.IoC;
using UnityEngine;

namespace {ns}
{{
    /// <summary>
    /// {module} 视图 - 挂到 UI 根节点；{(p.IControllerSource ? "注入 I" + module + "Controller 处理交互。" : "按需注入 I" + module + "Controller。")}
    /// </summary>
    public class {module}View : BaseView
    {{{injectBlock}
    }}
}}
";
    }

    private static string GetControllerContent(string ns, string module, ModuleLayerPresence p)
    {
        var injectSb = new StringBuilder();
        if (p.ViewClassSource)
            injectSb.AppendLine($"        [Inject] private {module}View _view;");
        if (p.IServiceSource)
            injectSb.AppendLine($"        [Inject] private I{module}Service _service;");
        if (p.IModelSource)
            injectSb.AppendLine($"        [Inject] private I{module}Model _model;");
        string injectBlock = injectSb.Length > 0
            ? injectSb.ToString()
            : $@"        // TODO: 按需 [Inject]：仅当存在约定路径下的 View / Interface 脚本时工具才会生成对应字段
";
        string impl = p.IControllerSource ? $" : I{module}Controller" : "";
        string implNote = p.IControllerSource
            ? ""
            : $@"
        // TODO: 未检测到 Interface/I{module}Controller.cs，未实现接口；生成后可改为 : I{module}Controller";
        string todo = (p.ViewClassSource || p.IServiceSource || p.IModelSource)
            ? "        // TODO: 实现打开/关闭、刷新、点击项等，通过 _service/_model 改数据，通过 _view 更新界面"
            : "        // TODO: 实现 I" + module + "Controller 约定的方法";
        bool useIoc = p.ViewClassSource || p.IServiceSource || p.IModelSource;
        string usingIoc = useIoc ? "using ForeverNine.IoC;\r\n\r\n" : "";
        return $@"{usingIoc}namespace {ns}
{{
    /// <summary>
    /// {module} 控制器 - 协调 View 与 Model/Service；注入字段随模块内已有脚本自动生成。
    /// </summary>
    public class {module}Controller{impl}
    {{{implNote}
{injectBlock}
{todo}
    }}
}}
";
    }

    private static string GetContainerContent(string ns, string module, ModuleLayerPresence p)
    {
        var bindSb = new StringBuilder();
        if (p.IModelSource && p.ModelClassSource)
            bindSb.AppendLine($"            Container.BindSingleton<I{module}Model, {module}Model>();");
        if (p.IServiceSource && p.ServiceClassSource)
            bindSb.AppendLine($"            Container.BindSingleton<I{module}Service, {module}Service>();");
        if (p.ViewClassSource)
            bindSb.AppendLine($"            Container.BindView<{module}View>();");
        if (p.IControllerSource && p.ControllerClassSource)
            bindSb.AppendLine($"            Container.BindSingleton<I{module}Controller, {module}Controller>();");
        string bindBody = bindSb.Length > 0
            ? bindSb.ToString()
            : "            // TODO: 未检测到可绑定的 M/V/C 与 Interface 组合，请先生成对应脚本或在此手写 BindSingleton / BindView\r\n";
        return $@"using ForeverNine.IoC;

namespace {ns}
{{
    /// <summary>
    /// {module} 模块 IoC 容器 - 绑定 M/V/C 与 Service，实现模块内功能与数据隔离。
    /// </summary>
    public class {module}Container : BaseMonoContainer
    {{
        protected override void BindDependencies()
        {{
{bindBody}        }}

        protected override void RegisterEvents()
        {{
            // TODO: EventManager.Instance.Register<T>(EventName.XXX, OnXXX);
        }}

        protected override void UnregisterEvents()
        {{
            // TODO: EventManager.Instance.Unregister<T>(EventName.XXX, OnXXX);
        }}

        protected override void Init()
        {{
            base.Init();
            // TODO: 模块业务初始化
        }}
    }}
}}
";
    }

    private static string GetModuleContent(string ns, string module)
    {
        // 保留原项目的 CFNG 模板；未接入其运行时的工程使用无第三方依赖的模块。
        if (!IsTypeAvailable("CFNG.Runtime.Core.Module.Module") ||
            !IsTypeAvailable("Cysharp.Threading.Tasks.UniTask"))
            return GetStandaloneModuleContent(ns, module);

        return $@"using CFNG.Runtime.Core.Module;
using Cysharp.Threading.Tasks;
using ForeverNine.IoC;

namespace {ns}
{{
    /// <summary>
    /// {module} 模块 - 在 OnInitialize 中创建 {module}Container，在 OnShutdown 中移除。
    /// 业务绑定与事件仍在 {module}Container 内完成。登录后在 GameStart.InitGameModules 中 AddModule 本类。
    /// </summary>
    public class {module}Module : Module
    {{
        public override void OnInitialize()
        {{
            ContainerManager.Instance.GetOrCreateContainer<{module}Container>(nameof({module}Container));
        }}

        public override void OnShutdown()
        {{
            // 停止 Play / ApplicationQuit 后 ContainerManager.Instance 可能为 null，ModuleManager.OnDestroy 仍会调 OnShutdown。
            ContainerManager.Instance?.RemoveContainer(nameof({module}Container));
        }}

        public override UniTask OnEventReceived(object eventName, object eventData = null)
        {{
            return UniTask.CompletedTask;
        }}
    }}
}}
";
    }

    private static bool IsTypeAvailable(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType(fullName, false) != null)
                return true;
        }
        return false;
    }

    private static string GetStandaloneModuleContent(string ns, string module)
    {
        return $@"using System;
using System.Threading.Tasks;
using ForeverNine.IoC;

namespace {ns}
{{
    /// <summary>
    /// {module} 独立模块。创建后调用 OnInitialize，结束时调用 OnShutdown 或 Dispose。
    /// 业务绑定与事件在 {module}Container 内完成。
    /// </summary>
    public class {module}Module : IDisposable
    {{
        public virtual void OnInitialize()
        {{
            ContainerManager.Instance.GetOrCreateContainer<{module}Container>(nameof({module}Container));
        }}

        public virtual void OnShutdown()
        {{
            ContainerManager.Instance?.RemoveContainer(nameof({module}Container));
        }}

        public virtual Task OnEventReceived(object eventName, object eventData = null)
        {{
            return Task.CompletedTask;
        }}

        public void Dispose()
        {{
            OnShutdown();
        }}
    }}
}}
";
    }
}
