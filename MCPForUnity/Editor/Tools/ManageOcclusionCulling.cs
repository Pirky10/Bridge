using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_occlusion_culling", AutoRegister = false)]
    public static class ManageOcclusionCulling
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
                    case "set_static": return SetOcclusionStatic(@params, p);
                    case "set_area": return SetOcclusionArea(@params, p);
                    case "bake": return Bake(@params, p);
                    case "clear": return Clear(@params, p);
                    case "set_portal": return SetPortal(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: set_static, set_area, bake, clear, set_portal, get_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object SetOcclusionStatic(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            bool occluder = p.GetBool("occluder_static", true);
            bool occludee = p.GetBool("occludee_static", true);

            Undo.RecordObject(go, "Set Occlusion Static");

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);

            if (occluder)
                flags |= StaticEditorFlags.OccluderStatic;
            else
                flags &= ~StaticEditorFlags.OccluderStatic;

            if (occludee)
                flags |= StaticEditorFlags.OccludeeStatic;
            else
                flags &= ~StaticEditorFlags.OccludeeStatic;

            GameObjectUtility.SetStaticEditorFlags(go, flags);

            // Optionally apply to children
            if (p.GetBool("include_children", false))
            {
                foreach (Transform child in go.GetComponentsInChildren<Transform>())
                {
                    if (child.gameObject == go) continue;
                    Undo.RecordObject(child.gameObject, "Set Occlusion Static");
                    StaticEditorFlags childFlags = GameObjectUtility.GetStaticEditorFlags(child.gameObject);
                    if (occluder) childFlags |= StaticEditorFlags.OccluderStatic;
                    else childFlags &= ~StaticEditorFlags.OccluderStatic;
                    if (occludee) childFlags |= StaticEditorFlags.OccludeeStatic;
                    else childFlags &= ~StaticEditorFlags.OccludeeStatic;
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, childFlags);
                }
            }

            return new SuccessResponse($"Set occlusion flags on '{target}'", new
            {
                occluderStatic = occluder,
                occludeeStatic = occludee
            });
        }

        private static object SetOcclusionArea(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "OcclusionArea");

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Occlusion Area");

            OcclusionArea area = go.AddComponent<OcclusionArea>();

            JToken center = p.GetRaw("center");
            if (center != null)
            {
                var c = center.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                    area.center = new Vector3(c[0], c[1], c[2]);
            }

            JToken size = p.GetRaw("size");
            if (size != null)
            {
                var s = size.ToObject<float[]>();
                if (s != null && s.Length >= 3)
                    area.size = new Vector3(s[0], s[1], s[2]);
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Created OcclusionArea '{name}'", new
            {
                name, center = area.center.ToString(), size = area.size.ToString()
            });
        }

        private static object Bake(JObject @params, ToolParams p)
        {
            float? smallestOccluder = p.GetFloat("smallest_occluder");
            float? smallestHole = p.GetFloat("smallest_hole");
            float? backfaceThreshold = p.GetFloat("backface_threshold");

            if (smallestOccluder.HasValue)
                StaticOcclusionCulling.smallestOccluder = smallestOccluder.Value;
            if (smallestHole.HasValue)
                StaticOcclusionCulling.smallestHole = smallestHole.Value;
            if (backfaceThreshold.HasValue)
                StaticOcclusionCulling.backfaceThreshold = backfaceThreshold.Value;

            bool success = StaticOcclusionCulling.GenerateInBackground();

            return new SuccessResponse(success ?
                "Occlusion culling bake started in background." :
                "Bake may have failed. Check console for details.", new
                {
                    smallestOccluder = StaticOcclusionCulling.smallestOccluder,
                    smallestHole = StaticOcclusionCulling.smallestHole,
                    backfaceThreshold = StaticOcclusionCulling.backfaceThreshold
                });
        }

        private static object Clear(JObject @params, ToolParams p)
        {
            StaticOcclusionCulling.Clear();
            return new SuccessResponse("Cleared occlusion culling data.");
        }

        private static object SetPortal(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Set Occlusion Portal");
            OcclusionPortal portal = go.GetComponent<OcclusionPortal>();
            if (portal == null)
                portal = go.AddComponent<OcclusionPortal>();

            if (p.Has("open")) portal.open = p.GetBool("open", true);

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Set OcclusionPortal on '{target}'", new
            {
                open = portal.open
            });
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            return new SuccessResponse("Occlusion culling info", new
            {
                isComputing = StaticOcclusionCulling.isRunning,
                hasData = StaticOcclusionCulling.umbraDataSize > 0,
                dataSize = StaticOcclusionCulling.umbraDataSize,
                smallestOccluder = StaticOcclusionCulling.smallestOccluder,
                smallestHole = StaticOcclusionCulling.smallestHole,
                backfaceThreshold = StaticOcclusionCulling.backfaceThreshold
            });
        }
    }
}
