using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_quality_settings", AutoRegister = false)]
    public static class ManageQualitySettings
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
                    case "set_level":
                        return SetLevel(@params, p);
                    case "set_vsync":
                        return SetVSync(@params, p);
                    case "set_shadow_settings":
                        return SetShadowSettings(@params, p);
                    case "set_anti_aliasing":
                        return SetAntiAliasing(@params, p);
                    case "set_texture_quality":
                        return SetTextureQuality(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: get_info, set_level, set_vsync, set_shadow_settings, set_anti_aliasing, set_texture_quality");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var names = QualitySettings.names;
            var levels = new List<object>();
            for (int i = 0; i < names.Length; i++)
            {
                levels.Add(new { index = i, name = names[i] });
            }

            return new SuccessResponse("Quality settings info", new
            {
                currentLevel = QualitySettings.GetQualityLevel(),
                currentLevelName = names[QualitySettings.GetQualityLevel()],
                levels,
                vSyncCount = QualitySettings.vSyncCount,
                antiAliasing = QualitySettings.antiAliasing,
                shadowResolution = QualitySettings.shadowResolution.ToString(),
                shadowDistance = QualitySettings.shadowDistance,
                masterTextureLimit = QualitySettings.globalTextureMipmapLimit,
                anisotropicFiltering = QualitySettings.anisotropicFiltering.ToString(),
                softParticles = QualitySettings.softParticles,
                realtimeReflectionProbes = QualitySettings.realtimeReflectionProbes,
                pixelLightCount = QualitySettings.pixelLightCount,
                lodBias = QualitySettings.lodBias,
                maximumLODLevel = QualitySettings.maximumLODLevel
            });
        }

        private static object SetLevel(JObject @params, ToolParams p)
        {
            int? level = p.GetInt("level");
            string levelName = p.Get("level_name");

            if (level.HasValue)
            {
                if (level.Value < 0 || level.Value >= QualitySettings.names.Length)
                    return new ErrorResponse($"Level {level.Value} out of range. Valid: 0-{QualitySettings.names.Length - 1}");

                QualitySettings.SetQualityLevel(level.Value, true);
                return new SuccessResponse($"Set quality level to {QualitySettings.names[level.Value]}", new
                {
                    level = level.Value,
                    name = QualitySettings.names[level.Value]
                });
            }

            if (!string.IsNullOrEmpty(levelName))
            {
                var names = QualitySettings.names;
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].Equals(levelName, StringComparison.OrdinalIgnoreCase))
                    {
                        QualitySettings.SetQualityLevel(i, true);
                        return new SuccessResponse($"Set quality level to '{names[i]}'", new { level = i, name = names[i] });
                    }
                }
                return new ErrorResponse($"Quality level '{levelName}' not found.");
            }

            return new ErrorResponse("Provide 'level' (index) or 'level_name'.");
        }

        private static object SetVSync(JObject @params, ToolParams p)
        {
            int? count = p.GetInt("vsync_count");
            if (!count.HasValue)
                return new ErrorResponse("'vsync_count' required (0=off, 1=every vblank, 2=every other).");

            QualitySettings.vSyncCount = Mathf.Clamp(count.Value, 0, 4);

            return new SuccessResponse($"Set VSync count to {QualitySettings.vSyncCount}");
        }

        private static object SetShadowSettings(JObject @params, ToolParams p)
        {
            float? distance = p.GetFloat("shadow_distance");
            if (distance.HasValue) QualitySettings.shadowDistance = distance.Value;

            string resolution = p.Get("shadow_resolution");
            if (!string.IsNullOrEmpty(resolution) && Enum.TryParse<ShadowResolution>(resolution, true, out var shadowRes))
                QualitySettings.shadowResolution = shadowRes;

            string shadowQuality = p.Get("shadow_quality");
            if (!string.IsNullOrEmpty(shadowQuality) && Enum.TryParse<ShadowQuality>(shadowQuality, true, out var sq))
                QualitySettings.shadows = sq;

            int? cascades = p.GetInt("shadow_cascades");
            if (cascades.HasValue) QualitySettings.shadowCascades = cascades.Value;

            return new SuccessResponse("Updated shadow settings", new
            {
                shadowDistance = QualitySettings.shadowDistance,
                shadowResolution = QualitySettings.shadowResolution.ToString(),
                shadows = QualitySettings.shadows.ToString(),
                shadowCascades = QualitySettings.shadowCascades
            });
        }

        private static object SetAntiAliasing(JObject @params, ToolParams p)
        {
            int? aa = p.GetInt("anti_aliasing");
            if (!aa.HasValue)
                return new ErrorResponse("'anti_aliasing' required (0=off, 2=2x, 4=4x, 8=8x MSAA).");

            QualitySettings.antiAliasing = aa.Value;

            return new SuccessResponse($"Set anti-aliasing to {QualitySettings.antiAliasing}x");
        }

        private static object SetTextureQuality(JObject @params, ToolParams p)
        {
            int? limit = p.GetInt("texture_limit");
            if (limit.HasValue) QualitySettings.globalTextureMipmapLimit = limit.Value;

            string filtering = p.Get("anisotropic_filtering");
            if (!string.IsNullOrEmpty(filtering) && Enum.TryParse<AnisotropicFiltering>(filtering, true, out var af))
                QualitySettings.anisotropicFiltering = af;

            return new SuccessResponse("Updated texture quality", new
            {
                textureLimit = QualitySettings.globalTextureMipmapLimit,
                anisotropicFiltering = QualitySettings.anisotropicFiltering.ToString()
            });
        }
    }
}
