using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_particle_system", AutoRegister = false)]
    public static class ManageParticleSystem
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
                    case "create":
                        return Create(@params, p);
                    case "configure_main":
                        return ConfigureMain(@params, p);
                    case "configure_emission":
                        return ConfigureEmission(@params, p);
                    case "configure_shape":
                        return ConfigureShape(@params, p);
                    case "configure_renderer":
                        return ConfigureRenderer(@params, p);
                    case "configure_color_over_lifetime":
                        return ConfigureColorOverLifetime(@params, p);
                    case "configure_size_over_lifetime":
                        return ConfigureSizeOverLifetime(@params, p);
                    case "play":
                        return Play(@params, p);
                    case "stop":
                        return Stop(@params, p);
                    case "get_particle_info":
                        return GetParticleInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: create, configure_main, configure_emission, configure_shape, configure_renderer, configure_color_over_lifetime, configure_size_over_lifetime, play, stop, get_particle_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static ParticleSystem FindPS(ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target)) return null;
            GameObject go = GameObject.Find(target);
            return go != null ? go.GetComponent<ParticleSystem>() : null;
        }

        private static object Create(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "ParticleSystem");

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Particle System");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            var main = ps.main;

            float? duration = p.GetFloat("duration");
            if (duration.HasValue) main.duration = duration.Value;

            float? startLifetime = p.GetFloat("start_lifetime");
            if (startLifetime.HasValue) main.startLifetime = startLifetime.Value;

            float? startSpeed = p.GetFloat("start_speed");
            if (startSpeed.HasValue) main.startSpeed = startSpeed.Value;

            float? startSize = p.GetFloat("start_size");
            if (startSize.HasValue) main.startSize = startSize.Value;

            int? maxParticles = p.GetInt("max_particles");
            if (maxParticles.HasValue) main.maxParticles = maxParticles.Value;

            if (p.Has("looping")) main.loop = p.GetBool("looping", true);
            if (p.Has("play_on_awake")) main.playOnAwake = p.GetBool("play_on_awake", true);

            JToken colorToken = p.GetRaw("start_color");
            if (colorToken != null)
            {
                var c = colorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                {
                    float a = c.Length >= 4 ? c[3] : 1f;
                    main.startColor = new Color(c[0], c[1], c[2], a);
                }
            }

            string simulationSpace = p.Get("simulation_space");
            if (!string.IsNullOrEmpty(simulationSpace) && Enum.TryParse<ParticleSystemSimulationSpace>(simulationSpace, true, out var space))
                main.simulationSpace = space;

            float? gravityModifier = p.GetFloat("gravity_modifier");
            if (gravityModifier.HasValue) main.gravityModifier = gravityModifier.Value;

            return new SuccessResponse($"Created Particle System '{name}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                duration = main.duration,
                startLifetime = main.startLifetime.constant,
                startSpeed = main.startSpeed.constant,
                maxParticles = main.maxParticles,
                looping = main.loop
            });
        }

        private static object ConfigureMain(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            ParticleSystem ps = FindPS(p);
            if (ps == null) return new ErrorResponse($"No ParticleSystem on '{target}'.");

            Undo.RecordObject(ps, "Configure Particle Main");
            var main = ps.main;

            float? duration = p.GetFloat("duration");
            if (duration.HasValue) main.duration = duration.Value;

            float? startLifetime = p.GetFloat("start_lifetime");
            if (startLifetime.HasValue) main.startLifetime = startLifetime.Value;

            float? startSpeed = p.GetFloat("start_speed");
            if (startSpeed.HasValue) main.startSpeed = startSpeed.Value;

            float? startSize = p.GetFloat("start_size");
            if (startSize.HasValue) main.startSize = startSize.Value;

            int? maxParticles = p.GetInt("max_particles");
            if (maxParticles.HasValue) main.maxParticles = maxParticles.Value;

            if (p.Has("looping")) main.loop = p.GetBool("looping", main.loop);
            if (p.Has("play_on_awake")) main.playOnAwake = p.GetBool("play_on_awake", main.playOnAwake);
            if (p.Has("prewarm")) main.prewarm = p.GetBool("prewarm", main.prewarm);

            JToken colorToken = p.GetRaw("start_color");
            if (colorToken != null)
            {
                var c = colorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                {
                    float a = c.Length >= 4 ? c[3] : 1f;
                    main.startColor = new Color(c[0], c[1], c[2], a);
                }
            }

            string simulationSpace = p.Get("simulation_space");
            if (!string.IsNullOrEmpty(simulationSpace) && Enum.TryParse<ParticleSystemSimulationSpace>(simulationSpace, true, out var space))
                main.simulationSpace = space;

            float? gravityModifier = p.GetFloat("gravity_modifier");
            if (gravityModifier.HasValue) main.gravityModifier = gravityModifier.Value;

            float? startRotation = p.GetFloat("start_rotation");
            if (startRotation.HasValue) main.startRotation = startRotation.Value * Mathf.Deg2Rad;

            EditorUtility.SetDirty(ps);

            return new SuccessResponse($"Configured main module on '{target}'");
        }

        private static object ConfigureEmission(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            ParticleSystem ps = FindPS(p);
            if (ps == null) return new ErrorResponse($"No ParticleSystem on '{target}'.");

            Undo.RecordObject(ps, "Configure Particle Emission");
            var emission = ps.emission;

            if (p.Has("enabled")) emission.enabled = p.GetBool("enabled", emission.enabled);

            float? rateOverTime = p.GetFloat("rate_over_time");
            if (rateOverTime.HasValue) emission.rateOverTime = rateOverTime.Value;

            float? rateOverDistance = p.GetFloat("rate_over_distance");
            if (rateOverDistance.HasValue) emission.rateOverDistance = rateOverDistance.Value;

            // Bursts
            JToken burstsToken = p.GetRaw("bursts");
            if (burstsToken != null && burstsToken.Type == JTokenType.Array)
            {
                var burstsArr = burstsToken as JArray;
                var bursts = new ParticleSystem.Burst[burstsArr.Count];
                for (int i = 0; i < burstsArr.Count; i++)
                {
                    var b = burstsArr[i];
                    float time = b["time"]?.ToObject<float>() ?? 0f;
                    int count = b["count"]?.ToObject<int>() ?? 10;
                    int cycles = b["cycles"]?.ToObject<int>() ?? 1;
                    float interval = b["interval"]?.ToObject<float>() ?? 0.01f;
                    bursts[i] = new ParticleSystem.Burst(time, (short)count, (short)count, cycles, interval);
                }
                emission.SetBursts(bursts);
            }

            EditorUtility.SetDirty(ps);

            return new SuccessResponse($"Configured emission on '{target}'", new
            {
                enabled = emission.enabled,
                rateOverTime = emission.rateOverTime.constant,
                burstCount = emission.burstCount
            });
        }

        private static object ConfigureShape(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            ParticleSystem ps = FindPS(p);
            if (ps == null) return new ErrorResponse($"No ParticleSystem on '{target}'.");

            Undo.RecordObject(ps, "Configure Particle Shape");
            var shape = ps.shape;

            if (p.Has("enabled")) shape.enabled = p.GetBool("enabled", shape.enabled);

            string shapeType = p.Get("shape_type");
            if (!string.IsNullOrEmpty(shapeType) && Enum.TryParse<ParticleSystemShapeType>(shapeType, true, out var st))
                shape.shapeType = st;

            float? radius = p.GetFloat("radius");
            if (radius.HasValue) shape.radius = radius.Value;

            float? angle = p.GetFloat("angle");
            if (angle.HasValue) shape.angle = angle.Value;

            float? arc = p.GetFloat("arc");
            if (arc.HasValue) shape.arc = arc.Value;

            float? radiusThickness = p.GetFloat("radius_thickness");
            if (radiusThickness.HasValue) shape.radiusThickness = radiusThickness.Value;

            JToken scaleToken = p.GetRaw("scale");
            if (scaleToken != null)
            {
                var s = scaleToken.ToObject<float[]>();
                if (s != null && s.Length >= 3)
                    shape.scale = new Vector3(s[0], s[1], s[2]);
            }

            EditorUtility.SetDirty(ps);

            return new SuccessResponse($"Configured shape on '{target}'", new
            {
                shapeType = shape.shapeType.ToString(),
                radius = shape.radius,
                angle = shape.angle
            });
        }

        private static object ConfigureRenderer(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return new ErrorResponse($"No ParticleSystemRenderer on '{target}'.");

            Undo.RecordObject(renderer, "Configure Particle Renderer");

            string renderMode = p.Get("render_mode");
            if (!string.IsNullOrEmpty(renderMode) && Enum.TryParse<ParticleSystemRenderMode>(renderMode, true, out var rm))
                renderer.renderMode = rm;

            string materialPath = p.Get("material_path");
            if (!string.IsNullOrEmpty(materialPath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(materialPath);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(sanitized);
                if (mat != null) renderer.material = mat;
            }

            string sortMode = p.Get("sort_mode");
            if (!string.IsNullOrEmpty(sortMode) && Enum.TryParse<ParticleSystemSortMode>(sortMode, true, out var sm))
                renderer.sortMode = sm;

            float? minParticleSize = p.GetFloat("min_particle_size");
            if (minParticleSize.HasValue) renderer.minParticleSize = minParticleSize.Value;

            float? maxParticleSize = p.GetFloat("max_particle_size");
            if (maxParticleSize.HasValue) renderer.maxParticleSize = maxParticleSize.Value;

            EditorUtility.SetDirty(renderer);

            return new SuccessResponse($"Configured renderer on '{target}'", new
            {
                renderMode = renderer.renderMode.ToString(),
                material = renderer.material != null ? renderer.material.name : null
            });
        }

        private static object ConfigureColorOverLifetime(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            ParticleSystem ps = FindPS(p);
            if (ps == null) return new ErrorResponse($"No ParticleSystem on '{target}'.");

            Undo.RecordObject(ps, "Configure Color Over Lifetime");
            var col = ps.colorOverLifetime;
            col.enabled = p.GetBool("enabled", true);

            JToken startColorToken = p.GetRaw("start_color");
            JToken endColorToken = p.GetRaw("end_color");

            if (startColorToken != null && endColorToken != null)
            {
                var sc = startColorToken.ToObject<float[]>();
                var ec = endColorToken.ToObject<float[]>();
                if (sc != null && sc.Length >= 3 && ec != null && ec.Length >= 3)
                {
                    Color startC = new Color(sc[0], sc[1], sc[2], sc.Length >= 4 ? sc[3] : 1f);
                    Color endC = new Color(ec[0], ec[1], ec[2], ec.Length >= 4 ? ec[3] : 0f);

                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        new[] { new GradientColorKey(startC, 0f), new GradientColorKey(endC, 1f) },
                        new[] { new GradientAlphaKey(startC.a, 0f), new GradientAlphaKey(endC.a, 1f) }
                    );
                    col.color = new ParticleSystem.MinMaxGradient(gradient);
                }
            }

            EditorUtility.SetDirty(ps);

            return new SuccessResponse($"Configured color over lifetime on '{target}'", new { enabled = col.enabled });
        }

        private static object ConfigureSizeOverLifetime(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            ParticleSystem ps = FindPS(p);
            if (ps == null) return new ErrorResponse($"No ParticleSystem on '{target}'.");

            Undo.RecordObject(ps, "Configure Size Over Lifetime");
            var sol = ps.sizeOverLifetime;
            sol.enabled = p.GetBool("enabled", true);

            float? startScale = p.GetFloat("start_scale");
            float? endScale = p.GetFloat("end_scale");

            if (startScale.HasValue && endScale.HasValue)
            {
                AnimationCurve curve = new AnimationCurve(
                    new Keyframe(0f, startScale.Value),
                    new Keyframe(1f, endScale.Value)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
            }

            EditorUtility.SetDirty(ps);

            return new SuccessResponse($"Configured size over lifetime on '{target}'", new { enabled = sol.enabled });
        }

        private static object Play(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            ParticleSystem ps = FindPS(p);
            if (ps == null) return new ErrorResponse($"No ParticleSystem on '{target}'.");

            bool withChildren = p.GetBool("with_children", true);
            ps.Play(withChildren);

            return new SuccessResponse($"Playing particle system on '{target}'");
        }

        private static object Stop(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            ParticleSystem ps = FindPS(p);
            if (ps == null) return new ErrorResponse($"No ParticleSystem on '{target}'.");

            bool withChildren = p.GetBool("with_children", true);
            bool clear = p.GetBool("clear", false);

            if (clear)
                ps.Stop(withChildren, ParticleSystemStopBehavior.StopEmittingAndClear);
            else
                ps.Stop(withChildren);

            return new SuccessResponse($"Stopped particle system on '{target}'");
        }

        private static object GetParticleInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            ParticleSystem ps = FindPS(p);
            if (ps == null) return new ErrorResponse($"No ParticleSystem on '{target}'.");

            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            return new SuccessResponse($"Particle System info for '{target}'", new
            {
                name = ps.gameObject.name,
                instanceId = ps.gameObject.GetInstanceID(),
                isPlaying = ps.isPlaying,
                particleCount = ps.particleCount,
                main = new
                {
                    duration = main.duration,
                    looping = main.loop,
                    startLifetime = main.startLifetime.constant,
                    startSpeed = main.startSpeed.constant,
                    startSize = main.startSize.constant,
                    maxParticles = main.maxParticles,
                    simulationSpace = main.simulationSpace.ToString(),
                    gravityModifier = main.gravityModifier.constant,
                    playOnAwake = main.playOnAwake
                },
                emission = new
                {
                    enabled = emission.enabled,
                    rateOverTime = emission.rateOverTime.constant,
                    burstCount = emission.burstCount
                },
                shape = new
                {
                    enabled = shape.enabled,
                    shapeType = shape.shapeType.ToString(),
                    radius = shape.radius,
                    angle = shape.angle
                }
            });
        }
    }
}
