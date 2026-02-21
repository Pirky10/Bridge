using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_scriptable_objects_bulk", AutoRegister = false)]
    public static class ManageScriptableObjectsBulk
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
                    case "create_instances": return CreateInstances(@params, p);
                    case "create_from_template": return CreateFromTemplate(@params, p);
                    case "bulk_set_field": return BulkSetField(@params, p);
                    case "list_types": return ListSOTypes(@params, p);
                    case "list_instances": return ListInstances(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: create_instances, create_from_template, bulk_set_field, list_types, list_instances");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object CreateInstances(JObject @params, ToolParams p)
        {
            var typeResult = p.GetRequired("so_type");
            var typeError = typeResult.GetOrError(out string soTypeName);
            if (typeError != null) return typeError;

            int count = p.GetInt("count") ?? 1;
            string folder = p.Get("folder", "Assets/ScriptableObjects");
            string namePrefix = p.Get("name_prefix", soTypeName);

            // Find the SO type
            Type soType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                soType = assembly.GetType(soTypeName);
                if (soType != null) break;
            }

            if (soType == null || !typeof(ScriptableObject).IsAssignableFrom(soType))
                return new ErrorResponse($"ScriptableObject type '{soTypeName}' not found.");

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var created = new List<string>();
            for (int i = 0; i < count; i++)
            {
                ScriptableObject so = ScriptableObject.CreateInstance(soType);
                string assetName = count == 1 ? $"{namePrefix}.asset" : $"{namePrefix}_{i + 1}.asset";
                string path = Path.Combine(folder, assetName);

                AssetDatabase.CreateAsset(so, path);
                created.Add(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created {created.Count} {soTypeName} instances", new
            {
                type = soTypeName, count = created.Count, paths = created
            });
        }

        private static object CreateFromTemplate(JObject @params, ToolParams p)
        {
            var templateResult = p.GetRequired("template_path");
            var templateError = templateResult.GetOrError(out string templatePath);
            if (templateError != null) return templateError;

            int count = p.GetInt("count") ?? 1;
            string folder = p.Get("folder", Path.GetDirectoryName(templatePath));
            string namePrefix = p.Get("name_prefix", Path.GetFileNameWithoutExtension(templatePath));

            ScriptableObject template = AssetDatabase.LoadAssetAtPath<ScriptableObject>(templatePath);
            if (template == null) return new ErrorResponse($"Template not found: {templatePath}");

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var created = new List<string>();
            for (int i = 0; i < count; i++)
            {
                ScriptableObject clone = UnityEngine.Object.Instantiate(template);
                string assetName = $"{namePrefix}_copy_{i + 1}.asset";
                string path = Path.Combine(folder, assetName);

                AssetDatabase.CreateAsset(clone, path);
                created.Add(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created {created.Count} copies from template", new
            {
                template = templatePath, count = created.Count, paths = created
            });
        }

        private static object BulkSetField(JObject @params, ToolParams p)
        {
            var folderResult = p.GetRequired("folder");
            var folderError = folderResult.GetOrError(out string folder);
            if (folderError != null) return folderError;

            var fieldResult = p.GetRequired("field_name");
            var fieldError = fieldResult.GetOrError(out string fieldName);
            if (fieldError != null) return fieldError;

            JToken valueToken = p.GetRaw("field_value");
            if (valueToken == null) return new ErrorResponse("'field_value' required.");

            string soFilter = p.Get("so_type", "ScriptableObject");

            string[] guids = AssetDatabase.FindAssets($"t:{soFilter}", new[] { folder });
            int modified = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                SerializedObject serialized = new SerializedObject(so);
                SerializedProperty prop = serialized.FindProperty(fieldName);
                if (prop == null) continue;

                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        prop.intValue = valueToken.Value<int>();
                        break;
                    case SerializedPropertyType.Float:
                        prop.floatValue = valueToken.Value<float>();
                        break;
                    case SerializedPropertyType.String:
                        prop.stringValue = valueToken.Value<string>();
                        break;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = valueToken.Value<bool>();
                        break;
                    default:
                        continue;
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                modified++;
            }

            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Set '{fieldName}' on {modified} ScriptableObjects in {folder}", new
            {
                field = fieldName, modified, folder
            });
        }

        private static object ListSOTypes(JObject @params, ToolParams p)
        {
            var types = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith("Unity") || assembly.FullName.StartsWith("System") ||
                    assembly.FullName.StartsWith("mscorlib") || assembly.FullName.StartsWith("netstandard"))
                    continue;

                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(ScriptableObject).IsAssignableFrom(type) && !type.IsAbstract &&
                        !type.FullName.StartsWith("UnityEditor") && !type.FullName.StartsWith("UnityEngine"))
                    {
                        types.Add(type.FullName);
                    }
                }
            }

            return new SuccessResponse($"Found {types.Count} custom ScriptableObject types", new { types });
        }

        private static object ListInstances(JObject @params, ToolParams p)
        {
            string soFilter = p.Get("so_type", "ScriptableObject");
            string folder = p.Get("folder");

            string[] guids = folder != null
                ? AssetDatabase.FindAssets($"t:{soFilter}", new[] { folder })
                : AssetDatabase.FindAssets($"t:{soFilter}");

            var instances = new List<object>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null)
                {
                    instances.Add(new { path, name = so.name, type = so.GetType().Name });
                }
                if (instances.Count >= 100) break;
            }

            return new SuccessResponse($"Found {instances.Count} ScriptableObject instances", new { instances });
        }
    }
}
