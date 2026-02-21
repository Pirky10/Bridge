using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_selection")]
    public static class ManageSelection
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
                    case "get_selection": return GetSelection();
                    case "set_selection": return SetSelection(@params);
                    case "select_all": return SelectAll();
                    case "select_by_type": return SelectByType(@params);
                    case "select_by_tag": return SelectByTag(@params);
                    case "select_by_layer": return SelectByLayer(@params);
                    case "clear_selection": return ClearSelection();
                    case "ping_object": return PingObject(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageSelection error: {e.Message}");
            }
        }

        private static object GetSelection()
        {
            var selected = Selection.gameObjects;
            var data = selected.Select(go => new
            {
                name = go.name,
                instanceID = go.GetInstanceID(),
                path = GameObjectLookup.GetGameObjectPath(go)
            }).ToArray();

            return new SuccessResponse($"Selection contains {selected.Length} object(s).", new
            {
                count = selected.Length,
                objects = data,
                activeObject = Selection.activeGameObject != null ? new
                {
                    name = Selection.activeGameObject.name,
                    instanceID = Selection.activeGameObject.GetInstanceID()
                } : null
            });
        }

        private static object SetSelection(JObject @params)
        {
            var targets = @params["targets"]?.ToObject<List<string>>();
            string single = @params["target"]?.ToString();
            bool addTo = @params["add_to_selection"]?.ToObject<bool>() ?? false;

            var toSelect = new List<UnityEngine.Object>();
            if (addTo) toSelect.AddRange(Selection.objects);

            if (!string.IsNullOrEmpty(single))
            {
                var go = GameObjectLookup.FindByTarget(new JValue(single), "by_id_or_name_or_path");
                if (go != null) toSelect.Add(go);
            }

            if (targets != null)
            {
                foreach (var t in targets)
                {
                    var go = GameObjectLookup.FindByTarget(new JValue(t), "by_id_or_name_or_path");
                    if (go != null) toSelect.Add(go);
                }
            }

            Selection.objects = toSelect.Distinct().ToArray();
            if (Selection.objects.Length > 0)
                Selection.activeGameObject = Selection.objects[0] as GameObject;

            return new SuccessResponse($"Selected {Selection.objects.Length} object(s).", new
            {
                count = Selection.objects.Length,
                names = Selection.gameObjects.Select(go => go.name).ToArray()
            });
        }

        private static object SelectAll()
        {
            var allObjects = GameObjectLookup.GetAllSceneObjects(false).ToArray();
            Selection.objects = allObjects;
            return new SuccessResponse($"Selected all {allObjects.Length} active scene objects.");
        }

        private static object SelectByType(JObject @params)
        {
            string typeName = @params["component_type"]?.ToString();
            if (string.IsNullOrEmpty(typeName))
                return new ErrorResponse("'component_type' is required.");

            Type type = GameObjectLookup.FindComponentType(typeName);
            if (type == null)
                return new ErrorResponse($"Component type '{typeName}' not found.");

            var matching = GameObjectLookup.GetAllSceneObjects(false)
                .Where(go => go.GetComponent(type) != null).ToArray();

            Selection.objects = matching;
            return new SuccessResponse($"Selected {matching.Length} objects with {typeName}.", new
            {
                count = matching.Length,
                names = matching.Select(go => go.name).ToArray()
            });
        }

        private static object SelectByTag(JObject @params)
        {
            string tag = @params["tag"]?.ToString();
            if (string.IsNullOrEmpty(tag))
                return new ErrorResponse("'tag' is required.");

            try
            {
                var tagged = GameObject.FindGameObjectsWithTag(tag);
                Selection.objects = tagged;
                return new SuccessResponse($"Selected {tagged.Length} objects with tag '{tag}'.", new
                {
                    count = tagged.Length,
                    names = tagged.Select(go => go.name).ToArray()
                });
            }
            catch (UnityException)
            {
                return new ErrorResponse($"Tag '{tag}' does not exist.");
            }
        }

        private static object SelectByLayer(JObject @params)
        {
            string layerName = @params["layer"]?.ToString();
            if (string.IsNullOrEmpty(layerName))
                return new ErrorResponse("'layer' is required.");

            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1 && !int.TryParse(layerName, out layer))
                return new ErrorResponse($"Layer '{layerName}' not found.");

            var matching = GameObjectLookup.GetAllSceneObjects(false)
                .Where(go => go.layer == layer).ToArray();

            Selection.objects = matching;
            return new SuccessResponse($"Selected {matching.Length} objects on layer '{layerName}'.", new
            {
                count = matching.Length,
                names = matching.Select(go => go.name).ToArray()
            });
        }

        private static object ClearSelection()
        {
            Selection.objects = new UnityEngine.Object[0];
            Selection.activeGameObject = null;
            return new SuccessResponse("Selection cleared.");
        }

        private static object PingObject(JObject @params)
        {
            string target = @params["target"]?.ToString();
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' is required.");

            var go = GameObjectLookup.FindByTarget(new JValue(target), "by_id_or_name_or_path");
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            EditorGUIUtility.PingObject(go);
            return new SuccessResponse($"Pinged '{go.name}' in hierarchy.", new
            {
                name = go.name,
                instanceID = go.GetInstanceID()
            });
        }
    }
}
