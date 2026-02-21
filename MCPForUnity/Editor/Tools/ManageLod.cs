using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_lod", AutoRegister = false)]
    public static class ManageLod
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
                    case "add_lod_group":
                        return AddLODGroup(@params, p);
                    case "configure":
                        return Configure(@params, p);
                    case "set_lod_level":
                        return SetLODLevel(@params, p);
                    case "get_lod_info":
                        return GetLODInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: add_lod_group, configure, set_lod_level, get_lod_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object AddLODGroup(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            if (go.GetComponent<LODGroup>() != null)
                return new ErrorResponse($"'{targetName}' already has a LODGroup.");

            Undo.RecordObject(go, "Add LODGroup");
            LODGroup lodGroup = go.AddComponent<LODGroup>();

            // Set default LOD levels if provided
            JToken levelsToken = p.GetRaw("levels");
            if (levelsToken != null && levelsToken.Type == JTokenType.Array)
            {
                var levelsArr = levelsToken as JArray;
                LOD[] lods = new LOD[levelsArr.Count];

                for (int i = 0; i < levelsArr.Count; i++)
                {
                    float threshold = 0.5f;
                    if (levelsArr[i].Type == JTokenType.Float || levelsArr[i].Type == JTokenType.Integer)
                    {
                        threshold = levelsArr[i].ToObject<float>();
                    }
                    else if (levelsArr[i].Type == JTokenType.Object)
                    {
                        threshold = levelsArr[i]["threshold"]?.ToObject<float>() ?? (1f - (float)(i + 1) / (levelsArr.Count + 1));
                    }

                    // Find child renderers for this LOD level
                    string childName = $"LOD{i}";
                    Transform childTransform = go.transform.Find(childName);
                    Renderer[] renderers;
                    if (childTransform != null)
                    {
                        renderers = childTransform.GetComponentsInChildren<Renderer>();
                    }
                    else
                    {
                        renderers = new Renderer[0];
                    }

                    lods[i] = new LOD(threshold, renderers);
                }

                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
            else
            {
                // Default 3-level LOD setup
                int lodCount = p.GetInt("lod_count") ?? 3;
                LOD[] lods = new LOD[lodCount];
                for (int i = 0; i < lodCount; i++)
                {
                    float threshold = 1f - (float)(i + 1) / (lodCount + 1);
                    lods[i] = new LOD(threshold, new Renderer[0]);
                }
                lodGroup.SetLODs(lods);
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added LODGroup to '{targetName}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                lodCount = lodGroup.lodCount
            });
        }

        private static object Configure(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            LODGroup lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup == null)
                return new ErrorResponse($"No LODGroup on '{targetName}'.");

            Undo.RecordObject(lodGroup, "Configure LODGroup");

            string fadeMode = p.Get("fade_mode");
            if (!string.IsNullOrEmpty(fadeMode) && Enum.TryParse<LODFadeMode>(fadeMode, true, out var mode))
                lodGroup.fadeMode = mode;

            bool? animateCrossFading = p.Has("animate_cross_fading") ? (bool?)p.GetBool("animate_cross_fading", lodGroup.animateCrossFading) : null;
            if (animateCrossFading.HasValue)
                lodGroup.animateCrossFading = animateCrossFading.Value;

            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(lodGroup);

            return new SuccessResponse($"Configured LODGroup on '{targetName}'", new
            {
                fadeMode = lodGroup.fadeMode.ToString(),
                animateCrossFading = lodGroup.animateCrossFading,
                lodCount = lodGroup.lodCount
            });
        }

        private static object SetLODLevel(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            int level = p.GetInt("level") ?? 0;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            LODGroup lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup == null)
                return new ErrorResponse($"No LODGroup on '{targetName}'.");

            LOD[] lods = lodGroup.GetLODs();
            if (level < 0 || level >= lods.Length)
                return new ErrorResponse($"LOD level {level} out of range. LODGroup has {lods.Length} levels.");

            Undo.RecordObject(lodGroup, "Set LOD Level");

            float? threshold = p.GetFloat("threshold");
            if (threshold.HasValue)
            {
                lods[level].screenRelativeTransitionHeight = threshold.Value;
                lodGroup.SetLODs(lods);
            }

            // Assign renderers from a child object name
            string rendererSource = p.Get("renderer_source");
            if (!string.IsNullOrEmpty(rendererSource))
            {
                Transform child = go.transform.Find(rendererSource);
                if (child != null)
                {
                    lods[level].renderers = child.GetComponentsInChildren<Renderer>();
                    lodGroup.SetLODs(lods);
                }
            }

            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(lodGroup);

            return new SuccessResponse($"Set LOD level {level} on '{targetName}'", new
            {
                level,
                threshold = lods[level].screenRelativeTransitionHeight,
                rendererCount = lods[level].renderers?.Length ?? 0
            });
        }

        private static object GetLODInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            LODGroup lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup == null)
                return new ErrorResponse($"No LODGroup on '{targetName}'.");

            LOD[] lods = lodGroup.GetLODs();
            var levels = new List<object>();
            for (int i = 0; i < lods.Length; i++)
            {
                levels.Add(new
                {
                    level = i,
                    screenTransitionHeight = lods[i].screenRelativeTransitionHeight,
                    rendererCount = lods[i].renderers?.Length ?? 0,
                    fadeTransitionWidth = lods[i].fadeTransitionWidth
                });
            }

            return new SuccessResponse($"LODGroup info for '{targetName}'", new
            {
                name = go.name,
                lodCount = lodGroup.lodCount,
                fadeMode = lodGroup.fadeMode.ToString(),
                animateCrossFading = lodGroup.animateCrossFading,
                levels
            });
        }
    }
}
