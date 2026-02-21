using System;
using System.Collections.Generic;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Manages URP/HDRP post-processing Volumes and overrides via reflection.
    /// No hard dependency on any render pipeline package.
    /// </summary>
    [McpForUnityTool("manage_post_processing", AutoRegister = false)]
    public static class ManagePostProcessing
    {
        // Cached types resolved at runtime via reflection
        private static Type _volumeType;
        private static Type _volumeProfileType;
        private static Type _volumeComponentType;
        private static Type _volumeParameterType;
        private static bool _typesResolved;

        private static bool ResolveTypes()
        {
            if (_typesResolved) return _volumeType != null;
            _typesResolved = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _volumeType ??= asm.GetType("UnityEngine.Rendering.Volume");
                _volumeProfileType ??= asm.GetType("UnityEngine.Rendering.VolumeProfile");
                _volumeComponentType ??= asm.GetType("UnityEngine.Rendering.VolumeComponent");
                _volumeParameterType ??= asm.GetType("UnityEngine.Rendering.VolumeParameter");
                if (_volumeType != null && _volumeProfileType != null && _volumeComponentType != null && _volumeParameterType != null)
                    break;
            }
            return _volumeType != null;
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            if (!ResolveTypes())
                return new ErrorResponse("Post-processing types not found. Install URP or HDRP package (com.unity.render-pipelines.universal or com.unity.render-pipelines.high-definition).");

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "add_volume": return AddVolume(@params, p);
                    case "add_override": return AddOverride(@params, p);
                    case "configure_override": return ConfigureOverride(@params, p);
                    case "remove_override": return RemoveOverride(@params, p);
                    case "set_volume_properties": return SetVolumeProperties(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: add_volume, add_override, configure_override, remove_override, set_volume_properties, get_info");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object AddVolume(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "Post Processing Volume");
            bool isGlobal = p.GetBool("is_global", true);

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Volume");

            // Add Volume component via reflection
            Component vol = go.AddComponent(_volumeType);
            _volumeType.GetProperty("isGlobal")?.SetValue(vol, isGlobal);

            float? priority = p.GetFloat("priority");
            if (priority.HasValue) _volumeType.GetProperty("priority")?.SetValue(vol, priority.Value);

            float? weight = p.GetFloat("weight");
            if (weight.HasValue) _volumeType.GetProperty("weight")?.SetValue(vol, weight.Value);

            // Create a new profile
            var profile = ScriptableObject.CreateInstance(_volumeProfileType);
            string profilePath = p.Get("profile_path", $"Assets/{name}_Profile.asset");
            AssetDatabase.CreateAsset(profile, profilePath);
            _volumeType.GetProperty("profile")?.SetValue(vol, profile);

            if (!isGlobal)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                JToken sizeToken = p.GetRaw("size");
                if (sizeToken != null)
                {
                    var s = sizeToken.ToObject<float[]>();
                    if (s != null && s.Length >= 3)
                        bc.size = new Vector3(s[0], s[1], s[2]);
                }
                float? blendDist = p.GetFloat("blend_distance");
                if (blendDist.HasValue) _volumeType.GetProperty("blendDistance")?.SetValue(vol, blendDist.Value);
            }

            EditorUtility.SetDirty(go);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created Volume '{name}'", new { name, isGlobal, profilePath });
        }

        private static object AddOverride(JObject @params, ToolParams p)
        {
            var r = GetVolumeOnTarget(p, out Component vol, out object profile);
            if (r != null) return r;

            var overrideResult = p.GetRequired("override_type");
            var overrideError = overrideResult.GetOrError(out string overrideType);
            if (overrideError != null) return overrideError;

            Type effectType = FindVolumeComponentType(overrideType);
            if (effectType == null)
                return new ErrorResponse($"Override type '{overrideType}' not found. Try: Bloom, ColorAdjustments, Vignette, DepthOfField, MotionBlur, LensDistortion, ChromaticAberration, FilmGrain, Tonemapping");

            Undo.RecordObject((UnityEngine.Object)profile, "Add Override");
            MethodInfo addMethod = _volumeProfileType.GetMethod("Add", new Type[] { typeof(Type), typeof(bool) });
            addMethod?.Invoke(profile, new object[] { effectType, true });

            EditorUtility.SetDirty((UnityEngine.Object)profile);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added {overrideType} to volume");
        }

        private static object ConfigureOverride(JObject @params, ToolParams p)
        {
            var r = GetVolumeOnTarget(p, out Component vol, out object profile);
            if (r != null) return r;

            var overrideResult = p.GetRequired("override_type");
            var overrideError = overrideResult.GetOrError(out string overrideType);
            if (overrideError != null) return overrideError;

            Type effectType = FindVolumeComponentType(overrideType);
            if (effectType == null) return new ErrorResponse($"Override type '{overrideType}' not found.");

            // Get components list from profile
            var componentsProp = _volumeProfileType.GetProperty("components");
            var components = componentsProp?.GetValue(profile) as System.Collections.IList;
            if (components == null) return new ErrorResponse("No components on profile.");

            object comp = null;
            foreach (var c in components)
                if (effectType.IsAssignableFrom(c.GetType())) { comp = c; break; }

            if (comp == null) return new ErrorResponse($"{overrideType} not found on profile. Add it first.");

            Undo.RecordObject((UnityEngine.Object)comp, "Configure Override");

            // Apply settings from the params
            JToken settings = p.GetRaw("settings");
            if (settings is JObject settingsObj)
                ApplySettings(comp, effectType, settingsObj);

            // Direct shortcuts
            string lcOverride = overrideType.ToLowerInvariant();
            if (lcOverride == "bloom")
            {
                SetFloatParam(comp, "intensity", p.GetFloat("intensity"));
                SetFloatParam(comp, "threshold", p.GetFloat("threshold"));
                SetFloatParam(comp, "scatter", p.GetFloat("scatter"));
            }
            else if (lcOverride.Contains("color"))
            {
                SetFloatParam(comp, "postExposure", p.GetFloat("post_exposure"));
                SetFloatParam(comp, "contrast", p.GetFloat("contrast"));
                SetFloatParam(comp, "saturation", p.GetFloat("saturation"));
            }
            else if (lcOverride == "vignette")
            {
                SetFloatParam(comp, "intensity", p.GetFloat("intensity"));
                SetFloatParam(comp, "smoothness", p.GetFloat("smoothness"));
            }

            EditorUtility.SetDirty((UnityEngine.Object)comp);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Configured {overrideType}");
        }

        private static void ApplySettings(object comp, Type effectType, JObject settingsObj)
        {
            foreach (var kvp in settingsObj)
            {
                FieldInfo field = effectType.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && _volumeParameterType.IsAssignableFrom(field.FieldType))
                {
                    var param = field.GetValue(comp);
                    if (param != null)
                    {
                        // param.overrideState = true
                        var overrideProp = param.GetType().GetProperty("overrideState");
                        overrideProp?.SetValue(param, true);
                        // param.value = ...
                        var valueProp = param.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                        if (valueProp != null)
                        {
                            try
                            {
                                object val = kvp.Value.ToObject(valueProp.PropertyType);
                                valueProp.SetValue(param, val);
                            }
                            catch { /* skip incompatible values */ }
                        }
                    }
                }
            }
        }

        private static void SetFloatParam(object comp, string name, float? value)
        {
            if (!value.HasValue) return;
            FieldInfo field = comp.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var param = field.GetValue(comp);
                if (param != null)
                {
                    param.GetType().GetProperty("overrideState")?.SetValue(param, true);
                    var valueProp = param.GetType().GetProperty("value");
                    if (valueProp != null) valueProp.SetValue(param, value.Value);
                }
            }
        }

        private static object RemoveOverride(JObject @params, ToolParams p)
        {
            var r = GetVolumeOnTarget(p, out Component vol, out object profile);
            if (r != null) return r;

            var overrideResult = p.GetRequired("override_type");
            var overrideError = overrideResult.GetOrError(out string overrideType);
            if (overrideError != null) return overrideError;

            Type effectType = FindVolumeComponentType(overrideType);
            if (effectType == null) return new ErrorResponse($"Type '{overrideType}' not found.");

            Undo.RecordObject((UnityEngine.Object)profile, "Remove Override");
            MethodInfo removeMethod = _volumeProfileType.GetMethod("Remove", new Type[] { typeof(Type) });
            removeMethod?.Invoke(profile, new object[] { effectType });
            EditorUtility.SetDirty((UnityEngine.Object)profile);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Removed {overrideType}");
        }

        private static object SetVolumeProperties(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Component vol = go.GetComponent(_volumeType);
            if (vol == null) return new ErrorResponse("No Volume component found.");

            Undo.RecordObject(vol, "Set Volume Properties");
            if (p.Has("is_global")) _volumeType.GetProperty("isGlobal")?.SetValue(vol, p.GetBool("is_global", true));
            float? weight = p.GetFloat("weight");
            if (weight.HasValue) _volumeType.GetProperty("weight")?.SetValue(vol, weight.Value);
            float? priority = p.GetFloat("priority");
            if (priority.HasValue) _volumeType.GetProperty("priority")?.SetValue(vol, priority.Value);
            float? blendDist = p.GetFloat("blend_distance");
            if (blendDist.HasValue) _volumeType.GetProperty("blendDistance")?.SetValue(vol, blendDist.Value);

            EditorUtility.SetDirty(vol);
            return new SuccessResponse($"Updated Volume properties on '{target}'");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Component vol = go.GetComponent(_volumeType);
            if (vol == null) return new ErrorResponse("No Volume.");

            bool isGlobal = (bool)(_volumeType.GetProperty("isGlobal")?.GetValue(vol) ?? false);
            float weight = (float)(_volumeType.GetProperty("weight")?.GetValue(vol) ?? 0f);
            float prio = (float)(_volumeType.GetProperty("priority")?.GetValue(vol) ?? 0f);
            float blend = (float)(_volumeType.GetProperty("blendDistance")?.GetValue(vol) ?? 0f);
            object profile = _volumeType.GetProperty("profile")?.GetValue(vol);

            var overrides = new List<string>();
            if (profile != null)
            {
                var componentsProp = _volumeProfileType.GetProperty("components");
                var components = componentsProp?.GetValue(profile) as System.Collections.IList;
                if (components != null)
                    foreach (var c in components)
                        overrides.Add(c.GetType().Name);
            }

            return new SuccessResponse("Volume info", new
            {
                name = go.name, isGlobal, weight, priority = prio,
                blendDistance = blend, hasProfile = profile != null,
                overrides, overrideCount = overrides.Count
            });
        }

        // --- Helpers ---

        private static object GetVolumeOnTarget(ToolParams p, out Component vol, out object profile)
        {
            vol = null; profile = null;
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            vol = go.GetComponent(_volumeType);
            if (vol == null) return new ErrorResponse($"No Volume on '{target}'.");

            profile = _volumeType.GetProperty("profile")?.GetValue(vol);
            if (profile == null) return new ErrorResponse("Volume has no profile.");

            return null;
        }

        private static Type FindVolumeComponentType(string name)
        {
            if (_volumeComponentType == null) return null;
            string[] tryNames = {
                $"UnityEngine.Rendering.Universal.{name}",
                $"UnityEngine.Rendering.HighDefinition.{name}",
                $"UnityEngine.Rendering.{name}",
                name
            };
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string n in tryNames)
                {
                    Type t = assembly.GetType(n);
                    if (t != null && _volumeComponentType.IsAssignableFrom(t)) return t;
                }
            }
            return null;
        }
    }
}
