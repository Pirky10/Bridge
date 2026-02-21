using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_project_settings", AutoRegister = false)]
    public static class ManageProjectSettings
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
                    case "set_product": return SetProduct(@params, p);
                    case "set_company": return SetCompany(@params, p);
                    case "set_version": return SetVersion(@params, p);
                    case "set_icon": return SetIcon(@params, p);
                    case "set_splash": return SetSplash(@params, p);
                    case "set_resolution": return SetResolution(@params, p);
                    case "set_scripting_backend": return SetScriptingBackend(@params, p);
                    case "set_api_compatibility": return SetApiCompat(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: set_product, set_company, set_version, set_icon, set_splash, set_resolution, set_scripting_backend, set_api_compatibility, get_info");
                }
            }
            catch (Exception ex) { return new ErrorResponse(ex.Message); }
        }

        private static object SetProduct(JObject @params, ToolParams p)
        {
            var nameResult = p.GetRequired("product_name");
            var nameError = nameResult.GetOrError(out string productName);
            if (nameError != null) return nameError;
            PlayerSettings.productName = productName;
            return new SuccessResponse($"Product name set to '{productName}'");
        }

        private static object SetCompany(JObject @params, ToolParams p)
        {
            var nameResult = p.GetRequired("company_name");
            var nameError = nameResult.GetOrError(out string companyName);
            if (nameError != null) return nameError;
            PlayerSettings.companyName = companyName;
            return new SuccessResponse($"Company name set to '{companyName}'");
        }

        private static object SetVersion(JObject @params, ToolParams p)
        {
            var verResult = p.GetRequired("version");
            var verError = verResult.GetOrError(out string version);
            if (verError != null) return verError;
            PlayerSettings.bundleVersion = version;
            return new SuccessResponse($"Version set to '{version}'");
        }

        private static object SetIcon(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("icon_path");
            var pathError = pathResult.GetOrError(out string iconPath);
            if (pathError != null) return pathError;

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon == null) return new ErrorResponse($"Icon not found at '{iconPath}'.");

            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Application);
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, icons, IconKind.Application);

            return new SuccessResponse($"Set default icon from '{iconPath}'");
        }

        private static object SetSplash(JObject @params, ToolParams p)
        {
            if (p.Has("show_splash")) PlayerSettings.SplashScreen.show = p.GetBool("show_splash", true);
            string style = p.Get("splash_style");
            if (!string.IsNullOrEmpty(style))
            {
                if (style.Equals("light", StringComparison.OrdinalIgnoreCase))
                    PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
                else
                    PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;
            }
            return new SuccessResponse("Splash screen configured.");
        }

        private static object SetResolution(JObject @params, ToolParams p)
        {
            int? width = p.GetInt("width");
            int? height = p.GetInt("height");
            if (p.Has("fullscreen")) PlayerSettings.fullScreenMode = p.GetBool("fullscreen", true) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (width.HasValue) PlayerSettings.defaultScreenWidth = width.Value;
            if (height.HasValue) PlayerSettings.defaultScreenHeight = height.Value;
            if (p.Has("resizable")) PlayerSettings.resizableWindow = p.GetBool("resizable", true);
            return new SuccessResponse("Resolution settings updated.");
        }

        private static object SetScriptingBackend(JObject @params, ToolParams p)
        {
            var backendResult = p.GetRequired("backend");
            var backendError = backendResult.GetOrError(out string backend);
            if (backendError != null) return backendError;

            ScriptingImplementation impl = backend.ToLowerInvariant() switch
            {
                "il2cpp" => ScriptingImplementation.IL2CPP,
                "mono" => ScriptingImplementation.Mono2x,
                _ => ScriptingImplementation.Mono2x,
            };

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), impl);
            return new SuccessResponse($"Scripting backend set to {impl}");
        }

        private static object SetApiCompat(JObject @params, ToolParams p)
        {
            var lvlResult = p.GetRequired("level");
            var lvlError = lvlResult.GetOrError(out string level);
            if (lvlError != null) return lvlError;

            ApiCompatibilityLevel api = level.ToLowerInvariant() switch
            {
                "net_standard" or "netstandard" or "net_standard_2_1" => ApiCompatibilityLevel.NET_Standard,
                "net_framework" or "netframework" or "net_4_6" => ApiCompatibilityLevel.NET_Unity_4_8,
                _ => ApiCompatibilityLevel.NET_Standard,
            };

            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), api);
            return new SuccessResponse($"API compatibility set to {api}");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            return new SuccessResponse("Project settings", new
            {
                productName = PlayerSettings.productName,
                companyName = PlayerSettings.companyName,
                version = PlayerSettings.bundleVersion,
                defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                defaultScreenHeight = PlayerSettings.defaultScreenHeight,
                fullScreenMode = PlayerSettings.fullScreenMode.ToString(),
                colorSpace = PlayerSettings.colorSpace.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString(),
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString(),
                showSplash = PlayerSettings.SplashScreen.show,
            });
        }
    }
}
