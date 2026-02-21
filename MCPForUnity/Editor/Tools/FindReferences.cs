using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("find_references", AutoRegister = false)]
    public static class FindReferences
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
                    case "find_asset_references": return FindAssetReferences(@params, p);
                    case "find_component_usage": return FindComponentUsage(@params, p);
                    case "find_script_references": return FindScriptReferences(@params, p);
                    case "find_missing_references": return FindMissingReferences(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: find_asset_references, find_component_usage, find_script_references, find_missing_references");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object FindAssetReferences(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("asset_path");
            var pathError = pathResult.GetOrError(out string assetPath);
            if (pathError != null) return pathError;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return new ErrorResponse($"Asset not found: {assetPath}");

            var references = new List<string>();
            string[] allAssets = AssetDatabase.GetAllAssetPaths();

            foreach (string path in allAssets)
            {
                if (path == assetPath) continue;
                if (!path.StartsWith("Assets/")) continue;
                if (path.EndsWith(".cs") || path.EndsWith(".shader")) continue;

                string[] deps = AssetDatabase.GetDependencies(path, false);
                foreach (string dep in deps)
                {
                    if (dep == assetPath)
                    {
                        references.Add(path);
                        break;
                    }
                }

                if (references.Count >= 100) break;
            }

            return new SuccessResponse($"Found {references.Count} references to '{assetPath}'", new
            {
                assetPath, referencedBy = references, count = references.Count
            });
        }

        private static object FindComponentUsage(JObject @params, ToolParams p)
        {
            var typeResult = p.GetRequired("component_type");
            var typeError = typeResult.GetOrError(out string componentType);
            if (typeError != null) return typeError;

            Type type = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(componentType) ?? assembly.GetType($"UnityEngine.{componentType}");
                if (type != null) break;
            }
            if (type == null) return new ErrorResponse($"Component type '{componentType}' not found.");

            var results = new List<string>();
            var allObjects = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                if (obj is Component comp)
                    results.Add(GetGameObjectPath(comp.gameObject));
            }

            return new SuccessResponse($"Found {results.Count} GameObjects using {componentType}", new
            {
                componentType, gameObjects = results, count = results.Count
            });
        }

        private static object FindScriptReferences(JObject @params, ToolParams p)
        {
            var scriptResult = p.GetRequired("script_path");
            var scriptError = scriptResult.GetOrError(out string scriptPath);
            if (scriptError != null) return scriptError;

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null)
                return new ErrorResponse($"Script not found: {scriptPath}");

            Type scriptType = script.GetClass();
            if (scriptType == null)
                return new ErrorResponse($"Could not get type from script: {scriptPath}");

            // Find in scene
            var sceneRefs = new List<string>();
            if (typeof(Component).IsAssignableFrom(scriptType))
            {
                var comps = UnityEngine.Object.FindObjectsByType(scriptType, FindObjectsSortMode.None);
                foreach (var c in comps)
                    if (c is Component comp)
                        sceneRefs.Add(GetGameObjectPath(comp.gameObject));
            }

            // Find in prefabs
            var prefabRefs = new List<string>();
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponentInChildren(scriptType, true) != null)
                    prefabRefs.Add(path);
                if (prefabRefs.Count >= 50) break;
            }

            return new SuccessResponse($"References for {scriptPath}", new
            {
                scriptPath, sceneReferences = sceneRefs, prefabReferences = prefabRefs
            });
        }

        private static object FindMissingReferences(JObject @params, ToolParams p)
        {
            bool searchScene = p.GetBool("search_scene", true);
            bool searchPrefabs = p.GetBool("search_prefabs", false);

            var missing = new List<object>();

            if (searchScene)
            {
                GameObject[] allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var go in allGOs)
                {
                    Component[] comps = go.GetComponents<Component>();
                    for (int i = 0; i < comps.Length; i++)
                    {
                        if (comps[i] == null)
                        {
                            missing.Add(new { gameObject = GetGameObjectPath(go), type = "missing_script", index = i });
                        }
                        else
                        {
                            SerializedObject so = new SerializedObject(comps[i]);
                            SerializedProperty sp = so.GetIterator();
                            while (sp.NextVisible(true))
                            {
                                if (sp.propertyType == SerializedPropertyType.ObjectReference &&
                                    sp.objectReferenceValue == null &&
                                    sp.objectReferenceInstanceIDValue != 0)
                                {
                                    missing.Add(new
                                    {
                                        gameObject = GetGameObjectPath(go),
                                        component = comps[i].GetType().Name,
                                        property = sp.propertyPath,
                                        type = "missing_reference"
                                    });
                                }
                            }
                        }
                    }
                    if (missing.Count >= 100) break;
                }
            }

            return new SuccessResponse($"Found {missing.Count} missing references", new
            {
                missingReferences = missing, count = missing.Count
            });
        }

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null) { path = t.name + "/" + path; t = t.parent; }
            return path;
        }
    }
}
