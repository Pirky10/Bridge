using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_audio_mixer", AutoRegister = false)]
    public static class ManageAudioMixer
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
                    case "add_group":
                        return AddGroup(@params, p);
                    case "set_volume":
                        return SetVolume(@params, p);
                    case "set_float":
                        return SetFloat(@params, p);
                    case "get_float":
                        return GetFloat(@params, p);
                    case "create_snapshot":
                        return CreateSnapshot(@params, p);
                    case "expose_parameter":
                        return ExposeParameter(@params, p);
                    case "assign_to_source":
                        return AssignToSource(@params, p);
                    case "get_mixer_info":
                        return GetMixerInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: create, add_group, set_volume, set_float, get_float, create_snapshot, expose_parameter, assign_to_source, get_mixer_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static AudioMixerController LoadMixerController(ToolParams p)
        {
            string path = p.Get("mixer_path");
            if (string.IsNullOrEmpty(path)) return null;
            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            return AssetDatabase.LoadAssetAtPath<AudioMixerController>(sanitized);
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

            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(sanitized);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            // Use AudioMixerController — the Editor-only class that properly creates
            // mixer assets with a Master group and default Snapshot.
            var controller = new AudioMixerController();
            AssetDatabase.CreateAsset(controller, sanitized);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created AudioMixer at '{sanitized}'", new
            {
                path = sanitized,
                name = controller.name,
                masterGroup = controller.masterGroup != null ? controller.masterGroup.name : "Master"
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

            AudioMixerController controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            // Check if group already exists
            var existing = controller.FindMatchingGroups(groupName);
            if (existing != null && existing.Length > 0)
                return new ErrorResponse($"Group '{groupName}' already exists.");

            // Find parent group
            string parentName = p.Get("parent_group", "Master");
            AudioMixerGroupController parent = controller.masterGroup;

            if (!parentName.Equals("Master", StringComparison.OrdinalIgnoreCase))
            {
                var parentMatches = controller.FindMatchingGroups(parentName);
                if (parentMatches != null && parentMatches.Length > 0)
                    parent = parentMatches[0] as AudioMixerGroupController ?? controller.masterGroup;
            }

            Undo.RecordObject(controller, "Add Mixer Group");

            // Create the new group
            AudioMixerGroupController newGroup = controller.CreateGroup(groupName, true);
            controller.AddChildToParent(newGroup, parent);

            // Auto-expose volume parameter so set_volume works immediately
            bool autoExpose = p.GetBool("auto_expose_volume", true);
            if (autoExpose)
            {
                try
                {
                    string exposedName = $"{groupName}Volume";
                    GUID volumeGuid = newGroup.GetGUIDForVolume();
                    controller.AddExposedParameter(
                        new AudioMixerController.ExposedAudioParameter
                        {
                            name = exposedName,
                            guid = volumeGuid
                        }
                    );
                }
                catch
                {
                    // Some Unity versions may have different method signatures
                    // Volume will work via Inspector exposure in that case
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added group '{groupName}' under '{parent.name}'", new
            {
                groupName,
                parentGroup = parent.name,
                volumeParameter = $"{groupName}Volume"
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

            AudioMixerController controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            // Convert 0-1 to dB (-80 to 0)
            float db = volume.Value > 0.001f ? 20f * Mathf.Log10(volume.Value) : -80f;

            bool success = controller.SetFloat(paramName, db);
            if (!success)
                return new ErrorResponse(
                    $"Parameter '{paramName}' not found or not exposed. " +
                    "Use the 'expose_parameter' action or 'auto_expose_volume' when creating groups.");

            EditorUtility.SetDirty(controller);

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
            if (!value.HasValue)
                return new ErrorResponse("'value' required.");

            AudioMixerController controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            bool success = controller.SetFloat(paramName, value.Value);
            if (!success)
                return new ErrorResponse($"Parameter '{paramName}' not found or not exposed.");

            EditorUtility.SetDirty(controller);

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

            AudioMixerController controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            float value;
            bool success = controller.GetFloat(paramName, out value);
            if (!success)
                return new ErrorResponse($"Parameter '{paramName}' not found or not exposed.");

            return new SuccessResponse($"{paramName} = {value}", new
            {
                parameter = paramName,
                value = value
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

            AudioMixerController controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            Undo.RecordObject(controller, "Create Snapshot");

            // Clone the current snapshot into a new one
            controller.CloneNewSnapshotFromTarget(true);

            // The new snapshot is auto-selected; rename it
            var snapshots = controller.snapshots;
            if (snapshots != null && snapshots.Length > 0)
            {
                var newSnapshot = snapshots[snapshots.Length - 1];
                newSnapshot.name = snapshotName;
                EditorUtility.SetDirty(newSnapshot);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created snapshot '{snapshotName}'", new
            {
                snapshotName,
                totalSnapshots = controller.snapshots?.Length
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

            AudioMixerController controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            var groups = controller.FindMatchingGroups(groupName);
            if (groups == null || groups.Length == 0)
                return new ErrorResponse($"Group '{groupName}' not found.");

            AudioMixerGroupController group = groups[0] as AudioMixerGroupController;
            if (group == null)
                return new ErrorResponse($"Group '{groupName}' could not be cast to AudioMixerGroupController.");

            Undo.RecordObject(controller, "Expose Parameter");

            try
            {
                GUID volumeGuid = group.GetGUIDForVolume();
                controller.AddExposedParameter(
                    new AudioMixerController.ExposedAudioParameter
                    {
                        name = exposedName,
                        guid = volumeGuid
                    }
                );
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
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
                return new ErrorResponse($"No AudioSource on '{target}'.");

            AudioMixerController controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            var groups = controller.FindMatchingGroups(groupName);
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

            AudioMixerController controller = LoadMixerController(p);
            if (controller == null)
                return new ErrorResponse($"AudioMixer not found at '{mixerPath}'.");

            // Gather all groups
            var allGroups = controller.FindMatchingGroups(string.Empty);
            var groupList = new List<object>();
            if (allGroups != null)
            {
                foreach (var group in allGroups)
                {
                    groupList.Add(new { name = group.name });
                }
            }

            // Gather exposed parameters
            var exposedParams = new List<string>();
            var exposed = controller.exposedParameters;
            if (exposed != null)
            {
                foreach (var ep in exposed)
                    exposedParams.Add(ep.name);
            }

            // Gather snapshots
            var snapshotNames = new List<string>();
            var snapshots = controller.snapshots;
            if (snapshots != null)
            {
                foreach (var s in snapshots)
                    snapshotNames.Add(s.name);
            }

            return new SuccessResponse("AudioMixer info", new
            {
                name = controller.name,
                path = mixerPath,
                groupCount = groupList.Count,
                groups = groupList,
                exposedParameters = exposedParams,
                snapshots = snapshotNames
            });
        }
    }
}
