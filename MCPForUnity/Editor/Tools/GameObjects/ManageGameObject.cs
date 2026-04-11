#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MCPForUnity.Editor.Helpers; // For Response class
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCPForUnity.Editor.Tools.GameObjects
{
    /// <summary>
    /// Handles GameObject manipulation within the current scene (CRUD, find, components).
    /// </summary>
    [McpForUnityTool("manage_gameobject", AutoRegister = false)]
    public static class ManageGameObject
    {
        // --- Main Handler ---

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                return new ErrorResponse("Parameters cannot be null.");
            }

            string action = @params["action"]?.ToString().ToLower();
            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Action parameter is required.");
            }

            // Parameters used by various actions
            JToken targetToken = @params["target"]; // Can be string (name/path) or int (instanceID)
            string name = @params["name"]?.ToString();

            // --- Usability Improvement: Alias 'name' to 'target' for modification actions ---
            // If 'target' is missing but 'name' is provided, and we aren't creating a new object,
            // assume the user meant "find object by name".
            if (targetToken == null && !string.IsNullOrEmpty(name) && action != "create")
            {
                targetToken = name;
                // We don't update @params["target"] because we use targetToken locally mostly,
                // but some downstream methods might parse @params directly. Let's update @params too for safety.
                @params["target"] = name;
            }
            // -------------------------------------------------------------------------------

            string searchMethod = @params["searchMethod"]?.ToString().ToLower();
            string tag = @params["tag"]?.ToString();
            string layer = @params["layer"]?.ToString();
            JToken parentToken = @params["parent"];

            // Coerce string JSON to JObject for 'componentProperties' if provided as a JSON string
            var componentPropsToken = @params["componentProperties"];
            if (componentPropsToken != null && componentPropsToken.Type == JTokenType.String)
            {
                try
                {
                    var parsed = JObject.Parse(componentPropsToken.ToString());
                    @params["componentProperties"] = parsed;
                }
                catch (Exception e)
                {
                    McpLog.Warn($"[ManageGameObject] Could not parse 'componentProperties' JSON string: {e.Message}");
                }
            }

            // --- Prefab Asset Check ---
            // Prefab assets require different tools. Only 'create' (instantiation) is valid here.
            string targetPath =
                targetToken?.Type == JTokenType.String ? targetToken.ToString() : null;
            if (
                !string.IsNullOrEmpty(targetPath)
                && targetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                && action != "create" // Allow prefab instantiation
            )
            {
                return new ErrorResponse(
                    $"Target '{targetPath}' is a prefab asset. " +
                    $"Use 'manage_asset' with action='modify' for prefab asset modifications, " +
                    $"or 'manage_prefabs' with action='modify_contents' to edit the prefab headlessly, or 'manage_prefabs' with action='close_prefab_stage' to exit prefab editing mode."
                );
            }
            // --- End Prefab Asset Check ---

            try
            {
                switch (action)
                {
                    // --- Primary lifecycle actions (kept in manage_gameobject) ---
                    case "create":
                        return GameObjectCreate.Handle(@params);
                    case "modify":
                        return GameObjectModify.Handle(@params, targetToken, searchMethod);
                    case "delete":
                        return GameObjectDelete.Handle(targetToken, searchMethod);
                    case "duplicate":
                        return GameObjectDuplicate.Handle(@params, targetToken, searchMethod);
                    case "move_relative":
                        return GameObjectMoveRelative.Handle(@params, targetToken, searchMethod);
                    case "look_at":
                        return GameObjectLookAt.Handle(@params, targetToken, searchMethod);

                    case "add_asset_to_scene":
                    {
                        string assetPath = @params["assetPath"]?.ToString();
                        if (string.IsNullOrEmpty(assetPath))
                            return new ErrorResponse("'assetPath' is required for add_asset_to_scene.");

                        assetPath = AssetPathUtility.SanitizeAssetPath(assetPath);
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        if (asset == null)
                            return new ErrorResponse($"Asset not found at '{assetPath}'.");

                        GameObject instance = null;
                        if (asset is GameObject prefab)
                        {
                            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        }
                        else
                        {
                            instance = (GameObject)UnityEngine.Object.Instantiate(asset);
                        }

                        if (instance == null)
                            return new ErrorResponse($"Could not instantiate asset at '{assetPath}'.");

                        Undo.RegisterCreatedObjectUndo(instance, "Add Asset To Scene");

                        // Apply position if provided
                        var pos = @params["position"];
                        if (pos != null)
                        {
                            instance.transform.position = new Vector3(
                                pos[0]?.ToObject<float>() ?? 0f,
                                pos[1]?.ToObject<float>() ?? 0f,
                                pos[2]?.ToObject<float>() ?? 0f
                            );
                        }

                        // Set parent if provided
                        string parentTarget = targetToken?.ToString();
                        if (!string.IsNullOrEmpty(parentTarget))
                        {
                            var parentGo = GameObject.Find(parentTarget);
                            if (parentGo != null)
                                instance.transform.SetParent(parentGo.transform, true);
                        }

                        return new SuccessResponse($"Added '{assetPath}' to scene as '{instance.name}'", new
                        {
                            instanceId = instance.GetInstanceID(),
                            name = instance.name,
                            assetPath
                        });
                    }

                    case "set_sibling_index":
                    {
                        if (targetToken == null)
                            return new ErrorResponse("'target' is required for set_sibling_index.");

                        int? siblingIndex = @params["siblingIndex"]?.ToObject<int>();
                        if (!siblingIndex.HasValue)
                            return new ErrorResponse("'siblingIndex' is required.");

                        var go = ManageGameObjectCommon.FindObjectInternal(targetToken, searchMethod, null);
                        if (go == null)
                            return new ErrorResponse($"GameObject '{targetToken}' not found.");

                        Undo.RecordObject(go.transform, "Set Sibling Index");
                        go.transform.SetSiblingIndex(siblingIndex.Value);

                        return new SuccessResponse($"Set sibling index of '{go.name}' to {siblingIndex.Value}", new
                        {
                            instanceId = go.GetInstanceID(),
                            name = go.name,
                            siblingIndex = go.transform.GetSiblingIndex()
                        });
                    }

                    default:
                        return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                McpLog.Error($"[ManageGameObject] Action '{action}' failed: {e}");
                return new ErrorResponse($"Internal error processing action '{action}': {e.Message}");
            }
        }
    }
}
