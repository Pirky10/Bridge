using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("validate_project", AutoRegister = false)]
    public static class ValidateProject
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
                    case "check_missing_scripts": return CheckMissingScripts(@params, p);
                    case "check_missing_references": return CheckMissingRefs(@params, p);
                    case "check_empty_gameobjects": return CheckEmpty(@params, p);
                    case "check_duplicate_names": return CheckDuplicates(@params, p);
                    case "full_validation": return FullValidation(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: check_missing_scripts, check_missing_references, check_empty_gameobjects, check_duplicate_names, full_validation");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object CheckMissingScripts(JObject @params, ToolParams p)
        {
            var issues = new List<object>();
            GameObject[] allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0)
                    issues.Add(new { path = GetPath(go), missingCount = missing });
                if (issues.Count >= 100) break;
            }
            return new SuccessResponse($"Found {issues.Count} objects with missing scripts", new { issues });
        }

        private static object CheckMissingRefs(JObject @params, ToolParams p)
        {
            var issues = new List<object>();
            GameObject[] allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    SerializedObject so = new SerializedObject(comp);
                    SerializedProperty sp = so.GetIterator();
                    while (sp.NextVisible(true))
                    {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference &&
                            sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0)
                        {
                            issues.Add(new { path = GetPath(go), component = comp.GetType().Name, property = sp.propertyPath });
                        }
                    }
                }
                if (issues.Count >= 100) break;
            }
            return new SuccessResponse($"Found {issues.Count} missing references", new { issues });
        }

        private static object CheckEmpty(JObject @params, ToolParams p)
        {
            var emptyGOs = new List<string>();
            GameObject[] allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                var comps = go.GetComponents<Component>();
                if (comps.Length == 1 && go.transform.childCount == 0) // Only Transform
                    emptyGOs.Add(GetPath(go));
                if (emptyGOs.Count >= 100) break;
            }
            return new SuccessResponse($"Found {emptyGOs.Count} empty GameObjects", new { emptyGameObjects = emptyGOs });
        }

        private static object CheckDuplicates(JObject @params, ToolParams p)
        {
            var names = new Dictionary<string, List<string>>();
            GameObject[] allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                if (!names.ContainsKey(go.name))
                    names[go.name] = new List<string>();
                names[go.name].Add(GetPath(go));
            }

            var duplicates = new List<object>();
            foreach (var kvp in names)
            {
                if (kvp.Value.Count > 1)
                    duplicates.Add(new { name = kvp.Key, count = kvp.Value.Count, paths = kvp.Value });
                if (duplicates.Count >= 50) break;
            }

            return new SuccessResponse($"Found {duplicates.Count} duplicate names", new { duplicates });
        }

        private static object FullValidation(JObject @params, ToolParams p)
        {
            var results = new Dictionary<string, object>();

            // Missing scripts
            int missingScripts = 0;
            GameObject[] allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allGOs)
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            results["missingScripts"] = missingScripts;

            // Missing references
            int missingRefs = 0;
            foreach (var go in allGOs)
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    SerializedObject so = new SerializedObject(comp);
                    SerializedProperty sp = so.GetIterator();
                    while (sp.NextVisible(true))
                    {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference &&
                            sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0)
                            missingRefs++;
                    }
                }
            }
            results["missingReferences"] = missingRefs;

            // Empty GOs
            int empty = 0;
            foreach (var go in allGOs)
            {
                var comps = go.GetComponents<Component>();
                if (comps.Length == 1 && go.transform.childCount == 0) empty++;
            }
            results["emptyGameObjects"] = empty;

            results["totalGameObjects"] = allGOs.Length;
            results["healthy"] = missingScripts == 0 && missingRefs == 0;

            return new SuccessResponse("Full project validation", results);
        }

        private static string GetPath(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null) { path = t.name + "/" + path; t = t.parent; }
            return path;
        }
    }
}
