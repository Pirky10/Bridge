using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_terrain", AutoRegister = false)]
    public static class ManageTerrain
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
                    case "create":
                        return CreateTerrain(@params, p);
                    case "set_height":
                        return SetHeight(@params, p);
                    case "set_size":
                        return SetSize(@params, p);
                    case "set_texture":
                        return SetTexture(@params, p);
                    case "add_tree_prototype":
                        return AddTreePrototype(@params, p);
                    case "add_detail_prototype":
                        return AddDetailPrototype(@params, p);
                    case "paint_texture":
                        return PaintTexture(@params, p);
                    case "get_terrain_info":
                        return GetTerrainInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: create, set_height, set_size, set_texture, add_tree_prototype, add_detail_prototype, paint_texture, get_terrain_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static Terrain FindTerrain(ToolParams p)
        {
            string target = p.Get("target");
            if (!string.IsNullOrEmpty(target))
            {
                GameObject go = GameObject.Find(target);
                if (go != null)
                    return go.GetComponent<Terrain>();
            }
            return Terrain.activeTerrain;
        }

        private static object CreateTerrain(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "Terrain");
            float width = p.GetFloat("width") ?? 500f;
            float length = p.GetFloat("length") ?? 500f;
            float height = p.GetFloat("height") ?? 600f;
            int heightmapRes = p.GetInt("heightmap_resolution") ?? 513;

            // Ensure power-of-two plus 1
            if (heightmapRes < 33) heightmapRes = 33;
            if (heightmapRes > 4097) heightmapRes = 4097;

            TerrainData terrainData = new TerrainData();
            terrainData.heightmapResolution = heightmapRes;
            terrainData.size = new Vector3(width, height, length);

            // Save terrain data as asset
            string dataPath = p.Get("data_path", $"Assets/{name}_Data.asset");
            string sanitized = AssetPathUtility.SanitizeAssetPath(dataPath);
            AssetDatabase.CreateAsset(terrainData, sanitized);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
            terrainGo.name = name;
            Undo.RegisterCreatedObjectUndo(terrainGo, "Create Terrain");

            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    terrainGo.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created terrain '{name}'", new
            {
                name = terrainGo.name,
                instanceId = terrainGo.GetInstanceID(),
                size = new { x = terrainData.size.x, y = terrainData.size.y, z = terrainData.size.z },
                heightmapResolution = terrainData.heightmapResolution
            });
        }

        private static object SetHeight(JObject @params, ToolParams p)
        {
            Terrain terrain = FindTerrain(p);
            if (terrain == null)
                return new ErrorResponse("No terrain found. Create one first or specify 'target'.");

            TerrainData data = terrain.terrainData;
            Undo.RecordObject(data, "Set Terrain Height");

            float height = p.GetFloat("height_value") ?? 0f;
            int x = p.GetInt("x") ?? 0;
            int y = p.GetInt("y") ?? 0;
            int width = p.GetInt("width") ?? 1;
            int heightCount = p.GetInt("height_count") ?? 1;

            // Normalize height (0-1 range relative to terrain height)
            float normalizedHeight = height / data.size.y;

            float[,] heights = new float[heightCount, width];
            for (int i = 0; i < heightCount; i++)
                for (int j = 0; j < width; j++)
                    heights[i, j] = normalizedHeight;

            data.SetHeights(x, y, heights);
            EditorUtility.SetDirty(data);

            return new SuccessResponse($"Set terrain height at ({x}, {y})", new
            {
                x, y, height = normalizedHeight,
                width, heightCount
            });
        }

        private static object SetSize(JObject @params, ToolParams p)
        {
            Terrain terrain = FindTerrain(p);
            if (terrain == null)
                return new ErrorResponse("No terrain found.");

            TerrainData data = terrain.terrainData;
            Undo.RecordObject(data, "Set Terrain Size");

            float width = p.GetFloat("width") ?? data.size.x;
            float height = p.GetFloat("height") ?? data.size.y;
            float length = p.GetFloat("length") ?? data.size.z;

            data.size = new Vector3(width, height, length);
            EditorUtility.SetDirty(data);

            return new SuccessResponse("Set terrain size", new
            {
                size = new { x = data.size.x, y = data.size.y, z = data.size.z }
            });
        }

        private static object SetTexture(JObject @params, ToolParams p)
        {
            Terrain terrain = FindTerrain(p);
            if (terrain == null)
                return new ErrorResponse("No terrain found.");

            var texturePathResult = p.GetRequired("texture_path");
            var textureError = texturePathResult.GetOrError(out string texturePath);
            if (textureError != null) return textureError;

            string sanitized = AssetPathUtility.SanitizeAssetPath(texturePath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sanitized);
            if (texture == null)
                return new ErrorResponse($"Texture not found at '{texturePath}'.");

            TerrainData data = terrain.terrainData;
            Undo.RecordObject(data, "Set Terrain Texture");

            float tileWidth = p.GetFloat("tile_width") ?? 15f;
            float tileHeight = p.GetFloat("tile_height") ?? 15f;

            TerrainLayer layer = new TerrainLayer();
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(tileWidth, tileHeight);

            string normalPath = p.Get("normal_path");
            if (!string.IsNullOrEmpty(normalPath))
            {
                string normSanitized = AssetPathUtility.SanitizeAssetPath(normalPath);
                Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normSanitized);
                if (normalMap != null)
                    layer.normalMapTexture = normalMap;
            }

            string layerAssetPath = AssetPathUtility.SanitizeAssetPath(
                p.Get("layer_path", $"Assets/TerrainLayer_{texture.name}.terrainlayer"));
            AssetDatabase.CreateAsset(layer, layerAssetPath);

            var layers = new List<TerrainLayer>(data.terrainLayers);
            layers.Add(layer);
            data.terrainLayers = layers.ToArray();

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(data);

            return new SuccessResponse($"Added terrain texture '{texture.name}'", new
            {
                texture = texture.name,
                layerIndex = layers.Count - 1,
                tileSize = new { x = tileWidth, y = tileHeight }
            });
        }

        private static object AddTreePrototype(JObject @params, ToolParams p)
        {
            Terrain terrain = FindTerrain(p);
            if (terrain == null)
                return new ErrorResponse("No terrain found.");

            var prefabPathResult = p.GetRequired("prefab_path");
            var prefabError = prefabPathResult.GetOrError(out string prefabPath);
            if (prefabError != null) return prefabError;

            string sanitized = AssetPathUtility.SanitizeAssetPath(prefabPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(sanitized);
            if (prefab == null)
                return new ErrorResponse($"Prefab not found at '{prefabPath}'.");

            TerrainData data = terrain.terrainData;
            Undo.RecordObject(data, "Add Tree Prototype");

            var treePrototypes = new List<TreePrototype>(data.treePrototypes);
            TreePrototype prototype = new TreePrototype { prefab = prefab };

            float? bendFactor = p.GetFloat("bend_factor");
            if (bendFactor.HasValue) prototype.bendFactor = bendFactor.Value;

            treePrototypes.Add(prototype);
            data.treePrototypes = treePrototypes.ToArray();

            EditorUtility.SetDirty(data);

            return new SuccessResponse($"Added tree prototype '{prefab.name}'", new
            {
                prefab = prefab.name,
                prototypeIndex = treePrototypes.Count - 1
            });
        }

        private static object AddDetailPrototype(JObject @params, ToolParams p)
        {
            Terrain terrain = FindTerrain(p);
            if (terrain == null)
                return new ErrorResponse("No terrain found.");

            TerrainData data = terrain.terrainData;
            Undo.RecordObject(data, "Add Detail Prototype");

            var details = new List<DetailPrototype>(data.detailPrototypes);
            DetailPrototype prototype = new DetailPrototype();

            string texturePath = p.Get("texture_path");
            if (!string.IsNullOrEmpty(texturePath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(texturePath);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sanitized);
                if (tex != null)
                    prototype.prototypeTexture = tex;
            }

            string prefabPath = p.Get("prefab_path");
            if (!string.IsNullOrEmpty(prefabPath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(prefabPath);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(sanitized);
                if (prefab != null)
                    prototype.prototype = prefab;
            }

            float? minWidth = p.GetFloat("min_width");
            float? maxWidth = p.GetFloat("max_width");
            float? minHeight = p.GetFloat("min_height");
            float? maxHeight = p.GetFloat("max_height");

            if (minWidth.HasValue) prototype.minWidth = minWidth.Value;
            if (maxWidth.HasValue) prototype.maxWidth = maxWidth.Value;
            if (minHeight.HasValue) prototype.minHeight = minHeight.Value;
            if (maxHeight.HasValue) prototype.maxHeight = maxHeight.Value;

            details.Add(prototype);
            data.detailPrototypes = details.ToArray();

            EditorUtility.SetDirty(data);

            return new SuccessResponse("Added detail prototype", new
            {
                prototypeIndex = details.Count - 1
            });
        }

        private static object PaintTexture(JObject @params, ToolParams p)
        {
            Terrain terrain = FindTerrain(p);
            if (terrain == null)
                return new ErrorResponse("No terrain found.");

            int layerIndex = p.GetInt("layer_index") ?? 0;
            int x = p.GetInt("x") ?? 0;
            int y = p.GetInt("y") ?? 0;
            int width = p.GetInt("width") ?? 1;
            int height = p.GetInt("height") ?? 1;
            float strength = p.GetFloat("strength") ?? 1f;

            TerrainData data = terrain.terrainData;

            if (layerIndex < 0 || layerIndex >= data.terrainLayers.Length)
                return new ErrorResponse($"Layer index {layerIndex} out of range. Terrain has {data.terrainLayers.Length} layers.");

            Undo.RecordObject(data, "Paint Terrain Texture");

            int alphamapWidth = data.alphamapWidth;
            int alphamapHeight = data.alphamapHeight;
            int layerCount = data.alphamapLayers;

            // Clamp coordinates
            x = Mathf.Clamp(x, 0, alphamapWidth - 1);
            y = Mathf.Clamp(y, 0, alphamapHeight - 1);
            width = Mathf.Min(width, alphamapWidth - x);
            height = Mathf.Min(height, alphamapHeight - y);

            float[,,] alphamaps = data.GetAlphamaps(x, y, width, height);

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    // Reduce other layers proportionally
                    float remainder = 1f - strength;
                    float existingTotal = 0f;
                    for (int l = 0; l < layerCount; l++)
                    {
                        if (l != layerIndex)
                            existingTotal += alphamaps[i, j, l];
                    }

                    for (int l = 0; l < layerCount; l++)
                    {
                        if (l == layerIndex)
                        {
                            alphamaps[i, j, l] = strength;
                        }
                        else if (existingTotal > 0)
                        {
                            alphamaps[i, j, l] = alphamaps[i, j, l] / existingTotal * remainder;
                        }
                    }
                }
            }

            data.SetAlphamaps(x, y, alphamaps);
            EditorUtility.SetDirty(data);

            return new SuccessResponse($"Painted texture at ({x}, {y}) with layer {layerIndex}", new
            {
                x, y, width, height, layerIndex, strength
            });
        }

        private static object GetTerrainInfo(JObject @params, ToolParams p)
        {
            Terrain terrain = FindTerrain(p);
            if (terrain == null)
                return new ErrorResponse("No terrain found in scene.");

            TerrainData data = terrain.terrainData;

            var layers = new List<object>();
            if (data.terrainLayers != null)
            {
                for (int i = 0; i < data.terrainLayers.Length; i++)
                {
                    var layer = data.terrainLayers[i];
                    layers.Add(new
                    {
                        index = i,
                        texture = layer.diffuseTexture != null ? layer.diffuseTexture.name : null,
                        tileSize = new { x = layer.tileSize.x, y = layer.tileSize.y }
                    });
                }
            }

            return new SuccessResponse($"Terrain info for '{terrain.name}'", new
            {
                name = terrain.name,
                instanceId = terrain.gameObject.GetInstanceID(),
                size = new { x = data.size.x, y = data.size.y, z = data.size.z },
                heightmapResolution = data.heightmapResolution,
                alphamapResolution = data.alphamapResolution,
                treePrototypeCount = data.treePrototypes.Length,
                detailPrototypeCount = data.detailPrototypes.Length,
                terrainLayers = layers,
                treeInstanceCount = data.treeInstanceCount
            });
        }
    }
}
