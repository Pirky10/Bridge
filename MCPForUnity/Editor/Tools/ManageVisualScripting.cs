#if UNITY_VISUALSCRIPTING
using Unity.VisualScripting;
#endif
using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
#if UNITY_VISUALSCRIPTING
    [McpForUnityTool("manage_visual_scripting")]
#endif
    public static class ManageVisualScripting
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' is required.");

#if !UNITY_VISUALSCRIPTING
            return new ErrorResponse(
                "Visual Scripting package is not installed. " +
                "Install com.unity.visualscripting via Package Manager to use this tool."
            );
#else
            try
            {
                switch (action)
                {
                    case "create_graph": return CreateGraph(@params);
                    case "assign_graph": return AssignGraph(@params);
                    case "get_graph_info": return GetGraphInfo(@params);
                    case "add_node": return AddNode(@params);
                    case "connect_nodes": return ConnectNodes(@params);
                    case "add_variable": return AddVariable(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageVisualScripting error: {e.Message}");
            }
#endif
        }

#if UNITY_VISUALSCRIPTING
        private static object CreateGraph(JObject @params)
        {
            string path = @params["path"]?.ToString() ?? "Assets/NewScriptGraph.asset";

            var asset = ScriptableObject.CreateInstance<ScriptGraphAsset>();
            asset.graph = new FlowGraph();

            // Add a default Start event node
            var startNode = new Start();
            startNode.position = new Vector2(0, 0);
            asset.graph.units.Add(startNode);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created Script Graph at '{path}'.", new
            {
                assetPath = path,
                nodeCount = asset.graph.units.Count
            });
        }

        private static object AssignGraph(JObject @params)
        {
            string target = @params["target"]?.ToString();
            string assetPath = @params["asset_path"]?.ToString();

            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' (GameObject name/path/id) is required.");
            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required.");

            var go = GameObjectLookup.FindByTarget(new JValue(target), "by_id_or_name_or_path");
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (asset == null) return new ErrorResponse($"ScriptGraphAsset not found at '{assetPath}'.");

            Undo.RecordObject(go, "Assign Visual Script Graph");
            var machine = go.GetComponent<ScriptMachine>();
            if (machine == null) machine = Undo.AddComponent<ScriptMachine>(go);

            machine.nest.SwitchToAsset(asset);
            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Assigned graph '{assetPath}' to '{go.name}'.", new
            {
                gameObject = go.name,
                graphAsset = assetPath
            });
        }

        private static object GetGraphInfo(JObject @params)
        {
            string assetPath = @params["asset_path"]?.ToString();
            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required.");

            var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (asset == null) return new ErrorResponse($"ScriptGraphAsset not found at '{assetPath}'.");

            var graph = asset.graph;

            var nodes = graph.units.Select(u => new
            {
                type = u.GetType().FullName,
                position = new { x = u.position.x, y = u.position.y },
                inputPorts = u.inputs.Select(p => p.key).ToList(),
                outputPorts = u.outputs.Select(p => p.key).ToList()
            }).ToList();

            var variables = graph.variables.declarations.Select(v => new
            {
                name = v.name,
                type = v.type?.FullName ?? "Unknown"
            }).ToList();

            var connections = graph.connections.Select(c => new
            {
                source = c.source?.key,
                sourceUnit = c.source?.unit?.GetType().Name,
                destination = c.destination?.key,
                destinationUnit = c.destination?.unit?.GetType().Name
            }).ToList();

            return new SuccessResponse($"Graph info for '{assetPath}'.", new
            {
                assetPath,
                title = asset.name,
                nodeCount = nodes.Count,
                connectionCount = connections.Count,
                variableCount = variables.Count,
                nodes,
                variables,
                connections
            });
        }

        private static object AddNode(JObject @params)
        {
            string assetPath = @params["asset_path"]?.ToString();
            string unitTypeName = @params["unit_type"]?.ToString();
            float posX = @params["x"]?.ToObject<float>() ?? 0f;
            float posY = @params["y"]?.ToObject<float>() ?? 0f;

            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required.");
            if (string.IsNullOrEmpty(unitTypeName))
                return new ErrorResponse("'unit_type' is required (e.g. 'Unity.VisualScripting.OnUpdate').");

            var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (asset == null) return new ErrorResponse($"ScriptGraphAsset not found at '{assetPath}'.");

            // Resolve the unit type from all loaded assemblies
            Type unitType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => t.FullName == unitTypeName || t.Name == unitTypeName);

            if (unitType == null)
                return new ErrorResponse($"Unit type '{unitTypeName}' not found in loaded assemblies.");
            if (!typeof(IUnit).IsAssignableFrom(unitType))
                return new ErrorResponse($"'{unitTypeName}' does not implement IUnit.");

            IUnit unit = (IUnit)Activator.CreateInstance(unitType);
            unit.position = new Vector2(posX, posY);

            Undo.RecordObject(asset, "Add Visual Script Node");
            asset.graph.units.Add(unit);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added '{unitType.Name}' node to graph '{assetPath}'.", new
            {
                unitType = unitType.FullName,
                position = new { x = posX, y = posY },
                totalNodes = asset.graph.units.Count
            });
        }

        private static object ConnectNodes(JObject @params)
        {
            string assetPath = @params["asset_path"]?.ToString();
            int sourceIndex = @params["source_node_index"]?.ToObject<int>() ?? -1;
            int destIndex = @params["destination_node_index"]?.ToObject<int>() ?? -1;
            string sourcePortKey = @params["source_port"]?.ToString();
            string destPortKey = @params["destination_port"]?.ToString();

            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required.");
            if (sourceIndex < 0 || destIndex < 0)
                return new ErrorResponse("'source_node_index' and 'destination_node_index' are required (0-based).");
            if (string.IsNullOrEmpty(sourcePortKey) || string.IsNullOrEmpty(destPortKey))
                return new ErrorResponse("'source_port' and 'destination_port' keys are required.");

            var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (asset == null) return new ErrorResponse($"ScriptGraphAsset not found at '{assetPath}'.");

            var units = asset.graph.units.ToList();
            if (sourceIndex >= units.Count)
                return new ErrorResponse($"source_node_index {sourceIndex} out of range (graph has {units.Count} nodes).");
            if (destIndex >= units.Count)
                return new ErrorResponse($"destination_node_index {destIndex} out of range (graph has {units.Count} nodes).");

            var sourceUnit = units[sourceIndex];
            var destUnit = units[destIndex];

            // Try control connection first (trigger ports)
            var sourceOutput = sourceUnit.controlOutputs.FirstOrDefault(p => p.key == sourcePortKey);
            var destInput = destUnit.controlInputs.FirstOrDefault(p => p.key == destPortKey);

            if (sourceOutput != null && destInput != null)
            {
                Undo.RecordObject(asset, "Connect Visual Script Nodes");
                asset.graph.controlConnections.Add(new ControlConnection(sourceOutput, destInput));
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return new SuccessResponse("Connected control ports.", new
                {
                    connectionType = "control",
                    source = new { nodeIndex = sourceIndex, port = sourcePortKey },
                    destination = new { nodeIndex = destIndex, port = destPortKey }
                });
            }

            // Try value connection (data ports)
            var sourceValueOutput = sourceUnit.valueOutputs.FirstOrDefault(p => p.key == sourcePortKey);
            var destValueInput = destUnit.valueInputs.FirstOrDefault(p => p.key == destPortKey);

            if (sourceValueOutput != null && destValueInput != null)
            {
                Undo.RecordObject(asset, "Connect Visual Script Nodes");
                asset.graph.valueConnections.Add(new ValueConnection(sourceValueOutput, destValueInput));
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return new SuccessResponse("Connected value ports.", new
                {
                    connectionType = "value",
                    source = new { nodeIndex = sourceIndex, port = sourcePortKey },
                    destination = new { nodeIndex = destIndex, port = destPortKey }
                });
            }

            // If we got here, the port keys didn't match
            var availableSourceOutputs = sourceUnit.controlOutputs.Select(p => p.key)
                .Concat(sourceUnit.valueOutputs.Select(p => p.key)).ToList();
            var availableDestInputs = destUnit.controlInputs.Select(p => p.key)
                .Concat(destUnit.valueInputs.Select(p => p.key)).ToList();

            return new ErrorResponse($"Could not find matching ports.", new
            {
                sourceNodeAvailableOutputs = availableSourceOutputs,
                destNodeAvailableInputs = availableDestInputs
            });
        }

        private static object AddVariable(JObject @params)
        {
            string assetPath = @params["asset_path"]?.ToString();
            string varName = @params["name"]?.ToString();
            string varTypeName = @params["type"]?.ToString() ?? "System.Single";

            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required.");
            if (string.IsNullOrEmpty(varName))
                return new ErrorResponse("'name' is required.");

            var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (asset == null) return new ErrorResponse($"ScriptGraphAsset not found at '{assetPath}'.");

            Type varType = Type.GetType(varTypeName);
            if (varType == null)
            {
                // Try searching assemblies
                varType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.FullName == varTypeName || t.Name == varTypeName);
            }

            varType ??= typeof(float); // Fallback to float

            // Create the variable declaration
            Undo.RecordObject(asset, "Add Visual Script Variable");
            var declarations = asset.graph.variables;
            var decl = new VariableDeclaration(varName, Activator.CreateInstance(varType));
            declarations.declarations.Set(decl.name, decl.value);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added variable '{varName}' of type '{varType.Name}'.", new
            {
                name = varName,
                type = varType.FullName,
                assetPath
            });
        }
#endif
    }
}
