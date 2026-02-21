using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_editor_prefs", AutoRegister = false)]
    public static class ManageEditorPrefs
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
                    case "get_editor_pref": return GetEditorPref(@params, p);
                    case "set_editor_pref": return SetEditorPref(@params, p);
                    case "delete_editor_pref": return DeleteEditorPref(@params, p);
                    case "has_editor_pref": return HasEditorPref(@params, p);
                    case "get_player_pref": return GetPlayerPref(@params, p);
                    case "set_player_pref": return SetPlayerPref(@params, p);
                    case "delete_player_pref": return DeletePlayerPref(@params, p);
                    case "delete_all_player_prefs": return DeleteAllPlayerPrefs(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: get_editor_pref, set_editor_pref, delete_editor_pref, has_editor_pref, get_player_pref, set_player_pref, delete_player_pref, delete_all_player_prefs");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object GetEditorPref(JObject @params, ToolParams p)
        {
            var keyResult = p.GetRequired("key");
            var keyError = keyResult.GetOrError(out string key);
            if (keyError != null) return keyError;
            string type = p.Get("value_type", "string").ToLowerInvariant();

            object val;
            switch (type)
            {
                case "int": val = EditorPrefs.GetInt(key); break;
                case "float": val = EditorPrefs.GetFloat(key); break;
                case "bool": val = EditorPrefs.GetBool(key); break;
                default: val = EditorPrefs.GetString(key); break;
            }
            return new SuccessResponse($"EditorPref '{key}'", new { key, value = val, type });
        }

        private static object SetEditorPref(JObject @params, ToolParams p)
        {
            var keyResult = p.GetRequired("key");
            var keyError = keyResult.GetOrError(out string key);
            if (keyError != null) return keyError;
            var valResult = p.GetRequired("value");
            var valError = valResult.GetOrError(out string value);
            if (valError != null) return valError;
            string type = p.Get("value_type", "string").ToLowerInvariant();

            switch (type)
            {
                case "int": EditorPrefs.SetInt(key, int.Parse(value)); break;
                case "float": EditorPrefs.SetFloat(key, float.Parse(value)); break;
                case "bool": EditorPrefs.SetBool(key, bool.Parse(value)); break;
                default: EditorPrefs.SetString(key, value); break;
            }
            return new SuccessResponse($"Set EditorPref '{key}' = '{value}'");
        }

        private static object DeleteEditorPref(JObject @params, ToolParams p)
        {
            var keyResult = p.GetRequired("key");
            var keyError = keyResult.GetOrError(out string key);
            if (keyError != null) return keyError;
            EditorPrefs.DeleteKey(key);
            return new SuccessResponse($"Deleted EditorPref '{key}'");
        }

        private static object HasEditorPref(JObject @params, ToolParams p)
        {
            var keyResult = p.GetRequired("key");
            var keyError = keyResult.GetOrError(out string key);
            if (keyError != null) return keyError;
            return new SuccessResponse($"HasKey '{key}'", new { key, exists = EditorPrefs.HasKey(key) });
        }

        private static object GetPlayerPref(JObject @params, ToolParams p)
        {
            var keyResult = p.GetRequired("key");
            var keyError = keyResult.GetOrError(out string key);
            if (keyError != null) return keyError;
            string type = p.Get("value_type", "string").ToLowerInvariant();
            object val;
            switch (type)
            {
                case "int": val = PlayerPrefs.GetInt(key); break;
                case "float": val = PlayerPrefs.GetFloat(key); break;
                default: val = PlayerPrefs.GetString(key); break;
            }
            return new SuccessResponse($"PlayerPref '{key}'", new { key, value = val, type });
        }

        private static object SetPlayerPref(JObject @params, ToolParams p)
        {
            var keyResult = p.GetRequired("key");
            var keyError = keyResult.GetOrError(out string key);
            if (keyError != null) return keyError;
            var valResult = p.GetRequired("value");
            var valError = valResult.GetOrError(out string value);
            if (valError != null) return valError;
            string type = p.Get("value_type", "string").ToLowerInvariant();
            switch (type)
            {
                case "int": PlayerPrefs.SetInt(key, int.Parse(value)); break;
                case "float": PlayerPrefs.SetFloat(key, float.Parse(value)); break;
                default: PlayerPrefs.SetString(key, value); break;
            }
            PlayerPrefs.Save();
            return new SuccessResponse($"Set PlayerPref '{key}' = '{value}'");
        }

        private static object DeletePlayerPref(JObject @params, ToolParams p)
        {
            var keyResult = p.GetRequired("key");
            var keyError = keyResult.GetOrError(out string key);
            if (keyError != null) return keyError;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return new SuccessResponse($"Deleted PlayerPref '{key}'");
        }

        private static object DeleteAllPlayerPrefs(JObject @params, ToolParams p)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            return new SuccessResponse("Deleted all PlayerPrefs.");
        }
    }
}
