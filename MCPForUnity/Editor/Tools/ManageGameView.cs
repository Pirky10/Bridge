using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_game_view")]
    public static class ManageGameView
    {
        // Cache the internal GameView type — it's internal so reflection is required
        private static readonly Type GameViewType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' is required.");

            try
            {
                switch (action)
                {
                    case "set_resolution": return SetResolution(@params);
                    case "set_scale": return SetScale(@params);
                    case "toggle_maximize_on_play": return ToggleMaximizeOnPlay(@params);
                    case "toggle_mute_audio": return ToggleMuteAudio(@params);
                    case "get_game_view_info": return GetGameViewInfo(@params);
                    case "list_resolutions": return ListResolutions(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageGameView error: {e.Message}");
            }
        }

        private static EditorWindow GetGameView()
        {
            if (GameViewType == null) return null;
            return EditorWindow.GetWindow(GameViewType, false, null, false);
        }

        private static object SetResolution(JObject @params)
        {
            var gameView = GetGameView();
            if (gameView == null) return new ErrorResponse("Could not open Game View window.");

            int sizeIndex = @params["size_index"]?.ToObject<int>() ?? -1;

            if (sizeIndex < 0)
                return new ErrorResponse("'size_index' is required. Use 'list_resolutions' to see available indices.");

            // GameView stores the selected resolution in its internal selectedSizeIndex property
            var prop = GameViewType.GetProperty(
                "selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

            if (prop == null)
                return new ErrorResponse("Could not find selectedSizeIndex on GameView (Unity version may differ).");

            prop.SetValue(gameView, sizeIndex);
            gameView.Repaint();

            return new SuccessResponse($"Game View resolution set to index {sizeIndex}.", new
            {
                sizeIndex
            });
        }

        private static object SetScale(JObject @params)
        {
            var gameView = GetGameView();
            if (gameView == null) return new ErrorResponse("Could not open Game View window.");

            float scale = @params["scale"]?.ToObject<float>() ?? 1f;

            // GameView uses an internal ZoomableArea (m_ZoomArea). The scale is set via m_defaultScale
            // or by directly setting the area.scale via reflection.
            var zoomAreaField = GameViewType.GetField(
                "m_ZoomArea", BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (zoomAreaField != null)
            {
                object zoomArea = zoomAreaField.GetValue(gameView);
                if (zoomArea != null)
                {
                    var scaleProperty = zoomArea.GetType().GetProperty(
                        "scale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    );
                    if (scaleProperty != null)
                    {
                        scaleProperty.SetValue(zoomArea, new Vector2(scale, scale));
                        gameView.Repaint();
                        return new SuccessResponse($"Game View scale set to {scale}.", new { scale });
                    }
                }
            }

            // Fallback: try the newer m_defaultScale field
            var defaultScaleField = GameViewType.GetField(
                "m_defaultScale", BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (defaultScaleField != null)
            {
                defaultScaleField.SetValue(gameView, scale);
                gameView.Repaint();
                return new SuccessResponse($"Game View default scale set to {scale}.", new { scale });
            }

            return new ErrorResponse("Could not find zoom/scale fields on GameView via reflection.");
        }

        private static object ToggleMaximizeOnPlay(JObject @params)
        {
            var gameView = GetGameView();
            if (gameView == null) return new ErrorResponse("Could not open Game View window.");

            // maximizeOnPlay is a public property on GameView
            var prop = GameViewType.GetProperty(
                "maximizeOnPlay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (prop == null)
                return new ErrorResponse("Could not find maximizeOnPlay property on GameView.");

            bool current = (bool)prop.GetValue(gameView);
            bool enabled = @params["enabled"]?.ToObject<bool>() ?? !current;

            prop.SetValue(gameView, enabled);
            gameView.Repaint();

            return new SuccessResponse(
                $"Maximize on Play is now {(enabled ? "enabled" : "disabled")}.",
                new { maximizeOnPlay = enabled }
            );
        }

        private static object ToggleMuteAudio(JObject @params)
        {
            bool current = EditorUtility.audioMasterMute;
            bool enabled = @params["enabled"]?.ToObject<bool>() ?? !current;

            EditorUtility.audioMasterMute = enabled;

            return new SuccessResponse(
                $"Audio mute is now {(enabled ? "on" : "off")}.",
                new { audioMuted = enabled }
            );
        }

        private static object GetGameViewInfo(JObject @params)
        {
            var gameView = GetGameView();
            if (gameView == null) return new ErrorResponse("Could not open Game View window.");

            bool maximizeOnPlay = false;
            int selectedSizeIndex = -1;

            var maxProp = GameViewType.GetProperty(
                "maximizeOnPlay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (maxProp != null) maximizeOnPlay = (bool)maxProp.GetValue(gameView);

            var sizeProp = GameViewType.GetProperty(
                "selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (sizeProp != null) selectedSizeIndex = (int)sizeProp.GetValue(gameView);

            return new SuccessResponse("Game View info.", new
            {
                maximizeOnPlay,
                selectedSizeIndex,
                audioMuted = EditorUtility.audioMasterMute,
                position = new
                {
                    x = gameView.position.x,
                    y = gameView.position.y,
                    width = gameView.position.width,
                    height = gameView.position.height
                }
            });
        }

        private static object ListResolutions(JObject @params)
        {
            // GameViewSizes are managed through an internal GameViewSizes singleton
            var sizesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            if (sizesType == null)
                return new ErrorResponse("Could not find GameViewSizes type.");

            var instanceProp = sizesType.GetProperty("instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (instanceProp == null)
                return new ErrorResponse("Could not find GameViewSizes.instance.");

            var instance = instanceProp.GetValue(null);
            var currentGroupMethod = sizesType.GetMethod("GetGroup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (currentGroupMethod == null)
                return new ErrorResponse("Could not find GetGroup method.");

            // Get the current build target group
            var currentGroupType = (int)GameViewSizeGroupType.Standalone;
#if UNITY_ANDROID
            currentGroupType = (int)GameViewSizeGroupType.Android;
#elif UNITY_IOS
            currentGroupType = (int)GameViewSizeGroupType.iOS;
#endif
            var group = currentGroupMethod.Invoke(instance, new object[] { (GameViewSizeGroupType)currentGroupType });
            if (group == null)
                return new ErrorResponse("Could not retrieve resolution group.");

            var getTotalCountMethod = group.GetType().GetMethod("GetTotalCount",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var getGameViewSizeMethod = group.GetType().GetMethod("GetGameViewSize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (getTotalCountMethod == null || getGameViewSizeMethod == null)
                return new ErrorResponse("Could not find size enumeration methods.");

            int totalCount = (int)getTotalCountMethod.Invoke(group, null);
            var resolutions = new List<object>();

            for (int i = 0; i < totalCount; i++)
            {
                var size = getGameViewSizeMethod.Invoke(group, new object[] { i });
                if (size == null) continue;

                var sizeType = size.GetType();
                var widthProp = sizeType.GetProperty("width",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var heightProp = sizeType.GetProperty("height",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var baseTextProp = sizeType.GetProperty("baseText",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                resolutions.Add(new
                {
                    index = i,
                    name = baseTextProp?.GetValue(size)?.ToString() ?? $"Size {i}",
                    width = widthProp?.GetValue(size),
                    height = heightProp?.GetValue(size)
                });
            }

            return new SuccessResponse("Available Game View resolutions.", new { resolutions });
        }
    }
}
