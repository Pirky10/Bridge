using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_canvas_group", AutoRegister = false)]
    public static class ManageCanvasGroup
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
                    case "add": return Add(@params, p);
                    case "configure": return Configure(@params, p);
                    case "remove": return Remove(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: add, configure, remove, get_info");
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
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            if (go.GetComponent<CanvasGroup>() != null)
                return new ErrorResponse($"'{target}' already has a CanvasGroup.");

            Undo.RecordObject(go, "Add CanvasGroup");
            CanvasGroup cg = go.AddComponent<CanvasGroup>();

            float? alpha = p.GetFloat("alpha");
            if (alpha.HasValue) cg.alpha = alpha.Value;

            if (p.Has("interactable")) cg.interactable = p.GetBool("interactable", true);
            if (p.Has("blocks_raycasts")) cg.blocksRaycasts = p.GetBool("blocks_raycasts", true);
            if (p.Has("ignore_parent_groups")) cg.ignoreParentGroups = p.GetBool("ignore_parent_groups", false);

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added CanvasGroup to '{target}'", new
            {
                alpha = cg.alpha,
                interactable = cg.interactable,
                blocksRaycasts = cg.blocksRaycasts
            });
        }

        private static object Configure(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) return new ErrorResponse($"No CanvasGroup on '{target}'.");

            Undo.RecordObject(cg, "Configure CanvasGroup");

            float? alpha = p.GetFloat("alpha");
            if (alpha.HasValue) cg.alpha = alpha.Value;

            if (p.Has("interactable")) cg.interactable = p.GetBool("interactable", cg.interactable);
            if (p.Has("blocks_raycasts")) cg.blocksRaycasts = p.GetBool("blocks_raycasts", cg.blocksRaycasts);
            if (p.Has("ignore_parent_groups")) cg.ignoreParentGroups = p.GetBool("ignore_parent_groups", cg.ignoreParentGroups);

            EditorUtility.SetDirty(cg);

            return new SuccessResponse($"Configured CanvasGroup on '{target}'", new
            {
                alpha = cg.alpha,
                interactable = cg.interactable,
                blocksRaycasts = cg.blocksRaycasts
            });
        }

        private static object Remove(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) return new ErrorResponse($"No CanvasGroup on '{target}'.");

            Undo.DestroyObjectImmediate(cg);
            return new SuccessResponse($"Removed CanvasGroup from '{target}'");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) return new ErrorResponse($"No CanvasGroup on '{target}'.");

            return new SuccessResponse("CanvasGroup info", new
            {
                name = go.name,
                alpha = cg.alpha,
                interactable = cg.interactable,
                blocksRaycasts = cg.blocksRaycasts,
                ignoreParentGroups = cg.ignoreParentGroups
            });
        }
    }
}
