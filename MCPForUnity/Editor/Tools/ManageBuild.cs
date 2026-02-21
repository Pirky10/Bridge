using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_build", AutoRegister = false)]
    public static class ManageBuild
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
                    case "get_settings":
                        return GetSettings(@params, p);
                    case "set_target_platform":
                        return SetTargetPlatform(@params, p);
                    case "add_scene":
                        return AddScene(@params, p);
                    case "remove_scene":
                        return RemoveScene(@params, p);
                    case "build":
                        return Build(@params, p);
                    case "set_scripting_backend":
                        return SetScriptingBackend(@params, p);
                    case "set_company_name":
                        return SetCompanyName(@params, p);
                    case "set_product_name":
                        return SetProductName(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: get_settings, set_target_platform, add_scene, remove_scene, build, set_scripting_backend, set_company_name, set_product_name");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object GetSettings(JObject @params, ToolParams p)
        {
            var scenes = EditorBuildSettings.scenes;
            var sceneList = new List<object>();
            foreach (var scene in scenes)
            {
                sceneList.Add(new
                {
                    path = scene.path,
                    enabled = scene.enabled,
                    guid = scene.guid.ToString()
                });
            }

            return new SuccessResponse("Build settings", new
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                bundleVersion = PlayerSettings.bundleVersion,
                scenes = sceneList,
                scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString(),
                apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString()
            });
        }

        private static object SetTargetPlatform(JObject @params, ToolParams p)
        {
            var platformResult = p.GetRequired("platform");
            var platformError = platformResult.GetOrError(out string platform);
            if (platformError != null) return platformError;

            if (!Enum.TryParse<BuildTarget>(platform, true, out var buildTarget))
                return new ErrorResponse($"Unknown platform: {platform}. Examples: StandaloneWindows64, StandaloneOSX, Android, iOS, WebGL");

            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(buildTarget);
            if (!BuildPipeline.IsBuildTargetSupported(group, buildTarget))
                return new ErrorResponse($"Build target '{platform}' is not supported/installed.");

            bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(group, buildTarget);

            if (success)
            {
                return new SuccessResponse($"Switched build target to {buildTarget}", new
                {
                    buildTarget = buildTarget.ToString(),
                    buildTargetGroup = group.ToString()
                });
            }

            return new ErrorResponse($"Failed to switch to build target '{platform}'.");
        }

        private static object AddScene(JObject @params, ToolParams p)
        {
            var scenePathResult = p.GetRequired("scene_path");
            var sceneError = scenePathResult.GetOrError(out string scenePath);
            if (sceneError != null) return sceneError;

            string sanitized = AssetPathUtility.SanitizeAssetPath(scenePath);

            // Check if scene already exists in build settings
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == sanitized))
            {
                return new ErrorResponse($"Scene '{sanitized}' is already in build settings.");
            }

            // Verify the scene asset exists
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(sanitized);
            if (sceneAsset == null)
                return new ErrorResponse($"Scene asset not found at '{sanitized}'.");

            bool enabled = p.GetBool("enabled", true);
            scenes.Add(new EditorBuildSettingsScene(sanitized, enabled));
            EditorBuildSettings.scenes = scenes.ToArray();

            return new SuccessResponse($"Added scene '{sanitized}' to build settings", new
            {
                path = sanitized,
                enabled = enabled,
                index = scenes.Count - 1
            });
        }

        private static object RemoveScene(JObject @params, ToolParams p)
        {
            var scenePathResult = p.GetRequired("scene_path");
            var sceneError = scenePathResult.GetOrError(out string scenePath);
            if (sceneError != null) return sceneError;

            string sanitized = AssetPathUtility.SanitizeAssetPath(scenePath);
            var scenes = EditorBuildSettings.scenes.ToList();

            int removed = scenes.RemoveAll(s => s.path == sanitized);
            if (removed == 0)
                return new ErrorResponse($"Scene '{sanitized}' not found in build settings.");

            EditorBuildSettings.scenes = scenes.ToArray();

            return new SuccessResponse($"Removed scene '{sanitized}' from build settings");
        }

        private static object Build(JObject @params, ToolParams p)
        {
            string outputPath = p.Get("output_path");
            if (string.IsNullOrEmpty(outputPath))
                return new ErrorResponse("'output_path' parameter is required.");

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                return new ErrorResponse("No scenes enabled in build settings.");

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string platformStr = p.Get("platform");
            if (!string.IsNullOrEmpty(platformStr) && Enum.TryParse<BuildTarget>(platformStr, true, out var t))
                target = t;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            // Parse build options
            bool development = p.GetBool("development", false);
            if (development) options.options |= BuildOptions.Development;

            bool autoRun = p.GetBool("auto_run", false);
            if (autoRun) options.options |= BuildOptions.AutoRunPlayer;

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                return new SuccessResponse($"Build succeeded", new
                {
                    result = summary.result.ToString(),
                    outputPath = summary.outputPath,
                    totalSize = summary.totalSize,
                    totalTime = summary.totalTime.TotalSeconds,
                    totalErrors = summary.totalErrors,
                    totalWarnings = summary.totalWarnings,
                    platform = summary.platform.ToString()
                });
            }

            return new ErrorResponse($"Build failed with result: {summary.result}", new
            {
                totalErrors = summary.totalErrors,
                totalWarnings = summary.totalWarnings
            });
        }

        private static object SetScriptingBackend(JObject @params, ToolParams p)
        {
            var backendResult = p.GetRequired("backend");
            var backendError = backendResult.GetOrError(out string backend);
            if (backendError != null) return backendError;

            if (!Enum.TryParse<ScriptingImplementation>(backend, true, out var scriptingBackend))
                return new ErrorResponse($"Unknown scripting backend: {backend}. Valid: Mono2x, IL2CPP");

            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.SetScriptingBackend(namedTarget, scriptingBackend);

            return new SuccessResponse($"Set scripting backend to {scriptingBackend}", new
            {
                backend = scriptingBackend.ToString(),
                targetGroup = namedTarget.ToString()
            });
        }

        private static object SetCompanyName(JObject @params, ToolParams p)
        {
            var nameResult = p.GetRequired("name");
            var nameError = nameResult.GetOrError(out string name);
            if (nameError != null) return nameError;

            PlayerSettings.companyName = name;
            return new SuccessResponse($"Set company name to '{name}'");
        }

        private static object SetProductName(JObject @params, ToolParams p)
        {
            var nameResult = p.GetRequired("name");
            var nameError = nameResult.GetOrError(out string name);
            if (nameError != null) return nameError;

            PlayerSettings.productName = name;
            return new SuccessResponse($"Set product name to '{name}'");
        }
    }
}
