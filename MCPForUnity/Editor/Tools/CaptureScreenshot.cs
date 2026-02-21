using System;
using System.IO;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("capture_screenshot", AutoRegister = false)]
    public static class CaptureScreenshot
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
                    case "game_view":
                        return CaptureGameView(@params, p);
                    case "scene_view":
                        return CaptureSceneView(@params, p);
                    case "camera":
                        return CaptureFromCamera(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: game_view, scene_view, camera");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static string GetOutputPath(ToolParams p, string defaultName)
        {
            string path = p.Get("output_path");
            if (string.IsNullOrEmpty(path))
            {
                string dir = "Assets/Screenshots";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                path = $"{dir}/{defaultName}_{timestamp}.png";
            }
            return path;
        }

        private static object CaptureGameView(JObject @params, ToolParams p)
        {
            string outputPath = GetOutputPath(p, "GameView");
            int superSize = p.GetInt("super_size") ?? 1;

            ScreenCapture.CaptureScreenshot(outputPath, superSize);

            return new SuccessResponse($"Captured game view screenshot", new
            {
                path = outputPath,
                superSize = superSize,
                note = "Screenshot will be saved on next frame render. The game must be playing for game view capture."
            });
        }

        private static object CaptureSceneView(JObject @params, ToolParams p)
        {
            string outputPath = GetOutputPath(p, "SceneView");

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return new ErrorResponse("No active Scene View found.");

            int width = p.GetInt("width") ?? (int)sceneView.position.width;
            int height = p.GetInt("height") ?? (int)sceneView.position.height;

            Camera sceneCamera = sceneView.camera;
            if (sceneCamera == null)
                return new ErrorResponse("Scene View camera not available.");

            RenderTexture rt = new RenderTexture(width, height, 24);
            RenderTexture prev = sceneCamera.targetTexture;

            sceneCamera.targetTexture = rt;
            sceneCamera.Render();

            RenderTexture.active = rt;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            sceneCamera.targetTexture = prev;
            RenderTexture.active = null;

            byte[] pngData = screenshot.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(screenshot);

            File.WriteAllBytes(outputPath, pngData);
            AssetDatabase.Refresh();

            return new SuccessResponse($"Captured Scene View screenshot", new
            {
                path = outputPath,
                width = width,
                height = height,
                fileSize = pngData.Length
            });
        }

        private static object CaptureFromCamera(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            string outputPath = GetOutputPath(p, "Camera");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Camera cam = go.GetComponent<Camera>();
            if (cam == null)
                return new ErrorResponse($"No Camera on '{target}'.");

            int width = p.GetInt("width") ?? 1920;
            int height = p.GetInt("height") ?? 1080;

            RenderTexture rt = new RenderTexture(width, height, 24);
            RenderTexture prev = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            cam.targetTexture = prev;
            RenderTexture.active = null;

            byte[] pngData = screenshot.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(screenshot);

            File.WriteAllBytes(outputPath, pngData);
            AssetDatabase.Refresh();

            return new SuccessResponse($"Captured screenshot from camera '{target}'", new
            {
                path = outputPath,
                width = width,
                height = height,
                fileSize = pngData.Length
            });
        }
    }
}
