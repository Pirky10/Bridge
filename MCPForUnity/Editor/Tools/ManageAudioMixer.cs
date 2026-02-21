using System;
using System.Collections.Generic;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Manages Unity AudioMixer via reflection on AudioMixerController (internal Editor API).
    /// This avoids the "inaccessible due to its protection level" compile error.
    /// </summary>
    [McpForUnityTool("manage_audio_mixer", AutoRegister = false)]
    public static class ManageAudioMixer
    {
        // Cached reflected types
        private static Type _controllerType;
        private static Type _groupControllerType;
        private static bool _typesResolved;

        private static bool ResolveTypes()
        {
            if (_typesResolved) return _controllerType != null;
            _typesResolved = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _controllerType ??= asm.GetType("UnityEditor.Audio.AudioMixerController");
                _groupControllerType ??= asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
                if (_controllerType != null && _groupControllerType != null) break;
            }
            return _controllerType != null;
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            if (!ResolveTypes())
                return new ErrorResponse("AudioMixerController type not found. This Unity version may not support programmatic mixer control.");

            action = action.ToLowerInvariant();

            try
            {
                switch (action)
                {
                    case "create": return Create(@params, p);
                    case "add_group": return AddGroup(@params, p);
                    case "set_volume": return SetVolume(@params, p);
                    case "set_float": return SetFloat(@params, p);
                    case "get_float": return GetFloat(@params, p);
                    case "create_snapshot": return CreateSnapshot(@params, p);
                    case "expose_parameter": return ExposeParameter(@params, p);
                    case "assign_to_source": return AssignToSource(@params, p);
                    case "get_mixer_info": return GetMixerInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: create, add_group, set_volume, set_float, get_float, create_snapshot, expose_parameter, assign_to_source, get_mixer_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static UnityEngine.Object LoadMixerController(ToolParams p)
        {
            string path = p.Get("mixer_path");
            if (string.IsNullOrEmpty(path)) return null;
            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            // Load as the reflected controller type
            return AssetDatabase.LoadAssetAtPath(sanitized, _controllerType);
        }

        private static object Create(JObject @params, ToolParams p)
        {
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' required (e.g., Assets/Audio/MainMixer.mixer).");

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".mixer", StringComparison.OrdinalIgnoreCase))
                sanitized += ".mixer";

            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(sanitized) != null)
                return new ErrorResponse($"AudioMixer already exists at '{sanitized}'.");

            string dir = System.IO.Path.GetDirectoryName(sanitized);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            // Create via reflection
            var controller = ScriptableObject.CreateInstance(_controllerType);
            AssetDatabase.CreateAsset(controller, sanitized);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string masterName = "Master";
            var masterGroupProp = _controllerType.GetProperty("masterGroup");
            if (masterGroupProp != null)
            {
                var mg = masterGroupProp.GetValue(controller);
                if (mg != null)
                {
                    var nameProp = mg.GetType().GetProperty("name");
                    if (nameProp != null) masterName = nameProp.GetValue(mg) as string ?? "Master";
                }
            }

            return new SuccessResponse($"Created AudioMixer at '{sanitized}'", new
            {
                path = sanitized,
                name = controller.name,
                masterGroup = masterName
            });
        }

        private static object AddGroup(JObject @params, ToolParams p)
        {
            var mixerPathResult = p.GetRequired("mixer_path");
            var mixerPathError = mixerPathResult.GetOrError(out string mixerPath);
            if (mixerPathError != null) return mixerPathError;

            var groupNameResult = p.GetRequired("group_name");
            var groupError = groupNameResult.GetOrError(out string groupName);
            if (groupError != null) return groupError;

            var controller = LoadMixerController(p);
            if (controller == null) return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            // Check if group exists
            var findMethod = _controllerType.GetMethod("FindMatchingGroups", new Type[] { typeof(string) });
            var existing = findMethod?.Invoke(controller, new object[] { groupName }) as AudioMixerGroup[];
            if (existing != null && existing.Length > 0)
                return new ErrorResponse($"Group '{groupName}' already exists.");

            // Find parent group
            string parentName = p.Get("parent_group", "Master");
            object parent = _controllerType.GetProperty("masterGroup")?.GetValue(controller);

            if (!parentName.Equals("Master", StringComparison.OrdinalIgnoreCase))
            {
                var parentMatches = findMethod?.Invoke(controller, new object[] { parentName }) as AudioMixerGroup[];
                if (parentMatches != null && parentMatches.Length > 0
                    && _groupControllerType.IsAssignableFrom(parentMatches[0].GetType()))
                    parent = parentMatches[0];
            }

            Undo.RecordObject(controller, "Add Mixer Group");

            // CreateGroup(string name, bool undo)
            var createGroupMethod = _controllerType.GetMethod("CreateGroup", new Type[] { typeof(string), typeof(bool) });
            var newGroup = createGroupMethod?.Invoke(controller, new object[] { groupName, true });

            // AddChildToParent(child, parent)
            if (newGroup != null && parent != null)
            {
                var addChildMethod = _controllerType.GetMethod("AddChildToParent",
                    new Type[] { _groupControllerType, _groupControllerType });
                addChildMethod?.Invoke(controller, new object[] { newGroup, parent });
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string parentGroupName = "Master";
            if (parent != null)
            {
                var nameProp = parent.GetType().GetProperty("name");
                if (nameProp != null) parentGroupName = nameProp.GetValue(parent) as string ?? "Master";
            }

            return new SuccessResponse($"Added group '{groupName}' under '{parentGroupName}'", new
            {
                groupName,
                parentGroup = parentGroupName
            });
        }

        private static object SetVolume(JObject @params, ToolParams p)
        {
            var mixerPathResult = p.GetRequired("mixer_path");
            var mixerError = mixerPathResult.GetOrError(out string mixerPath);
            if (mixerError != null) return mixerError;

            var paramNameResult = p.GetRequired("parameter_name");
            var paramError = paramNameResult.GetOrError(out string paramName);
            if (paramError != null) return paramError;

            float? volume = p.GetFloat("volume");
            if (!volume.HasValue)
                return new ErrorResponse("'volume' required (0-1 range, converted to dB).");

            // Load as AudioMixer (public API) for SetFloat
            string sanitized = AssetPathUtility.SanitizeAssetPath(mixerPath);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(sanitized);
            if (mixer == null) return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            float db = volume.Value > 0.001f ? 20f * Mathf.Log10(volume.Value) : -80f;
            bool success = mixer.SetFloat(paramName, db);
            if (!success)
                return new ErrorResponse(
                    $"Parameter '{paramName}' not found or not exposed. " +
                    "Expose it first via the AudioMixer Inspector (right-click volume > 'Expose to script').");

            EditorUtility.SetDirty(mixer);

            return new SuccessResponse($"Set {paramName} = {db:F1} dB ({volume.Value:P0})", new
            {
                parameter = paramName,
                volumeLinear = volume.Value,
                volumeDb = db
            });
        }

        private static object SetFloat(JObject @params, ToolParams p)
        {
            var mixerPathResult = p.GetRequired("mixer_path");
            var mixerError = mixerPathResult.GetOrError(out string mixerPath);
            if (mixerError != null) return mixerError;

            var paramNameResult = p.GetRequired("parameter_name");
            var paramError = paramNameResult.GetOrError(out string paramName);
            if (paramError != null) return paramError;

            float? value = p.GetFloat("value");
            if (!value.HasValue) return new ErrorResponse("'value' required.");

            string sanitized = AssetPathUtility.SanitizeAssetPath(mixerPath);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(sanitized);
            if (mixer == null) return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            bool success = mixer.SetFloat(paramName, value.Value);
            if (!success)
                return new ErrorResponse($"Parameter '{paramName}' not found or not exposed.");

            EditorUtility.SetDirty(mixer);

            return new SuccessResponse($"Set {paramName} = {value.Value}", new
            {
                parameter = paramName,
                value = value.Value
            });
        }

        private static object GetFloat(JObject @params, ToolParams p)
        {
            var mixerPathResult = p.GetRequired("mixer_path");
            var mixerError = mixerPathResult.GetOrError(out string mixerPath);
            if (mixerError != null) return mixerError;

            var paramNameResult = p.GetRequired("parameter_name");
            var paramError = paramNameResult.GetOrError(out string paramName);
            if (paramError != null) return paramError;

            string sanitized = AssetPathUtility.SanitizeAssetPath(mixerPath);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(sanitized);
            if (mixer == null) return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            float value;
            bool success = mixer.GetFloat(paramName, out value);
            if (!success)
                return new ErrorResponse($"Parameter '{paramName}' not found or not exposed.");

            return new SuccessResponse($"{paramName} = {value}", new
            {
                parameter = paramName,
                value
            });
        }

        private static object CreateSnapshot(JObject @params, ToolParams p)
        {
            var mixerPathResult = p.GetRequired("mixer_path");
            var mixerError = mixerPathResult.GetOrError(out string mixerPath);
            if (mixerError != null) return mixerError;

            var snapshotNameResult = p.GetRequired("snapshot_name");
            var snapshotError = snapshotNameResult.GetOrError(out string snapshotName);
            if (snapshotError != null) return snapshotError;

            var controller = LoadMixerController(p);
            if (controller == null) return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            Undo.RecordObject(controller, "Create Snapshot");

            // CloneNewSnapshotFromTarget(bool)
            var cloneMethod = _controllerType.GetMethod("CloneNewSnapshotFromTarget", new Type[] { typeof(bool) });
            cloneMethod?.Invoke(controller, new object[] { true });

            // Rename the last snapshot
            var snapshotsProp = _controllerType.GetProperty("snapshots");
            var snapshots = snapshotsProp?.GetValue(controller) as Array;
            int count = snapshots?.Length ?? 0;
            if (snapshots != null && count > 0)
            {
                var newSnapshot = snapshots.GetValue(count - 1) as UnityEngine.Object;
                if (newSnapshot != null)
                {
                    newSnapshot.name = snapshotName;
                    EditorUtility.SetDirty(newSnapshot);
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created snapshot '{snapshotName}'", new
            {
                snapshotName,
                totalSnapshots = count
            });
        }

        private static object ExposeParameter(JObject @params, ToolParams p)
        {
            var mixerPathResult = p.GetRequired("mixer_path");
            var mixerError = mixerPathResult.GetOrError(out string mixerPath);
            if (mixerError != null) return mixerError;

            var groupNameResult = p.GetRequired("group_name");
            var groupError = groupNameResult.GetOrError(out string groupName);
            if (groupError != null) return groupError;

            string exposedName = p.Get("exposed_name", $"{groupName}Volume");

            var controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            var findMethod = _controllerType.GetMethod("FindMatchingGroups", new Type[] { typeof(string) });
            var groups = findMethod?.Invoke(controller, new object[] { groupName }) as AudioMixerGroup[];
            if (groups == null || groups.Length == 0)
                return new ErrorResponse($"Group '{groupName}' not found.");

            if (!_groupControllerType.IsAssignableFrom(groups[0].GetType()))
                return new ErrorResponse($"Group '{groupName}' could not be cast to AudioMixerGroupController.");

            var group = groups[0];

            Undo.RecordObject(controller, "Expose Parameter");

            try
            {
                // group.GetGUIDForVolume()
                var getGuidMethod = _groupControllerType.GetMethod("GetGUIDForVolume");
                var volumeGuid = getGuidMethod?.Invoke(group, null);

                // Create ExposedAudioParameter struct
                var exposedParamType = _controllerType.GetNestedType("ExposedAudioParameter");
                if (exposedParamType != null && volumeGuid != null)
                {
                    var exposedParam = Activator.CreateInstance(exposedParamType);
                    exposedParamType.GetField("name")?.SetValue(exposedParam, exposedName);
                    exposedParamType.GetField("guid")?.SetValue(exposedParam, volumeGuid);

                    var addExposedMethod = _controllerType.GetMethod("AddExposedParameter");
                    addExposedMethod?.Invoke(controller, new object[] { exposedParam });
                }
                else
                {
                    return new ErrorResponse("Could not resolve exposed parameter types. Expose manually via Inspector.");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Could not expose parameter: {ex.Message}. " +
                    "Try exposing manually: right-click the volume slider > 'Expose Volume to Script'.");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Exposed volume for '{groupName}' as '{exposedName}'", new
            {
                groupName,
                exposedParameterName = exposedName
            });
        }

        private static object AssignToSource(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var mixerPathResult = p.GetRequired("mixer_path");
            var mixerError = mixerPathResult.GetOrError(out string mixerPath);
            if (mixerError != null) return mixerError;

            string groupName = p.Get("group_name", "Master");

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null) return new ErrorResponse($"No AudioSource on '{target}'.");

            // Use public AudioMixer API for finding groups
            string sanitized = AssetPathUtility.SanitizeAssetPath(mixerPath);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(sanitized);
            if (mixer == null) return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            var groups = mixer.FindMatchingGroups(groupName);
            if (groups == null || groups.Length == 0)
                return new ErrorResponse($"Group '{groupName}' not found in mixer.");

            Undo.RecordObject(source, "Assign Mixer Group");
            source.outputAudioMixerGroup = groups[0];
            EditorUtility.SetDirty(source);

            return new SuccessResponse($"Assigned '{target}' to mixer group '{groupName}'", new
            {
                target,
                mixerGroup = groups[0].name
            });
        }

        private static object GetMixerInfo(JObject @params, ToolParams p)
        {
            var mixerPathResult = p.GetRequired("mixer_path");
            var mixerError = mixerPathResult.GetOrError(out string mixerPath);
            if (mixerError != null) return mixerError;

            // Use public API for read-only info
            string sanitized = AssetPathUtility.SanitizeAssetPath(mixerPath);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(sanitized);
            if (mixer == null) return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            var allGroups = mixer.FindMatchingGroups(string.Empty);
            var groupList = new List<object>();
            if (allGroups != null)
                foreach (var group in allGroups)
                    groupList.Add(new { name = group.name });

            // Use reflection for exposed params and snapshots (controller-only)
            var exposedParams = new List<string>();
            var snapshotNames = new List<string>();

            var controller = LoadMixerController(p);
            if (controller != null)
            {
                var exposedProp = _controllerType.GetProperty("exposedParameters");
                var exposed = exposedProp?.GetValue(controller) as Array;
                if (exposed != null)
                {
                    foreach (var ep in exposed)
                    {
                        var nameField = ep.GetType().GetField("name");
                        var epName = nameField?.GetValue(ep) as string;
                        if (epName != null) exposedParams.Add(epName);
                    }
                }

                var snapshotsProp = _controllerType.GetProperty("snapshots");
                var snapshots = snapshotsProp?.GetValue(controller) as Array;
                if (snapshots != null)
                {
                    foreach (var s in snapshots)
                    {
                        if (s is UnityEngine.Object obj) snapshotNames.Add(obj.name);
                    }
                }
            }

            return new SuccessResponse("AudioMixer info", new
            {
                name = mixer.name,
                path = mixerPath,
                groupCount = groupList.Count,
                groups = groupList,
                exposedParameters = exposedParams,
                snapshots = snapshotNames
            });
        }
    }
}
