using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_presets", AutoRegister = false)]
    public static class ManagePresets
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "save": return SavePreset(@params, p);
                    case "apply": return ApplyPreset(@params, p);
                    case "list": return ListPresets(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: save, apply, list, get_info");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object SavePreset(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            string componentType = p.Get("component_type");
            string savePath = p.Get("save_path", $"Assets/Presets/{target}_preset.preset");

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Component comp = null;
            if (!string.IsNullOrEmpty(componentType))
            {
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c.GetType().Name.Equals(componentType, StringComparison.OrdinalIgnoreCase))
                    { comp = c; break; }
                }
                if (comp == null) return new ErrorResponse($"Component '{componentType}' not found on '{target}'.");
            }
            else
            {
                comp = go.GetComponents<Component>()[go.GetComponents<Component>().Length > 1 ? 1 : 0];
            }

            Preset preset = new Preset(comp);
            string dir = System.IO.Path.GetDirectoryName(savePath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(preset, savePath);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Saved preset from '{target}' ({comp.GetType().Name}) to {savePath}");
        }

        private static object ApplyPreset(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var presetResult = p.GetRequired("preset_path");
            var presetError = presetResult.GetOrError(out string presetPath);
            if (presetError != null) return presetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
            if (preset == null) return new ErrorResponse($"Preset not found: {presetPath}");

            // Find matching component
            foreach (var comp in go.GetComponents<Component>())
            {
                if (preset.CanBeAppliedTo(comp))
                {
                    Undo.RecordObject(comp, "Apply Preset");
                    preset.ApplyTo(comp);
                    EditorUtility.SetDirty(comp);
                    return new SuccessResponse($"Applied preset to '{target}' ({comp.GetType().Name})");
                }
            }

            return new ErrorResponse($"No matching component on '{target}' for this preset (type: {preset.GetTargetTypeName()}).");
        }

        private static object ListPresets(JObject @params, ToolParams p)
        {
            string[] guids = AssetDatabase.FindAssets("t:Preset");
            var presets = new List<object>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<Preset>(path);
                if (preset != null)
                    presets.Add(new { path, targetType = preset.GetTargetTypeName() });
            }
            return new SuccessResponse($"Found {presets.Count} presets", new { presets });
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("preset_path");
            var pathError = pathResult.GetOrError(out string presetPath);
            if (pathError != null) return pathError;

            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
            if (preset == null) return new ErrorResponse($"Preset not found: {presetPath}");

            var props = new List<string>();
            foreach (var prop in preset.PropertyModifications)
                props.Add($"{prop.propertyPath} = {prop.value}");

            return new SuccessResponse("Preset info", new
            {
                path = presetPath,
                targetType = preset.GetTargetTypeName(),
                propertyCount = props.Count,
                properties = props
            });
        }
    }
}
