using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_scene_view")]
    public static class ManageSceneView
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' is required.");

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null && action != "get_scene_view_info")
            {
                return new ErrorResponse("No active Scene View found. Open a Scene View window first.");
            }

            try
            {
                switch (action)
                {
                    case "frame_selection": return FrameSelection(sceneView, @params);
                    case "look_at": return LookAt(sceneView, @params);
                    case "set_camera_position": return SetCameraPosition(sceneView, @params);
                    case "set_2d_mode": return Set2DMode(sceneView, @params);
                    case "set_scene_lighting": return SetSceneLighting(sceneView, @params);
                    case "set_draw_mode": return SetDrawMode(sceneView, @params);
                    case "set_orthographic": return SetOrthographic(sceneView, @params);
                    case "get_scene_view_info": return GetSceneViewInfo();
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageSceneView error: {e.Message}");
            }
        }

        private static object FrameSelection(SceneView sv, JObject @params)
        {
            string target = @params["target"]?.ToString();
            if (!string.IsNullOrEmpty(target))
            {
                var go = GameObjectLookup.FindByTarget(new JValue(target), "by_id_or_name_or_path");
                if (go == null)
                    return new ErrorResponse($"GameObject '{target}' not found.");
                Selection.activeGameObject = go;
            }

            if (Selection.activeGameObject == null)
                return new ErrorResponse("No object selected to frame.");

            sv.FrameSelected();
            return new SuccessResponse($"Framed '{Selection.activeGameObject.name}' in Scene View.");
        }

        private static object LookAt(SceneView sv, JObject @params)
        {
            string target = @params["target"]?.ToString();
            float size = @params["size"]?.ToObject<float>() ?? sv.size;

            Vector3 point;
            if (!string.IsNullOrEmpty(target))
            {
                var go = GameObjectLookup.FindByTarget(new JValue(target), "by_id_or_name_or_path");
                if (go == null)
                    return new ErrorResponse($"GameObject '{target}' not found.");
                point = go.transform.position;
            }
            else
            {
                var pos = @params["position"];
                if (pos == null)
                    return new ErrorResponse("'target' or 'position' is required.");
                point = new Vector3(
                    pos[0]?.ToObject<float>() ?? 0f,
                    pos[1]?.ToObject<float>() ?? 0f,
                    pos[2]?.ToObject<float>() ?? 0f
                );
            }

            sv.LookAt(point, sv.rotation, size);
            sv.Repaint();
            return new SuccessResponse($"Scene View looking at ({point.x:F1}, {point.y:F1}, {point.z:F1}).");
        }

        private static object SetCameraPosition(SceneView sv, JObject @params)
        {
            var pos = @params["position"];
            var rot = @params["rotation"];
            float? size = @params["size"]?.ToObject<float>();

            if (pos != null)
            {
                sv.pivot = new Vector3(
                    pos[0]?.ToObject<float>() ?? 0f,
                    pos[1]?.ToObject<float>() ?? 0f,
                    pos[2]?.ToObject<float>() ?? 0f
                );
            }

            if (rot != null)
            {
                sv.rotation = Quaternion.Euler(
                    rot[0]?.ToObject<float>() ?? 0f,
                    rot[1]?.ToObject<float>() ?? 0f,
                    rot[2]?.ToObject<float>() ?? 0f
                );
            }

            if (size.HasValue)
                sv.size = size.Value;

            sv.Repaint();
            return new SuccessResponse("Scene View camera updated.", new
            {
                pivot = new float[] { sv.pivot.x, sv.pivot.y, sv.pivot.z },
                rotation = new float[] { sv.rotation.eulerAngles.x, sv.rotation.eulerAngles.y, sv.rotation.eulerAngles.z },
                size = sv.size
            });
        }

        private static object Set2DMode(SceneView sv, JObject @params)
        {
            bool enabled = @params["enabled"]?.ToObject<bool>() ?? !sv.in2DMode;
            sv.in2DMode = enabled;
            sv.Repaint();
            return new SuccessResponse($"2D mode {(enabled ? "enabled" : "disabled")}.");
        }

        private static object SetSceneLighting(SceneView sv, JObject @params)
        {
            bool enabled = @params["enabled"]?.ToObject<bool>() ?? !sv.sceneLighting;
            sv.sceneLighting = enabled;
            sv.Repaint();
            return new SuccessResponse($"Scene lighting {(enabled ? "enabled" : "disabled")}.");
        }

        private static object SetDrawMode(SceneView sv, JObject @params)
        {
            string mode = @params["draw_mode"]?.ToString();
            if (string.IsNullOrEmpty(mode))
                return new ErrorResponse("'draw_mode' is required.");

            DrawCameraMode cameraMode;
            switch (mode.ToLowerInvariant())
            {
                case "textured": cameraMode = DrawCameraMode.Textured; break;
                case "wireframe": cameraMode = DrawCameraMode.Wireframe; break;
                case "texturedwire": cameraMode = DrawCameraMode.TexturedWire; break;
                case "shadowcascades": cameraMode = DrawCameraMode.ShadowCascades; break;
                case "overdraw": cameraMode = DrawCameraMode.Overdraw; break;
                default:
                    if (!Enum.TryParse(mode, true, out cameraMode))
                        return new ErrorResponse($"Unknown draw mode: '{mode}'.");
                    break;
            }

            sv.cameraMode = SceneView.GetBuiltinCameraMode(cameraMode);
            sv.Repaint();
            return new SuccessResponse($"Draw mode set to '{cameraMode}'.");
        }

        private static object SetOrthographic(SceneView sv, JObject @params)
        {
            bool ortho = @params["orthographic"]?.ToObject<bool>() ?? !sv.orthographic;
            sv.orthographic = ortho;
            sv.Repaint();
            return new SuccessResponse($"Orthographic mode {(ortho ? "enabled" : "disabled")}.");
        }

        private static object GetSceneViewInfo()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return new SuccessResponse("No active Scene View.", new { available = false });

            return new SuccessResponse("Scene View info retrieved.", new
            {
                available = true,
                pivot = new float[] { sv.pivot.x, sv.pivot.y, sv.pivot.z },
                rotation = new float[] { sv.rotation.eulerAngles.x, sv.rotation.eulerAngles.y, sv.rotation.eulerAngles.z },
                size = sv.size,
                orthographic = sv.orthographic,
                in2DMode = sv.in2DMode,
                sceneLighting = sv.sceneLighting,
                drawMode = sv.cameraMode.drawMode.ToString()
            });
        }
    }
}
