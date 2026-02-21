using System;
using System.Collections.Generic;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_rendering_layers", AutoRegister = false)]
    public static class ManageRenderingLayers
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
                    case "set_rendering_layer": return SetRenderingLayer(@params, p);
                    case "rename_layer": return RenameLayer(@params, p);
                    case "set_light_layers": return SetLightLayers(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: set_rendering_layer, rename_layer, set_light_layers, get_info");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object SetRenderingLayer(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return new ErrorResponse($"No Renderer on '{target}'.");

            int? layerMask = p.GetInt("rendering_layer_mask");
            if (!layerMask.HasValue) return new ErrorResponse("'rendering_layer_mask' required.");

            Undo.RecordObject(renderer, "Set Rendering Layer");
            renderer.renderingLayerMask = (uint)layerMask.Value;
            EditorUtility.SetDirty(renderer);

            return new SuccessResponse($"Set rendering layer mask on '{target}'", new
            {
                name = target, renderingLayerMask = renderer.renderingLayerMask
            });
        }

        private static object RenameLayer(JObject @params, ToolParams p)
        {
            int? index = p.GetInt("layer_index");
            if (!index.HasValue) return new ErrorResponse("'layer_index' required (0-31).");

            var nameResult = p.GetRequired("layer_name");
            var nameError = nameResult.GetOrError(out string layerName);
            if (nameError != null) return nameError;

            // Access RenderPipelineAsset to rename rendering layers
            var rpAsset = GraphicsSettings.currentRenderPipeline;
            if (rpAsset == null)
                return new ErrorResponse("No SRP render pipeline active. Rendering layers require URP or HDRP.");

            // Try via SerializedObject on the RP asset
            SerializedObject so = new SerializedObject(rpAsset);
            var namesProp = so.FindProperty("m_RenderingLayerNames");
            if (namesProp == null)
            {
                // Try alternate property name
                namesProp = so.FindProperty("renderingLayerNames");
            }

            if (namesProp != null && namesProp.isArray && index.Value < namesProp.arraySize)
            {
                namesProp.GetArrayElementAtIndex(index.Value).stringValue = layerName;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(rpAsset);
                AssetDatabase.SaveAssets();
                return new SuccessResponse($"Renamed rendering layer {index.Value} to '{layerName}'");
            }

            // Fallback: try RenderPipelineGlobalSettings
            var globalSettings = GraphicsSettings.currentRenderPipelineGlobalSettings;
            if (globalSettings != null)
            {
                SerializedObject gso = new SerializedObject(globalSettings);
                var gNamesProp = gso.FindProperty("m_RenderingLayerNames");
                if (gNamesProp != null && gNamesProp.isArray)
                {
                    while (gNamesProp.arraySize <= index.Value)
                        gNamesProp.InsertArrayElementAtIndex(gNamesProp.arraySize);

                    gNamesProp.GetArrayElementAtIndex(index.Value).stringValue = layerName;
                    gso.ApplyModifiedProperties();
                    EditorUtility.SetDirty(globalSettings);
                    return new SuccessResponse($"Renamed rendering layer {index.Value} to '{layerName}'");
                }
            }

            return new ErrorResponse("Could not find rendering layer names property. Rename manually in Project Settings > Graphics.");
        }

        private static object SetLightLayers(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Light light = go.GetComponent<Light>();
            if (light == null) return new ErrorResponse($"No Light on '{target}'.");

            int? layerMask = p.GetInt("rendering_layer_mask");
            if (!layerMask.HasValue) return new ErrorResponse("'rendering_layer_mask' required.");

            Undo.RecordObject(light, "Set Light Rendering Layer");
            light.renderingLayerMask = layerMask.Value;
            EditorUtility.SetDirty(light);

            return new SuccessResponse($"Set light rendering layer on '{target}'", new
            {
                name = target, renderingLayerMask = light.renderingLayerMask
            });
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var info = new Dictionary<string, object>();

            var rpAsset = GraphicsSettings.currentRenderPipeline;
            info["hasRenderPipeline"] = rpAsset != null;
            if (rpAsset != null)
                info["pipelineName"] = rpAsset.name;

            // Try to get rendering layer names
            var names = new List<string>();
            if (rpAsset != null)
            {
                SerializedObject so = new SerializedObject(rpAsset);
                var namesProp = so.FindProperty("m_RenderingLayerNames");
                if (namesProp != null && namesProp.isArray)
                {
                    for (int i = 0; i < namesProp.arraySize; i++)
                        names.Add($"[{i}] {namesProp.GetArrayElementAtIndex(i).stringValue}");
                }
            }

            if (names.Count == 0)
            {
                // Fallback default names
                names.Add("[0] Default");
                for (int i = 1; i < 8; i++)
                    names.Add($"[{i}] Layer {i}");
            }

            info["renderingLayers"] = names;
            info["layerCount"] = names.Count;

            return new SuccessResponse("Rendering layers info", info);
        }
    }
}
