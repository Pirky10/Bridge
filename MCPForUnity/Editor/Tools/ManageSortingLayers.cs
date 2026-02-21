using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_sorting_layers", AutoRegister = false)]
    public static class ManageSortingLayers
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
                    case "add": return AddSortingLayer(@params, p);
                    case "remove": return RemoveSortingLayer(@params, p);
                    case "reorder": return Reorder(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: add, remove, reorder, get_info");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object AddSortingLayer(JObject @params, ToolParams p)
        {
            var nameResult = p.GetRequired("layer_name");
            var nameError = nameResult.GetOrError(out string layerName);
            if (nameError != null) return nameError;

            // Check if already exists
            foreach (var sl in SortingLayer.layers)
                if (sl.name == layerName)
                    return new ErrorResponse($"Sorting layer '{layerName}' already exists.");

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
            SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");

            sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
            var newElement = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
            newElement.FindPropertyRelative("name").stringValue = layerName;
            newElement.FindPropertyRelative("uniqueID").intValue = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            tagManager.ApplyModifiedProperties();

            return new SuccessResponse($"Added sorting layer '{layerName}'");
        }

        private static object RemoveSortingLayer(JObject @params, ToolParams p)
        {
            var nameResult = p.GetRequired("layer_name");
            var nameError = nameResult.GetOrError(out string layerName);
            if (nameError != null) return nameError;

            if (layerName == "Default")
                return new ErrorResponse("Cannot remove Default sorting layer.");

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
            SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");

            for (int i = 0; i < sortingLayers.arraySize; i++)
            {
                if (sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layerName)
                {
                    sortingLayers.DeleteArrayElementAtIndex(i);
                    tagManager.ApplyModifiedProperties();
                    return new SuccessResponse($"Removed sorting layer '{layerName}'");
                }
            }

            return new ErrorResponse($"Sorting layer '{layerName}' not found.");
        }

        private static object Reorder(JObject @params, ToolParams p)
        {
            JToken orderToken = p.GetRaw("order");
            if (orderToken == null)
                return new ErrorResponse("'order' array required (list of layer names in desired order).");

            var newOrder = orderToken.ToObject<string[]>();
            if (newOrder == null || newOrder.Length == 0)
                return new ErrorResponse("'order' must be a non-empty array of layer names.");

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
            SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");

            // Collect current layers
            var current = new Dictionary<string, int>();
            for (int i = 0; i < sortingLayers.arraySize; i++)
            {
                var el = sortingLayers.GetArrayElementAtIndex(i);
                current[el.FindPropertyRelative("name").stringValue] = el.FindPropertyRelative("uniqueID").intValue;
            }

            // Clear and rebuild
            sortingLayers.ClearArray();
            foreach (string name in newOrder)
            {
                if (!current.ContainsKey(name)) continue;
                sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
                var el = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
                el.FindPropertyRelative("name").stringValue = name;
                el.FindPropertyRelative("uniqueID").intValue = current[name];
            }

            // Add any layers not in the new order
            foreach (var kvp in current)
            {
                if (Array.IndexOf(newOrder, kvp.Key) < 0)
                {
                    sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
                    var el = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
                    el.FindPropertyRelative("name").stringValue = kvp.Key;
                    el.FindPropertyRelative("uniqueID").intValue = kvp.Value;
                }
            }

            tagManager.ApplyModifiedProperties();
            return new SuccessResponse("Reordered sorting layers.");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var layers = new List<object>();
            foreach (var sl in SortingLayer.layers)
                layers.Add(new { name = sl.name, id = sl.id, value = sl.value });

            return new SuccessResponse("Sorting layers", new { layers, count = layers.Count });
        }
    }
}
