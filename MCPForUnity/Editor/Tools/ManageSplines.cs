using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Helpers;
#if UNITY_SPLINES
using UnityEngine.Splines;
using Unity.Mathematics;
#endif

namespace MCPForUnity.Editor.Tools
{
#if UNITY_SPLINES
    [McpForUnityTool("manage_splines")]
#endif
    public static class ManageSplines
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' is required.");

#if !UNITY_SPLINES
            return new ErrorResponse(
                "Splines package (com.unity.splines) is not installed. " +
                "Install it via Package Manager to use this tool."
            );
#else
            try
            {
                switch (action)
                {
                    case "create_spline": return CreateSpline(@params);
                    case "add_knot": return AddKnot(@params);
                    case "remove_knot": return RemoveKnot(@params);
                    case "set_knot": return SetKnot(@params);
                    case "set_closed": return SetClosed(@params);
                    case "get_spline_info": return GetSplineInfo(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageSplines error: {e.Message}");
            }
#endif
        }

#if UNITY_SPLINES
        private static object CreateSpline(JObject @params)
        {
            string name = @params["name"]?.ToString() ?? "New Spline";
            var go = new GameObject(name);
            var container = go.AddComponent<SplineContainer>();
            Undo.RegisterCreatedObjectUndo(go, "Create Spline");

            return new SuccessResponse($"Created Spline '{name}'", new
            {
                instanceID = go.GetInstanceID(),
                name = go.name
            });
        }

        private static object AddKnot(JObject @params)
        {
            var container = FindSplineContainer(@params);
            if (container == null) return new ErrorResponse("Target SplineContainer not found.");

            float x = @params["x"]?.ToObject<float>() ?? 0f;
            float y = @params["y"]?.ToObject<float>() ?? 0f;
            float z = @params["z"]?.ToObject<float>() ?? 0f;

            Undo.RecordObject(container, "Add Spline Knot");
            container.Spline.Add(new BezierKnot(new float3(x, y, z)));
            EditorUtility.SetDirty(container);

            return new SuccessResponse("Added knot to spline.", new
            {
                knotIndex = container.Spline.Count - 1,
                position = new { x, y, z },
                totalKnots = container.Spline.Count
            });
        }

        private static object RemoveKnot(JObject @params)
        {
            var container = FindSplineContainer(@params);
            if (container == null) return new ErrorResponse("Target SplineContainer not found.");

            int index = @params["knot_index"]?.ToObject<int>() ?? -1;
            if (index < 0 || index >= container.Spline.Count)
                return new ErrorResponse(
                    $"Invalid knot index: {index}. Spline has {container.Spline.Count} knots (0-{container.Spline.Count - 1})."
                );

            Undo.RecordObject(container, "Remove Spline Knot");
            container.Spline.RemoveAt(index);
            EditorUtility.SetDirty(container);

            return new SuccessResponse($"Removed knot at index {index}.", new
            {
                removedIndex = index,
                remainingKnots = container.Spline.Count
            });
        }

        private static object SetKnot(JObject @params)
        {
            var container = FindSplineContainer(@params);
            if (container == null) return new ErrorResponse("Target SplineContainer not found.");

            int index = @params["knot_index"]?.ToObject<int>() ?? -1;
            if (index < 0 || index >= container.Spline.Count)
                return new ErrorResponse($"Invalid knot index: {index}.");

            Undo.RecordObject(container, "Set Spline Knot");
            var knot = container.Spline[index];

            if (@params["x"] != null || @params["y"] != null || @params["z"] != null)
            {
                float x = @params["x"]?.ToObject<float>() ?? knot.Position.x;
                float y = @params["y"]?.ToObject<float>() ?? knot.Position.y;
                float z = @params["z"]?.ToObject<float>() ?? knot.Position.z;
                knot.Position = new float3(x, y, z);
            }

            container.Spline[index] = knot;
            EditorUtility.SetDirty(container);

            return new SuccessResponse($"Updated knot at index {index}.", new
            {
                knotIndex = index,
                position = new { x = knot.Position.x, y = knot.Position.y, z = knot.Position.z }
            });
        }

        private static object SetClosed(JObject @params)
        {
            var container = FindSplineContainer(@params);
            if (container == null) return new ErrorResponse("Target SplineContainer not found.");

            bool closed = @params["closed"]?.ToObject<bool>() ?? !container.Spline.Closed;

            Undo.RecordObject(container, "Set Spline Closed");
            container.Spline.Closed = closed;
            EditorUtility.SetDirty(container);

            return new SuccessResponse($"Spline closed set to {closed}.", new { closed });
        }

        private static object GetSplineInfo(JObject @params)
        {
            var container = FindSplineContainer(@params);
            if (container == null) return new ErrorResponse("Target SplineContainer not found.");

            var spline = container.Spline;
            var knots = new List<object>();
            for (int i = 0; i < spline.Count; i++)
            {
                var k = spline[i];
                knots.Add(new
                {
                    index = i,
                    position = new { x = k.Position.x, y = k.Position.y, z = k.Position.z }
                });
            }

            return new SuccessResponse($"Spline info for '{container.gameObject.name}'.", new
            {
                name = container.gameObject.name,
                knotCount = spline.Count,
                closed = spline.Closed,
                length = spline.GetLength(),
                knots
            });
        }

        /// <summary>
        /// Finds a SplineContainer on the target GameObject using the project's standard lookup.
        /// </summary>
        private static SplineContainer FindSplineContainer(JObject @params)
        {
            string target = @params["target"]?.ToString();
            if (string.IsNullOrEmpty(target)) return null;

            var go = GameObjectLookup.FindByTarget(new JValue(target), "by_id_or_name_or_path");
            return go != null ? go.GetComponent<SplineContainer>() : null;
        }
#endif
    }
}
