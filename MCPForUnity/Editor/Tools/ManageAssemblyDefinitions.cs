using System;
using System.IO;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_assembly_definitions", AutoRegister = false)]
    public static class ManageAssemblyDefinitions
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
                    case "create": return Create(@params, p);
                    case "add_reference": return AddReference(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    case "list": return ListAll(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: create, add_reference, get_info, list");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object Create(JObject @params, ToolParams p)
        {
            var nameResult = p.GetRequired("name");
            var nameError = nameResult.GetOrError(out string name);
            if (nameError != null) return nameError;

            string folder = p.Get("folder", "Assets/Scripts/" + name);
            string path = Path.Combine(folder, name + ".asmdef");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var asmdef = new JObject
            {
                ["name"] = name,
                ["rootNamespace"] = p.Get("root_namespace", ""),
                ["references"] = new JArray(),
                ["includePlatforms"] = new JArray(),
                ["excludePlatforms"] = new JArray(),
                ["allowUnsafeCode"] = p.GetBool("allow_unsafe", false),
                ["overrideReferences"] = false,
                ["precompiledReferences"] = new JArray(),
                ["autoReferenced"] = p.GetBool("auto_referenced", true),
                ["defineConstraints"] = new JArray(),
                ["versionDefines"] = new JArray(),
                ["noEngineReferences"] = p.GetBool("no_engine_references", false)
            };

            JToken refs = p.GetRaw("references");
            if (refs != null) asmdef["references"] = refs;

            JToken platforms = p.GetRaw("include_platforms");
            if (platforms != null) asmdef["includePlatforms"] = platforms;

            JToken defines = p.GetRaw("define_constraints");
            if (defines != null) asmdef["defineConstraints"] = defines;

            File.WriteAllText(path, asmdef.ToString(Formatting.Indented));
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created assembly definition '{name}' at {path}");
        }

        private static object AddReference(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("asmdef_path");
            var pathError = pathResult.GetOrError(out string asmdefPath);
            if (pathError != null) return pathError;

            var refResult = p.GetRequired("reference");
            var refError = refResult.GetOrError(out string reference);
            if (refError != null) return refError;

            if (!File.Exists(asmdefPath))
                return new ErrorResponse($"File not found: {asmdefPath}");

            string json = File.ReadAllText(asmdefPath);
            JObject asmdef = JObject.Parse(json);

            JArray references = asmdef["references"] as JArray ?? new JArray();
            if (!references.ToString().Contains(reference))
            {
                references.Add(reference);
                asmdef["references"] = references;
                File.WriteAllText(asmdefPath, asmdef.ToString(Formatting.Indented));
                AssetDatabase.Refresh();
            }

            return new SuccessResponse($"Added reference '{reference}' to {asmdefPath}");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("asmdef_path");
            var pathError = pathResult.GetOrError(out string asmdefPath);
            if (pathError != null) return pathError;

            if (!File.Exists(asmdefPath))
                return new ErrorResponse($"File not found: {asmdefPath}");

            string json = File.ReadAllText(asmdefPath);
            JObject asmdef = JObject.Parse(json);

            return new SuccessResponse($"Assembly definition: {asmdefPath}", asmdef);
        }

        private static object ListAll(JObject @params, ToolParams p)
        {
            string[] guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            var results = new System.Collections.Generic.List<object>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                results.Add(new { path, name = Path.GetFileNameWithoutExtension(path) });
            }

            return new SuccessResponse($"Found {results.Count} assembly definitions", new { assemblies = results });
        }
    }
}
