using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_lightmap", AutoRegister = false)]
    public static class ManageLightmap
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            action = action.ToLowerInvariant();

            try
            {
                switch (action)
                {
                    case "set_static": return SetLightmapStatic(@params, p);
                    case "configure_settings": return ConfigureSettings(@params, p);
                    case "bake": return Bake(@params, p);
                    case "clear": return Clear(@params, p);
                    case "set_lightmap_parameters": return SetLightmapParameters(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: set_static, configure_settings, bake, clear, set_lightmap_parameters, get_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object SetLightmapStatic(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            bool contribute = p.GetBool("contribute_gi", true);

            Undo.RecordObject(go, "Set Lightmap Static");
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);

            if (contribute)
                flags |= StaticEditorFlags.ContributeGI;
            else
                flags &= ~StaticEditorFlags.ContributeGI;

            GameObjectUtility.SetStaticEditorFlags(go, flags);

            if (p.GetBool("include_children", false))
            {
                foreach (Transform child in go.GetComponentsInChildren<Transform>())
                {
                    if (child.gameObject == go) continue;
                    Undo.RecordObject(child.gameObject, "Set Lightmap Static");
                    StaticEditorFlags cf = GameObjectUtility.GetStaticEditorFlags(child.gameObject);
                    if (contribute) cf |= StaticEditorFlags.ContributeGI;
                    else cf &= ~StaticEditorFlags.ContributeGI;
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, cf);
                }
            }

            // Set lightmap scale if specified
            float? scale = p.GetFloat("lightmap_scale");
            if (scale.HasValue)
            {
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    SerializedObject so = new SerializedObject(renderer);
                    var scaleProp = so.FindProperty("m_ScaleInLightmap");
                    if (scaleProp != null)
                    {
                        scaleProp.floatValue = scale.Value;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            return new SuccessResponse($"Set lightmap static on '{target}'", new { contributeGI = contribute });
        }

        private static object ConfigureSettings(JObject @params, ToolParams p)
        {
            // Lightmapping settings via LightmapEditorSettings
            SerializedObject so = new SerializedObject(
                UnityEngine.Object.FindObjectsByType<LightmapSettings>(FindObjectsSortMode.None).Length > 0
                    ? UnityEngine.Object.FindObjectsByType<LightmapSettings>(FindObjectsSortMode.None)[0]
                    : (UnityEngine.Object)Lightmapping.lightingSettings
            );

            // Use Lightmapping API directly
            float? indirectResolution = p.GetFloat("indirect_resolution");
            float? directResolution = p.GetFloat("direct_resolution");
            int? maxLightmapSize = p.GetInt("max_lightmap_size");
            int? bounces = p.GetInt("bounces");

            // Use LightingSettings if available
            var settings = Lightmapping.lightingSettings;
            if (settings != null)
            {
                Undo.RecordObject(settings, "Configure Lightmap Settings");

                if (indirectResolution.HasValue)
                    settings.indirectResolution = indirectResolution.Value;
                if (directResolution.HasValue)
                    settings.lightmapResolution = directResolution.Value;
                if (maxLightmapSize.HasValue)
                    settings.lightmapMaxSize = maxLightmapSize.Value;
                if (bounces.HasValue)
                    settings.maxBounces = bounces.Value;

                string lightmapper = p.Get("lightmapper");
                if (!string.IsNullOrEmpty(lightmapper))
                {
                    if (lightmapper.Equals("progressive_cpu", StringComparison.OrdinalIgnoreCase))
                        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
                    else if (lightmapper.Equals("progressive_gpu", StringComparison.OrdinalIgnoreCase))
                        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
                }

                if (p.Has("compress_lightmaps"))
                    settings.compressLightmaps = p.GetBool("compress_lightmaps", true);

                if (p.Has("ambient_occlusion"))
                    settings.ao = p.GetBool("ambient_occlusion", false);

                float? aoDistance = p.GetFloat("ao_max_distance");
                if (aoDistance.HasValue)
                    settings.aoMaxDistance = aoDistance.Value;

                EditorUtility.SetDirty(settings);
            }

            return new SuccessResponse("Configured lightmap settings");
        }

        private static object Bake(JObject @params, ToolParams p)
        {
            bool async = p.GetBool("async", true);

            if (async)
            {
                Lightmapping.BakeAsync();
                return new SuccessResponse("Lightmap bake started (async). Check progress in Lighting window.");
            }
            else
            {
                Lightmapping.Bake();
                return new SuccessResponse("Lightmap bake completed.");
            }
        }

        private static object Clear(JObject @params, ToolParams p)
        {
            Lightmapping.Clear();
            Lightmapping.ClearDiskCache();
            return new SuccessResponse("Cleared all lightmap data and disk cache.");
        }

        private static object SetLightmapParameters(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return new ErrorResponse($"No Renderer on '{target}'.");

            SerializedObject so = new SerializedObject(renderer);

            float? scale = p.GetFloat("lightmap_scale");
            if (scale.HasValue)
            {
                var scaleProp = so.FindProperty("m_ScaleInLightmap");
                if (scaleProp != null) scaleProp.floatValue = scale.Value;
            }

            string receiveGI = p.Get("receive_gi");
            if (!string.IsNullOrEmpty(receiveGI))
            {
                var giProp = so.FindProperty("m_ReceiveGI");
                if (giProp != null)
                {
                    if (receiveGI.Equals("lightmaps", StringComparison.OrdinalIgnoreCase))
                        giProp.intValue = 1;
                    else if (receiveGI.Equals("lightprobes", StringComparison.OrdinalIgnoreCase))
                        giProp.intValue = 2;
                }
            }

            so.ApplyModifiedProperties();

            return new SuccessResponse($"Set lightmap parameters on '{target}'");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var settings = Lightmapping.lightingSettings;
            var info = new Dictionary<string, object>
            {
                { "isRunning", Lightmapping.isRunning },
                { "lightmapCount", LightmapSettings.lightmaps?.Length ?? 0 },
            };

            if (settings != null)
            {
                info["lightmapper"] = settings.lightmapper.ToString();
                info["indirectResolution"] = settings.indirectResolution;
                info["directResolution"] = settings.lightmapResolution;
                info["maxSize"] = settings.lightmapMaxSize;
                info["bounces"] = settings.maxBounces;
                info["compressLightmaps"] = settings.compressLightmaps;
                info["ao"] = settings.ao;
            }

            return new SuccessResponse("Lightmap info", info);
        }
    }
}
