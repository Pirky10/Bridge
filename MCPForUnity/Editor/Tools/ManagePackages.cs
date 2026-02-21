using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_packages", AutoRegister = false)]
    public static class ManagePackages
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
                    case "list":
                        return ListPackages(@params, p);
                    case "install":
                        return InstallPackage(@params, p);
                    case "remove":
                        return RemovePackage(@params, p);
                    case "search":
                        return SearchPackage(@params, p);
                    case "get_info":
                        return GetPackageInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: list, install, remove, search, get_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static bool WaitForRequest(Request request, int timeoutMs = 30000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!request.IsCompleted && sw.ElapsedMilliseconds < timeoutMs)
            {
                System.Threading.Thread.Sleep(100);
            }
            return request.IsCompleted;
        }

        private static object ListPackages(JObject @params, ToolParams p)
        {
            bool offlineMode = p.GetBool("offline_mode", false);
            var listRequest = Client.List(offlineMode);

            if (!WaitForRequest(listRequest))
                return new ErrorResponse("Package list request timed out.");

            if (listRequest.Status == StatusCode.Failure)
                return new ErrorResponse($"Failed to list packages: {listRequest.Error?.message}");

            var packages = new List<object>();
            foreach (var pkg in listRequest.Result)
            {
                packages.Add(new
                {
                    name = pkg.name,
                    version = pkg.version,
                    displayName = pkg.displayName,
                    source = pkg.source.ToString(),
                    status = pkg.status.ToString()
                });
            }

            return new SuccessResponse($"Found {packages.Count} packages", new { packages });
        }

        private static object InstallPackage(JObject @params, ToolParams p)
        {
            var packageResult = p.GetRequired("package_id");
            var packageError = packageResult.GetOrError(out string packageId);
            if (packageError != null) return packageError;

            var addRequest = Client.Add(packageId);

            if (!WaitForRequest(addRequest))
                return new ErrorResponse("Package install request timed out.");

            if (addRequest.Status == StatusCode.Failure)
                return new ErrorResponse($"Failed to install '{packageId}': {addRequest.Error?.message}");

            return new SuccessResponse($"Installed package '{packageId}'", new
            {
                name = addRequest.Result.name,
                version = addRequest.Result.version,
                displayName = addRequest.Result.displayName
            });
        }

        private static object RemovePackage(JObject @params, ToolParams p)
        {
            var packageResult = p.GetRequired("package_id");
            var packageError = packageResult.GetOrError(out string packageId);
            if (packageError != null) return packageError;

            var removeRequest = Client.Remove(packageId);

            if (!WaitForRequest(removeRequest))
                return new ErrorResponse("Package remove request timed out.");

            if (removeRequest.Status == StatusCode.Failure)
                return new ErrorResponse($"Failed to remove '{packageId}': {removeRequest.Error?.message}");

            return new SuccessResponse($"Removed package '{packageId}'");
        }

        private static object SearchPackage(JObject @params, ToolParams p)
        {
            var queryResult = p.GetRequired("query");
            var queryError = queryResult.GetOrError(out string query);
            if (queryError != null) return queryError;

            var searchRequest = Client.SearchAll(query);

            if (!WaitForRequest(searchRequest))
                return new ErrorResponse("Package search request timed out.");

            if (searchRequest.Status == StatusCode.Failure)
                return new ErrorResponse($"Search failed: {searchRequest.Error?.message}");

            var results = new List<object>();
            foreach (var pkg in searchRequest.Result)
            {
                results.Add(new
                {
                    name = pkg.name,
                    version = pkg.version,
                    displayName = pkg.displayName,
                    description = pkg.description
                });
            }

            return new SuccessResponse($"Found {results.Count} packages matching '{query}'", new { packages = results });
        }

        private static object GetPackageInfo(JObject @params, ToolParams p)
        {
            var packageResult = p.GetRequired("package_id");
            var packageError = packageResult.GetOrError(out string packageId);
            if (packageError != null) return packageError;

            // List installed and find the package
            var listRequest = Client.List(true);

            if (!WaitForRequest(listRequest))
                return new ErrorResponse("Package info request timed out.");

            if (listRequest.Status == StatusCode.Failure)
                return new ErrorResponse($"Failed to get package info: {listRequest.Error?.message}");

            foreach (var pkg in listRequest.Result)
            {
                if (pkg.name == packageId || pkg.packageId == packageId)
                {
                    return new SuccessResponse($"Package info for '{packageId}'", new
                    {
                        name = pkg.name,
                        version = pkg.version,
                        displayName = pkg.displayName,
                        description = pkg.description,
                        source = pkg.source.ToString(),
                        status = pkg.status.ToString(),
                        resolvedPath = pkg.resolvedPath,
                        dependencies = System.Array.ConvertAll(
                            pkg.dependencies ?? new UnityEditor.PackageManager.DependencyInfo[0],
                            d => new { name = d.name, version = d.version })
                    });
                }
            }

            return new ErrorResponse($"Package '{packageId}' not found among installed packages.");
        }
    }
}
