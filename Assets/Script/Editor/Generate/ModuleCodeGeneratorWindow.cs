using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// IoC MVC 模块代码生成面板：按类型与自定义类名单独生成脚本；完整模块请用菜单「一键生成完整模块」。
/// </summary>
public class ModuleCodeGeneratorWindow : EditorWindow
{
    private const string EditorPrefsModuleRootPath = "ModuleCodeGenerator.ModuleRootAssetPath";
    private const string EditorPrefsSinglePrefix = "ModuleCodeGenerator.SinglePrefix";
    private const string EditorPrefsSingleNs = "ModuleCodeGenerator.SingleNs";
    private const string EditorPrefsAdvancedOpen = "ModuleCodeGenerator.AdvancedOpen";

    private string _moduleRootAssetPath = "";
    private string _folderDiskPath;
    private string _folderPascalName;
    private bool _moduleRootPathValid;
    private string _singlePrefix = "";
    private string _namespaceOverride = "";
    private bool[] _kindEnabled;
    private static readonly ModuleScriptKind[] AllKinds = (ModuleScriptKind[])Enum.GetValues(typeof(ModuleScriptKind));
    private Vector2 _scroll;
    private bool _advancedOpen;

    [MenuItem("Assets/IOC工具/打开模块代码生成面板…", false, 20)]
    private static void Open()
    {
        var w = GetWindow<ModuleCodeGeneratorWindow>(true, "IoC 模块代码（单独生成）", true);
        w.minSize = new Vector2(420, 380);
    }

    private void OnEnable()
    {
        _kindEnabled = new bool[AllKinds.Length];
        _moduleRootAssetPath = EditorPrefs.GetString(EditorPrefsModuleRootPath, "");
        if (string.IsNullOrWhiteSpace(_moduleRootAssetPath) &&
            ModuleCodeGenerator.TryGetSelectedModuleFolder(out var ap, out _, out _))
        {
            _moduleRootAssetPath = ap;
        }

        ApplyModuleRootPath(_moduleRootAssetPath);

        _singlePrefix = EditorPrefs.GetString(EditorPrefsSinglePrefix, "");
        _namespaceOverride = EditorPrefs.GetString(EditorPrefsSingleNs, "");
        _advancedOpen = EditorPrefs.GetBool(EditorPrefsAdvancedOpen, false);
    }

    private void ApplyModuleRootPath(string path)
    {
        _moduleRootAssetPath = path ?? "";
        if (ModuleCodeGenerator.TryResolveModuleFolderFromAssetPath(_moduleRootAssetPath, out var disk, out var pascal))
        {
            _folderDiskPath = disk;
            _folderPascalName = pascal;
            _moduleRootPathValid = true;
            EditorPrefs.SetString(EditorPrefsModuleRootPath, _moduleRootAssetPath);
        }
        else
        {
            _folderDiskPath = null;
            _folderPascalName = null;
            _moduleRootPathValid = false;
        }
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("模块根目录", EditorStyles.boldLabel);

        DefaultAsset folderObj = null;
        if (AssetDatabase.IsValidFolder(_moduleRootAssetPath))
            folderObj = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_moduleRootAssetPath);

        EditorGUI.BeginChangeCheck();
        var newFolderObj = EditorGUILayout.ObjectField("拖入文件夹", folderObj, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck() && newFolderObj != null)
        {
            string np = AssetDatabase.GetAssetPath(newFolderObj);
            if (AssetDatabase.IsValidFolder(np))
            {
                _moduleRootAssetPath = np;
                ApplyModuleRootPath(np);
            }
        }

        EditorGUI.BeginChangeCheck();
        string delayedPath = EditorGUILayout.DelayedTextField("模块路径 (Assets/...)", _moduleRootAssetPath);
        if (EditorGUI.EndChangeCheck())
        {
            _moduleRootAssetPath = delayedPath;
            ApplyModuleRootPath(delayedPath);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("使用 Project 当前选中文件夹"))
            {
                if (ModuleCodeGenerator.TryGetSelectedModuleFolder(out var ap, out _, out _))
                {
                    _moduleRootAssetPath = ap;
                    ApplyModuleRootPath(ap);
                }
                else
                {
                    EditorUtility.DisplayDialog("模块根目录", "请在 Project 中选中且仅选中一个文件夹。", "确定");
                }
            }
        }

        if (!_moduleRootPathValid)
        {
            if (string.IsNullOrWhiteSpace(_moduleRootAssetPath))
                EditorGUILayout.HelpBox("请填写或拖入模块根目录（与「一键生成完整模块」使用的文件夹含义相同）。", MessageType.Warning);
            else
                EditorGUILayout.HelpBox("当前路径不是有效文件夹，请检查拼写或是否存在于工程中。", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("模块名（来自文件夹名）", _folderPascalName);
            EditorGUILayout.LabelField("默认命名空间", "ForeverNine." + _folderPascalName);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "一键生成 Interface / Model / Service / View / Controller / Container / Module 全套脚本，请使用菜单：\n" +
            "「Assets → IOC工具 → 一键生成完整模块 (IoC MVC)」\n" +
            "（菜单项仍使用 Project 当前选中文件夹；本面板路径可独立设置。）",
            MessageType.Info);

        EditorGUILayout.Space(10);
        if (GUILayout.Button(_advancedOpen ? "▼ 收起单独脚本设置" : "▶ 单独脚本设置（不同类名的 View / Controller 等）"))
        {
            _advancedOpen = !_advancedOpen;
            if (_advancedOpen && string.IsNullOrEmpty((_singlePrefix ?? "").Trim()) && !string.IsNullOrEmpty(_folderPascalName))
                _singlePrefix = _folderPascalName;
            EditorPrefs.SetBool(EditorPrefsAdvancedOpen, _advancedOpen);
        }

        if (_advancedOpen)
        {
            DrawSingleScriptSection();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSingleScriptSection()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("在上方「模块根目录」下按子目录写入；类名前缀可与文件夹名不同，用于同一容器内多个 View/Controller。", MessageType.Info);

        using (new EditorGUI.DisabledScope(!_moduleRootPathValid))
        {
            EditorGUILayout.LabelField("类名前缀（PascalCase，如 ShopHeader）");
            _singlePrefix = EditorGUILayout.TextField(_singlePrefix);
            EditorGUILayout.LabelField("命名空间（留空则用 ForeverNine." + (_folderPascalName ?? "?") + "）");
            _namespaceOverride = EditorGUILayout.TextField(_namespaceOverride);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("要生成的脚本类型", EditorStyles.boldLabel);
            for (int i = 0; i < AllKinds.Length; i++)
            {
                _kindEnabled[i] = EditorGUILayout.ToggleLeft(ModuleCodeGenerator.GetScriptKindLabel(AllKinds[i]), _kindEnabled[i]);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全选"))
                {
                    for (int i = 0; i < _kindEnabled.Length; i++)
                        _kindEnabled[i] = true;
                }
                if (GUILayout.Button("全不选"))
                {
                    for (int i = 0; i < _kindEnabled.Length; i++)
                        _kindEnabled[i] = false;
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("生成所选类型的脚本", GUILayout.Height(28)))
            {
                RunPartialGenerate();
            }
        }
    }

    private void RunPartialGenerate()
    {
        if (!_moduleRootPathValid || string.IsNullOrEmpty(_folderDiskPath))
        {
            EditorUtility.DisplayDialog("单独生成", "请先设置有效的模块根目录路径。", "确定");
            return;
        }

        string prefix = (_singlePrefix ?? "").Trim();
        if (string.IsNullOrEmpty(prefix))
        {
            EditorUtility.DisplayDialog("单独生成", "请填写类名前缀。", "确定");
            return;
        }

        if (!ModuleCodeGenerator.IsValidCSharpTypeName(prefix))
        {
            EditorUtility.DisplayDialog("单独生成", "类名前缀需为合法 C# 标识符（字母或下划线开头，仅字母数字下划线）。", "确定");
            return;
        }

        string ns = (_namespaceOverride ?? "").Trim();
        if (string.IsNullOrEmpty(ns))
            ns = "ForeverNine." + _folderPascalName;

        var list = new List<ModuleScriptKind>();
        for (int i = 0; i < _kindEnabled.Length; i++)
        {
            if (_kindEnabled[i])
                list.Add(AllKinds[i]);
        }

        if (list.Count == 0)
        {
            EditorUtility.DisplayDialog("单独生成", "请至少勾选一种脚本类型。", "确定");
            return;
        }

        int created = ModuleCodeGenerator.GeneratePartialScripts(_folderDiskPath, prefix, ns, list);
        EditorPrefs.SetString(EditorPrefsSinglePrefix, prefix);
        EditorPrefs.SetString(EditorPrefsSingleNs, _namespaceOverride ?? "");

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "单独生成",
            $"已生成 {created} 个新文件（已存在的文件不会覆盖）。\n路径：{_moduleRootAssetPath}\n命名空间：{ns}\n前缀：{prefix}",
            "确定");
    }
}
