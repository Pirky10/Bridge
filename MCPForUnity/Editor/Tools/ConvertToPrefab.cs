using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("convert_to_prefab", AutoRegister = false)]
    public static class ConvertToPrefab
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
                    case "create_prefab": return CreatePrefab(@params, p);
                    case "create_variant": return CreateVariant(@params, p);
                    case "unpack": return Unpack(@params, p);
                    case "apply_overrides": return ApplyOverrides(@params, p);
                    case "revert_overrides": return RevertOverrides(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: create_prefab, create_variant, unpack, apply_overrides, revert_overrides, get_info");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object CreatePrefab(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            string savePath = p.Get("save_path", $"Assets/Prefabs/{go.name}.prefab");
            string dir = System.IO.Path.GetDirectoryName(savePath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            bool success;
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, savePath, InteractionMode.AutomatedAction, out success);

            if (!success) return new ErrorResponse("Failed to create prefab.");

            return new SuccessResponse($"Created prefab at '{savePath}'", new { path = savePath, name = go.name });
        }

        private static object CreateVariant(JObject @params, ToolParams p)
        {
            var baseResult = p.GetRequired("base_prefab_path");
            var baseError = baseResult.GetOrError(out string basePath);
            if (baseError != null) return baseError;

            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (basePrefab == null) return new ErrorResponse($"Prefab not found: {basePath}");

            string variantPath = p.Get("variant_path", basePath.Replace(".prefab", "_Variant.prefab"));

            GameObject instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            if (instance == null) return new ErrorResponse("Could not instantiate base prefab.");

            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            UnityEngine.Object.DestroyImmediate(instance);

            if (variant == null) return new ErrorResponse("Failed to create variant.");

            return new SuccessResponse($"Created prefab variant at '{variantPath}'");
        }

        private static object Unpack(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new ErrorResponse($"'{target}' is not a prefab instance.");

            bool fully = p.GetBool("completely", false);

            Undo.RecordObject(go, "Unpack Prefab");
            PrefabUtility.UnpackPrefabInstance(go,
                fully ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);

            return new SuccessResponse($"Unpacked prefab '{target}' ({(fully ? "completely" : "outermost")})");
        }

        private static object ApplyOverrides(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new ErrorResponse($"'{target}' is not a prefab instance.");

            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            return new SuccessResponse($"Applied overrides from '{target}' to prefab.");
        }

        private static object RevertOverrides(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new ErrorResponse($"'{target}' is not a prefab instance.");

            PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
            return new SuccessResponse($"Reverted overrides on '{target}'.");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            bool isPrefab = PrefabUtility.IsPartOfPrefabInstance(go);
            string prefabPath = isPrefab ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) : null;
            bool hasOverrides = isPrefab && PrefabUtility.HasPrefabInstanceAnyOverrides(go, false);

            return new SuccessResponse("Prefab info", new
            {
                name = go.name,
                isPrefabInstance = isPrefab,
                prefabAssetPath = prefabPath,
                hasOverrides,
                prefabType = PrefabUtility.GetPrefabAssetType(go).ToString()
            });
        }
    }
}
