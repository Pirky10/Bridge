using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_undo")]
    public static class ManageUndo
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' is required.");

            try
            {
                switch (action)
                {
                    case "undo": return PerformUndo(@params);
                    case "redo": return PerformRedo(@params);
                    case "get_undo_history": return GetUndoHistory();
                    case "begin_group": return BeginGroup(@params);
                    case "end_group": return EndGroup();
                    case "clear_undo": return ClearUndo();
                    case "collapse_group": return CollapseGroup(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageUndo error: {e.Message}");
            }
        }

        private static object PerformUndo(JObject @params)
        {
            int steps = @params["steps"]?.ToObject<int>() ?? 1;
            for (int i = 0; i < steps; i++)
                Undo.PerformUndo();
            return new SuccessResponse($"Performed {steps} undo step(s).");
        }

        private static object PerformRedo(JObject @params)
        {
            int steps = @params["steps"]?.ToObject<int>() ?? 1;
            for (int i = 0; i < steps; i++)
                Undo.PerformRedo();
            return new SuccessResponse($"Performed {steps} redo step(s).");
        }

        private static object GetUndoHistory()
        {
            Undo.GetCurrentGroupName();
            string currentGroup = Undo.GetCurrentGroupName();
            int currentGroupIndex = Undo.GetCurrentGroup();

            return new SuccessResponse("Undo history retrieved.", new
            {
                currentGroupName = currentGroup,
                currentGroupIndex = currentGroupIndex
            });
        }

        private static object BeginGroup(JObject @params)
        {
            string groupName = @params["group_name"]?.ToString() ?? "MCP Undo Group";
            Undo.SetCurrentGroupName(groupName);
            int groupIndex = Undo.GetCurrentGroup();
            return new SuccessResponse($"Undo group '{groupName}' started.", new
            {
                groupName,
                groupIndex
            });
        }

        private static object EndGroup()
        {
            Undo.IncrementCurrentGroup();
            return new SuccessResponse("Undo group ended.");
        }

        private static object ClearUndo()
        {
            Undo.ClearAll();
            return new SuccessResponse("Undo history cleared.");
        }

        private static object CollapseGroup(JObject @params)
        {
            string groupName = @params["group_name"]?.ToString() ?? "MCP Collapsed Group";
            int groupIndex = Undo.GetCurrentGroup();
            Undo.CollapseUndoOperations(groupIndex);
            return new SuccessResponse($"Collapsed undo operations into group '{groupName}'.", new
            {
                groupName,
                groupIndex
            });
        }
    }
}
