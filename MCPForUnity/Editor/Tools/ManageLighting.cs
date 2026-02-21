using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_lighting", AutoRegister = false)]
    public static class ManageLighting
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
                    case "create_light":
                        return CreateLight(@params, p);
                    case "configure_light":
                        return ConfigureLight(@params, p);
                    case "add_light_probe_group":
                        return AddLightProbeGroup(@params, p);
                    case "add_reflection_probe":
                        return AddReflectionProbe(@params, p);
                    case "set_ambient":
                        return SetAmbient(@params, p);
                    case "get_lighting_info":
                        return GetLightingInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: create_light, configure_light, add_light_probe_group, add_reflection_probe, set_ambient, get_lighting_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static LightType ParseLightType(string type)
        {
            if (string.IsNullOrEmpty(type)) return LightType.Point;
            switch (type.ToLowerInvariant())
            {
                case "directional": return LightType.Directional;
                case "spot": return LightType.Spot;
                case "area":
                case "rectangle": return LightType.Rectangle;
                default: return LightType.Point;
            }
        }

        private static object CreateLight(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "Light");
            string lightType = p.Get("light_type", "Point");

            GameObject lightGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(lightGo, "Create Light");

            Light light = lightGo.AddComponent<Light>();
            light.type = ParseLightType(lightType);

            // Position
            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    lightGo.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            // Rotation
            JToken rotToken = p.GetRaw("rotation");
            if (rotToken != null)
            {
                var rot = rotToken.ToObject<float[]>();
                if (rot != null && rot.Length >= 3)
                    lightGo.transform.eulerAngles = new Vector3(rot[0], rot[1], rot[2]);
            }

            // Color
            JToken colorToken = p.GetRaw("color");
            if (colorToken != null)
            {
                var color = colorToken.ToObject<float[]>();
                if (color != null && color.Length >= 3)
                {
                    float a = color.Length >= 4 ? color[3] : 1f;
                    light.color = new Color(color[0], color[1], color[2], a);
                }
            }

            // Intensity
            float? intensity = p.GetFloat("intensity");
            if (intensity.HasValue) light.intensity = intensity.Value;

            // Range
            float? range = p.GetFloat("range");
            if (range.HasValue) light.range = range.Value;

            // Spot angle
            float? spotAngle = p.GetFloat("spot_angle");
            if (spotAngle.HasValue) light.spotAngle = spotAngle.Value;

            // Shadows
            string shadows = p.Get("shadows");
            if (!string.IsNullOrEmpty(shadows) && Enum.TryParse<LightShadows>(shadows, true, out var shadowType))
                light.shadows = shadowType;

            return new SuccessResponse($"Created {lightType} light '{name}'", new
            {
                name = lightGo.name,
                instanceId = lightGo.GetInstanceID(),
                type = light.type.ToString(),
                intensity = light.intensity,
                range = light.range,
                color = new { r = light.color.r, g = light.color.g, b = light.color.b }
            });
        }

        private static object ConfigureLight(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Light light = go.GetComponent<Light>();
            if (light == null)
                return new ErrorResponse($"No Light component on '{target}'.");

            Undo.RecordObject(light, "Configure Light");

            string lightType = p.Get("light_type");
            if (!string.IsNullOrEmpty(lightType))
                light.type = ParseLightType(lightType);

            float? intensity = p.GetFloat("intensity");
            if (intensity.HasValue) light.intensity = intensity.Value;

            float? range = p.GetFloat("range");
            if (range.HasValue) light.range = range.Value;

            float? spotAngle = p.GetFloat("spot_angle");
            if (spotAngle.HasValue) light.spotAngle = spotAngle.Value;

            JToken colorToken = p.GetRaw("color");
            if (colorToken != null)
            {
                var color = colorToken.ToObject<float[]>();
                if (color != null && color.Length >= 3)
                {
                    float a = color.Length >= 4 ? color[3] : 1f;
                    light.color = new Color(color[0], color[1], color[2], a);
                }
            }

            string shadows = p.Get("shadows");
            if (!string.IsNullOrEmpty(shadows) && Enum.TryParse<LightShadows>(shadows, true, out var shadowType))
                light.shadows = shadowType;

            float? shadowStrength = p.GetFloat("shadow_strength");
            if (shadowStrength.HasValue) light.shadowStrength = shadowStrength.Value;

            float? bounceIntensity = p.GetFloat("bounce_intensity");
            if (bounceIntensity.HasValue) light.bounceIntensity = bounceIntensity.Value;

            EditorUtility.SetDirty(light);

            return new SuccessResponse($"Configured Light on '{target}'", new
            {
                type = light.type.ToString(),
                intensity = light.intensity,
                range = light.range,
                color = new { r = light.color.r, g = light.color.g, b = light.color.b }
            });
        }

        private static object AddLightProbeGroup(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "LightProbeGroup");

            GameObject probeGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(probeGo, "Add Light Probe Group");

            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    probeGo.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            LightProbeGroup probeGroup = probeGo.AddComponent<LightProbeGroup>();

            // Set probe positions if provided
            JToken probesToken = p.GetRaw("probe_positions");
            if (probesToken != null && probesToken.Type == JTokenType.Array)
            {
                var probeArray = probesToken as JArray;
                var positions = new List<Vector3>();
                foreach (var probe in probeArray)
                {
                    var coords = probe.ToObject<float[]>();
                    if (coords != null && coords.Length >= 3)
                        positions.Add(new Vector3(coords[0], coords[1], coords[2]));
                }
                if (positions.Count > 0)
                    probeGroup.probePositions = positions.ToArray();
            }

            return new SuccessResponse($"Created LightProbeGroup '{name}'", new
            {
                name = probeGo.name,
                instanceId = probeGo.GetInstanceID(),
                probeCount = probeGroup.probePositions.Length
            });
        }

        private static object AddReflectionProbe(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "ReflectionProbe");

            GameObject probeGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(probeGo, "Add Reflection Probe");

            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    probeGo.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();

            float? resolution = p.GetFloat("resolution");
            if (resolution.HasValue)
                probe.resolution = (int)resolution.Value;

            JToken sizeToken = p.GetRaw("size");
            if (sizeToken != null)
            {
                var size = sizeToken.ToObject<float[]>();
                if (size != null && size.Length >= 3)
                    probe.size = new Vector3(size[0], size[1], size[2]);
            }

            float? intensity = p.GetFloat("intensity");
            if (intensity.HasValue) probe.intensity = intensity.Value;

            string mode = p.Get("mode");
            if (!string.IsNullOrEmpty(mode) && Enum.TryParse<ReflectionProbeMode>(mode, true, out var probeMode))
                probe.mode = probeMode;

            return new SuccessResponse($"Created ReflectionProbe '{name}'", new
            {
                name = probeGo.name,
                instanceId = probeGo.GetInstanceID(),
                resolution = probe.resolution,
                size = new { x = probe.size.x, y = probe.size.y, z = probe.size.z }
            });
        }

        private static object SetAmbient(JObject @params, ToolParams p)
        {
            string mode = p.Get("mode");
            if (!string.IsNullOrEmpty(mode))
            {
                switch (mode.ToLowerInvariant())
                {
                    case "skybox":
                        RenderSettings.ambientMode = AmbientMode.Skybox;
                        break;
                    case "trilight":
                        RenderSettings.ambientMode = AmbientMode.Trilight;
                        break;
                    case "flat":
                    case "color":
                        RenderSettings.ambientMode = AmbientMode.Flat;
                        break;
                }
            }

            JToken colorToken = p.GetRaw("color");
            if (colorToken != null)
            {
                var color = colorToken.ToObject<float[]>();
                if (color != null && color.Length >= 3)
                {
                    float a = color.Length >= 4 ? color[3] : 1f;
                    RenderSettings.ambientLight = new Color(color[0], color[1], color[2], a);
                }
            }

            float? ambientIntensity = p.GetFloat("intensity");
            if (ambientIntensity.HasValue)
                RenderSettings.ambientIntensity = ambientIntensity.Value;

            JToken skyColorToken = p.GetRaw("sky_color");
            if (skyColorToken != null)
            {
                var c = skyColorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                    RenderSettings.ambientSkyColor = new Color(c[0], c[1], c[2]);
            }

            JToken equatorColorToken = p.GetRaw("equator_color");
            if (equatorColorToken != null)
            {
                var c = equatorColorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                    RenderSettings.ambientEquatorColor = new Color(c[0], c[1], c[2]);
            }

            JToken groundColorToken = p.GetRaw("ground_color");
            if (groundColorToken != null)
            {
                var c = groundColorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                    RenderSettings.ambientGroundColor = new Color(c[0], c[1], c[2]);
            }

            if (p.Has("fog"))
            {
                RenderSettings.fog = p.GetBool("fog", RenderSettings.fog);
            }

            JToken fogColorToken = p.GetRaw("fog_color");
            if (fogColorToken != null)
            {
                var c = fogColorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                    RenderSettings.fogColor = new Color(c[0], c[1], c[2]);
            }

            float? fogDensity = p.GetFloat("fog_density");
            if (fogDensity.HasValue) RenderSettings.fogDensity = fogDensity.Value;

            return new SuccessResponse("Updated ambient/environment settings", new
            {
                ambientMode = RenderSettings.ambientMode.ToString(),
                ambientIntensity = RenderSettings.ambientIntensity,
                fog = RenderSettings.fog,
                fogDensity = RenderSettings.fogDensity
            });
        }

        private static object GetLightingInfo(JObject @params, ToolParams p)
        {
            string target = p.Get("target");

            if (!string.IsNullOrEmpty(target))
            {
                GameObject go = GameObject.Find(target);
                if (go == null)
                    return new ErrorResponse($"GameObject '{target}' not found.");

                Light light = go.GetComponent<Light>();
                if (light == null)
                    return new ErrorResponse($"No Light on '{target}'.");

                return new SuccessResponse($"Light info for '{target}'", new
                {
                    name = go.name,
                    instanceId = go.GetInstanceID(),
                    type = light.type.ToString(),
                    color = new { r = light.color.r, g = light.color.g, b = light.color.b },
                    intensity = light.intensity,
                    range = light.range,
                    spotAngle = light.spotAngle,
                    shadows = light.shadows.ToString(),
                    shadowStrength = light.shadowStrength,
                    bounceIntensity = light.bounceIntensity,
                    enabled = light.enabled
                });
            }

            // Global lighting info
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var lightList = new List<object>();
            foreach (var l in lights)
            {
                lightList.Add(new
                {
                    name = l.gameObject.name,
                    instanceId = l.gameObject.GetInstanceID(),
                    type = l.type.ToString(),
                    intensity = l.intensity,
                    enabled = l.enabled
                });
            }

            return new SuccessResponse($"Found {lights.Length} lights", new
            {
                lights = lightList,
                ambient = new
                {
                    mode = RenderSettings.ambientMode.ToString(),
                    intensity = RenderSettings.ambientIntensity,
                    skyColor = new { r = RenderSettings.ambientSkyColor.r, g = RenderSettings.ambientSkyColor.g, b = RenderSettings.ambientSkyColor.b },
                    fog = RenderSettings.fog,
                    fogColor = new { r = RenderSettings.fogColor.r, g = RenderSettings.fogColor.g, b = RenderSettings.fogColor.b },
                    fogDensity = RenderSettings.fogDensity
                }
            });
        }
    }
}
