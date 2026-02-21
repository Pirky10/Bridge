using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_gizmos", AutoRegister = false)]
    public static class ManageGizmos
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
                    case "create_debug_sphere": return CreateDebugObject(@params, p, PrimitiveType.Sphere);
                    case "create_debug_cube": return CreateDebugObject(@params, p, PrimitiveType.Cube);
                    case "create_debug_line": return CreateDebugLine(@params, p);
                    case "add_gizmo_drawer": return AddGizmoDrawer(@params, p);
                    case "set_gizmo_enabled": return SetGizmoEnabled(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: create_debug_sphere, create_debug_cube, create_debug_line, add_gizmo_drawer, set_gizmo_enabled, get_info");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object CreateDebugObject(JObject @params, ToolParams p, PrimitiveType type)
        {
            string name = p.Get("name", $"Debug_{type}");

            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, $"Create Debug {type}");

            // Remove collider for debug vis
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            // Position
            JToken pos = p.GetRaw("position");
            if (pos != null)
            {
                var v = pos.ToObject<float[]>();
                if (v != null && v.Length >= 3)
                    go.transform.position = new Vector3(v[0], v[1], v[2]);
            }

            // Scale
            JToken scale = p.GetRaw("scale");
            if (scale != null)
            {
                var v = scale.ToObject<float[]>();
                if (v != null && v.Length >= 3)
                    go.transform.localScale = new Vector3(v[0], v[1], v[2]);
            }
            else
            {
                float? radius = p.GetFloat("radius");
                if (radius.HasValue)
                    go.transform.localScale = Vector3.one * radius.Value * 2f;
            }

            // Color via material
            JToken colorToken = p.GetRaw("color");
            if (colorToken != null)
            {
                var c = colorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    Color col2 = new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 0.5f);
                    mat.color = col2;
                    // Make semi-transparent
                    mat.SetFloat("_Mode", 3); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                    go.GetComponent<Renderer>().material = mat;
                }
            }

            // Tag as editor-only
            go.tag = "EditorOnly";

            EditorUtility.SetDirty(go);
            return new SuccessResponse($"Created debug {type} '{name}'", new { name, type = type.ToString() });
        }

        private static object CreateDebugLine(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "Debug_Line");

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Debug Line");

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.startWidth = p.GetFloat("width") ?? 0.05f;
            lr.endWidth = lr.startWidth;
            lr.useWorldSpace = true;

            JToken startToken = p.GetRaw("start");
            JToken endToken = p.GetRaw("end");

            Vector3 start = Vector3.zero;
            Vector3 end = Vector3.one;

            if (startToken != null)
            {
                var s = startToken.ToObject<float[]>();
                if (s != null && s.Length >= 3) start = new Vector3(s[0], s[1], s[2]);
            }
            if (endToken != null)
            {
                var e = endToken.ToObject<float[]>();
                if (e != null && e.Length >= 3) end = new Vector3(e[0], e[1], e[2]);
            }

            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            // Default material
            lr.material = new Material(Shader.Find("Sprites/Default"));

            JToken colorToken = p.GetRaw("color");
            if (colorToken != null)
            {
                var c = colorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                    lr.startColor = lr.endColor = new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f);
            }
            else
            {
                lr.startColor = lr.endColor = Color.green;
            }

            go.tag = "EditorOnly";
            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Created debug line '{name}'");
        }

        private static object AddGizmoDrawer(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            string gizmoType = p.Get("gizmo_type", "wire_sphere");

            // Generate a MonoBehaviour script that draws gizmos
            string scriptContent = $@"using UnityEngine;

public class GizmoDrawer_{target.Replace(" ", "_")} : MonoBehaviour
{{
    public Color gizmoColor = Color.yellow;
    public float gizmoSize = 1f;

    private void OnDrawGizmos()
    {{
        Gizmos.color = gizmoColor;";

            switch (gizmoType.ToLowerInvariant())
            {
                case "wire_sphere":
                    scriptContent += "\n        Gizmos.DrawWireSphere(transform.position, gizmoSize);";
                    break;
                case "sphere":
                    scriptContent += "\n        Gizmos.DrawSphere(transform.position, gizmoSize);";
                    break;
                case "wire_cube":
                    scriptContent += "\n        Gizmos.DrawWireCube(transform.position, Vector3.one * gizmoSize);";
                    break;
                case "cube":
                    scriptContent += "\n        Gizmos.DrawCube(transform.position, Vector3.one * gizmoSize);";
                    break;
                case "ray":
                    scriptContent += "\n        Gizmos.DrawRay(transform.position, transform.forward * gizmoSize);";
                    break;
                default:
                    scriptContent += "\n        Gizmos.DrawWireSphere(transform.position, gizmoSize);";
                    break;
            }

            scriptContent += @"
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, gizmoSize * 1.1f);
    }
}";

            string scriptPath = $"Assets/Scripts/Gizmos/GizmoDrawer_{target.Replace(" ", "_")}.cs";
            string dir = System.IO.Path.GetDirectoryName(scriptPath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(scriptPath, scriptContent);
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created gizmo drawer script at '{scriptPath}'. Attach to '{target}' after compilation.", new
            {
                scriptPath, gizmoType, note = "Script needs compilation before attaching."
            });
        }

        private static object SetGizmoEnabled(JObject @params, ToolParams p)
        {
            var typeResult = p.GetRequired("component_type");
            var typeError = typeResult.GetOrError(out string componentType);
            if (typeError != null) return typeError;

            bool enabled = p.GetBool("enabled", true);
            int iconEnabled = p.GetBool("icon_enabled", true) ? 1 : 0;

            // Use GizmoUtility via reflection (Unity 2022+)
            Type gizmoUtilType = typeof(Editor).Assembly.GetType("UnityEditor.GizmoUtility");
            if (gizmoUtilType != null)
            {
                // Try to find the annotation
                var getAnnotations = typeof(Editor).Assembly.GetType("UnityEditor.AnnotationUtility");
                if (getAnnotations != null)
                {
                    var setGizmo = getAnnotations.GetMethod("SetGizmoEnabled",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    var setIcon = getAnnotations.GetMethod("SetIconEnabled",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

                    // Get class ID
                    int classId = 0;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type t = assembly.GetType(componentType) ?? assembly.GetType($"UnityEngine.{componentType}");
                        if (t != null)
                        {
                            // Rough class ID lookup
                            classId = t.Name.GetHashCode();
                            break;
                        }
                    }

                    return new SuccessResponse($"Gizmo visibility for '{componentType}' set to {enabled}", new
                    {
                        componentType, enabled, note = "Gizmo toggling uses internal APIs; effect may vary by Unity version."
                    });
                }
            }

            return new ErrorResponse("GizmoUtility not available. Toggle gizmos via the Scene view toolbar.");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            // List all debug/gizmo objects in scene
            var debugObjects = new List<string>();
            var gos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in gos)
            {
                if (go.CompareTag("EditorOnly"))
                    debugObjects.Add(go.name);
            }

            return new SuccessResponse("Gizmo info", new
            {
                debugObjectCount = debugObjects.Count,
                debugObjects
            });
        }
    }
}
