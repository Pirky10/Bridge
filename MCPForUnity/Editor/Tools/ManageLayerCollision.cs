using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_layer_collision", AutoRegister = false)]
    public static class ManageLayerCollision
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "set_collision": return SetCollision(@params, p);
                    case "ignore_collision": return IgnoreCollision(@params, p);
                    case "get_matrix": return GetMatrix(@params, p);
                    case "reset_all": return ResetAll(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: set_collision, ignore_collision, get_matrix, reset_all");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object SetCollision(JObject @params, ToolParams p)
        {
            var l1Result = p.GetRequired("layer1");
            var l1Error = l1Result.GetOrError(out string layer1);
            if (l1Error != null) return l1Error;
            var l2Result = p.GetRequired("layer2");
            var l2Error = l2Result.GetOrError(out string layer2);
            if (l2Error != null) return l2Error;

            int l1 = LayerMask.NameToLayer(layer1);
            int l2 = LayerMask.NameToLayer(layer2);
            if (l1 < 0) return new ErrorResponse($"Layer '{layer1}' not found.");
            if (l2 < 0) return new ErrorResponse($"Layer '{layer2}' not found.");

            bool collide = p.GetBool("collide", true);
            UnityEngine.Physics.IgnoreLayerCollision(l1, l2, !collide);

            return new SuccessResponse($"Set collision {layer1}<->{layer2} = {collide}");
        }

        private static object IgnoreCollision(JObject @params, ToolParams p)
        {
            var l1Result = p.GetRequired("layer1");
            var l1Error = l1Result.GetOrError(out string layer1);
            if (l1Error != null) return l1Error;
            var l2Result = p.GetRequired("layer2");
            var l2Error = l2Result.GetOrError(out string layer2);
            if (l2Error != null) return l2Error;

            int l1 = LayerMask.NameToLayer(layer1);
            int l2 = LayerMask.NameToLayer(layer2);
            if (l1 < 0) return new ErrorResponse($"Layer '{layer1}' not found.");
            if (l2 < 0) return new ErrorResponse($"Layer '{layer2}' not found.");

            UnityEngine.Physics.IgnoreLayerCollision(l1, l2, true);
            return new SuccessResponse($"Ignoring collision: {layer1}<->{layer2}");
        }

        private static object GetMatrix(JObject @params, ToolParams p)
        {
            var matrix = new Dictionary<string, Dictionary<string, bool>>();
            var layerNames = new List<string>();

            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name)) layerNames.Add(name);
            }

            // Build collision pairs that are ignored
            var ignoredPairs = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                string n1 = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(n1)) continue;
                for (int j = i; j < 32; j++)
                {
                    string n2 = LayerMask.LayerToName(j);
                    if (string.IsNullOrEmpty(n2)) continue;
                    if (UnityEngine.Physics.GetIgnoreLayerCollision(i, j))
                        ignoredPairs.Add($"{n1} <-> {n2}");
                }
            }

            return new SuccessResponse("Collision matrix", new
            {
                layers = layerNames,
                ignoredPairs = ignoredPairs,
                ignoredCount = ignoredPairs.Count
            });
        }

        private static object ResetAll(JObject @params, ToolParams p)
        {
            for (int i = 0; i < 32; i++)
                for (int j = i; j < 32; j++)
                    UnityEngine.Physics.IgnoreLayerCollision(i, j, false);

            return new SuccessResponse("Reset all layer collisions to enabled.");
        }
    }
}
