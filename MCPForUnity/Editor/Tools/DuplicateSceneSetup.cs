using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("duplicate_scene_setup", AutoRegister = false)]
    public static class DuplicateSceneSetup
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
                    case "duplicate": return Duplicate(@params, p);
                    case "copy_objects": return CopyObjects(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: duplicate, copy_objects, get_info");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object Duplicate(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("new_scene_path");
            var pathError = pathResult.GetOrError(out string newPath);
            if (pathError != null) return pathError;

            Scene currentScene = SceneManager.GetActiveScene();
            if (!currentScene.IsValid())
                return new ErrorResponse("No active scene.");

            // Save current scene first
            EditorSceneManager.SaveScene(currentScene);

            // Copy the scene file
            string sourcePath = currentScene.path;
            if (string.IsNullOrEmpty(sourcePath))
                return new ErrorResponse("Scene has not been saved yet.");

            AssetDatabase.CopyAsset(sourcePath, newPath);
            AssetDatabase.Refresh();

            bool openNew = p.GetBool("open_new", false);
            if (openNew)
                EditorSceneManager.OpenScene(newPath);

            return new SuccessResponse($"Duplicated scene to '{newPath}'", new
            {
                source = sourcePath, destination = newPath, opened = openNew
            });
        }

        private static object CopyObjects(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target_scene_path");
            var targetError = targetResult.GetOrError(out string targetPath);
            if (targetError != null) return targetError;

            JToken objectsToken = p.GetRaw("objects");
            if (objectsToken == null)
                return new ErrorResponse("'objects' array required (list of GameObject names).");

            var objectNames = objectsToken.ToObject<string[]>();
            Scene currentScene = SceneManager.GetActiveScene();

            // Open target scene additively
            Scene targetScene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Additive);

            int copied = 0;
            foreach (string name in objectNames)
            {
                GameObject go = GameObject.Find(name);
                if (go != null && go.scene == currentScene)
                {
                    GameObject clone = UnityEngine.Object.Instantiate(go);
                    clone.name = go.name;
                    SceneManager.MoveGameObjectToScene(clone, targetScene);
                    copied++;
                }
            }

            EditorSceneManager.SaveScene(targetScene);
            EditorSceneManager.CloseScene(targetScene, true);

            return new SuccessResponse($"Copied {copied} objects to '{targetPath}'");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            Scene scene = SceneManager.GetActiveScene();
            var rootObjects = new List<string>();
            foreach (var go in scene.GetRootGameObjects())
                rootObjects.Add(go.name);

            return new SuccessResponse("Scene info", new
            {
                name = scene.name,
                path = scene.path,
                rootObjectCount = scene.rootCount,
                rootObjects
            });
        }
    }
}
