using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_tags_layers", AutoRegister = false)]
    public static class ManageTagsLayers
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
                    case "list_tags":
                        return ListTags(@params, p);
                    case "add_tag":
                        return AddTag(@params, p);
                    case "remove_tag":
                        return RemoveTag(@params, p);
                    case "set_tag":
                        return SetTag(@params, p);
                    case "list_layers":
                        return ListLayers(@params, p);
                    case "set_layer":
                        return SetLayer(@params, p);
                    case "set_layer_name":
                        return SetLayerName(@params, p);
                    case "list_sorting_layers":
                        return ListSortingLayers(@params, p);
                    case "add_sorting_layer":
                        return AddSortingLayer(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: list_tags, add_tag, remove_tag, set_tag, list_layers, set_layer, set_layer_name, list_sorting_layers, add_sorting_layer");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static SerializedObject GetTagManager()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            return new SerializedObject(asset);
        }

        private static object ListTags(JObject @params, ToolParams p)
        {
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            return new SuccessResponse($"Found {tags.Length} tags", new { tags });
        }

        private static object AddTag(JObject @params, ToolParams p)
        {
            var tagResult = p.GetRequired("tag");
            var tagError = tagResult.GetOrError(out string tag);
            if (tagError != null) return tagError;

            // Check if tag already exists
            foreach (var existing in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (existing == tag)
                    return new ErrorResponse($"Tag '{tag}' already exists.");
            }

            SerializedObject tagManager = GetTagManager();
            SerializedProperty tags = tagManager.FindProperty("tags");

            // Find first empty slot or add new
            int insertIndex = tags.arraySize;
            tags.InsertArrayElementAtIndex(insertIndex);
            tags.GetArrayElementAtIndex(insertIndex).stringValue = tag;

            tagManager.ApplyModifiedProperties();

            return new SuccessResponse($"Added tag '{tag}'", new { tag, index = insertIndex });
        }

        private static object RemoveTag(JObject @params, ToolParams p)
        {
            var tagResult = p.GetRequired("tag");
            var tagError = tagResult.GetOrError(out string tag);
            if (tagError != null) return tagError;

            SerializedObject tagManager = GetTagManager();
            SerializedProperty tags = tagManager.FindProperty("tags");

            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    tags.DeleteArrayElementAtIndex(i);
                    tagManager.ApplyModifiedProperties();
                    return new SuccessResponse($"Removed tag '{tag}'");
                }
            }

            return new ErrorResponse($"Tag '{tag}' not found.");
        }

        private static object SetTag(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var tagResult = p.GetRequired("tag");
            var tagError = tagResult.GetOrError(out string tag);
            if (tagError != null) return tagError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Set Tag");
            go.tag = tag;
            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Set tag '{tag}' on '{target}'", new { target, tag });
        }

        private static object ListLayers(JObject @params, ToolParams p)
        {
            var layers = new List<object>();
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(new { index = i, name = layerName });
                }
            }

            return new SuccessResponse($"Found {layers.Count} named layers", new { layers });
        }

        private static object SetLayer(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            int layer = -1;

            int? layerIndex = p.GetInt("layer_index");
            if (layerIndex.HasValue)
            {
                layer = layerIndex.Value;
            }
            else
            {
                string layerName = p.Get("layer_name");
                if (!string.IsNullOrEmpty(layerName))
                {
                    layer = LayerMask.NameToLayer(layerName);
                    if (layer < 0)
                        return new ErrorResponse($"Layer '{layerName}' not found.");
                }
            }

            if (layer < 0 || layer > 31)
                return new ErrorResponse("Provide 'layer_index' (0-31) or 'layer_name'.");

            Undo.RecordObject(go, "Set Layer");
            bool recursive = p.GetBool("recursive", false);

            if (recursive)
                SetLayerRecursive(go, layer);
            else
                go.layer = layer;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Set layer {LayerMask.LayerToName(layer)} on '{target}'", new
            {
                target, layer, layerName = LayerMask.LayerToName(layer), recursive
            });
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        private static object SetLayerName(JObject @params, ToolParams p)
        {
            int? layerIndex = p.GetInt("layer_index");
            if (!layerIndex.HasValue)
                return new ErrorResponse("'layer_index' required (8-31 for user layers).");

            var nameResult = p.GetRequired("name");
            var nameError = nameResult.GetOrError(out string name);
            if (nameError != null) return nameError;

            if (layerIndex.Value < 8 || layerIndex.Value > 31)
                return new ErrorResponse("Can only set names for user layers (index 8-31).");

            SerializedObject tagManager = GetTagManager();
            SerializedProperty layers = tagManager.FindProperty("layers");

            layers.GetArrayElementAtIndex(layerIndex.Value).stringValue = name;
            tagManager.ApplyModifiedProperties();

            return new SuccessResponse($"Set layer {layerIndex.Value} name to '{name}'");
        }

        private static object ListSortingLayers(JObject @params, ToolParams p)
        {
            var sortingLayers = new List<object>();
            foreach (var layer in SortingLayer.layers)
            {
                sortingLayers.Add(new { id = layer.id, name = layer.name, value = layer.value });
            }

            return new SuccessResponse($"Found {sortingLayers.Count} sorting layers", new { sortingLayers });
        }

        private static object AddSortingLayer(JObject @params, ToolParams p)
        {
            var nameResult = p.GetRequired("name");
            var nameError = nameResult.GetOrError(out string name);
            if (nameError != null) return nameError;

            SerializedObject tagManager = GetTagManager();
            SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");

            // Check if already exists
            for (int i = 0; i < sortingLayers.arraySize; i++)
            {
                if (sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                    return new ErrorResponse($"Sorting layer '{name}' already exists.");
            }

            sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
            var newLayer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
            newLayer.FindPropertyRelative("name").stringValue = name;
            newLayer.FindPropertyRelative("uniqueID").intValue = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            tagManager.ApplyModifiedProperties();

            return new SuccessResponse($"Added sorting layer '{name}'", new { name });
        }
    }
}
