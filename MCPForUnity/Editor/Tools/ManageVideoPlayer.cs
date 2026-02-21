using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_video_player", AutoRegister = false)]
    public static class ManageVideoPlayer
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
                    case "add":
                        return Add(@params, p);
                    case "configure":
                        return Configure(@params, p);
                    case "set_clip":
                        return SetClip(@params, p);
                    case "set_url":
                        return SetUrl(@params, p);
                    case "play":
                        return Play(@params, p);
                    case "stop":
                        return Stop(@params, p);
                    case "pause":
                        return Pause(@params, p);
                    case "set_render_mode":
                        return SetRenderMode(@params, p);
                    case "get_video_info":
                        return GetVideoInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: add, configure, set_clip, set_url, play, stop, pause, set_render_mode, get_video_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object Add(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            if (go.GetComponent<VideoPlayer>() != null)
                return new ErrorResponse($"'{target}' already has a VideoPlayer.");

            Undo.RecordObject(go, "Add VideoPlayer");
            VideoPlayer player = go.AddComponent<VideoPlayer>();

            player.playOnAwake = p.GetBool("play_on_awake", false);
            player.isLooping = p.GetBool("loop", false);

            // Set source
            string clipPath = p.Get("clip_path");
            if (!string.IsNullOrEmpty(clipPath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(clipPath);
                VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(sanitized);
                if (clip != null)
                {
                    player.source = VideoSource.VideoClip;
                    player.clip = clip;
                }
            }

            string url = p.Get("url");
            if (!string.IsNullOrEmpty(url))
            {
                player.source = VideoSource.Url;
                player.url = url;
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added VideoPlayer to '{target}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                source = player.source.ToString(),
                clip = player.clip != null ? player.clip.name : null,
                url = player.url
            });
        }

        private static object Configure(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            VideoPlayer player = go.GetComponent<VideoPlayer>();
            if (player == null)
                return new ErrorResponse($"No VideoPlayer on '{target}'.");

            Undo.RecordObject(player, "Configure VideoPlayer");

            if (p.Has("loop")) player.isLooping = p.GetBool("loop", player.isLooping);
            if (p.Has("play_on_awake")) player.playOnAwake = p.GetBool("play_on_awake", player.playOnAwake);

            float? playbackSpeed = p.GetFloat("playback_speed");
            if (playbackSpeed.HasValue) player.playbackSpeed = playbackSpeed.Value;

            if (p.Has("skip_on_drop")) player.skipOnDrop = p.GetBool("skip_on_drop", player.skipOnDrop);

            float? volume = p.GetFloat("volume");
            if (volume.HasValue)
            {
                player.SetDirectAudioVolume(0, Mathf.Clamp01(volume.Value));
            }

            EditorUtility.SetDirty(player);

            return new SuccessResponse($"Configured VideoPlayer on '{target}'", new
            {
                loop = player.isLooping,
                playOnAwake = player.playOnAwake,
                playbackSpeed = player.playbackSpeed
            });
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

            VideoPlayer player = go.GetComponent<VideoPlayer>();
            if (player == null)
                return new ErrorResponse($"No VideoPlayer on '{target}'.");

            string sanitized = AssetPathUtility.SanitizeAssetPath(clipPath);
            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(sanitized);
            if (clip == null)
                return new ErrorResponse($"VideoClip not found at '{clipPath}'.");

            Undo.RecordObject(player, "Set VideoClip");
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            EditorUtility.SetDirty(player);

            return new SuccessResponse($"Set clip '{clip.name}' on '{target}'", new
            {
                clip = clip.name,
                width = clip.width,
                height = clip.height,
                length = clip.length,
                frameRate = clip.frameRate
            });
        }

        private static object SetUrl(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var urlResult = p.GetRequired("url");
            var urlError = urlResult.GetOrError(out string url);
            if (urlError != null) return urlError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            VideoPlayer player = go.GetComponent<VideoPlayer>();
            if (player == null)
                return new ErrorResponse($"No VideoPlayer on '{target}'.");

            Undo.RecordObject(player, "Set Video URL");
            player.source = VideoSource.Url;
            player.url = url;
            EditorUtility.SetDirty(player);

            return new SuccessResponse($"Set URL on '{target}'", new { url });
        }

        private static object Play(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            VideoPlayer player = go.GetComponent<VideoPlayer>();
            if (player == null)
                return new ErrorResponse($"No VideoPlayer on '{target}'.");

            player.Play();

            return new SuccessResponse($"Playing video on '{target}'");
        }

        private static object Stop(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            VideoPlayer player = go.GetComponent<VideoPlayer>();
            if (player == null)
                return new ErrorResponse($"No VideoPlayer on '{target}'.");

            player.Stop();

            return new SuccessResponse($"Stopped video on '{target}'");
        }

        private static object Pause(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            VideoPlayer player = go.GetComponent<VideoPlayer>();
            if (player == null)
                return new ErrorResponse($"No VideoPlayer on '{target}'.");

            player.Pause();

            return new SuccessResponse($"Paused video on '{target}'");
        }

        private static object SetRenderMode(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var modeResult = p.GetRequired("render_mode");
            var modeError = modeResult.GetOrError(out string mode);
            if (modeError != null) return modeError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            VideoPlayer player = go.GetComponent<VideoPlayer>();
            if (player == null)
                return new ErrorResponse($"No VideoPlayer on '{target}'.");

            Undo.RecordObject(player, "Set Video Render Mode");

            if (Enum.TryParse<VideoRenderMode>(mode, true, out var renderMode))
            {
                player.renderMode = renderMode;

                // If RenderTexture mode, create one if needed
                if (renderMode == VideoRenderMode.RenderTexture)
                {
                    int width = p.GetInt("width") ?? 1920;
                    int height = p.GetInt("height") ?? 1080;

                    if (player.targetTexture == null)
                    {
                        RenderTexture rt = new RenderTexture(width, height, 0);
                        string rtPath = p.Get("render_texture_path", $"Assets/VideoRT_{go.name}.renderTexture");
                        string sanitized = AssetPathUtility.SanitizeAssetPath(rtPath);
                        AssetDatabase.CreateAsset(rt, sanitized);
                        player.targetTexture = rt;
                        AssetDatabase.SaveAssets();
                    }
                }

                EditorUtility.SetDirty(player);

                return new SuccessResponse($"Set render mode to {renderMode} on '{target}'", new
                {
                    renderMode = player.renderMode.ToString(),
                    targetTexture = player.targetTexture != null ? player.targetTexture.name : null
                });
            }

            return new ErrorResponse($"Invalid render mode: {mode}. Valid: CameraFarPlane, CameraNearPlane, RenderTexture, MaterialOverride, APIOnly");
        }

        private static object GetVideoInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            VideoPlayer player = go.GetComponent<VideoPlayer>();
            if (player == null)
                return new ErrorResponse($"No VideoPlayer on '{target}'.");

            return new SuccessResponse($"VideoPlayer info for '{target}'", new
            {
                name = go.name,
                source = player.source.ToString(),
                clip = player.clip != null ? player.clip.name : null,
                clipPath = player.clip != null ? AssetDatabase.GetAssetPath(player.clip) : null,
                url = player.url,
                isPlaying = player.isPlaying,
                isPaused = player.isPaused,
                isLooping = player.isLooping,
                playOnAwake = player.playOnAwake,
                playbackSpeed = player.playbackSpeed,
                renderMode = player.renderMode.ToString(),
                targetTexture = player.targetTexture != null ? player.targetTexture.name : null,
                time = player.time,
                length = player.length
            });
        }
    }
}
