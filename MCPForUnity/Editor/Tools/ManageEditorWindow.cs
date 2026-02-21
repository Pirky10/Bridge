using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_editor_window")]
    public static class ManageEditorWindow
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' is required.");

            try
            {
                switch (action)
                {
                    case "create_editor_window": return CreateWindow(@params);
                    case "open_editor_window": return OpenWindow(@params);
                    case "close_editor_window": return CloseWindow(@params);
                    case "list_editor_windows": return ListWindows(@params);
                    case "focus_editor_window": return FocusWindow(@params);
                    case "resize_editor_window": return ResizeWindow(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageEditorWindow error: {e.Message}");
            }
        }

        private static object CreateWindow(JObject @params)
        {
            string windowName = @params["window_name"]?.ToString() ?? "MyCustomWindow";
            string title = @params["title"]?.ToString() ?? "My Custom Window";
            string savePath = @params["save_path"]?.ToString() ?? $"Assets/Editor/{windowName}.cs";

            // Ensure the Editor directory exists
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), savePath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string scriptContent = $@"using UnityEditor;
using UnityEngine;

public class {windowName} : EditorWindow
{{
    [MenuItem(""Window/Custom/{title}"")]
    public static void ShowWindow()
    {{
        GetWindow<{windowName}>(""{title}"");
    }}

    private void OnGUI()
    {{
        GUILayout.Label(""{title}"", EditorStyles.boldLabel);
    }}
}}";

            File.WriteAllText(fullPath, scriptContent);
            AssetDatabase.ImportAsset(savePath);

            return new SuccessResponse(
                $"Created EditorWindow script at '{savePath}'. Recompile to open it.",
                new { savePath, windowName, title }
            );
        }

        private static object OpenWindow(JObject @params)
        {
            string windowTypeName = @params["window_type"]?.ToString();
            if (string.IsNullOrEmpty(windowTypeName))
                return new ErrorResponse("'window_type' is required (e.g. 'SceneView', 'UnityEditor.InspectorWindow').");

            Type windowType = ResolveEditorWindowType(windowTypeName);
            if (windowType == null)
                return new ErrorResponse(
                    $"Could not find EditorWindow type '{windowTypeName}'. " +
                    "Use fully qualified name like 'UnityEditor.ConsoleWindow' or short name like 'SceneView'."
                );

            var window = EditorWindow.GetWindow(windowType);
            window.Show();

            return new SuccessResponse($"Opened window '{windowType.Name}'.", new
            {
                type = windowType.FullName,
                title = window.titleContent?.text,
                position = new { x = window.position.x, y = window.position.y, w = window.position.width, h = window.position.height }
            });
        }

        private static object CloseWindow(JObject @params)
        {
            string windowTypeName = @params["window_type"]?.ToString();
            if (string.IsNullOrEmpty(windowTypeName))
                return new ErrorResponse("'window_type' is required.");

            Type windowType = ResolveEditorWindowType(windowTypeName);
            if (windowType == null)
                return new ErrorResponse($"Could not find EditorWindow type '{windowTypeName}'.");

            var windows = UnityEngine.Resources.FindObjectsOfTypeAll(windowType)
                .Cast<EditorWindow>()
                .ToList();

            if (windows.Count == 0)
                return new ErrorResponse($"No open windows of type '{windowTypeName}' found.");

            int closed = 0;
            foreach (var w in windows)
            {
                w.Close();
                closed++;
            }

            return new SuccessResponse($"Closed {closed} window(s) of type '{windowType.Name}'.", new
            {
                type = windowType.FullName,
                closedCount = closed
            });
        }

        private static object ListWindows(JObject @params)
        {
            var allWindows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();

            var windows = allWindows
                .Select(w => new
                {
                    title = w.titleContent?.text,
                    type = w.GetType().FullName,
                    docked = w.docked,
                    hasFocus = w.hasFocus,
                    position = new
                    {
                        x = w.position.x,
                        y = w.position.y,
                        width = w.position.width,
                        height = w.position.height
                    }
                })
                .OrderBy(w => w.type)
                .ToList();

            return new SuccessResponse($"Found {windows.Count} editor window(s).", new { windows });
        }

        private static object FocusWindow(JObject @params)
        {
            string windowTypeName = @params["window_type"]?.ToString();
            if (string.IsNullOrEmpty(windowTypeName))
                return new ErrorResponse("'window_type' is required.");

            Type windowType = ResolveEditorWindowType(windowTypeName);
            if (windowType == null)
                return new ErrorResponse($"Could not find EditorWindow type '{windowTypeName}'.");

            var window = EditorWindow.GetWindow(windowType);
            window.Focus();

            return new SuccessResponse($"Focused window '{windowType.Name}'.", new
            {
                type = windowType.FullName,
                title = window.titleContent?.text
            });
        }

        private static object ResizeWindow(JObject @params)
        {
            string windowTypeName = @params["window_type"]?.ToString();
            if (string.IsNullOrEmpty(windowTypeName))
                return new ErrorResponse("'window_type' is required.");

            Type windowType = ResolveEditorWindowType(windowTypeName);
            if (windowType == null)
                return new ErrorResponse($"Could not find EditorWindow type '{windowTypeName}'.");

            var window = EditorWindow.GetWindow(windowType);

            float x = @params["x"]?.ToObject<float>() ?? window.position.x;
            float y = @params["y"]?.ToObject<float>() ?? window.position.y;
            float w = @params["width"]?.ToObject<float>() ?? window.position.width;
            float h = @params["height"]?.ToObject<float>() ?? window.position.height;

            window.position = new Rect(x, y, w, h);
            window.Repaint();

            return new SuccessResponse($"Resized window '{windowType.Name}'.", new
            {
                type = windowType.FullName,
                position = new { x, y, width = w, height = h }
            });
        }

        // ─── Helpers ────────────────────────────────────────────

        /// <summary>
        /// Resolves an EditorWindow type from a name. Supports both short names (e.g. "SceneView")
        /// and fully qualified names (e.g. "UnityEditor.SceneView").
        /// </summary>
        private static Type ResolveEditorWindowType(string typeName)
        {
            // Direct Assembly lookup — most EditorWindow types are in UnityEditor assembly
            var editorAssembly = typeof(EditorWindow).Assembly;

            // Try fully qualified first
            Type type = editorAssembly.GetType(typeName);
            if (type != null && typeof(EditorWindow).IsAssignableFrom(type))
                return type;

            // Try with UnityEditor prefix
            type = editorAssembly.GetType("UnityEditor." + typeName);
            if (type != null && typeof(EditorWindow).IsAssignableFrom(type))
                return type;

            // Search all assemblies as fallback
            type = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t =>
                    typeof(EditorWindow).IsAssignableFrom(t) &&
                    (t.FullName == typeName || t.Name == typeName)
                );

            return type;
        }
    }
}
