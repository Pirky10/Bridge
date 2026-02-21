using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_asset_bundle")]
    public static class ManageAssetBundle
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
                    case "set_bundle_name": return SetBundleName(@params);
                    case "clear_bundle_name": return ClearBundleName(@params);
                    case "build_bundles": return BuildBundles(@params);
                    case "list_bundles": return ListBundles(@params);
                    case "get_bundle_info": return GetBundleInfo(@params);
                    case "get_bundle_dependencies": return GetBundleDependencies(@params);
                    case "clean_unused_bundles": return CleanUnusedBundles(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageAssetBundle error: {e.Message}");
            }
        }

        private static object SetBundleName(JObject @params)
        {
            string assetPath = @params["asset_path"]?.ToString();
            string bundleName = @params["bundle_name"]?.ToString();
            string variant = @params["variant"]?.ToString();

            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(bundleName))
                return new ErrorResponse("'asset_path' and 'bundle_name' are required.");

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null) return new ErrorResponse($"Asset not found at '{assetPath}'.");

            importer.assetBundleName = bundleName;
            if (!string.IsNullOrEmpty(variant))
                importer.assetBundleVariant = variant;

            importer.SaveAndReimport();

            return new SuccessResponse($"Assigned '{assetPath}' to bundle '{bundleName}'.", new
            {
                assetPath,
                bundleName,
                variant = importer.assetBundleVariant
            });
        }

        private static object ClearBundleName(JObject @params)
        {
            string assetPath = @params["asset_path"]?.ToString();
            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required.");

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null) return new ErrorResponse($"Asset not found at '{assetPath}'.");

            string previousBundle = importer.assetBundleName;
            importer.assetBundleName = "";
            importer.assetBundleVariant = "";
            importer.SaveAndReimport();

            return new SuccessResponse($"Cleared bundle assignment from '{assetPath}'.", new
            {
                assetPath,
                previousBundle
            });
        }

        private static object BuildBundles(JObject @params)
        {
            string outputPath = @params["output_path"]?.ToString() ?? "AssetBundles";
            string targetStr = @params["build_target"]?.ToString();

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (!string.IsNullOrEmpty(targetStr) && Enum.TryParse(targetStr, true, out BuildTarget parsed))
                target = parsed;

            var manifest = BuildPipeline.BuildAssetBundles(
                outputPath, BuildAssetBundleOptions.None, target
            );

            if (manifest == null)
                return new ErrorResponse("Build failed. Check Unity console for details.");

            var builtBundles = manifest.GetAllAssetBundles();

            return new SuccessResponse($"Built {builtBundles.Length} bundle(s) to '{outputPath}'.", new
            {
                outputPath,
                buildTarget = target.ToString(),
                bundles = builtBundles
            });
        }

        private static object ListBundles(JObject @params)
        {
            string[] bundleNames = AssetDatabase.GetAllAssetBundleNames();
            string[] unused = AssetDatabase.GetUnusedAssetBundleNames();

            var bundles = bundleNames.Select(name => new
            {
                name,
                assetCount = AssetDatabase.GetAssetPathsFromAssetBundle(name).Length,
                isUnused = unused.Contains(name)
            }).ToList();

            return new SuccessResponse($"Found {bundleNames.Length} bundle(s).", new
            {
                totalBundles = bundleNames.Length,
                unusedBundles = unused.Length,
                bundles
            });
        }

        private static object GetBundleInfo(JObject @params)
        {
            string bundleName = @params["bundle_name"]?.ToString();
            if (string.IsNullOrEmpty(bundleName))
                return new ErrorResponse("'bundle_name' is required.");

            string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
            if (assets.Length == 0)
                return new ErrorResponse($"No assets found in bundle '{bundleName}'. Bundle may not exist.");

            string[] dependencies = AssetDatabase.GetAssetBundleDependencies(bundleName, false);
            string[] allDependencies = AssetDatabase.GetAssetBundleDependencies(bundleName, true);

            return new SuccessResponse($"Bundle '{bundleName}' info.", new
            {
                bundleName,
                assetCount = assets.Length,
                assets,
                directDependencies = dependencies,
                allDependencies = allDependencies
            });
        }

        private static object GetBundleDependencies(JObject @params)
        {
            string bundleName = @params["bundle_name"]?.ToString();
            if (string.IsNullOrEmpty(bundleName))
                return new ErrorResponse("'bundle_name' is required.");

            bool recursive = @params["recursive"]?.ToObject<bool>() ?? true;
            string[] deps = AssetDatabase.GetAssetBundleDependencies(bundleName, recursive);

            return new SuccessResponse($"Dependencies for '{bundleName}'.", new
            {
                bundleName,
                recursive,
                dependencyCount = deps.Length,
                dependencies = deps
            });
        }

        private static object CleanUnusedBundles(JObject @params)
        {
            string[] unused = AssetDatabase.GetUnusedAssetBundleNames();
            if (unused.Length == 0)
                return new SuccessResponse("No unused bundle names to remove.");

            AssetDatabase.RemoveUnusedAssetBundleNames();

            return new SuccessResponse($"Removed {unused.Length} unused bundle name(s).", new
            {
                removedBundles = unused
            });
        }
    }
}
