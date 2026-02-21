using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_layout", AutoRegister = false)]
    public static class ManageLayout
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
                    case "add_horizontal": return AddLayoutGroup(@params, p, "Horizontal");
                    case "add_vertical": return AddLayoutGroup(@params, p, "Vertical");
                    case "add_grid": return AddGridLayout(@params, p);
                    case "add_content_size_fitter": return AddContentSizeFitter(@params, p);
                    case "add_layout_element": return AddLayoutElement(@params, p);
                    case "configure": return Configure(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: add_horizontal, add_vertical, add_grid, add_content_size_fitter, add_layout_element, configure, get_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object AddLayoutGroup(JObject @params, ToolParams p, string type)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, $"Add {type}LayoutGroup");

            HorizontalOrVerticalLayoutGroup lg;
            if (type == "Horizontal")
                lg = go.AddComponent<HorizontalLayoutGroup>();
            else
                lg = go.AddComponent<VerticalLayoutGroup>();

            float? spacing = p.GetFloat("spacing");
            if (spacing.HasValue) lg.spacing = spacing.Value;

            if (p.Has("child_force_expand_width")) lg.childForceExpandWidth = p.GetBool("child_force_expand_width", true);
            if (p.Has("child_force_expand_height")) lg.childForceExpandHeight = p.GetBool("child_force_expand_height", true);
            if (p.Has("child_control_width")) lg.childControlWidth = p.GetBool("child_control_width", true);
            if (p.Has("child_control_height")) lg.childControlHeight = p.GetBool("child_control_height", true);
            if (p.Has("child_scale_width")) lg.childScaleWidth = p.GetBool("child_scale_width", false);
            if (p.Has("child_scale_height")) lg.childScaleHeight = p.GetBool("child_scale_height", false);

            // Padding
            int? padLeft = p.GetInt("padding_left");
            int? padRight = p.GetInt("padding_right");
            int? padTop = p.GetInt("padding_top");
            int? padBottom = p.GetInt("padding_bottom");
            int? padAll = p.GetInt("padding");

            if (padAll.HasValue)
                lg.padding = new RectOffset(padAll.Value, padAll.Value, padAll.Value, padAll.Value);
            else if (padLeft.HasValue || padRight.HasValue || padTop.HasValue || padBottom.HasValue)
                lg.padding = new RectOffset(
                    padLeft ?? 0, padRight ?? 0, padTop ?? 0, padBottom ?? 0);

            string alignment = p.Get("child_alignment");
            if (!string.IsNullOrEmpty(alignment) && Enum.TryParse<TextAnchor>(alignment, true, out var anchor))
                lg.childAlignment = anchor;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added {type}LayoutGroup to '{target}'");
        }

        private static object AddGridLayout(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add GridLayoutGroup");
            GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();

            JToken cellSize = p.GetRaw("cell_size");
            if (cellSize != null)
            {
                var cs = cellSize.ToObject<float[]>();
                if (cs != null && cs.Length >= 2)
                    grid.cellSize = new Vector2(cs[0], cs[1]);
            }

            JToken spacingToken = p.GetRaw("spacing");
            if (spacingToken != null)
            {
                if (spacingToken.Type == JTokenType.Array)
                {
                    var s = spacingToken.ToObject<float[]>();
                    if (s != null && s.Length >= 2)
                        grid.spacing = new Vector2(s[0], s[1]);
                }
                else
                {
                    float sv = spacingToken.Value<float>();
                    grid.spacing = new Vector2(sv, sv);
                }
            }

            string startCorner = p.Get("start_corner");
            if (!string.IsNullOrEmpty(startCorner) && Enum.TryParse<GridLayoutGroup.Corner>(startCorner, true, out var corner))
                grid.startCorner = corner;

            string startAxis = p.Get("start_axis");
            if (!string.IsNullOrEmpty(startAxis) && Enum.TryParse<GridLayoutGroup.Axis>(startAxis, true, out var axis))
                grid.startAxis = axis;

            string constraint = p.Get("constraint");
            if (!string.IsNullOrEmpty(constraint) && Enum.TryParse<GridLayoutGroup.Constraint>(constraint, true, out var c))
                grid.constraint = c;

            int? constraintCount = p.GetInt("constraint_count");
            if (constraintCount.HasValue) grid.constraintCount = constraintCount.Value;

            string alignment = p.Get("child_alignment");
            if (!string.IsNullOrEmpty(alignment) && Enum.TryParse<TextAnchor>(alignment, true, out var anchor))
                grid.childAlignment = anchor;

            int? padAll = p.GetInt("padding");
            if (padAll.HasValue)
                grid.padding = new RectOffset(padAll.Value, padAll.Value, padAll.Value, padAll.Value);

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added GridLayoutGroup to '{target}'");
        }

        private static object AddContentSizeFitter(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add ContentSizeFitter");
            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();

            string hFit = p.Get("horizontal_fit");
            if (!string.IsNullOrEmpty(hFit) && Enum.TryParse<ContentSizeFitter.FitMode>(hFit, true, out var hMode))
                fitter.horizontalFit = hMode;

            string vFit = p.Get("vertical_fit");
            if (!string.IsNullOrEmpty(vFit) && Enum.TryParse<ContentSizeFitter.FitMode>(vFit, true, out var vMode))
                fitter.verticalFit = vMode;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added ContentSizeFitter to '{target}'");
        }

        private static object AddLayoutElement(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add LayoutElement");
            LayoutElement elem = go.AddComponent<LayoutElement>();

            float? minWidth = p.GetFloat("min_width");
            if (minWidth.HasValue) elem.minWidth = minWidth.Value;

            float? minHeight = p.GetFloat("min_height");
            if (minHeight.HasValue) elem.minHeight = minHeight.Value;

            float? preferredWidth = p.GetFloat("preferred_width");
            if (preferredWidth.HasValue) elem.preferredWidth = preferredWidth.Value;

            float? preferredHeight = p.GetFloat("preferred_height");
            if (preferredHeight.HasValue) elem.preferredHeight = preferredHeight.Value;

            float? flexibleWidth = p.GetFloat("flexible_width");
            if (flexibleWidth.HasValue) elem.flexibleWidth = flexibleWidth.Value;

            float? flexibleHeight = p.GetFloat("flexible_height");
            if (flexibleHeight.HasValue) elem.flexibleHeight = flexibleHeight.Value;

            if (p.Has("ignore_layout")) elem.ignoreLayout = p.GetBool("ignore_layout", false);
            
            int? layoutPriority = p.GetInt("layout_priority");
            if (layoutPriority.HasValue) elem.layoutPriority = layoutPriority.Value;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added LayoutElement to '{target}'");
        }

        private static object Configure(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            // Try to find any layout component
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            var glg = go.GetComponent<GridLayoutGroup>();

            if (hlg == null && vlg == null && glg == null)
                return new ErrorResponse($"No layout component on '{target}'.");

            HorizontalOrVerticalLayoutGroup hovlg = hlg != null ? (HorizontalOrVerticalLayoutGroup)hlg : vlg;

            if (hovlg != null)
            {
                Undo.RecordObject(hovlg, "Configure Layout");

                float? spacing = p.GetFloat("spacing");
                if (spacing.HasValue) hovlg.spacing = spacing.Value;

                if (p.Has("child_force_expand_width")) hovlg.childForceExpandWidth = p.GetBool("child_force_expand_width", true);
                if (p.Has("child_force_expand_height")) hovlg.childForceExpandHeight = p.GetBool("child_force_expand_height", true);

                string alignment = p.Get("child_alignment");
                if (!string.IsNullOrEmpty(alignment) && Enum.TryParse<TextAnchor>(alignment, true, out var anchor))
                    hovlg.childAlignment = anchor;

                EditorUtility.SetDirty(hovlg);
            }
            else if (glg != null)
            {
                Undo.RecordObject(glg, "Configure Grid Layout");

                JToken cellSize = p.GetRaw("cell_size");
                if (cellSize != null)
                {
                    var cs = cellSize.ToObject<float[]>();
                    if (cs != null && cs.Length >= 2)
                        glg.cellSize = new Vector2(cs[0], cs[1]);
                }

                string constraint = p.Get("constraint");
                if (!string.IsNullOrEmpty(constraint) && Enum.TryParse<GridLayoutGroup.Constraint>(constraint, true, out var c))
                    glg.constraint = c;

                int? constraintCount = p.GetInt("constraint_count");
                if (constraintCount.HasValue) glg.constraintCount = constraintCount.Value;

                EditorUtility.SetDirty(glg);
            }

            return new SuccessResponse($"Configured layout on '{target}'");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            var info = new System.Collections.Generic.Dictionary<string, object> { { "name", go.name } };

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            var glg = go.GetComponent<GridLayoutGroup>();
            var csf = go.GetComponent<ContentSizeFitter>();
            var le = go.GetComponent<LayoutElement>();

            if (hlg != null) info["horizontalLayoutGroup"] = new { spacing = hlg.spacing, childAlignment = hlg.childAlignment.ToString() };
            if (vlg != null) info["verticalLayoutGroup"] = new { spacing = vlg.spacing, childAlignment = vlg.childAlignment.ToString() };
            if (glg != null) info["gridLayoutGroup"] = new { cellSize = new[] { glg.cellSize.x, glg.cellSize.y }, spacing = new[] { glg.spacing.x, glg.spacing.y }, constraint = glg.constraint.ToString() };
            if (csf != null) info["contentSizeFitter"] = new { horizontalFit = csf.horizontalFit.ToString(), verticalFit = csf.verticalFit.ToString() };
            if (le != null) info["layoutElement"] = new { minWidth = le.minWidth, minHeight = le.minHeight, preferredWidth = le.preferredWidth, preferredHeight = le.preferredHeight };

            if (hlg == null && vlg == null && glg == null && csf == null && le == null)
                return new ErrorResponse($"No layout components on '{target}'.");

            return new SuccessResponse("Layout info", info);
        }
    }
}
