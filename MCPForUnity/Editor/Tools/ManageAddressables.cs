using System;
using System.Collections.Generic;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_addressables", AutoRegister = false)]
    public static class ManageAddressables
    {
        // Uses reflection to avoid hard dependency on com.unity.addressables

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            var actionError = actionResult.GetOrError(out string action);
            if (actionError != null) return actionError;

            action = action.ToLowerInvariant();

            // Check if Addressables is installed
            Type settingsType = GetAddressableType("AddressableAssetSettings");
            if (settingsType == null)
                return new ErrorResponse("Addressables package not installed. Install: com.unity.addressables");

            try
            {
                switch (action)
                {
                    case "init": return Init(@params, p, settingsType);
                    case "mark_addressable": return MarkAddressable(@params, p);
                    case "create_group": return CreateGroup(@params, p, settingsType);
                    case "set_label": return SetLabel(@params, p);
                    case "set_address": return SetAddress(@params, p);
                    case "build": return Build(@params, p);
                    case "get_info": return GetInfo(@params, p, settingsType);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: init, mark_addressable, create_group, set_label, set_address, build, get_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static Type GetAddressableType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = assembly.GetType($"UnityEditor.AddressableAssets.{typeName}");
                if (t != null) return t;
                t = assembly.GetType($"UnityEditor.AddressableAssets.Settings.{typeName}");
                if (t != null) return t;
            }
            return null;
        }

        private static object GetSettings(Type settingsType)
        {
            // AddressableAssetSettingsDefaultObject.Settings
            Type defaultObjType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                defaultObjType = assembly.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
                if (defaultObjType != null) break;
            }

            if (defaultObjType == null) return null;

            var settingsProp = defaultObjType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
            return settingsProp?.GetValue(null);
        }

        private static object Init(JObject @params, ToolParams p, Type settingsType)
        {
            var settings = GetSettings(settingsType);
            if (settings != null)
                return new SuccessResponse("Addressables already initialized.");

            // Try to create default settings
            Type defaultObjType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                defaultObjType = assembly.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
                if (defaultObjType != null) break;
            }

            if (defaultObjType != null)
            {
                var getMethod = defaultObjType.GetMethod("GetSettings", BindingFlags.Public | BindingFlags.Static);
                if (getMethod != null)
                {
                    var result = getMethod.Invoke(null, new object[] { true });
                    if (result != null)
                        return new SuccessResponse("Addressables initialized.");
                }
            }

            return new ErrorResponse("Could not initialize Addressables. Try Window > Asset Management > Addressables > Groups.");
        }

        private static object MarkAddressable(JObject @params, ToolParams p)
        {
            var assetPathResult = p.GetRequired("asset_path");
            var pathError = assetPathResult.GetOrError(out string assetPath);
            if (pathError != null) return pathError;

            string sanitized = AssetPathUtility.SanitizeAssetPath(assetPath);

            // Use AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry
            Type settingsType = GetAddressableType("AddressableAssetSettings");
            var settings = GetSettings(settingsType);
            if (settings == null)
                return new ErrorResponse("Addressables not initialized. Run 'init' first.");

            string guid = AssetDatabase.AssetPathToGUID(sanitized);
            if (string.IsNullOrEmpty(guid))
                return new ErrorResponse($"Asset not found at '{sanitized}'.");

            string groupName = p.Get("group");
            string address = p.Get("address");

            // CreateOrMoveEntry(guid, group)
            var createMethod = settingsType.GetMethod("CreateOrMoveEntry",
                new Type[] { typeof(string), GetAddressableType("AddressableAssetGroup"), typeof(bool), typeof(bool) });

            if (createMethod == null)
            {
                // Try simpler overload
                createMethod = settingsType.GetMethod("CreateOrMoveEntry",
                    new Type[] { typeof(string), GetAddressableType("AddressableAssetGroup") });
            }

            object group = null;
            if (!string.IsNullOrEmpty(groupName))
            {
                var findGroupMethod = settingsType.GetMethod("FindGroup", new Type[] { typeof(string) });
                if (findGroupMethod != null)
                    group = findGroupMethod.Invoke(settings, new object[] { groupName });
            }

            if (group == null)
            {
                // Use default group
                var defaultGroupProp = settingsType.GetProperty("DefaultGroup");
                if (defaultGroupProp != null)
                    group = defaultGroupProp.GetValue(settings);
            }

            if (createMethod != null && group != null)
            {
                object entry;
                if (createMethod.GetParameters().Length == 4)
                    entry = createMethod.Invoke(settings, new object[] { guid, group, false, false });
                else
                    entry = createMethod.Invoke(settings, new object[] { guid, group });

                if (entry != null && !string.IsNullOrEmpty(address))
                {
                    var addrProp = entry.GetType().GetProperty("address");
                    if (addrProp != null) addrProp.SetValue(entry, address);
                }

                EditorUtility.SetDirty(settings as UnityEngine.Object);
                AssetDatabase.SaveAssets();

                return new SuccessResponse($"Marked '{sanitized}' as addressable", new
                {
                    path = sanitized,
                    address = address ?? sanitized,
                    group = group.ToString()
                });
            }

            return new ErrorResponse("Could not create addressable entry via reflection.");
        }

        private static object CreateGroup(JObject @params, ToolParams p, Type settingsType)
        {
            var nameResult = p.GetRequired("group_name");
            var nameError = nameResult.GetOrError(out string groupName);
            if (nameError != null) return nameError;

            var settings = GetSettings(settingsType);
            if (settings == null)
                return new ErrorResponse("Addressables not initialized.");

            var createGroupMethod = settingsType.GetMethod("CreateGroup",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(List<>).MakeGenericType(GetAddressableType("AddressableAssetGroupSchema") ?? typeof(object)) },
                null);

            // Simpler approach via reflection
            var methods = settingsType.GetMethods();
            foreach (var m in methods)
            {
                if (m.Name == "CreateGroup" && m.GetParameters().Length >= 2)
                {
                    try
                    {
                        var newGroup = m.Invoke(settings, new object[] { groupName, false, false, false, null, new Type[0] });
                        EditorUtility.SetDirty(settings as UnityEngine.Object);
                        AssetDatabase.SaveAssets();
                        return new SuccessResponse($"Created group '{groupName}'");
                    }
                    catch { continue; }
                }
            }

            return new ErrorResponse("Could not create group. Try creating via Window > Asset Management > Addressables > Groups.");
        }

        private static object SetLabel(JObject @params, ToolParams p)
        {
            var assetPathResult = p.GetRequired("asset_path");
            var pathError = assetPathResult.GetOrError(out string assetPath);
            if (pathError != null) return pathError;

            var labelResult = p.GetRequired("label");
            var labelError = labelResult.GetOrError(out string label);
            if (labelError != null) return labelError;

            Type settingsType = GetAddressableType("AddressableAssetSettings");
            var settings = GetSettings(settingsType);
            if (settings == null) return new ErrorResponse("Addressables not initialized.");

            string sanitized = AssetPathUtility.SanitizeAssetPath(assetPath);
            string guid = AssetDatabase.AssetPathToGUID(sanitized);
            if (string.IsNullOrEmpty(guid)) return new ErrorResponse($"Asset not found: {sanitized}");

            // FindAssetEntry
            var findMethod = settingsType.GetMethod("FindAssetEntry", new Type[] { typeof(string) });
            if (findMethod == null) return new ErrorResponse("FindAssetEntry method not found.");

            var entry = findMethod.Invoke(settings, new object[] { guid });
            if (entry == null) return new ErrorResponse($"Asset not addressable: {sanitized}. Mark it addressable first.");

            var setLabelMethod = entry.GetType().GetMethod("SetLabel");
            if (setLabelMethod != null)
            {
                // AddLabel to settings first
                var addLabelMethod = settingsType.GetMethod("AddLabel", new Type[] { typeof(string), typeof(bool) });
                if (addLabelMethod != null)
                    addLabelMethod.Invoke(settings, new object[] { label, true });

                setLabelMethod.Invoke(entry, new object[] { label, true, true });
                EditorUtility.SetDirty(settings as UnityEngine.Object);
                AssetDatabase.SaveAssets();

                return new SuccessResponse($"Set label '{label}' on '{sanitized}'");
            }

            return new ErrorResponse("Could not set label.");
        }

        private static object SetAddress(JObject @params, ToolParams p)
        {
            var assetPathResult = p.GetRequired("asset_path");
            var pathError = assetPathResult.GetOrError(out string assetPath);
            if (pathError != null) return pathError;

            var addressResult = p.GetRequired("address");
            var addrError = addressResult.GetOrError(out string address);
            if (addrError != null) return addrError;

            Type settingsType = GetAddressableType("AddressableAssetSettings");
            var settings = GetSettings(settingsType);
            if (settings == null) return new ErrorResponse("Addressables not initialized.");

            string sanitized = AssetPathUtility.SanitizeAssetPath(assetPath);
            string guid = AssetDatabase.AssetPathToGUID(sanitized);

            var findMethod = settingsType.GetMethod("FindAssetEntry", new Type[] { typeof(string) });
            if (findMethod == null) return new ErrorResponse("FindAssetEntry not found.");

            var entry = findMethod.Invoke(settings, new object[] { guid });
            if (entry == null) return new ErrorResponse($"Asset not addressable: {sanitized}");

            var addrProp = entry.GetType().GetProperty("address");
            if (addrProp != null)
            {
                addrProp.SetValue(entry, address);
                EditorUtility.SetDirty(settings as UnityEngine.Object);
                AssetDatabase.SaveAssets();
                return new SuccessResponse($"Set address = '{address}' on '{sanitized}'");
            }

            return new ErrorResponse("Could not set address.");
        }

        private static object Build(JObject @params, ToolParams p)
        {
            Type buildScriptType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                buildScriptType = assembly.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettings");
                if (buildScriptType != null) break;
            }

            if (buildScriptType != null)
            {
                var buildMethod = buildScriptType.GetMethod("BuildPlayerContent", BindingFlags.Public | BindingFlags.Static);
                if (buildMethod != null)
                {
                    buildMethod.Invoke(null, null);
                    return new SuccessResponse("Addressables build started. Check console for progress.");
                }
            }

            return new ErrorResponse("Could not start Addressables build. Use Window > Asset Management > Addressables > Build.");
        }

        private static object GetInfo(JObject @params, ToolParams p, Type settingsType)
        {
            var settings = GetSettings(settingsType);
            if (settings == null)
                return new ErrorResponse("Addressables not initialized.");

            var groupsProp = settingsType.GetProperty("groups");
            var labelsProp = settingsType.GetMethod("GetLabels");

            var info = new Dictionary<string, object> { { "initialized", true } };

            if (groupsProp != null)
            {
                var groups = groupsProp.GetValue(settings) as System.Collections.IList;
                if (groups != null)
                {
                    info["groupCount"] = groups.Count;
                    var groupNames = new List<string>();
                    foreach (var g in groups)
                        groupNames.Add(g.ToString());
                    info["groups"] = groupNames;
                }
            }

            if (labelsProp != null)
            {
                var labels = labelsProp.Invoke(settings, null) as System.Collections.IList;
                if (labels != null)
                {
                    var labelList = new List<string>();
                    foreach (var l in labels)
                        labelList.Add(l.ToString());
                    info["labels"] = labelList;
                }
            }

            return new SuccessResponse("Addressables info", info);
        }
    }
}
