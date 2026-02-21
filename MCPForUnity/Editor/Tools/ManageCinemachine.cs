using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_cinemachine", AutoRegister = false)]
    public static class ManageCinemachine
    {
        // Cinemachine 3.x uses different namespaces/types than 2.x.
        // We use reflection to support both versions transparently.

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            action = action.ToLowerInvariant();

            // Check if Cinemachine is installed
            Type cmBrainType = FindCinemachineType("CinemachineBrain") ?? FindCinemachineType("CinemachineVirtualCamera");
            if (cmBrainType == null)
            {
                return new ErrorResponse(
                    "Cinemachine is not installed. Install via Package Manager: " +
                    "com.unity.cinemachine");
            }

            try
            {
                switch (action)
                {
                    case "add_brain":
                        return AddBrain(@params, p);
                    case "create_virtual_camera":
                        return CreateVirtualCamera(@params, p);
                    case "configure_virtual_camera":
                        return ConfigureVirtualCamera(@params, p);
                    case "set_follow":
                        return SetFollow(@params, p);
                    case "set_look_at":
                        return SetLookAt(@params, p);
                    case "create_freelook":
                        return CreateFreeLook(@params, p);
                    case "create_state_driven":
                        return CreateStateDriven(@params, p);
                    case "set_priority":
                        return SetPriority(@params, p);
                    case "get_cinemachine_info":
                        return GetCinemachineInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: add_brain, create_virtual_camera, configure_virtual_camera, set_follow, set_look_at, create_freelook, create_state_driven, set_priority, get_cinemachine_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static Type FindCinemachineType(string typeName)
        {
            // Cinemachine 3.x namespace
            Type t = Type.GetType($"Unity.Cinemachine.{typeName}, Unity.Cinemachine");
            if (t != null) return t;

            // Cinemachine 2.x namespace
            t = Type.GetType($"Cinemachine.{typeName}, Cinemachine");
            if (t != null) return t;

            // Try assembly-qualified search
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = assembly.GetType($"Unity.Cinemachine.{typeName}");
                if (t != null) return t;
                t = assembly.GetType($"Cinemachine.{typeName}");
                if (t != null) return t;
            }

            return null;
        }

        private static object AddBrain(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Camera cam = go.GetComponent<Camera>();
            if (cam == null) return new ErrorResponse($"No Camera on '{target}'. CinemachineBrain requires a Camera.");

            Type brainType = FindCinemachineType("CinemachineBrain");
            if (brainType == null)
                return new ErrorResponse("CinemachineBrain type not found. Is Cinemachine installed?");

            if (go.GetComponent(brainType) != null)
                return new ErrorResponse($"'{target}' already has a CinemachineBrain.");

            Undo.RecordObject(go, "Add CinemachineBrain");
            var brain = go.AddComponent(brainType);

            // Configure blend time
            float? defaultBlend = p.GetFloat("default_blend");
            if (defaultBlend.HasValue)
            {
                var blendProp = brainType.GetProperty("DefaultBlend") ?? brainType.GetProperty("m_DefaultBlend");
                if (blendProp != null)
                {
                    // Try to set via reflection — blend is a struct
                    try
                    {
                        var blendObj = blendProp.GetValue(brain);
                        var timeProp = blendObj.GetType().GetField("m_Time") ?? blendObj.GetType().GetProperty("Time");
                        if (timeProp != null)
                        {
                            if (timeProp is System.Reflection.FieldInfo fi) fi.SetValue(blendObj, defaultBlend.Value);
                            else if (timeProp is System.Reflection.PropertyInfo pi) pi.SetValue(blendObj, defaultBlend.Value);
                            blendProp.SetValue(brain, blendObj);
                        }
                    }
                    catch { /* Ignore reflection issues */ }
                }
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added CinemachineBrain to '{target}'", new
            {
                name = go.name, instanceId = go.GetInstanceID()
            });
        }

        private static object CreateVirtualCamera(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "VirtualCamera");

            Type vcamType = FindCinemachineType("CinemachineVirtualCamera")
                         ?? FindCinemachineType("CinemachineCamera");
            if (vcamType == null)
                return new ErrorResponse("CinemachineVirtualCamera/CinemachineCamera type not found.");

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Virtual Camera");

            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            JToken rotToken = p.GetRaw("rotation");
            if (rotToken != null)
            {
                var rot = rotToken.ToObject<float[]>();
                if (rot != null && rot.Length >= 3)
                    go.transform.eulerAngles = new Vector3(rot[0], rot[1], rot[2]);
            }

            var vcam = go.AddComponent(vcamType);

            // Set priority
            int? priority = p.GetInt("priority");
            if (priority.HasValue)
            {
                var priorityProp = vcamType.GetProperty("Priority");
                if (priorityProp != null)
                    priorityProp.SetValue(vcam, priority.Value);
            }

            // Set FOV
            float? fov = p.GetFloat("fov");
            if (fov.HasValue)
            {
                var lensProp = vcamType.GetProperty("m_Lens") ?? vcamType.GetField("m_Lens");
                if (lensProp != null)
                {
                    try
                    {
                        object lens;
                        if (lensProp is System.Reflection.PropertyInfo pi) lens = pi.GetValue(vcam);
                        else lens = ((System.Reflection.FieldInfo)lensProp).GetValue(vcam);

                        var fovField = lens.GetType().GetField("FieldOfView") ?? lens.GetType().GetField("m_FieldOfView");
                        if (fovField != null)
                        {
                            fovField.SetValue(lens, fov.Value);
                            if (lensProp is System.Reflection.PropertyInfo pi2) pi2.SetValue(vcam, lens);
                            else ((System.Reflection.FieldInfo)lensProp).SetValue(vcam, lens);
                        }
                    }
                    catch { /* Ignore lens issues */ }
                }
            }

            // Set Follow/LookAt targets
            string follow = p.Get("follow");
            if (!string.IsNullOrEmpty(follow))
            {
                GameObject followGo = GameObject.Find(follow);
                if (followGo != null)
                {
                    var followProp = vcamType.GetProperty("Follow");
                    if (followProp != null) followProp.SetValue(vcam, followGo.transform);
                }
            }

            string lookAt = p.Get("look_at");
            if (!string.IsNullOrEmpty(lookAt))
            {
                GameObject lookAtGo = GameObject.Find(lookAt);
                if (lookAtGo != null)
                {
                    var lookAtProp = vcamType.GetProperty("LookAt");
                    if (lookAtProp != null) lookAtProp.SetValue(vcam, lookAtGo.transform);
                }
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Created Virtual Camera '{name}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                type = vcamType.Name
            });
        }

        private static object ConfigureVirtualCamera(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Type vcamType = FindCinemachineType("CinemachineVirtualCamera")
                         ?? FindCinemachineType("CinemachineCamera");
            if (vcamType == null)
                return new ErrorResponse("Cinemachine virtual camera type not found.");

            var vcam = go.GetComponent(vcamType);
            if (vcam == null)
                return new ErrorResponse($"No Cinemachine virtual camera on '{target}'.");

            Undo.RecordObject(vcam, "Configure Virtual Camera");

            // Body type (e.g., Transposer, FramingTransposer)
            string bodyType = p.Get("body_type");
            if (!string.IsNullOrEmpty(bodyType))
            {
                // CinemachineVirtualCamera uses AddCinemachineComponent
                var method = vcamType.GetMethod("AddCinemachineComponent", new Type[] { });
                if (method != null && method.IsGenericMethod)
                {
                    Type componentType = FindCinemachineType(bodyType);
                    if (componentType != null)
                    {
                        try
                        {
                            var generic = method.MakeGenericMethod(componentType);
                            generic.Invoke(vcam, null);
                        }
                        catch { /* Ignore — component might already exist or type mismatch */ }
                    }
                }
            }

            // Noise profile
            string noiseProfile = p.Get("noise_profile");
            if (!string.IsNullOrEmpty(noiseProfile))
            {
                Type noiseType = FindCinemachineType("CinemachineBasicMultiChannelPerlin");
                if (noiseType != null)
                {
                    var getCmMethod = vcamType.GetMethod("GetCinemachineComponent", new Type[] { });
                    if (getCmMethod != null && getCmMethod.IsGenericMethod)
                    {
                        try
                        {
                            var generic = getCmMethod.MakeGenericMethod(noiseType);
                            var noise = generic.Invoke(vcam, null);
                            if (noise == null)
                            {
                                var addMethod = vcamType.GetMethod("AddCinemachineComponent", new Type[] { });
                                if (addMethod != null)
                                {
                                    var addGeneric = addMethod.MakeGenericMethod(noiseType);
                                    noise = addGeneric.Invoke(vcam, null);
                                }
                            }

                            if (noise != null)
                            {
                                // Load noise profile asset
                                string[] guids = AssetDatabase.FindAssets($"t:NoiseSettings {noiseProfile}");
                                if (guids.Length > 0)
                                {
                                    var profileAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                                        AssetDatabase.GUIDToAssetPath(guids[0]));
                                    var profileProp = noiseType.GetField("m_NoiseProfile") ??
                                                      noiseType.GetField("NoiseProfile");
                                    if (profileProp != null && profileAsset != null)
                                        profileProp.SetValue(noise, profileAsset);
                                }

                                float? amplitudeGain = p.GetFloat("amplitude_gain");
                                if (amplitudeGain.HasValue)
                                {
                                    var ampField = noiseType.GetField("m_AmplitudeGain");
                                    if (ampField != null) ampField.SetValue(noise, amplitudeGain.Value);
                                }

                                float? frequencyGain = p.GetFloat("frequency_gain");
                                if (frequencyGain.HasValue)
                                {
                                    var freqField = noiseType.GetField("m_FrequencyGain");
                                    if (freqField != null) freqField.SetValue(noise, frequencyGain.Value);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            EditorUtility.SetDirty(vcam);

            return new SuccessResponse($"Configured virtual camera on '{target}'");
        }

        private static object SetFollow(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var followResult = p.GetRequired("follow");
            var followError = followResult.GetOrError(out string follow);
            if (followError != null) return followError;

            GameObject vcamGo = GameObject.Find(target);
            if (vcamGo == null) return new ErrorResponse($"GameObject '{target}' not found.");

            GameObject followGo = GameObject.Find(follow);
            if (followGo == null) return new ErrorResponse($"Follow target '{follow}' not found.");

            Type vcamType = FindCinemachineType("CinemachineVirtualCamera")
                         ?? FindCinemachineType("CinemachineCamera");
            if (vcamType == null) return new ErrorResponse("Cinemachine type not found.");

            var vcam = vcamGo.GetComponent(vcamType);
            if (vcam == null) return new ErrorResponse($"No virtual camera on '{target}'.");

            Undo.RecordObject(vcam, "Set Follow Target");
            var followProp = vcamType.GetProperty("Follow");
            if (followProp != null)
                followProp.SetValue(vcam, followGo.transform);

            EditorUtility.SetDirty(vcam);

            return new SuccessResponse($"Set Follow = '{follow}' on '{target}'");
        }

        private static object SetLookAt(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var lookAtResult = p.GetRequired("look_at");
            var lookAtError = lookAtResult.GetOrError(out string lookAt);
            if (lookAtError != null) return lookAtError;

            GameObject vcamGo = GameObject.Find(target);
            if (vcamGo == null) return new ErrorResponse($"GameObject '{target}' not found.");

            GameObject lookAtGo = GameObject.Find(lookAt);
            if (lookAtGo == null) return new ErrorResponse($"LookAt target '{lookAt}' not found.");

            Type vcamType = FindCinemachineType("CinemachineVirtualCamera")
                         ?? FindCinemachineType("CinemachineCamera");
            if (vcamType == null) return new ErrorResponse("Cinemachine type not found.");

            var vcam = vcamGo.GetComponent(vcamType);
            if (vcam == null) return new ErrorResponse($"No virtual camera on '{target}'.");

            Undo.RecordObject(vcam, "Set LookAt Target");
            var lookAtProp = vcamType.GetProperty("LookAt");
            if (lookAtProp != null)
                lookAtProp.SetValue(vcam, lookAtGo.transform);

            EditorUtility.SetDirty(vcam);

            return new SuccessResponse($"Set LookAt = '{lookAt}' on '{target}'");
        }

        private static object CreateFreeLook(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "FreeLookCamera");

            Type freeLookType = FindCinemachineType("CinemachineFreeLook");
            if (freeLookType == null)
                return new ErrorResponse("CinemachineFreeLook type not found. Might need Cinemachine 2.x.");

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create FreeLook Camera");

            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            var freeLook = go.AddComponent(freeLookType);

            string follow = p.Get("follow");
            if (!string.IsNullOrEmpty(follow))
            {
                GameObject followGo = GameObject.Find(follow);
                if (followGo != null)
                {
                    var followProp = freeLookType.GetProperty("Follow");
                    if (followProp != null) followProp.SetValue(freeLook, followGo.transform);
                }
            }

            string lookAt = p.Get("look_at");
            if (!string.IsNullOrEmpty(lookAt))
            {
                GameObject lookAtGo = GameObject.Find(lookAt);
                if (lookAtGo != null)
                {
                    var lookAtProp = freeLookType.GetProperty("LookAt");
                    if (lookAtProp != null) lookAtProp.SetValue(freeLook, lookAtGo.transform);
                }
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Created FreeLook Camera '{name}'", new
            {
                name = go.name, instanceId = go.GetInstanceID()
            });
        }

        private static object CreateStateDriven(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "StateDrivenCamera");

            Type sdType = FindCinemachineType("CinemachineStateDrivenCamera");
            if (sdType == null)
                return new ErrorResponse("CinemachineStateDrivenCamera type not found.");

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create State-Driven Camera");
            go.AddComponent(sdType);

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Created State-Driven Camera '{name}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                note = "Assign an Animated Target and child virtual cameras via the Inspector."
            });
        }

        private static object SetPriority(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            int? priority = p.GetInt("priority");
            if (!priority.HasValue) return new ErrorResponse("'priority' required.");

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Type vcamType = FindCinemachineType("CinemachineVirtualCamera")
                         ?? FindCinemachineType("CinemachineCamera");
            if (vcamType == null) return new ErrorResponse("Cinemachine type not found.");

            var vcam = go.GetComponent(vcamType);
            if (vcam == null)
            {
                // Try FreeLook
                Type freeLookType = FindCinemachineType("CinemachineFreeLook");
                if (freeLookType != null) vcam = go.GetComponent(freeLookType);
                if (vcam != null) vcamType = freeLookType;
            }

            if (vcam == null) return new ErrorResponse($"No Cinemachine camera on '{target}'.");

            Undo.RecordObject(vcam, "Set Priority");
            var priorityProp = vcamType.GetProperty("Priority");
            if (priorityProp != null)
                priorityProp.SetValue(vcam, priority.Value);

            EditorUtility.SetDirty(vcam);

            return new SuccessResponse($"Set priority = {priority.Value} on '{target}'");
        }

        private static object GetCinemachineInfo(JObject @params, ToolParams p)
        {
            string target = p.Get("target");

            if (!string.IsNullOrEmpty(target))
            {
                GameObject go = GameObject.Find(target);
                if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

                var info = new Dictionary<string, object> { { "name", go.name } };

                Type vcamType = FindCinemachineType("CinemachineVirtualCamera")
                             ?? FindCinemachineType("CinemachineCamera");
                if (vcamType != null)
                {
                    var vcam = go.GetComponent(vcamType);
                    if (vcam != null)
                    {
                        info["type"] = vcamType.Name;
                        var followProp = vcamType.GetProperty("Follow");
                        var lookAtProp = vcamType.GetProperty("LookAt");
                        var priorityProp = vcamType.GetProperty("Priority");

                        if (followProp != null)
                        {
                            Transform f = followProp.GetValue(vcam) as Transform;
                            info["follow"] = f != null ? f.name : null;
                        }
                        if (lookAtProp != null)
                        {
                            Transform l = lookAtProp.GetValue(vcam) as Transform;
                            info["lookAt"] = l != null ? l.name : null;
                        }
                        if (priorityProp != null)
                            info["priority"] = priorityProp.GetValue(vcam);

                        return new SuccessResponse("Cinemachine camera info", info);
                    }
                }

                Type brainType = FindCinemachineType("CinemachineBrain");
                if (brainType != null)
                {
                    var brain = go.GetComponent(brainType);
                    if (brain != null)
                    {
                        info["type"] = "CinemachineBrain";
                        return new SuccessResponse("CinemachineBrain info", info);
                    }
                }

                return new ErrorResponse($"No Cinemachine component on '{target}'.");
            }

            // List all virtual cameras in scene
            Type vcType = FindCinemachineType("CinemachineVirtualCamera")
                       ?? FindCinemachineType("CinemachineCamera");
            if (vcType == null) return new ErrorResponse("Cinemachine types not found.");

            var cameras = UnityEngine.Object.FindObjectsByType(vcType, FindObjectsSortMode.None);
            var camList = new List<object>();
            foreach (var cam in cameras)
            {
                var comp = cam as Component;
                if (comp == null) continue;
                var itemInfo = new Dictionary<string, object>
                {
                    { "name", comp.gameObject.name },
                    { "type", cam.GetType().Name }
                };

                var pProp = cam.GetType().GetProperty("Priority");
                if (pProp != null) itemInfo["priority"] = pProp.GetValue(cam);

                camList.Add(itemInfo);
            }

            return new SuccessResponse($"Found {camList.Count} Cinemachine cameras", new { cameras = camList });
        }
    }
}
