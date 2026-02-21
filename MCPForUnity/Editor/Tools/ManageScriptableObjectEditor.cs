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
    [McpForUnityTool("manage_scriptable_object_editor")]
    public static class ManageScriptableObjectEditor
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
                    case "create_so": return CreateScriptableObject(@params);
                    case "generate_editor": return GenerateEditorScript(@params);
                    case "list_so_types": return ListScriptableObjectTypes(@params);
                    case "get_so_info": return GetScriptableObjectInfo(@params);
                    case "set_so_field": return SetScriptableObjectField(@params);
                    case "list_so_assets": return ListScriptableObjectAssets(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageScriptableObjectEditor error: {e.Message}");
            }
        }

        private static object CreateScriptableObject(JObject @params)
        {
            string typeName = @params["type"]?.ToString();
            string path = @params["path"]?.ToString();

            if (string.IsNullOrEmpty(typeName))
                return new ErrorResponse("'type' is required (fully qualified type name).");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' is required (e.g. 'Assets/Data/MyAsset.asset').");

            Type soType = FindScriptableObjectType(typeName);
            if (soType == null)
                return new ErrorResponse($"ScriptableObject type '{typeName}' not found.");

            var instance = ScriptableObject.CreateInstance(soType);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                string fullDir = Path.Combine(Application.dataPath.Replace("/Assets", ""), dir);
                if (!Directory.Exists(fullDir)) Directory.CreateDirectory(fullDir);
            }

            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created '{soType.Name}' at '{path}'.", new
            {
                assetPath = path,
                type = soType.FullName
            });
        }

        private static object GenerateEditorScript(JObject @params)
        {
            string soTypeName = @params["type"]?.ToString();
            string savePath = @params["save_path"]?.ToString();

            if (string.IsNullOrEmpty(soTypeName))
                return new ErrorResponse("'type' is required.");

            Type soType = FindScriptableObjectType(soTypeName);
            if (soType == null)
                return new ErrorResponse($"ScriptableObject type '{soTypeName}' not found.");

            // Get all serialized fields to generate proper property drawers
            var fields = soType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                .ToList();

            string editorClassName = $"{soType.Name}Editor";
            savePath = savePath ?? $"Assets/Editor/{editorClassName}.cs";

            // Generate property field code for each serialized field
            var propertyLines = fields
                .Select(f => $"            EditorGUILayout.PropertyField(serializedObject.FindProperty(\"{f.Name}\"));")
                .ToList();

            string fieldDrawers = propertyLines.Count > 0
                ? string.Join("\n", propertyLines)
                : "            DrawDefaultInspector();";

            string scriptContent = $@"using UnityEditor;
using UnityEngine;

[CustomEditor(typeof({soType.FullName}))]
public class {editorClassName} : Editor
{{
    public override void OnInspectorGUI()
    {{
        serializedObject.Update();

        EditorGUILayout.LabelField(""{soType.Name}"", EditorStyles.boldLabel);
        EditorGUILayout.Space();

{fieldDrawers}

        EditorGUILayout.Space();
        if (GUILayout.Button(""Reset to Defaults""))
        {{
            Undo.RecordObject(target, ""Reset {soType.Name}"");
            var defaultInstance = CreateInstance<{soType.FullName}>();
            EditorUtility.CopySerialized(defaultInstance, target);
            DestroyImmediate(defaultInstance);
        }}

        serializedObject.ApplyModifiedProperties();
    }}
}}";

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), savePath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, scriptContent);
            AssetDatabase.ImportAsset(savePath);

            return new SuccessResponse($"Generated custom editor for '{soType.Name}' at '{savePath}'.", new
            {
                savePath,
                soType = soType.FullName,
                editorClass = editorClassName,
                fieldsCount = fields.Count
            });
        }

        private static object ListScriptableObjectTypes(JObject @params)
        {
            bool includeUnity = @params["include_unity"]?.ToObject<bool>() ?? false;

            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .Where(t => includeUnity || t.Assembly.FullName.Contains("Assembly-CSharp")) // Filter to project types
                .Select(t => new
                {
                    name = t.Name,
                    fullName = t.FullName,
                    assembly = t.Assembly.GetName().Name,
                    fieldCount = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Count(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                })
                .OrderBy(t => t.fullName)
                .ToList();

            return new SuccessResponse($"Found {types.Count} ScriptableObject type(s).", new { types });
        }

        private static object GetScriptableObjectInfo(JObject @params)
        {
            string assetPath = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'path' is required.");

            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (so == null) return new ErrorResponse($"No ScriptableObject found at '{assetPath}'.");

            var serializedObj = new SerializedObject(so);
            var properties = new List<object>();

            var prop = serializedObj.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    // Skip the m_Script reference field
                    if (prop.name == "m_Script") continue;

                    properties.Add(new
                    {
                        name = prop.name,
                        displayName = prop.displayName,
                        type = prop.propertyType.ToString(),
                        value = GetPropertyValue(prop),
                        isArray = prop.isArray,
                        arraySize = prop.isArray ? prop.arraySize : 0,
                        tooltip = prop.tooltip
                    });
                }
                while (prop.NextVisible(false));
            }

            return new SuccessResponse($"Info for '{assetPath}'.", new
            {
                assetPath,
                type = so.GetType().FullName,
                name = so.name,
                propertyCount = properties.Count,
                properties
            });
        }

        private static object SetScriptableObjectField(JObject @params)
        {
            string assetPath = @params["path"]?.ToString();
            string fieldName = @params["field"]?.ToString();

            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'path' is required.");
            if (string.IsNullOrEmpty(fieldName))
                return new ErrorResponse("'field' is required.");
            if (@params["value"] == null)
                return new ErrorResponse("'value' is required.");

            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (so == null) return new ErrorResponse($"No ScriptableObject found at '{assetPath}'.");

            var serializedObj = new SerializedObject(so);
            var prop = serializedObj.FindProperty(fieldName);
            if (prop == null)
                return new ErrorResponse($"Property '{fieldName}' not found on '{so.GetType().Name}'.");

            Undo.RecordObject(so, $"Set {fieldName}");

            bool success = SetPropertyValue(prop, @params["value"]);
            if (!success)
                return new ErrorResponse($"Could not set property '{fieldName}' (type: {prop.propertyType}).");

            serializedObj.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Set '{fieldName}' on '{so.name}'.", new
            {
                assetPath,
                field = fieldName,
                newValue = GetPropertyValue(prop)
            });
        }

        private static object ListScriptableObjectAssets(JObject @params)
        {
            string typeName = @params["type"]?.ToString();
            string folder = @params["folder"]?.ToString() ?? "Assets";

            string filter = string.IsNullOrEmpty(typeName)
                ? "t:ScriptableObject"
                : $"t:{typeName}";

            var guids = AssetDatabase.FindAssets(filter, new[] { folder });
            var assets = guids.Select(guid =>
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                return new
                {
                    path,
                    name = asset != null ? asset.name : Path.GetFileNameWithoutExtension(path),
                    type = asset != null ? asset.GetType().FullName : "Unknown"
                };
            }).ToList();

            return new SuccessResponse($"Found {assets.Count} ScriptableObject asset(s).", new { assets });
        }

        // ─── Helpers ────────────────────────────────────────────

        private static Type FindScriptableObjectType(string typeName)
        {
            // Try TypeCache first (fast)
            var match = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
            return match;
        }

        private static string GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F4");
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                case SerializedPropertyType.Enum:
                    return prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
                        ? prop.enumNames[prop.enumValueIndex] : prop.intValue.ToString();
                case SerializedPropertyType.Vector2: return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4: return prop.vector4Value.ToString();
                default: return $"({prop.propertyType})";
            }
        }

        private static bool SetPropertyValue(SerializedProperty prop, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = value.ToObject<int>();
                    return true;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.ToObject<bool>();
                    return true;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.ToObject<float>();
                    return true;
                case SerializedPropertyType.String:
                    prop.stringValue = value.ToString();
                    return true;
                case SerializedPropertyType.Color:
                    if (value is JObject colorObj)
                    {
                        prop.colorValue = new Color(
                            colorObj["r"]?.ToObject<float>() ?? 0,
                            colorObj["g"]?.ToObject<float>() ?? 0,
                            colorObj["b"]?.ToObject<float>() ?? 0,
                            colorObj["a"]?.ToObject<float>() ?? 1
                        );
                        return true;
                    }
                    return false;
                case SerializedPropertyType.Vector2:
                    if (value is JObject v2Obj)
                    {
                        prop.vector2Value = new Vector2(
                            v2Obj["x"]?.ToObject<float>() ?? 0,
                            v2Obj["y"]?.ToObject<float>() ?? 0
                        );
                        return true;
                    }
                    return false;
                case SerializedPropertyType.Vector3:
                    if (value is JObject v3Obj)
                    {
                        prop.vector3Value = new Vector3(
                            v3Obj["x"]?.ToObject<float>() ?? 0,
                            v3Obj["y"]?.ToObject<float>() ?? 0,
                            v3Obj["z"]?.ToObject<float>() ?? 0
                        );
                        return true;
                    }
                    return false;
                case SerializedPropertyType.Enum:
                    if (value.Type == JTokenType.Integer)
                    {
                        prop.enumValueIndex = value.ToObject<int>();
                        return true;
                    }
                    else if (value.Type == JTokenType.String)
                    {
                        int idx = Array.IndexOf(prop.enumNames, value.ToString());
                        if (idx >= 0) { prop.enumValueIndex = idx; return true; }
                    }
                    return false;
                default:
                    return false;
            }
        }
    }
}
