using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_2d_tools", AutoRegister = false)]
    public static class Manage2DTools
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
                    case "create_tilemap":
                        return CreateTilemap(@params, p);
                    case "set_tile":
                        return SetTile(@params, p);
                    case "fill_area":
                        return FillArea(@params, p);
                    case "clear_tilemap":
                        return ClearTilemap(@params, p);
                    case "configure_tilemap":
                        return ConfigureTilemap(@params, p);
                    case "create_sprite_shape":
                        return CreateSpriteShape(@params, p);
                    case "add_sprite_mask":
                        return AddSpriteMask(@params, p);
                    case "get_tilemap_info":
                        return GetTilemapInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: create_tilemap, set_tile, fill_area, clear_tilemap, configure_tilemap, create_sprite_shape, add_sprite_mask, get_tilemap_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object CreateTilemap(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "Tilemap");
            string gridName = p.Get("grid_name");

            // Find or create a Grid
            GameObject gridGo;
            if (!string.IsNullOrEmpty(gridName))
            {
                gridGo = GameObject.Find(gridName);
                if (gridGo == null || gridGo.GetComponent<Grid>() == null)
                {
                    gridGo = new GameObject(gridName);
                    Undo.RegisterCreatedObjectUndo(gridGo, "Create Grid");
                    gridGo.AddComponent<Grid>();
                }
            }
            else
            {
                // Find existing Grid or create one
                Grid existingGrid = UnityEngine.Object.FindFirstObjectByType<Grid>();
                if (existingGrid != null)
                {
                    gridGo = existingGrid.gameObject;
                }
                else
                {
                    gridGo = new GameObject("Grid");
                    Undo.RegisterCreatedObjectUndo(gridGo, "Create Grid");
                    gridGo.AddComponent<Grid>();
                }
            }

            // Create tilemap as child of grid
            GameObject tilemapGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(tilemapGo, "Create Tilemap");
            tilemapGo.transform.SetParent(gridGo.transform);

            Tilemap tilemap = tilemapGo.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapGo.AddComponent<TilemapRenderer>();

            // Configure renderer
            string sortingLayer = p.Get("sorting_layer");
            if (!string.IsNullOrEmpty(sortingLayer))
                renderer.sortingLayerName = sortingLayer;

            int? sortingOrder = p.GetInt("sorting_order");
            if (sortingOrder.HasValue) renderer.sortingOrder = sortingOrder.Value;

            string renderMode = p.Get("render_mode");
            if (!string.IsNullOrEmpty(renderMode))
            {
                if (renderMode.Equals("individual", StringComparison.OrdinalIgnoreCase))
                    renderer.mode = TilemapRenderer.Mode.Individual;
                else if (renderMode.Equals("chunk", StringComparison.OrdinalIgnoreCase))
                    renderer.mode = TilemapRenderer.Mode.Chunk;
            }

            // Add collision if requested
            if (p.GetBool("add_collider", false))
            {
                tilemapGo.AddComponent<TilemapCollider2D>();
            }

            return new SuccessResponse($"Created Tilemap '{name}' under Grid '{gridGo.name}'", new
            {
                tilemapName = tilemapGo.name,
                gridName = gridGo.name,
                instanceId = tilemapGo.GetInstanceID(),
                hasCollider = tilemapGo.GetComponent<TilemapCollider2D>() != null
            });
        }

        private static object SetTile(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var tilePathResult = p.GetRequired("tile_path");
            var tileError = tilePathResult.GetOrError(out string tilePath);
            if (tileError != null) return tileError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Tilemap tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return new ErrorResponse($"No Tilemap on '{target}'.");

            string sanitized = AssetPathUtility.SanitizeAssetPath(tilePath);
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(sanitized);
            if (tile == null) return new ErrorResponse($"Tile not found at '{tilePath}'.");

            int x = p.GetInt("x") ?? 0;
            int y = p.GetInt("y") ?? 0;
            int z = p.GetInt("z") ?? 0;

            Undo.RecordObject(tilemap, "Set Tile");
            tilemap.SetTile(new Vector3Int(x, y, z), tile);

            return new SuccessResponse($"Set tile at ({x}, {y}, {z})", new
            {
                position = new { x, y, z },
                tile = tile.name
            });
        }

        private static object FillArea(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var tilePathResult = p.GetRequired("tile_path");
            var tileError = tilePathResult.GetOrError(out string tilePath);
            if (tileError != null) return tileError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Tilemap tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return new ErrorResponse($"No Tilemap on '{target}'.");

            string sanitized = AssetPathUtility.SanitizeAssetPath(tilePath);
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(sanitized);
            if (tile == null) return new ErrorResponse($"Tile not found at '{tilePath}'.");

            int startX = p.GetInt("start_x") ?? 0;
            int startY = p.GetInt("start_y") ?? 0;
            int endX = p.GetInt("end_x") ?? 10;
            int endY = p.GetInt("end_y") ?? 10;

            Undo.RecordObject(tilemap, "Fill Tilemap Area");

            int count = 0;
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                    count++;
                }
            }

            return new SuccessResponse($"Filled {count} tiles from ({startX},{startY}) to ({endX},{endY})", new
            {
                tilesPlaced = count,
                from = new { x = startX, y = startY },
                to = new { x = endX, y = endY }
            });
        }

        private static object ClearTilemap(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Tilemap tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return new ErrorResponse($"No Tilemap on '{target}'.");

            Undo.RecordObject(tilemap, "Clear Tilemap");
            tilemap.ClearAllTiles();

            return new SuccessResponse($"Cleared all tiles on '{target}'");
        }

        private static object ConfigureTilemap(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Tilemap tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return new ErrorResponse($"No Tilemap on '{target}'.");

            Undo.RecordObject(tilemap, "Configure Tilemap");

            JToken colorToken = p.GetRaw("color");
            if (colorToken != null)
            {
                var c = colorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                {
                    float a = c.Length >= 4 ? c[3] : 1f;
                    tilemap.color = new Color(c[0], c[1], c[2], a);
                }
            }

            JToken tileAnchorToken = p.GetRaw("tile_anchor");
            if (tileAnchorToken != null)
            {
                var ta = tileAnchorToken.ToObject<float[]>();
                if (ta != null && ta.Length >= 3)
                    tilemap.tileAnchor = new Vector3(ta[0], ta[1], ta[2]);
            }

            string orientation = p.Get("orientation");
            if (!string.IsNullOrEmpty(orientation) && Enum.TryParse<Tilemap.Orientation>(orientation, true, out var orient))
                tilemap.orientation = orient;

            // Configure renderer
            TilemapRenderer renderer = go.GetComponent<TilemapRenderer>();
            if (renderer != null)
            {
                string sortingLayer = p.Get("sorting_layer");
                if (!string.IsNullOrEmpty(sortingLayer))
                    renderer.sortingLayerName = sortingLayer;

                int? sortingOrder = p.GetInt("sorting_order");
                if (sortingOrder.HasValue) renderer.sortingOrder = sortingOrder.Value;
            }

            EditorUtility.SetDirty(tilemap);

            return new SuccessResponse($"Configured Tilemap on '{target}'");
        }

        private static object CreateSpriteShape(JObject @params, ToolParams p)
        {
            // SpriteShape requires com.unity.2d.spriteshape package
            string name = p.Get("name", "SpriteShape");

            // Check if package is available by trying to find the type
            Type spriteShapeControllerType = Type.GetType("UnityEngine.U2D.SpriteShapeController, Unity.2D.SpriteShape.Runtime");
            if (spriteShapeControllerType == null)
            {
                return new ErrorResponse(
                    "The 2D Sprite Shape package is not installed. " +
                    "Install it via Package Manager: com.unity.2d.spriteshape");
            }

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Sprite Shape");

            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            // Add SpriteShapeController via reflection
            go.AddComponent(spriteShapeControllerType);

            return new SuccessResponse($"Created SpriteShape '{name}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                note = "Use the Scene view to edit the shape's spline points."
            });
        }

        private static object AddSpriteMask(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            if (go.GetComponent<SpriteMask>() != null)
                return new ErrorResponse($"'{target}' already has a SpriteMask.");

            Undo.RecordObject(go, "Add SpriteMask");
            SpriteMask mask = go.AddComponent<SpriteMask>();

            // Set sprite
            string spritePath = p.Get("sprite_path");
            if (!string.IsNullOrEmpty(spritePath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(spritePath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sanitized);
                if (sprite != null) mask.sprite = sprite;
            }

            // Sorting range
            int? frontLayer = p.GetInt("front_sorting_order");
            if (frontLayer.HasValue) mask.frontSortingOrder = frontLayer.Value;

            int? backLayer = p.GetInt("back_sorting_order");
            if (backLayer.HasValue) mask.backSortingOrder = backLayer.Value;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added SpriteMask to '{target}'", new
            {
                name = go.name,
                sprite = mask.sprite != null ? mask.sprite.name : null
            });
        }

        private static object GetTilemapInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Tilemap tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return new ErrorResponse($"No Tilemap on '{target}'.");

            BoundsInt bounds = tilemap.cellBounds;
            int tileCount = 0;
            foreach (var pos in bounds.allPositionsWithin)
            {
                if (tilemap.HasTile(pos)) tileCount++;
            }

            TilemapRenderer renderer = go.GetComponent<TilemapRenderer>();

            return new SuccessResponse($"Tilemap info for '{target}'", new
            {
                name = go.name,
                tileCount,
                cellBounds = new
                {
                    xMin = bounds.xMin, xMax = bounds.xMax,
                    yMin = bounds.yMin, yMax = bounds.yMax,
                    zMin = bounds.zMin, zMax = bounds.zMax
                },
                cellSize = new { x = tilemap.cellSize.x, y = tilemap.cellSize.y, z = tilemap.cellSize.z },
                orientation = tilemap.orientation.ToString(),
                color = new { r = tilemap.color.r, g = tilemap.color.g, b = tilemap.color.b, a = tilemap.color.a },
                hasCollider = go.GetComponent<TilemapCollider2D>() != null,
                sortingLayer = renderer != null ? renderer.sortingLayerName : null,
                sortingOrder = renderer?.sortingOrder
            });
        }
    }
}
