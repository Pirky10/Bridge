using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_render_pipeline", AutoRegister = false)]
    public static class ManageRenderPipeline
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
                    case "get_info":
                        return GetInfo(@params, p);
                    case "set_render_pipeline":
                        return SetRenderPipeline(@params, p);
                    case "configure_urp":
                        return ConfigureURP(@params, p);
                    case "configure_hdrp":
                        return ConfigureHDRP(@params, p);
                    case "set_color_space":
                        return SetColorSpace(@params, p);
                    case "configure_camera_rendering":
                        return ConfigureCameraRendering(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: get_info, set_render_pipeline, configure_urp, configure_hdrp, set_color_space, configure_camera_rendering");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var currentRP = GraphicsSettings.currentRenderPipeline;
            var defaultRP = GraphicsSettings.defaultRenderPipeline;

            string rpType = "Built-in";
            string rpName = "Built-in Render Pipeline";
            string rpAssetPath = null;

            if (defaultRP != null)
            {
                rpName = defaultRP.name;
                rpType = defaultRP.GetType().Name;
                rpAssetPath = AssetDatabase.GetAssetPath(defaultRP);

                if (rpType.Contains("Universal") || rpType.Contains("URP"))
                    rpType = "URP";
                else if (rpType.Contains("HDRender") || rpType.Contains("HDRP"))
                    rpType = "HDRP";
            }

            return new SuccessResponse("Render pipeline info", new
            {
                pipeline = rpType,
                pipelineName = rpName,
                pipelineAssetPath = rpAssetPath,
                pipelineAssetType = defaultRP != null ? defaultRP.GetType().FullName : "Built-in",
                colorSpace = PlayerSettings.colorSpace.ToString(),
                graphicsApiWindows = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64),
                tier1 = new
                {
                    hdr = EditorGraphicsSettings.GetTierSettings(BuildTargetGroup.Standalone, GraphicsTier.Tier1).hdr
                }
            });
        }

        private static object SetRenderPipeline(JObject @params, ToolParams p)
        {
            string pipelinePath = p.Get("pipeline_path");

            if (string.IsNullOrEmpty(pipelinePath))
            {
                // Set to built-in
                GraphicsSettings.defaultRenderPipeline = null;
                return new SuccessResponse("Set to Built-in Render Pipeline");
            }

            string sanitized = AssetPathUtility.SanitizeAssetPath(pipelinePath);
            RenderPipelineAsset rpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(sanitized);
            if (rpAsset == null)
                return new ErrorResponse($"RenderPipelineAsset not found at '{pipelinePath}'.");

            GraphicsSettings.defaultRenderPipeline = rpAsset;
            EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());

            return new SuccessResponse($"Set render pipeline to '{rpAsset.name}'", new
            {
                name = rpAsset.name,
                type = rpAsset.GetType().Name,
                path = sanitized
            });
        }

        private static object ConfigureURP(JObject @params, ToolParams p)
        {
            var rpAsset = GraphicsSettings.defaultRenderPipeline;
            if (rpAsset == null)
                return new ErrorResponse("No render pipeline asset set. Install URP and assign a UniversalRenderPipelineAsset first.");

            Type urpType = rpAsset.GetType();
            if (!urpType.Name.Contains("Universal") && !urpType.FullName.Contains("Universal"))
                return new ErrorResponse($"Current pipeline is {urpType.Name}, not URP.");

            Undo.RecordObject(rpAsset, "Configure URP");
            SerializedObject so = new SerializedObject(rpAsset);

            // HDR
            if (p.Has("hdr"))
            {
                var hdrProp = so.FindProperty("m_SupportsHDR");
                if (hdrProp != null) hdrProp.boolValue = p.GetBool("hdr", true);
            }

            // MSAA
            int? msaa = p.GetInt("msaa");
            if (msaa.HasValue)
            {
                var msaaProp = so.FindProperty("m_MSAA");
                if (msaaProp != null) msaaProp.intValue = msaa.Value;
            }

            // Render scale
            float? renderScale = p.GetFloat("render_scale");
            if (renderScale.HasValue)
            {
                var scaleProp = so.FindProperty("m_RenderScale");
                if (scaleProp != null) scaleProp.floatValue = renderScale.Value;
            }

            // Shadow distance
            float? shadowDistance = p.GetFloat("shadow_distance");
            if (shadowDistance.HasValue)
            {
                var shadowProp = so.FindProperty("m_ShadowDistance");
                if (shadowProp != null) shadowProp.floatValue = shadowDistance.Value;
            }

            // Shadow cascades
            int? cascades = p.GetInt("shadow_cascades");
            if (cascades.HasValue)
            {
                var cascadeProp = so.FindProperty("m_ShadowCascadeCount");
                if (cascadeProp != null) cascadeProp.intValue = cascades.Value;
            }

            // Soft shadows
            if (p.Has("soft_shadows"))
            {
                var softProp = so.FindProperty("m_SoftShadowsSupported");
                if (softProp != null) softProp.boolValue = p.GetBool("soft_shadows", true);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rpAsset);

            return new SuccessResponse("Configured URP settings", new
            {
                pipelineName = rpAsset.name
            });
        }

        private static object ConfigureHDRP(JObject @params, ToolParams p)
        {
            var rpAsset = GraphicsSettings.defaultRenderPipeline;
            if (rpAsset == null)
                return new ErrorResponse("No render pipeline asset set. Install HDRP and assign an HDRenderPipelineAsset first.");

            Type hdrpType = rpAsset.GetType();
            if (!hdrpType.Name.Contains("HDRender") && !hdrpType.FullName.Contains("HighDefinition"))
                return new ErrorResponse($"Current pipeline is {hdrpType.Name}, not HDRP.");

            Undo.RecordObject(rpAsset, "Configure HDRP");
            SerializedObject so = new SerializedObject(rpAsset);

            // Use SerializedObject for HDRP settings — property names vary by version
            float? shadowDistance = p.GetFloat("shadow_distance");
            if (shadowDistance.HasValue)
            {
                var iter = so.GetIterator();
                while (iter.NextVisible(true))
                {
                    if (iter.name.Contains("shadowDistance") || iter.name.Contains("ShadowDistance"))
                    {
                        if (iter.propertyType == SerializedPropertyType.Float)
                            iter.floatValue = shadowDistance.Value;
                        break;
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rpAsset);

            return new SuccessResponse("Configured HDRP settings", new
            {
                pipelineName = rpAsset.name,
                note = "Many HDRP settings require the HDRP Wizard (Window > Rendering > HDRP Wizard)."
            });
        }

        private static object SetColorSpace(JObject @params, ToolParams p)
        {
            var spaceResult = p.GetRequired("color_space");
            var spaceError = spaceResult.GetOrError(out string colorSpace);
            if (spaceError != null) return spaceError;

            if (Enum.TryParse<ColorSpace>(colorSpace, true, out var cs))
            {
                PlayerSettings.colorSpace = cs;
                return new SuccessResponse($"Set color space to {cs}", new { colorSpace = cs.ToString() });
            }

            return new ErrorResponse($"Invalid color space: {colorSpace}. Valid: Linear, Gamma");
        }

        private static object ConfigureCameraRendering(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Camera cam = go.GetComponent<Camera>();
            if (cam == null) return new ErrorResponse($"No Camera on '{target}'.");

            Undo.RecordObject(cam, "Configure Camera Rendering");

            if (p.Has("hdr")) cam.allowHDR = p.GetBool("hdr", cam.allowHDR);
            if (p.Has("msaa")) cam.allowMSAA = p.GetBool("msaa", cam.allowMSAA);
            if (p.Has("dynamic_resolution")) cam.allowDynamicResolution = p.GetBool("dynamic_resolution", cam.allowDynamicResolution);
            if (p.Has("occlusion_culling")) cam.useOcclusionCulling = p.GetBool("occlusion_culling", cam.useOcclusionCulling);

            string renderingPath = p.Get("rendering_path");
            if (!string.IsNullOrEmpty(renderingPath) && Enum.TryParse<RenderingPath>(renderingPath, true, out var rp))
                cam.renderingPath = rp;

            // For URP — try to set additional camera data via reflection
            Type additionalDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (additionalDataType != null)
            {
                var addData = go.GetComponent(additionalDataType);
                if (addData != null)
                {
                    SerializedObject so = new SerializedObject(addData);

                    if (p.Has("render_type"))
                    {
                        var renderTypeProp = so.FindProperty("m_CameraType");
                        if (renderTypeProp != null)
                        {
                            string renderType = p.Get("render_type");
                            if (renderType.Equals("Base", StringComparison.OrdinalIgnoreCase))
                                renderTypeProp.intValue = 0;
                            else if (renderType.Equals("Overlay", StringComparison.OrdinalIgnoreCase))
                                renderTypeProp.intValue = 1;
                        }
                    }

                    if (p.Has("post_processing"))
                    {
                        var ppProp = so.FindProperty("m_RenderPostProcessing");
                        if (ppProp != null) ppProp.boolValue = p.GetBool("post_processing", true);
                    }

                    if (p.Has("anti_aliasing"))
                    {
                        var aaProp = so.FindProperty("m_Antialiasing");
                        if (aaProp != null) aaProp.intValue = p.GetInt("anti_aliasing") ?? 0;
                    }

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(addData);
                }
            }

            EditorUtility.SetDirty(cam);

            return new SuccessResponse($"Configured camera rendering on '{target}'", new
            {
                hdr = cam.allowHDR,
                msaa = cam.allowMSAA,
                renderingPath = cam.renderingPath.ToString()
            });
        }
    }
}
