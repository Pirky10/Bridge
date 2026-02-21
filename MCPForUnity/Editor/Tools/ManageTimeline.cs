using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
#if UNITY_TIMELINE
using UnityEngine.Timeline;
using UnityEngine.Playables;
#endif

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_timeline", AutoRegister = false)]
    public static class ManageTimeline
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            action = action.ToLowerInvariant();

#if !UNITY_TIMELINE
            return new ErrorResponse(
                "The Timeline package is not installed. Install it via Package Manager " +
                "(com.unity.timeline) to use manage_timeline.");
#else
            try
            {
                switch (action)
                {
                    case "create_asset":
                        return CreateAsset(@params, p);
                    case "add_track":
                        return AddTrack(@params, p);
                    case "add_clip":
                        return AddClip(@params, p);
                    case "set_clip_timing":
                        return SetClipTiming(@params, p);
                    case "bind_track":
                        return BindTrack(@params, p);
                    case "get_timeline_info":
                        return GetTimelineInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: create_asset, add_track, add_clip, set_clip_timing, bind_track, get_timeline_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
#endif
        }

#if UNITY_TIMELINE
        private static object CreateAsset(JObject @params, ToolParams p)
        {
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' parameter is required (e.g., Assets/Timelines/Cutscene.playable).");

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".playable", StringComparison.OrdinalIgnoreCase))
                sanitized += ".playable";

            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(sanitized) != null)
                return new ErrorResponse($"Timeline asset already exists at '{sanitized}'.");

            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, sanitized);
            AssetDatabase.SaveAssets();

            float? duration = p.GetFloat("duration");
            if (duration.HasValue) timeline.fixedDuration = duration.Value;

            // Optionally assign to a PlayableDirector
            string targetName = p.Get("target");
            if (!string.IsNullOrEmpty(targetName))
            {
                GameObject go = GameObject.Find(targetName);
                if (go != null)
                {
                    PlayableDirector director = go.GetComponent<PlayableDirector>();
                    if (director == null)
                    {
                        Undo.RecordObject(go, "Add PlayableDirector");
                        director = go.AddComponent<PlayableDirector>();
                    }
                    Undo.RecordObject(director, "Assign Timeline");
                    director.playableAsset = timeline;
                    EditorUtility.SetDirty(director);
                }
            }

            return new SuccessResponse($"Created Timeline at '{sanitized}'", new
            {
                path = sanitized,
                duration = timeline.duration
            });
        }

        private static object AddTrack(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            string trackType = p.Get("track_type", "AnimationTrack");

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".playable", StringComparison.OrdinalIgnoreCase))
                sanitized += ".playable";

            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(sanitized);
            if (timeline == null)
                return new ErrorResponse($"Timeline not found at '{path}'.");

            TrackAsset track;
            switch (trackType.ToLowerInvariant())
            {
                case "animationtrack":
                case "animation":
                    track = timeline.CreateTrack<AnimationTrack>(null, p.Get("track_name", "Animation Track"));
                    break;
                case "audiotrack":
                case "audio":
                    track = timeline.CreateTrack<AudioTrack>(null, p.Get("track_name", "Audio Track"));
                    break;
                case "activationtrack":
                case "activation":
                    track = timeline.CreateTrack<ActivationTrack>(null, p.Get("track_name", "Activation Track"));
                    break;
                case "grouptrack":
                case "group":
                    track = timeline.CreateTrack<GroupTrack>(null, p.Get("track_name", "Group Track"));
                    break;
                default:
                    return new ErrorResponse($"Unknown track type: {trackType}. Valid types: AnimationTrack, AudioTrack, ActivationTrack, GroupTrack");
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added {trackType} to timeline", new
            {
                trackName = track.name,
                trackType = track.GetType().Name
            });
        }

        private static object AddClip(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            var trackNameResult = p.GetRequired("track_name");
            var trackError = trackNameResult.GetOrError(out string trackName);
            if (trackError != null) return trackError;

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".playable", StringComparison.OrdinalIgnoreCase))
                sanitized += ".playable";

            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(sanitized);
            if (timeline == null)
                return new ErrorResponse($"Timeline not found at '{path}'.");

            TrackAsset targetTrack = null;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track.name == trackName)
                {
                    targetTrack = track;
                    break;
                }
            }

            if (targetTrack == null)
                return new ErrorResponse($"Track '{trackName}' not found in timeline.");

            double start = p.GetFloat("start") ?? 0.0;
            double duration = p.GetFloat("duration") ?? 1.0;

            TimelineClip clip = null;

            // For audio tracks, try to load the audio clip
            if (targetTrack is AudioTrack audioTrack)
            {
                string clipPath = p.Get("clip_path");
                if (!string.IsNullOrEmpty(clipPath))
                {
                    string clipSanitized = AssetPathUtility.SanitizeAssetPath(clipPath);
                    AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipSanitized);
                    if (audioClip != null)
                    {
                        clip = audioTrack.CreateClip(audioClip);
                    }
                }
            }

            if (clip == null)
            {
                clip = targetTrack.CreateDefaultClip();
            }

            clip.start = start;
            clip.duration = duration;

            string clipName = p.Get("clip_name");
            if (!string.IsNullOrEmpty(clipName))
                clip.displayName = clipName;

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added clip to track '{trackName}'", new
            {
                clipName = clip.displayName,
                start = clip.start,
                duration = clip.duration
            });
        }

        private static object SetClipTiming(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            var trackNameResult = p.GetRequired("track_name");
            var trackError = trackNameResult.GetOrError(out string trackName);
            if (trackError != null) return trackError;

            int clipIndex = p.GetInt("clip_index") ?? 0;

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".playable", StringComparison.OrdinalIgnoreCase))
                sanitized += ".playable";

            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(sanitized);
            if (timeline == null)
                return new ErrorResponse($"Timeline not found at '{path}'.");

            TrackAsset targetTrack = null;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track.name == trackName) { targetTrack = track; break; }
            }
            if (targetTrack == null)
                return new ErrorResponse($"Track '{trackName}' not found.");

            var clips = targetTrack.GetClips();
            int idx = 0;
            TimelineClip targetClip = null;
            foreach (var c in clips)
            {
                if (idx == clipIndex) { targetClip = c; break; }
                idx++;
            }
            if (targetClip == null)
                return new ErrorResponse($"Clip index {clipIndex} out of range.");

            double? start = p.GetFloat("start");
            double? duration = p.GetFloat("duration");

            if (start.HasValue) targetClip.start = start.Value;
            if (duration.HasValue) targetClip.duration = duration.Value;

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Updated clip timing", new
            {
                clipName = targetClip.displayName,
                start = targetClip.start,
                duration = targetClip.duration
            });
        }

        private static object BindTrack(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var trackNameResult = p.GetRequired("track_name");
            var trackError = trackNameResult.GetOrError(out string trackName);
            if (trackError != null) return trackError;

            var bindTargetResult = p.GetRequired("bind_target");
            var bindError = bindTargetResult.GetOrError(out string bindTarget);
            if (bindError != null) return bindError;

            GameObject directorGo = GameObject.Find(target);
            if (directorGo == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            PlayableDirector director = directorGo.GetComponent<PlayableDirector>();
            if (director == null)
                return new ErrorResponse($"No PlayableDirector on '{target}'.");

            TimelineAsset timeline = director.playableAsset as TimelineAsset;
            if (timeline == null)
                return new ErrorResponse($"No timeline assigned to PlayableDirector on '{target}'.");

            TrackAsset targetTrack = null;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track.name == trackName) { targetTrack = track; break; }
            }
            if (targetTrack == null)
                return new ErrorResponse($"Track '{trackName}' not found.");

            GameObject bindGo = GameObject.Find(bindTarget);
            if (bindGo == null)
                return new ErrorResponse($"Bind target '{bindTarget}' not found.");

            Undo.RecordObject(director, "Bind Timeline Track");

            // Determine what to bind based on track type
            UnityEngine.Object bindObj = bindGo;
            if (targetTrack is AnimationTrack)
            {
                Animator animator = bindGo.GetComponent<Animator>();
                if (animator != null) bindObj = animator;
            }
            else if (targetTrack is AudioTrack)
            {
                AudioSource source = bindGo.GetComponent<AudioSource>();
                if (source != null) bindObj = source;
            }

            director.SetGenericBinding(targetTrack, bindObj);
            EditorUtility.SetDirty(director);

            return new SuccessResponse($"Bound track '{trackName}' to '{bindTarget}'", new
            {
                trackName, bindTarget, bindType = bindObj.GetType().Name
            });
        }

        private static object GetTimelineInfo(JObject @params, ToolParams p)
        {
            string path = p.Get("path");
            string target = p.Get("target");

            TimelineAsset timeline = null;

            if (!string.IsNullOrEmpty(path))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(path);
                if (!sanitized.EndsWith(".playable", StringComparison.OrdinalIgnoreCase))
                    sanitized += ".playable";
                timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(sanitized);
            }
            else if (!string.IsNullOrEmpty(target))
            {
                GameObject go = GameObject.Find(target);
                if (go != null)
                {
                    PlayableDirector director = go.GetComponent<PlayableDirector>();
                    if (director != null)
                        timeline = director.playableAsset as TimelineAsset;
                }
            }

            if (timeline == null)
                return new ErrorResponse("Timeline not found. Provide 'path' or 'target' with PlayableDirector.");

            var tracks = new System.Collections.Generic.List<object>();
            foreach (var track in timeline.GetOutputTracks())
            {
                var clips = new System.Collections.Generic.List<object>();
                foreach (var clip in track.GetClips())
                {
                    clips.Add(new
                    {
                        name = clip.displayName,
                        start = clip.start,
                        duration = clip.duration,
                        end = clip.end
                    });
                }
                tracks.Add(new
                {
                    name = track.name,
                    type = track.GetType().Name,
                    muted = track.muted,
                    clips
                });
            }

            return new SuccessResponse("Timeline info", new
            {
                duration = timeline.duration,
                outputTrackCount = timeline.outputTrackCount,
                tracks
            });
        }
#endif
    }
}
