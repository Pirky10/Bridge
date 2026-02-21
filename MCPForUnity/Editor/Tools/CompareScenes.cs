using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("compare_scenes")]
    public static class CompareScenes
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
                    case "diff_scenes": return DiffScenes(@params);
                    case "diff_objects": return DiffObjects(@params);
                    case "get_scene_stats": return GetSceneStats(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"CompareScenes error: {e.Message}");
            }
        }

        private static object DiffScenes(JObject @params)
        {
            // Accept either loaded scenes or scene asset paths
            string sourcePath = @params["source_scene"]?.ToString();
            string targetPath = @params["target_scene"]?.ToString();

            Scene scene1, scene2;

            if (!string.IsNullOrEmpty(sourcePath) && !string.IsNullOrEmpty(targetPath))
            {
                // Open scenes by asset path — use additive mode to compare
                scene1 = GetOrOpenScene(sourcePath);
                scene2 = GetOrOpenScene(targetPath);
            }
            else
            {
                // Compare currently loaded scenes
                int sceneCount = SceneManager.sceneCount;
                if (sceneCount < 2)
                    return new ErrorResponse(
                        "Need at least 2 loaded scenes to compare. " +
                        "Either load multiple scenes or pass 'source_scene' and 'target_scene' asset paths."
                    );
                scene1 = SceneManager.GetSceneAt(0);
                scene2 = SceneManager.GetSceneAt(1);
            }

            if (!scene1.IsValid() || !scene1.isLoaded)
                return new ErrorResponse($"Scene '{scene1.name}' is not loaded or invalid.");
            if (!scene2.IsValid() || !scene2.isLoaded)
                return new ErrorResponse($"Scene '{scene2.name}' is not loaded or invalid.");

            // Build dictionaries of all GameObjects by their hierarchy path
            var objs1 = BuildHierarchyMap(scene1);
            var objs2 = BuildHierarchyMap(scene2);

            var onlyInScene1 = objs1.Keys.Except(objs2.Keys).OrderBy(s => s).ToList();
            var onlyInScene2 = objs2.Keys.Except(objs1.Keys).OrderBy(s => s).ToList();
            var commonPaths = objs1.Keys.Intersect(objs2.Keys).OrderBy(s => s).ToList();

            // Compare common objects for component differences
            var componentDiffs = new List<object>();
            foreach (string path in commonPaths)
            {
                var go1 = objs1[path];
                var go2 = objs2[path];

                var comps1 = go1.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name).OrderBy(s => s).ToList();
                var comps2 = go2.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name).OrderBy(s => s).ToList();

                var missingIn2 = comps1.Except(comps2).ToList();
                var missingIn1 = comps2.Except(comps1).ToList();

                bool activeChanged = go1.activeSelf != go2.activeSelf;
                bool layerChanged = go1.layer != go2.layer;
                bool tagChanged = go1.tag != go2.tag;

                if (missingIn2.Count > 0 || missingIn1.Count > 0 || activeChanged || layerChanged || tagChanged)
                {
                    componentDiffs.Add(new
                    {
                        path,
                        componentsOnlyInScene1 = missingIn2,
                        componentsOnlyInScene2 = missingIn1,
                        activeChanged = activeChanged
                            ? new { scene1 = go1.activeSelf, scene2 = go2.activeSelf }
                            : null,
                        layerChanged = layerChanged
                            ? new { scene1 = LayerMask.LayerToName(go1.layer), scene2 = LayerMask.LayerToName(go2.layer) }
                            : null,
                        tagChanged = tagChanged
                            ? new { scene1 = go1.tag, scene2 = go2.tag }
                            : null
                    });
                }
            }

            return new SuccessResponse(
                $"Comparison between '{scene1.name}' and '{scene2.name}'.",
                new
                {
                    scene1 = scene1.name,
                    scene2 = scene2.name,
                    scene1ObjectCount = objs1.Count,
                    scene2ObjectCount = objs2.Count,
                    onlyInScene1 = new { count = onlyInScene1.Count, paths = onlyInScene1 },
                    onlyInScene2 = new { count = onlyInScene2.Count, paths = onlyInScene2 },
                    commonObjectsWithDifferences = new { count = componentDiffs.Count, diffs = componentDiffs }
                }
            );
        }

        private static object DiffObjects(JObject @params)
        {
            string target1 = @params["target1"]?.ToString();
            string target2 = @params["target2"]?.ToString();

            if (string.IsNullOrEmpty(target1) || string.IsNullOrEmpty(target2))
                return new ErrorResponse("'target1' and 'target2' are required.");

            var go1 = GameObjectLookup.FindByTarget(new JValue(target1), "by_id_or_name_or_path");
            var go2 = GameObjectLookup.FindByTarget(new JValue(target2), "by_id_or_name_or_path");

            if (go1 == null) return new ErrorResponse($"Object not found: '{target1}'.");
            if (go2 == null) return new ErrorResponse($"Object not found: '{target2}'.");

            // Compare components
            var comps1 = go1.GetComponents<Component>().Where(c => c != null).ToList();
            var comps2 = go2.GetComponents<Component>().Where(c => c != null).ToList();

            var compNames1 = comps1.Select(c => c.GetType().Name).ToList();
            var compNames2 = comps2.Select(c => c.GetType().Name).ToList();

            // Compare serialized properties of shared components
            var propertyDiffs = new List<object>();
            var sharedTypes = compNames1.Intersect(compNames2).ToList();

            foreach (string typeName in sharedTypes)
            {
                var c1 = comps1.First(c => c.GetType().Name == typeName);
                var c2 = comps2.First(c => c.GetType().Name == typeName);

                var so1 = new SerializedObject(c1);
                var so2 = new SerializedObject(c2);

                var prop1 = so1.GetIterator();
                if (prop1.NextVisible(true))
                {
                    do
                    {
                        var prop2 = so2.FindProperty(prop1.propertyPath);
                        if (prop2 == null) continue;

                        // Compare serialized values via their string representation
                        string val1 = GetPropertyValueString(prop1);
                        string val2 = GetPropertyValueString(prop2);

                        if (val1 != val2)
                        {
                            propertyDiffs.Add(new
                            {
                                component = typeName,
                                property = prop1.propertyPath,
                                displayName = prop1.displayName,
                                value1 = val1,
                                value2 = val2
                            });
                        }
                    }
                    while (prop1.NextVisible(false));
                }
            }

            return new SuccessResponse($"Diff between '{go1.name}' and '{go2.name}'.", new
            {
                object1 = go1.name,
                object2 = go2.name,
                componentsOnlyIn1 = compNames1.Except(compNames2).ToList(),
                componentsOnlyIn2 = compNames2.Except(compNames1).ToList(),
                transform = new
                {
                    positionDelta = Vector3.Distance(go1.transform.position, go2.transform.position),
                    rotationDelta = Quaternion.Angle(go1.transform.rotation, go2.transform.rotation),
                    scaleDelta = Vector3.Distance(go1.transform.localScale, go2.transform.localScale)
                },
                propertyDifferences = new { count = propertyDiffs.Count, diffs = propertyDiffs }
            });
        }

        private static object GetSceneStats(JObject @params)
        {
            string sceneName = @params["scene"]?.ToString();

            Scene scene;
            if (!string.IsNullOrEmpty(sceneName))
            {
                scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid()) scene = SceneManager.GetSceneByPath(sceneName);
            }
            else
            {
                scene = SceneManager.GetActiveScene();
            }

            if (!scene.IsValid() || !scene.isLoaded)
                return new ErrorResponse($"Scene '{sceneName ?? "active"}' is not loaded.");

            var rootObjects = scene.GetRootGameObjects();
            int totalObjects = 0;
            var componentCounts = new Dictionary<string, int>();
            int totalComponents = 0;
            int inactiveObjects = 0;

            foreach (var root in rootObjects)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    totalObjects++;
                    if (!t.gameObject.activeSelf) inactiveObjects++;

                    foreach (var c in t.GetComponents<Component>())
                    {
                        if (c == null) continue; // Missing script
                        totalComponents++;
                        string typeName = c.GetType().Name;
                        if (!componentCounts.ContainsKey(typeName))
                            componentCounts[typeName] = 0;
                        componentCounts[typeName]++;
                    }
                }
            }

            var topComponents = componentCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(15)
                .Select(kvp => new { type = kvp.Key, count = kvp.Value })
                .ToList();

            return new SuccessResponse($"Stats for scene '{scene.name}'.", new
            {
                sceneName = scene.name,
                scenePath = scene.path,
                isDirty = scene.isDirty,
                rootObjectCount = rootObjects.Length,
                totalObjectCount = totalObjects,
                inactiveObjectCount = inactiveObjects,
                totalComponentCount = totalComponents,
                uniqueComponentTypes = componentCounts.Count,
                topComponentTypes = topComponents
            });
        }

        // ─── Helpers ────────────────────────────────────────────

        private static Scene GetOrOpenScene(string scenePath)
        {
            // Check if scene is already loaded
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == scenePath) return s;
            }

            // Open it additively
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        private static Dictionary<string, GameObject> BuildHierarchyMap(Scene scene)
        {
            var map = new Dictionary<string, GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                AddToMap(map, root, "");
            }
            return map;
        }

        private static void AddToMap(Dictionary<string, GameObject> map, GameObject go, string parentPath)
        {
            string path = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";
            map[path] = go;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                AddToMap(map, go.transform.GetChild(i).gameObject, path);
            }
        }

        private static string GetPropertyValueString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F4");
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                case SerializedPropertyType.Enum: return prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
                    ? prop.enumNames[prop.enumValueIndex] : prop.intValue.ToString();
                case SerializedPropertyType.Vector2: return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4: return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect: return prop.rectValue.ToString();
                case SerializedPropertyType.Quaternion: return prop.quaternionValue.ToString();
                default: return $"({prop.propertyType})";
            }
        }
    }
}
