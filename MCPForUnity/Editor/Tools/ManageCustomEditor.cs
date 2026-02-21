using System;
using System.IO;
using System.Text;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_custom_editor", AutoRegister = false)]
    public static class ManageCustomEditor
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
                    case "generate_editor": return GenerateEditor(@params, p);
                    case "generate_property_drawer": return GeneratePropertyDrawer(@params, p);
                    case "list_custom_editors": return ListCustomEditors(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: generate_editor, generate_property_drawer, list_custom_editors");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object GenerateEditor(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target_script");
            var targetError = targetResult.GetOrError(out string targetScript);
            if (targetError != null) return targetError;

            // Load the target script to inspect its fields
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(targetScript);
            if (script == null) return new ErrorResponse($"Script not found: {targetScript}");

            Type targetType = script.GetClass();
            if (targetType == null) return new ErrorResponse("Could not get type from script.");

            string className = targetType.Name;
            string editorName = className + "Editor";

            string savePath = p.Get("save_path", $"Assets/Editor/{editorName}.cs");

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"[CustomEditor(typeof({className}))]");
            sb.AppendLine($"public class {editorName} : Editor");
            sb.AppendLine("{");

            // Collect serialized fields
            var fields = targetType.GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            // Generate serialized property declarations
            foreach (var field in fields)
            {
                bool isSerializable = field.IsPublic ||
                    Attribute.IsDefined(field, typeof(SerializeField));
                if (!isSerializable) continue;
                if (field.IsNotSerialized) continue;

                sb.AppendLine($"    private SerializedProperty {field.Name}Prop;");
            }

            sb.AppendLine();
            sb.AppendLine("    private void OnEnable()");
            sb.AppendLine("    {");
            foreach (var field in fields)
            {
                bool isSerializable = field.IsPublic ||
                    Attribute.IsDefined(field, typeof(SerializeField));
                if (!isSerializable || field.IsNotSerialized) continue;
                sb.AppendLine($"        {field.Name}Prop = serializedObject.FindProperty(\"{field.Name}\");");
            }
            sb.AppendLine("    }");

            sb.AppendLine();
            sb.AppendLine("    public override void OnInspectorGUI()");
            sb.AppendLine("    {");
            sb.AppendLine("        serializedObject.Update();");
            sb.AppendLine();

            // Add header
            sb.AppendLine($"        EditorGUILayout.LabelField(\"{className} Inspector\", EditorStyles.boldLabel);");
            sb.AppendLine("        EditorGUILayout.Space();");

            foreach (var field in fields)
            {
                bool isSerializable = field.IsPublic ||
                    Attribute.IsDefined(field, typeof(SerializeField));
                if (!isSerializable || field.IsNotSerialized) continue;

                // Pretty name
                string label = System.Text.RegularExpressions.Regex.Replace(
                    field.Name, "(\\B[A-Z])", " $1");
                label = char.ToUpper(label[0]) + label.Substring(1);

                // Check for Header/Space attributes
                if (Attribute.IsDefined(field, typeof(HeaderAttribute)))
                {
                    var header = (HeaderAttribute)Attribute.GetCustomAttribute(field, typeof(HeaderAttribute));
                    sb.AppendLine($"        EditorGUILayout.Space();");
                    sb.AppendLine($"        EditorGUILayout.LabelField(\"{header.header}\", EditorStyles.boldLabel);");
                }

                sb.AppendLine($"        EditorGUILayout.PropertyField({field.Name}Prop, new GUIContent(\"{label}\"));");
            }

            sb.AppendLine();
            sb.AppendLine("        serializedObject.ApplyModifiedProperties();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string dir = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(savePath, sb.ToString());
            AssetDatabase.Refresh();

            return new SuccessResponse($"Generated custom editor '{editorName}' at {savePath}", new
            {
                editorScript = savePath, targetScript, className,
                fieldCount = fields.Length
            });
        }

        private static object GeneratePropertyDrawer(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target_type");
            var targetError = targetResult.GetOrError(out string targetType);
            if (targetError != null) return targetError;

            string drawerName = targetType + "Drawer";
            string savePath = p.Get("save_path", $"Assets/Editor/{drawerName}.cs");

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"[CustomPropertyDrawer(typeof({targetType}))]");
            sb.AppendLine($"public class {drawerName} : PropertyDrawer");
            sb.AppendLine("{");
            sb.AppendLine("    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)");
            sb.AppendLine("    {");
            sb.AppendLine("        EditorGUI.BeginProperty(position, label, property);");
            sb.AppendLine("        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);");
            sb.AppendLine();
            sb.AppendLine("        var indent = EditorGUI.indentLevel;");
            sb.AppendLine("        EditorGUI.indentLevel = 0;");
            sb.AppendLine();
            sb.AppendLine("        // TODO: Draw your custom properties here");
            sb.AppendLine("        // Example:");
            sb.AppendLine("        // var nameRect = new Rect(position.x, position.y, position.width * 0.5f, position.height);");
            sb.AppendLine("        // EditorGUI.PropertyField(nameRect, property.FindPropertyRelative(\"fieldName\"), GUIContent.none);");
            sb.AppendLine();
            sb.AppendLine("        EditorGUI.indentLevel = indent;");
            sb.AppendLine("        EditorGUI.EndProperty();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)");
            sb.AppendLine("    {");
            sb.AppendLine("        return EditorGUIUtility.singleLineHeight;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string dir = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(savePath, sb.ToString());
            AssetDatabase.Refresh();

            return new SuccessResponse($"Generated property drawer '{drawerName}' at {savePath}");
        }

        private static object ListCustomEditors(JObject @params, ToolParams p)
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Editor" });
            var editors = new System.Collections.Generic.List<object>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null)
                {
                    Type type = script.GetClass();
                    if (type != null && (typeof(UnityEditor.Editor).IsAssignableFrom(type) ||
                        typeof(PropertyDrawer).IsAssignableFrom(type)))
                    {
                        editors.Add(new
                        {
                            name = type.Name,
                            path,
                            isEditor = typeof(UnityEditor.Editor).IsAssignableFrom(type),
                            isPropertyDrawer = typeof(PropertyDrawer).IsAssignableFrom(type)
                        });
                    }
                }
            }

            return new SuccessResponse($"Found {editors.Count} custom editors", new { editors });
        }
    }
}
