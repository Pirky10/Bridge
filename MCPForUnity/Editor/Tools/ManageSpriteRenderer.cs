using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_sprite_renderer", AutoRegister = false)]
    public static class ManageSpriteRenderer
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
                        return Create(@params, p);
                    case "set_sprite":
                        return SetSprite(@params, p);
                    case "configure":
                        return Configure(@params, p);
                    case "set_sorting":
                        return SetSorting(@params, p);
                    case "get_sprite_info":
                        return GetSpriteInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: create, set_sprite, configure, set_sorting, get_sprite_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object Create(JObject @params, ToolParams p)
        {
            string name = p.Get("name", "Sprite");

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Sprite");
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();

            // Position
            JToken posToken = p.GetRaw("position");
            if (posToken != null)
            {
                var pos = posToken.ToObject<float[]>();
                if (pos != null && pos.Length >= 3)
                    go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            // Sprite
            string spritePath = p.Get("sprite_path");
            if (!string.IsNullOrEmpty(spritePath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(spritePath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sanitized);
                if (sprite != null) renderer.sprite = sprite;
            }

            // Color
            JToken colorToken = p.GetRaw("color");
            if (colorToken != null)
            {
                var c = colorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                {
                    float a = c.Length >= 4 ? c[3] : 1f;
                    renderer.color = new Color(c[0], c[1], c[2], a);
                }
            }

            // Sorting
            string sortingLayer = p.Get("sorting_layer");
            if (!string.IsNullOrEmpty(sortingLayer))
                renderer.sortingLayerName = sortingLayer;

            int? sortingOrder = p.GetInt("sorting_order");
            if (sortingOrder.HasValue) renderer.sortingOrder = sortingOrder.Value;

            // Flip
            if (p.Has("flip_x")) renderer.flipX = p.GetBool("flip_x", false);
            if (p.Has("flip_y")) renderer.flipY = p.GetBool("flip_y", false);

            return new SuccessResponse($"Created Sprite '{name}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                sprite = renderer.sprite != null ? renderer.sprite.name : null,
                sortingLayer = renderer.sortingLayerName,
                sortingOrder = renderer.sortingOrder
            });
        }

        private static object SetSprite(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            var spritePathResult = p.GetRequired("sprite_path");
            var spriteError = spritePathResult.GetOrError(out string spritePath);
            if (spriteError != null) return spriteError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                return new ErrorResponse($"No SpriteRenderer on '{target}'.");

            string sanitized = AssetPathUtility.SanitizeAssetPath(spritePath);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sanitized);
            if (sprite == null)
                return new ErrorResponse($"Sprite not found at '{spritePath}'.");

            Undo.RecordObject(renderer, "Set Sprite");
            renderer.sprite = sprite;
            EditorUtility.SetDirty(renderer);

            return new SuccessResponse($"Set sprite '{sprite.name}' on '{target}'", new
            {
                sprite = sprite.name,
                textureSize = new { width = sprite.texture.width, height = sprite.texture.height },
                pixelsPerUnit = sprite.pixelsPerUnit
            });
        }

        private static object Configure(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                return new ErrorResponse($"No SpriteRenderer on '{target}'.");

            Undo.RecordObject(renderer, "Configure SpriteRenderer");

            JToken colorToken = p.GetRaw("color");
            if (colorToken != null)
            {
                var c = colorToken.ToObject<float[]>();
                if (c != null && c.Length >= 3)
                {
                    float a = c.Length >= 4 ? c[3] : 1f;
                    renderer.color = new Color(c[0], c[1], c[2], a);
                }
            }

            if (p.Has("flip_x")) renderer.flipX = p.GetBool("flip_x", renderer.flipX);
            if (p.Has("flip_y")) renderer.flipY = p.GetBool("flip_y", renderer.flipY);

            string drawMode = p.Get("draw_mode");
            if (!string.IsNullOrEmpty(drawMode) && Enum.TryParse<SpriteDrawMode>(drawMode, true, out var dm))
                renderer.drawMode = dm;

            string maskInteraction = p.Get("mask_interaction");
            if (!string.IsNullOrEmpty(maskInteraction) && Enum.TryParse<SpriteMaskInteraction>(maskInteraction, true, out var mi))
                renderer.maskInteraction = mi;

            EditorUtility.SetDirty(renderer);

            return new SuccessResponse($"Configured SpriteRenderer on '{target}'", new
            {
                color = new { r = renderer.color.r, g = renderer.color.g, b = renderer.color.b, a = renderer.color.a },
                flipX = renderer.flipX,
                flipY = renderer.flipY,
                drawMode = renderer.drawMode.ToString()
            });
        }

        private static object SetSorting(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                return new ErrorResponse($"No SpriteRenderer on '{target}'.");

            Undo.RecordObject(renderer, "Set Sprite Sorting");

            string sortingLayer = p.Get("sorting_layer");
            if (!string.IsNullOrEmpty(sortingLayer))
                renderer.sortingLayerName = sortingLayer;

            int? sortingOrder = p.GetInt("sorting_order");
            if (sortingOrder.HasValue)
                renderer.sortingOrder = sortingOrder.Value;

            EditorUtility.SetDirty(renderer);

            return new SuccessResponse($"Set sorting on '{target}'", new
            {
                sortingLayer = renderer.sortingLayerName,
                sortingOrder = renderer.sortingOrder
            });
        }

        private static object GetSpriteInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                return new ErrorResponse($"No SpriteRenderer on '{target}'.");

            return new SuccessResponse($"SpriteRenderer info for '{target}'", new
            {
                name = go.name,
                sprite = renderer.sprite != null ? renderer.sprite.name : null,
                spritePath = renderer.sprite != null ? AssetDatabase.GetAssetPath(renderer.sprite) : null,
                color = new { r = renderer.color.r, g = renderer.color.g, b = renderer.color.b, a = renderer.color.a },
                flipX = renderer.flipX,
                flipY = renderer.flipY,
                sortingLayer = renderer.sortingLayerName,
                sortingOrder = renderer.sortingOrder,
                drawMode = renderer.drawMode.ToString(),
                maskInteraction = renderer.maskInteraction.ToString(),
                bounds = new { x = renderer.bounds.center.x, y = renderer.bounds.center.y, z = renderer.bounds.center.z }
            });
        }
    }
}
