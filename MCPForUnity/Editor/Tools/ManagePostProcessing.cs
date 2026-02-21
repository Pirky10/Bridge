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
    [McpForUnityTool("manage_post_processing", AutoRegister = false)]
    public static class ManagePostProcessing
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

            Volume vol = go.AddComponent<Volume>();
            vol.isGlobal = isGlobal;

            float? priority = p.GetFloat("priority");
            if (priority.HasValue) vol.priority = priority.Value;

            float? weight = p.GetFloat("weight");
            if (weight.HasValue) vol.weight = weight.Value;

            // Create a new profile
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            string profilePath = p.Get("profile_path", $"Assets/{name}_Profile.asset");
            AssetDatabase.CreateAsset(profile, profilePath);
            vol.profile = profile;

            if (!isGlobal)
            {
                // Add box collider for local volumes
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
                if (blendDist.HasValue) vol.blendDistance = blendDist.Value;
            }

            EditorUtility.SetDirty(go);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created Volume '{name}'", new
            {
                name, isGlobal, profilePath
            });
        }

        private static object AddOverride(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var overrideResult = p.GetRequired("override_type");
            var overrideError = overrideResult.GetOrError(out string overrideType);
            if (overrideError != null) return overrideError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Volume vol = go.GetComponent<Volume>();
            if (vol == null) return new ErrorResponse($"No Volume on '{target}'.");
            if (vol.profile == null) return new ErrorResponse("Volume has no profile.");

            // Find effect type via reflection
            Type effectType = FindVolumeComponentType(overrideType);
            if (effectType == null)
                return new ErrorResponse($"Override type '{overrideType}' not found. Try: Bloom, ColorAdjustments, Vignette, DepthOfField, MotionBlur, LensDistortion, ChromaticAberration, FilmGrain, Tonemapping");

            Undo.RecordObject(vol.profile, "Add Override");
            var component = vol.profile.Add(effectType, true);

            EditorUtility.SetDirty(vol.profile);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added {overrideType} to '{target}'");
        }

        private static object ConfigureOverride(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var overrideResult = p.GetRequired("override_type");
            var overrideError = overrideResult.GetOrError(out string overrideType);
            if (overrideError != null) return overrideError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Volume vol = go.GetComponent<Volume>();
            if (vol == null || vol.profile == null) return new ErrorResponse("No Volume/profile found.");

            Type effectType = FindVolumeComponentType(overrideType);
            if (effectType == null) return new ErrorResponse($"Override type '{overrideType}' not found.");

            VolumeComponent comp = null;
            foreach (var c in vol.profile.components)
                if (effectType.IsAssignableFrom(c.GetType())) { comp = c; break; }

            if (comp == null)
                return new ErrorResponse($"{overrideType} not found on profile. Add it first.");

            Undo.RecordObject(comp, "Configure Override");

            // Apply settings from the params
            JToken settings = p.GetRaw("settings");
            if (settings is JObject settingsObj)
            {
                foreach (var kvp in settingsObj)
                {
                    FieldInfo field = effectType.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null && typeof(VolumeParameter).IsAssignableFrom(field.FieldType))
                    {
                        var param = field.GetValue(comp) as VolumeParameter;
                        if (param != null)
                        {
                            param.overrideState = true;
                            // Set value via reflection
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

            // Direct settings shortcuts
            if (comp.GetType().Name == "Bloom" || overrideType.ToLowerInvariant() == "bloom")
            {
                SetFloatParam(comp, "intensity", p.GetFloat("intensity"));
                SetFloatParam(comp, "threshold", p.GetFloat("threshold"));
                SetFloatParam(comp, "scatter", p.GetFloat("scatter"));
            }
            else if (overrideType.ToLowerInvariant().Contains("color"))
            {
                SetFloatParam(comp, "postExposure", p.GetFloat("post_exposure"));
                SetFloatParam(comp, "contrast", p.GetFloat("contrast"));
                SetFloatParam(comp, "saturation", p.GetFloat("saturation"));
            }
            else if (overrideType.ToLowerInvariant() == "vignette")
            {
                SetFloatParam(comp, "intensity", p.GetFloat("intensity"));
                SetFloatParam(comp, "smoothness", p.GetFloat("smoothness"));
            }

            EditorUtility.SetDirty(comp);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Configured {overrideType} on '{target}'");
        }

        private static void SetFloatParam(VolumeComponent comp, string name, float? value)
        {
            if (!value.HasValue) return;
            FieldInfo field = comp.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var param = field.GetValue(comp) as VolumeParameter;
                if (param != null)
                {
                    param.overrideState = true;
                    var valueProp = param.GetType().GetProperty("value");
                    if (valueProp != null) valueProp.SetValue(param, value.Value);
                }
            }
        }

        private static object RemoveOverride(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var overrideResult = p.GetRequired("override_type");
            var overrideError = overrideResult.GetOrError(out string overrideType);
            if (overrideError != null) return overrideError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Volume vol = go.GetComponent<Volume>();
            if (vol == null || vol.profile == null) return new ErrorResponse("No Volume/profile.");

            Type effectType = FindVolumeComponentType(overrideType);
            if (effectType == null) return new ErrorResponse($"Type '{overrideType}' not found.");

            Undo.RecordObject(vol.profile, "Remove Override");
            vol.profile.Remove(effectType);
            EditorUtility.SetDirty(vol.profile);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Removed {overrideType} from '{target}'");
        }

        private static object SetVolumeProperties(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Volume vol = go.GetComponent<Volume>();
            if (vol == null) return new ErrorResponse("No Volume component found.");

            Undo.RecordObject(vol, "Set Volume Properties");
            if (p.Has("is_global")) vol.isGlobal = p.GetBool("is_global", true);
            float? weight = p.GetFloat("weight");
            if (weight.HasValue) vol.weight = weight.Value;
            float? priority = p.GetFloat("priority");
            if (priority.HasValue) vol.priority = priority.Value;
            float? blendDist = p.GetFloat("blend_distance");
            if (blendDist.HasValue) vol.blendDistance = blendDist.Value;

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

            Volume vol = go.GetComponent<Volume>();
            if (vol == null) return new ErrorResponse("No Volume.");

            var overrides = new List<string>();
            if (vol.profile != null)
                foreach (var c in vol.profile.components)
                    overrides.Add(c.GetType().Name);

            return new SuccessResponse("Volume info", new
            {
                name = go.name, isGlobal = vol.isGlobal,
                weight = vol.weight, priority = vol.priority,
                blendDistance = vol.blendDistance,
                hasProfile = vol.profile != null,
                overrides, overrideCount = overrides.Count
            });
        }

        private static Type FindVolumeComponentType(string name)
        {
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
                    if (t != null && typeof(VolumeComponent).IsAssignableFrom(t)) return t;
                }
            }
            return null;
        }
    }
}
