using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Manages Shader Graph assets by reading/writing the underlying JSON file format.
    /// Shader Graph files (.shadergraph) are serialized as JSON, so we can inspect and
    /// manipulate them directly. For authoring complex graphs, use the Shader Graph editor.
    /// </summary>
    [McpForUnityTool("manage_shader_graph")]
    public static class ManageShaderGraph
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
                    case "create_shader_graph": return CreateShaderGraph(@params);
                    case "get_graph_info": return GetGraphInfo(@params);
                    case "list_shader_graphs": return ListShaderGraphs(@params);
                    case "add_property": return AddProperty(@params);
                    case "get_properties": return GetProperties(@params);
                    case "open_in_editor": return OpenInEditor(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageShaderGraph error: {e.Message}");
            }
        }

        private static object CreateShaderGraph(JObject @params)
        {
            string path = @params["path"]?.ToString() ?? "Assets/NewShaderGraph.shadergraph";
            if (!path.EndsWith(".shadergraph")) path += ".shadergraph";

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(
                    Path.Combine(Application.dataPath.Replace("/Assets", ""), dir)))
            {
                Directory.CreateDirectory(
                    Path.Combine(Application.dataPath.Replace("/Assets", ""), dir));
            }

            // A minimal valid Shader Graph file that Unity will recognize and open correctly.
            // This is the actual file format Unity uses.
            var graphJson = new JObject
            {
                ["m_SGVersion"] = 3,
                ["m_Type"] = "UnityEditor.ShaderGraph.GraphData",
                ["m_ObjectId"] = Guid.NewGuid().ToString("N"),
                ["m_Properties"] = new JArray(),
                ["m_Keywords"] = new JArray(),
                ["m_Dropdowns"] = new JArray(),
                ["m_CategoryData"] = new JArray(),
                ["m_Nodes"] = new JArray(),
                ["m_GroupDatas"] = new JArray(),
                ["m_StickyNoteDatas"] = new JArray(),
                ["m_Edges"] = new JArray(),
                ["m_VertexContext"] = new JObject
                {
                    ["m_Position"] = new JObject { ["x"] = 0.0, ["y"] = 0.0 },
                    ["m_Blocks"] = new JArray()
                },
                ["m_FragmentContext"] = new JObject
                {
                    ["m_Position"] = new JObject { ["x"] = 200.0, ["y"] = 0.0 },
                    ["m_Blocks"] = new JArray()
                },
                ["m_Path"] = "Shader Graphs"
            };

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            File.WriteAllText(fullPath, graphJson.ToString());
            AssetDatabase.ImportAsset(path);

            return new SuccessResponse($"Created Shader Graph at '{path}'.", new
            {
                assetPath = path
            });
        }

        private static object GetGraphInfo(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' is required.");

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            if (!File.Exists(fullPath))
                return new ErrorResponse($"Shader Graph file not found: '{path}'.");

            string json = File.ReadAllText(fullPath);
            JObject graph = JObject.Parse(json);

            var nodes = graph["m_Nodes"]?.Select(n => new
            {
                objectId = n["m_ObjectId"]?.ToString(),
                type = n["m_Type"]?.ToString(),
                name = n["m_Name"]?.ToString(),
                position = n["m_Position"] != null
                    ? new { x = n["m_Position"]["x"]?.ToObject<float>(), y = n["m_Position"]["y"]?.ToObject<float>() }
                    : null
            }).ToList();

            var properties = graph["m_Properties"]?.Select(pr => new
            {
                objectId = pr["m_ObjectId"]?.ToString(),
                name = pr["m_Name"]?.ToString(),
                type = pr["m_Type"]?.ToString(),
                defaultReferenceName = pr["m_DefaultReferenceName"]?.ToString()
            }).ToList();

            var edges = graph["m_Edges"]?.Select(e => new
            {
                sourceNodeId = e["m_OutputSlot"]?["m_Node"]?["m_ObjectId"]?.ToString(),
                sourceSlotId = e["m_OutputSlot"]?["m_SlotId"]?.ToObject<int>(),
                destNodeId = e["m_InputSlot"]?["m_Node"]?["m_ObjectId"]?.ToString(),
                destSlotId = e["m_InputSlot"]?["m_SlotId"]?.ToObject<int>()
            }).ToList();

            var keywords = graph["m_Keywords"]?.Select(k => new
            {
                name = k["m_Name"]?.ToString(),
                type = k["m_Type"]?.ToString()
            }).ToList();

            return new SuccessResponse($"Shader Graph info for '{path}'.", new
            {
                assetPath = path,
                sgVersion = graph["m_SGVersion"]?.ToObject<int>(),
                graphPath = graph["m_Path"]?.ToString(),
                nodeCount = nodes?.Count ?? 0,
                propertyCount = properties?.Count ?? 0,
                edgeCount = edges?.Count ?? 0,
                keywordCount = keywords?.Count ?? 0,
                nodes,
                properties,
                edges,
                keywords
            });
        }

        private static object ListShaderGraphs(JObject @params)
        {
            string searchFolder = @params["folder"]?.ToString() ?? "Assets";
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { searchFolder });

            // Also search for .shadergraph files directly
            string[] sgGuids = AssetDatabase.FindAssets("", new[] { searchFolder });

            var shaderGraphs = new List<object>();
            foreach (string guid in sgGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith(".shadergraph") || assetPath.EndsWith(".shadersubgraph"))
                {
                    shaderGraphs.Add(new
                    {
                        path = assetPath,
                        name = Path.GetFileNameWithoutExtension(assetPath),
                        type = assetPath.EndsWith(".shadersubgraph") ? "SubGraph" : "ShaderGraph"
                    });
                }
            }

            return new SuccessResponse($"Found {shaderGraphs.Count} Shader Graph(s) in '{searchFolder}'.", new
            {
                shaderGraphs,
                searchFolder
            });
        }

        private static object AddProperty(JObject @params)
        {
            string path = @params["path"]?.ToString();
            string propName = @params["property_name"]?.ToString();
            string propType = @params["property_type"]?.ToString() ?? "Color";

            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' is required.");
            if (string.IsNullOrEmpty(propName))
                return new ErrorResponse("'property_name' is required.");

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            if (!File.Exists(fullPath))
                return new ErrorResponse($"Shader Graph file not found: '{path}'.");

            string json = File.ReadAllText(fullPath);
            JObject graph = JObject.Parse(json);

            var properties = graph["m_Properties"] as JArray ?? new JArray();

            // Build a shader graph property entry matching Unity's format
            string typeString;
            switch (propType.ToLowerInvariant())
            {
                case "color": typeString = "UnityEditor.ShaderGraph.Internal.ColorShaderProperty"; break;
                case "float": typeString = "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty"; break;
                case "vector2": typeString = "UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty"; break;
                case "vector3": typeString = "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty"; break;
                case "vector4": typeString = "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty"; break;
                case "texture2d": typeString = "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty"; break;
                case "boolean": typeString = "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty"; break;
                default: typeString = $"UnityEditor.ShaderGraph.Internal.{propType}ShaderProperty"; break;
            }

            string refName = "_" + propName.Replace(" ", "_");
            var newProperty = new JObject
            {
                ["m_ObjectId"] = Guid.NewGuid().ToString("N"),
                ["m_Name"] = propName,
                ["m_Type"] = typeString,
                ["m_DefaultReferenceName"] = refName,
                ["m_OverrideReferenceName"] = "",
                ["m_Precision"] = 0,
                ["overrideHLSLDeclaration"] = false,
                ["hlslDeclarationOverride"] = 0,
                ["m_Hidden"] = false
            };

            properties.Add(newProperty);
            graph["m_Properties"] = properties;

            File.WriteAllText(fullPath, graph.ToString());
            AssetDatabase.ImportAsset(path);

            return new SuccessResponse($"Added property '{propName}' ({propType}) to '{path}'.", new
            {
                assetPath = path,
                propertyName = propName,
                propertyType = propType,
                referenceName = refName,
                totalProperties = properties.Count
            });
        }

        private static object GetProperties(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' is required.");

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            if (!File.Exists(fullPath))
                return new ErrorResponse($"Shader Graph file not found: '{path}'.");

            string json = File.ReadAllText(fullPath);
            JObject graph = JObject.Parse(json);

            var properties = graph["m_Properties"]?.Select(pr => new
            {
                name = pr["m_Name"]?.ToString(),
                type = pr["m_Type"]?.ToString(),
                referenceName = pr["m_DefaultReferenceName"]?.ToString(),
                hidden = pr["m_Hidden"]?.ToObject<bool>() ?? false
            }).ToList();

            return new SuccessResponse($"Properties in '{path}'.", new
            {
                assetPath = path,
                propertyCount = properties?.Count ?? 0,
                properties
            });
        }

        private static object OpenInEditor(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' is required.");

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null)
                return new ErrorResponse($"Asset not found at '{path}'.");

            AssetDatabase.OpenAsset(asset);

            return new SuccessResponse($"Opened Shader Graph '{path}' in editor.", new
            {
                assetPath = path
            });
        }
    }
}
