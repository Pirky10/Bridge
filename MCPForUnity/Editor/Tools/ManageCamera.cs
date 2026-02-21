using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_camera", AutoRegister = false)]
    public static class ManageCamera
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
                        return CreateCamera(@params, p);
                    case "configure":
                        return ConfigureCamera(@params, p);
                    case "set_clear_flags":
                        return SetClearFlags(@params, p);
                    case "set_culling_mask":
                        return SetCullingMask(@params, p);
                    case "set_viewport":
                        return SetViewport(@params, p);
                    case "get_camera_info":
                        return GetCameraInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: create, configure, set_clear_flags, set_culling_mask, set_viewport, get_camera_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object CreateCamera(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "Camera");

            GameObject camGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");

            Camera cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            // Position
            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    camGo.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            // Rotation
            JToken rotToken = p.GetRaw("rotation");
            if (rotToken != null)
            {
                var rot = rotToken.ToObject<float[]>();
                if (rot != null && rot.Length >= 3)
                    camGo.transform.eulerAngles = new Vector3(rot[0], rot[1], rot[2]);
            }

            // Field of view
            float? fov = p.GetFloat("field_of_view");
            if (fov.HasValue) cam.fieldOfView = fov.Value;

            // Near/far clip
            float? nearClip = p.GetFloat("near_clip");
            float? farClip = p.GetFloat("far_clip");
            if (nearClip.HasValue) cam.nearClipPlane = nearClip.Value;
            if (farClip.HasValue) cam.farClipPlane = farClip.Value;

            // Orthographic
            bool orthographic = p.GetBool("orthographic", false);
            cam.orthographic = orthographic;
            if (orthographic)
            {
                float? orthoSize = p.GetFloat("orthographic_size");
                if (orthoSize.HasValue) cam.orthographicSize = orthoSize.Value;
            }

            // Depth
            float? depth = p.GetFloat("depth");
            if (depth.HasValue) cam.depth = depth.Value;

            return new SuccessResponse($"Created Camera '{name}'", new
            {
                name = camGo.name,
                instanceId = camGo.GetInstanceID(),
                fieldOfView = cam.fieldOfView,
                nearClipPlane = cam.nearClipPlane,
                farClipPlane = cam.farClipPlane,
                orthographic = cam.orthographic,
                depth = cam.depth
            });
        }

        private static object ConfigureCamera(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Camera cam = go.GetComponent<Camera>();
            if (cam == null)
                return new ErrorResponse($"No Camera on '{target}'.");

            Undo.RecordObject(cam, "Configure Camera");

            float? fov = p.GetFloat("field_of_view");
            if (fov.HasValue) cam.fieldOfView = fov.Value;

            float? nearClip = p.GetFloat("near_clip");
            float? farClip = p.GetFloat("far_clip");
            if (nearClip.HasValue) cam.nearClipPlane = nearClip.Value;
            if (farClip.HasValue) cam.farClipPlane = farClip.Value;

            if (p.Has("orthographic"))
            {
                cam.orthographic = p.GetBool("orthographic", cam.orthographic);
            }

            float? orthoSize = p.GetFloat("orthographic_size");
            if (orthoSize.HasValue) cam.orthographicSize = orthoSize.Value;

            float? depth = p.GetFloat("depth");
            if (depth.HasValue) cam.depth = depth.Value;

            JToken bgToken = p.GetRaw("background_color");
            if (bgToken != null)
            {
                var bg = bgToken.ToObject<float[]>();
                if (bg != null && bg.Length >= 3)
                {
                    float a = bg.Length >= 4 ? bg[3] : 1f;
                    cam.backgroundColor = new Color(bg[0], bg[1], bg[2], a);
                }
            }

            EditorUtility.SetDirty(cam);

            return new SuccessResponse($"Configured Camera on '{target}'", new
            {
                fieldOfView = cam.fieldOfView,
                nearClipPlane = cam.nearClipPlane,
                farClipPlane = cam.farClipPlane,
                orthographic = cam.orthographic,
                depth = cam.depth
            });
        }

        private static object SetClearFlags(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            string flags = p.Get("flags", "Skybox");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Camera cam = go.GetComponent<Camera>();
            if (cam == null)
                return new ErrorResponse($"No Camera on '{target}'.");

            Undo.RecordObject(cam, "Set Camera Clear Flags");

            if (Enum.TryParse<CameraClearFlags>(flags, true, out var clearFlags))
            {
                cam.clearFlags = clearFlags;
                EditorUtility.SetDirty(cam);
                return new SuccessResponse($"Set clear flags to {clearFlags} on '{target}'");
            }

            return new ErrorResponse($"Invalid clear flags: {flags}. Valid: Skybox, SolidColor, Depth, Nothing");
        }

        private static object SetCullingMask(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Camera cam = go.GetComponent<Camera>();
            if (cam == null)
                return new ErrorResponse($"No Camera on '{target}'.");

            Undo.RecordObject(cam, "Set Camera Culling Mask");

            JToken layersToken = p.GetRaw("layers");
            if (layersToken != null)
            {
                var layerNames = layersToken.ToObject<string[]>();
                if (layerNames != null)
                {
                    int mask = 0;
                    foreach (var layerName in layerNames)
                    {
                        int layer = LayerMask.NameToLayer(layerName);
                        if (layer >= 0)
                            mask |= (1 << layer);
                    }
                    cam.cullingMask = mask;
                    EditorUtility.SetDirty(cam);
                    return new SuccessResponse($"Set culling mask on '{target}'", new { cullingMask = cam.cullingMask });
                }
            }

            // Accept raw integer mask
            int? maskInt = p.GetInt("mask");
            if (maskInt.HasValue)
            {
                cam.cullingMask = maskInt.Value;
                EditorUtility.SetDirty(cam);
                return new SuccessResponse($"Set culling mask on '{target}'", new { cullingMask = cam.cullingMask });
            }

            return new ErrorResponse("Provide 'layers' (array of layer names) or 'mask' (integer bitmask).");
        }

        private static object SetViewport(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Camera cam = go.GetComponent<Camera>();
            if (cam == null)
                return new ErrorResponse($"No Camera on '{target}'.");

            JToken viewportToken = p.GetRaw("viewport");
            if (viewportToken == null)
                return new ErrorResponse("'viewport' parameter is required as [x, y, width, height] array.");

            var vp = viewportToken.ToObject<float[]>();
            if (vp == null || vp.Length < 4)
                return new ErrorResponse("'viewport' must be [x, y, width, height] (normalized 0-1).");

            Undo.RecordObject(cam, "Set Camera Viewport");
            cam.rect = new Rect(vp[0], vp[1], vp[2], vp[3]);
            EditorUtility.SetDirty(cam);

            return new SuccessResponse($"Set viewport on '{target}'", new
            {
                viewport = new { x = cam.rect.x, y = cam.rect.y, width = cam.rect.width, height = cam.rect.height }
            });
        }

        private static object GetCameraInfo(JObject @params, ToolParams p)
        {
            string target = p.Get("target");

            if (!string.IsNullOrEmpty(target))
            {
                GameObject go = GameObject.Find(target);
                if (go == null)
                    return new ErrorResponse($"GameObject '{target}' not found.");

                Camera cam = go.GetComponent<Camera>();
                if (cam == null)
                    return new ErrorResponse($"No Camera on '{target}'.");

                return new SuccessResponse($"Camera info for '{target}'", new
                {
                    name = go.name,
                    instanceId = go.GetInstanceID(),
                    fieldOfView = cam.fieldOfView,
                    nearClipPlane = cam.nearClipPlane,
                    farClipPlane = cam.farClipPlane,
                    orthographic = cam.orthographic,
                    orthographicSize = cam.orthographicSize,
                    depth = cam.depth,
                    clearFlags = cam.clearFlags.ToString(),
                    cullingMask = cam.cullingMask,
                    backgroundColor = new { r = cam.backgroundColor.r, g = cam.backgroundColor.g, b = cam.backgroundColor.b, a = cam.backgroundColor.a },
                    viewport = new { x = cam.rect.x, y = cam.rect.y, width = cam.rect.width, height = cam.rect.height },
                    position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                    rotation = new { x = go.transform.eulerAngles.x, y = go.transform.eulerAngles.y, z = go.transform.eulerAngles.z }
                });
            }

            // List all cameras
            var cameras = Camera.allCameras;
            var cameraList = new List<object>();
            foreach (var cam in cameras)
            {
                cameraList.Add(new
                {
                    name = cam.gameObject.name,
                    instanceId = cam.gameObject.GetInstanceID(),
                    depth = cam.depth,
                    fieldOfView = cam.fieldOfView,
                    orthographic = cam.orthographic
                });
            }

            return new SuccessResponse($"Found {cameras.Length} cameras", new { cameras = cameraList });
        }
    }
}
