using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_audio", AutoRegister = false)]
    public static class ManageAudio
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
                    case "add_source":
                        return AddSource(@params, p);
                    case "configure_source":
                        return ConfigureSource(@params, p);
                    case "add_listener":
                        return AddListener(@params, p);
                    case "play":
                        return Play(@params, p);
                    case "stop":
                        return Stop(@params, p);
                    case "pause":
                        return Pause(@params, p);
                    case "set_clip":
                        return SetClip(@params, p);
                    case "set_volume":
                        return SetVolume(@params, p);
                    case "get_audio_info":
                        return GetAudioInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: add_source, configure_source, add_listener, play, stop, pause, set_clip, set_volume, get_audio_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object AddSource(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add AudioSource");
            AudioSource source = go.AddComponent<AudioSource>();

            // Configure common properties
            float? volume = p.GetFloat("volume");
            float? pitch = p.GetFloat("pitch");
            bool loop = p.GetBool("loop", false);
            bool playOnAwake = p.GetBool("play_on_awake", true);
            float? spatialBlend = p.GetFloat("spatial_blend");
            float? minDistance = p.GetFloat("min_distance");
            float? maxDistance = p.GetFloat("max_distance");

            if (volume.HasValue) source.volume = volume.Value;
            if (pitch.HasValue) source.pitch = pitch.Value;
            source.loop = loop;
            source.playOnAwake = playOnAwake;
            if (spatialBlend.HasValue) source.spatialBlend = spatialBlend.Value;
            if (minDistance.HasValue) source.minDistance = minDistance.Value;
            if (maxDistance.HasValue) source.maxDistance = maxDistance.Value;

            // Assign clip if provided
            string clipPath = p.Get("clip_path");
            if (!string.IsNullOrEmpty(clipPath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(clipPath);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sanitized);
                if (clip != null)
                    source.clip = clip;
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added AudioSource to '{target}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                volume = source.volume,
                loop = source.loop,
                clip = source.clip != null ? source.clip.name : null
            });
        }

        private static object ConfigureSource(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
                return new ErrorResponse($"No AudioSource on '{target}'.");

            Undo.RecordObject(source, "Configure AudioSource");

            float? volume = p.GetFloat("volume");
            float? pitch = p.GetFloat("pitch");
            float? spatialBlend = p.GetFloat("spatial_blend");
            float? minDistance = p.GetFloat("min_distance");
            float? maxDistance = p.GetFloat("max_distance");

            if (volume.HasValue) source.volume = volume.Value;
            if (pitch.HasValue) source.pitch = pitch.Value;
            if (p.Has("loop")) source.loop = p.GetBool("loop", source.loop);
            if (p.Has("play_on_awake")) source.playOnAwake = p.GetBool("play_on_awake", source.playOnAwake);
            if (p.Has("mute")) source.mute = p.GetBool("mute", source.mute);
            if (spatialBlend.HasValue) source.spatialBlend = spatialBlend.Value;
            if (minDistance.HasValue) source.minDistance = minDistance.Value;
            if (maxDistance.HasValue) source.maxDistance = maxDistance.Value;

            float? priority = p.GetFloat("priority");
            if (priority.HasValue) source.priority = (int)priority.Value;

            string rolloffMode = p.Get("rolloff_mode");
            if (!string.IsNullOrEmpty(rolloffMode) && Enum.TryParse<AudioRolloffMode>(rolloffMode, true, out var mode))
                source.rolloffMode = mode;

            EditorUtility.SetDirty(source);

            return new SuccessResponse($"Configured AudioSource on '{target}'", new
            {
                volume = source.volume,
                pitch = source.pitch,
                loop = source.loop,
                spatialBlend = source.spatialBlend
            });
        }

        private static object AddListener(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            if (go.GetComponent<AudioListener>() != null)
                return new ErrorResponse($"'{target}' already has an AudioListener.");

            Undo.RecordObject(go, "Add AudioListener");
            go.AddComponent<AudioListener>();

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added AudioListener to '{target}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID()
            });
        }

        private static object Play(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
                return new ErrorResponse($"No AudioSource on '{target}'.");

            // Optionally set clip before play
            string clipPath = p.Get("clip_path");
            if (!string.IsNullOrEmpty(clipPath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(clipPath);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sanitized);
                if (clip != null)
                    source.clip = clip;
            }

            source.Play();

            return new SuccessResponse($"Playing audio on '{target}'", new
            {
                clip = source.clip != null ? source.clip.name : null,
                isPlaying = source.isPlaying
            });
        }

        private static object Stop(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
                return new ErrorResponse($"No AudioSource on '{target}'.");

            source.Stop();

            return new SuccessResponse($"Stopped audio on '{target}'");
        }

        private static object Pause(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
                return new ErrorResponse($"No AudioSource on '{target}'.");

            source.Pause();

            return new SuccessResponse($"Paused audio on '{target}'");
        }

        private static object SetClip(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var clipPathResult = p.GetRequired("clip_path");
            var clipError = clipPathResult.GetOrError(out string clipPath);
            if (clipError != null) return clipError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
                return new ErrorResponse($"No AudioSource on '{target}'.");

            string sanitized = AssetPathUtility.SanitizeAssetPath(clipPath);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sanitized);
            if (clip == null)
                return new ErrorResponse($"AudioClip not found at '{clipPath}'.");

            Undo.RecordObject(source, "Set AudioClip");
            source.clip = clip;
            EditorUtility.SetDirty(source);

            return new SuccessResponse($"Set clip '{clip.name}' on '{target}'", new
            {
                clip = clip.name,
                length = clip.length,
                frequency = clip.frequency,
                channels = clip.channels
            });
        }

        private static object SetVolume(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            float? volume = p.GetFloat("volume");
            if (!volume.HasValue)
                return new ErrorResponse("'volume' parameter is required (0.0 to 1.0).");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
                return new ErrorResponse($"No AudioSource on '{target}'.");

            Undo.RecordObject(source, "Set Audio Volume");
            source.volume = Mathf.Clamp01(volume.Value);
            EditorUtility.SetDirty(source);

            return new SuccessResponse($"Set volume to {source.volume} on '{target}'");
        }

        private static object GetAudioInfo(JObject @params, ToolParams p)
        {
            string target = p.Get("target");

            if (!string.IsNullOrEmpty(target))
            {
                GameObject go = GameObject.Find(target);
                if (go == null)
                    return new ErrorResponse($"GameObject '{target}' not found.");

                var info = new Dictionary<string, object>();
                info["name"] = go.name;
                info["instanceId"] = go.GetInstanceID();

                AudioSource source = go.GetComponent<AudioSource>();
                if (source != null)
                {
                    info["audioSource"] = new
                    {
                        clip = source.clip != null ? source.clip.name : null,
                        clipPath = source.clip != null ? AssetDatabase.GetAssetPath(source.clip) : null,
                        volume = source.volume,
                        pitch = source.pitch,
                        loop = source.loop,
                        playOnAwake = source.playOnAwake,
                        mute = source.mute,
                        spatialBlend = source.spatialBlend,
                        minDistance = source.minDistance,
                        maxDistance = source.maxDistance,
                        isPlaying = source.isPlaying,
                        priority = source.priority,
                        rolloffMode = source.rolloffMode.ToString()
                    };
                }

                AudioListener listener = go.GetComponent<AudioListener>();
                if (listener != null)
                {
                    info["hasAudioListener"] = true;
                }

                return new SuccessResponse($"Audio info for '{target}'", info);
            }

            // List all AudioSources in scene
            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            var sourceList = new List<object>();
            foreach (var s in sources)
            {
                sourceList.Add(new
                {
                    gameObject = s.gameObject.name,
                    instanceId = s.gameObject.GetInstanceID(),
                    clip = s.clip != null ? s.clip.name : null,
                    volume = s.volume,
                    isPlaying = s.isPlaying,
                    loop = s.loop
                });
            }

            return new SuccessResponse($"Found {sources.Length} AudioSources", new { audioSources = sourceList });
        }
    }
}
