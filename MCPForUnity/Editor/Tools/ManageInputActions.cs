using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
#if UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_input_actions", AutoRegister = false)]
    public static class ManageInputActions
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            action = action.ToLowerInvariant();

#if !UNITY_INPUT_SYSTEM
            return new ErrorResponse(
                "The Input System package is not installed. Install it via Package Manager " +
                "(com.unity.inputsystem) to use manage_input_actions.");
#else
            try
            {
                switch (action)
                {
                    case "create_asset":
                        return CreateAsset(@params, p);
                    case "add_action_map":
                        return AddActionMap(@params, p);
                    case "add_action":
                        return AddAction(@params, p);
                    case "add_binding":
                        return AddBinding(@params, p);
                    case "get_info":
                        return GetInfo(@params, p);
                    case "assign_to_player_input":
                        return AssignToPlayerInput(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: create_asset, add_action_map, add_action, add_binding, get_info, assign_to_player_input");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
#endif
        }

#if UNITY_INPUT_SYSTEM
        private static InputActionAsset LoadAsset(string path)
        {
            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
                sanitized += ".inputactions";
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(sanitized);
        }

        private static object CreateAsset(JObject @params, ToolParams p)
        {
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' parameter is required (e.g., Assets/Input/PlayerControls.inputactions).");

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
                sanitized += ".inputactions";

            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(sanitized) != null)
                return new ErrorResponse($"Input action asset already exists at '{sanitized}'.");

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();

            // Optionally add a default action map
            string defaultMap = p.Get("default_map");
            if (!string.IsNullOrEmpty(defaultMap))
            {
                asset.AddActionMap(defaultMap);
            }

            string json = asset.ToJson();
            System.IO.File.WriteAllText(sanitized, json);
            UnityEngine.Object.DestroyImmediate(asset);

            AssetDatabase.ImportAsset(sanitized);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created Input Action Asset at '{sanitized}'", new
            {
                path = sanitized,
                defaultMap = defaultMap
            });
        }

        private static object AddActionMap(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            var mapNameResult = p.GetRequired("map_name");
            var mapError = mapNameResult.GetOrError(out string mapName);
            if (mapError != null) return mapError;

            var asset = LoadAsset(path);
            if (asset == null)
                return new ErrorResponse($"Input action asset not found at '{path}'.");

            // Check if map already exists
            if (asset.FindActionMap(mapName) != null)
                return new ErrorResponse($"Action map '{mapName}' already exists.");

            asset.AddActionMap(mapName);

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
                sanitized += ".inputactions";
            System.IO.File.WriteAllText(sanitized, asset.ToJson());

            AssetDatabase.ImportAsset(sanitized);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added action map '{mapName}'", new { mapName });
        }

        private static object AddAction(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            var mapNameResult = p.GetRequired("map_name");
            var mapError = mapNameResult.GetOrError(out string mapName);
            if (mapError != null) return mapError;

            var actionNameResult = p.GetRequired("action_name");
            var actionNameError = actionNameResult.GetOrError(out string actionName);
            if (actionNameError != null) return actionNameError;

            string actionType = p.Get("action_type", "Value");

            var asset = LoadAsset(path);
            if (asset == null)
                return new ErrorResponse($"Input action asset not found at '{path}'.");

            var map = asset.FindActionMap(mapName);
            if (map == null)
                return new ErrorResponse($"Action map '{mapName}' not found.");

            InputActionType inputActionType = InputActionType.Value;
            if (Enum.TryParse<InputActionType>(actionType, true, out var parsed))
                inputActionType = parsed;

            map.AddAction(actionName, inputActionType);

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
                sanitized += ".inputactions";
            System.IO.File.WriteAllText(sanitized, asset.ToJson());

            AssetDatabase.ImportAsset(sanitized);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added action '{actionName}' to map '{mapName}'", new
            {
                actionName,
                mapName,
                actionType = inputActionType.ToString()
            });
        }

        private static object AddBinding(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            var mapNameResult = p.GetRequired("map_name");
            var mapError = mapNameResult.GetOrError(out string mapName);
            if (mapError != null) return mapError;

            var actionNameResult = p.GetRequired("action_name");
            var actionNameError = actionNameResult.GetOrError(out string actionName);
            if (actionNameError != null) return actionNameError;

            var bindingPathResult = p.GetRequired("binding_path");
            var bindingError = bindingPathResult.GetOrError(out string bindingPath);
            if (bindingError != null) return bindingError;

            var asset = LoadAsset(path);
            if (asset == null)
                return new ErrorResponse($"Input action asset not found at '{path}'.");

            var map = asset.FindActionMap(mapName);
            if (map == null)
                return new ErrorResponse($"Action map '{mapName}' not found.");

            var inputAction = map.FindAction(actionName);
            if (inputAction == null)
                return new ErrorResponse($"Action '{actionName}' not found in map '{mapName}'.");

            inputAction.AddBinding(bindingPath);

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
                sanitized += ".inputactions";
            System.IO.File.WriteAllText(sanitized, asset.ToJson());

            AssetDatabase.ImportAsset(sanitized);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Added binding '{bindingPath}' to action '{actionName}'", new
            {
                actionName,
                mapName,
                bindingPath
            });
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            var asset = LoadAsset(path);
            if (asset == null)
                return new ErrorResponse($"Input action asset not found at '{path}'.");

            var maps = new System.Collections.Generic.List<object>();
            foreach (var map in asset.actionMaps)
            {
                var actions = new System.Collections.Generic.List<object>();
                foreach (var action in map.actions)
                {
                    var bindings = new System.Collections.Generic.List<object>();
                    foreach (var binding in action.bindings)
                    {
                        bindings.Add(new
                        {
                            path = binding.path,
                            interactions = binding.interactions,
                            processors = binding.processors,
                            isComposite = binding.isComposite,
                            isPartOfComposite = binding.isPartOfComposite
                        });
                    }
                    actions.Add(new
                    {
                        name = action.name,
                        type = action.type.ToString(),
                        bindings
                    });
                }
                maps.Add(new { name = map.name, actions });
            }

            return new SuccessResponse($"Input actions info for '{path}'", new { actionMaps = maps });
        }

        private static object AssignToPlayerInput(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            var asset = LoadAsset(path);
            if (asset == null)
                return new ErrorResponse($"Input action asset not found at '{path}'.");

            PlayerInput playerInput = go.GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                Undo.RecordObject(go, "Add PlayerInput");
                playerInput = go.AddComponent<PlayerInput>();
            }

            Undo.RecordObject(playerInput, "Assign Input Actions");
            playerInput.actions = asset;

            string defaultMap = p.Get("default_map");
            if (!string.IsNullOrEmpty(defaultMap))
                playerInput.defaultActionMap = defaultMap;

            EditorUtility.SetDirty(playerInput);

            return new SuccessResponse($"Assigned input actions to PlayerInput on '{target}'", new
            {
                target,
                assetPath = path,
                defaultMap = playerInput.defaultActionMap
            });
        }
#endif
    }
}
