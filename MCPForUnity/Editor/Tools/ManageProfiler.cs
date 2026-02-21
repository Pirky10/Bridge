using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_profiler", AutoRegister = false)]
    public static class ManageProfiler
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
                    case "get_stats":
                        return GetStats(@params, p);
                    case "start_recording":
                        return StartRecording(@params, p);
                    case "stop_recording":
                        return StopRecording(@params, p);
                    case "get_memory_info":
                        return GetMemoryInfo(@params, p);
                    case "get_frame_timing":
                        return GetFrameTiming(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: get_stats, start_recording, stop_recording, get_memory_info, get_frame_timing");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object GetStats(JObject @params, ToolParams p)
        {
            bool isPlaying = Application.isPlaying;

            var stats = new Dictionary<string, object>
            {
                { "isPlaying", isPlaying },
                { "targetFrameRate", Application.targetFrameRate },
                { "platform", Application.platform.ToString() },
                { "unityVersion", Application.unityVersion },
                { "systemLanguage", Application.systemLanguage.ToString() },
                { "profilerEnabled", Profiler.enabled }
            };

            if (isPlaying)
            {
                stats["currentFps"] = 1.0f / Time.unscaledDeltaTime;
                stats["deltaTime"] = Time.deltaTime;
                stats["timeScale"] = Time.timeScale;
                stats["frameCount"] = Time.frameCount;
            }

            return new SuccessResponse("Profiler stats", stats);
        }

        private static object StartRecording(JObject @params, ToolParams p)
        {
            string logFile = p.Get("log_file");

            Profiler.enabled = true;

            if (!string.IsNullOrEmpty(logFile))
            {
                Profiler.logFile = logFile;
                Profiler.enableBinaryLog = true;
            }

            return new SuccessResponse("Profiler recording started", new
            {
                enabled = Profiler.enabled,
                logFile = Profiler.logFile
            });
        }

        private static object StopRecording(JObject @params, ToolParams p)
        {
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;

            string logFile = Profiler.logFile;
            Profiler.logFile = "";

            return new SuccessResponse("Profiler recording stopped", new
            {
                logFile = logFile,
                enabled = Profiler.enabled
            });
        }

        private static object GetMemoryInfo(JObject @params, ToolParams p)
        {
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            long totalUnused = Profiler.GetTotalUnusedReservedMemoryLong();
            long monoHeap = Profiler.GetMonoHeapSizeLong();
            long monoUsed = Profiler.GetMonoUsedSizeLong();
            long tempAllocator = Profiler.GetTempAllocatorSize();

            return new SuccessResponse("Memory info", new
            {
                totalAllocatedMB = totalAllocated / (1024f * 1024f),
                totalReservedMB = totalReserved / (1024f * 1024f),
                totalUnusedMB = totalUnused / (1024f * 1024f),
                monoHeapMB = monoHeap / (1024f * 1024f),
                monoUsedMB = monoUsed / (1024f * 1024f),
                tempAllocatorMB = tempAllocator / (1024f * 1024f),
                totalAllocatedBytes = totalAllocated,
                totalReservedBytes = totalReserved,
                monoHeapBytes = monoHeap,
                monoUsedBytes = monoUsed
            });
        }

        private static object GetFrameTiming(JObject @params, ToolParams p)
        {
            if (!Application.isPlaying)
            {
                return new SuccessResponse("Frame timing (not in play mode)", new
                {
                    isPlaying = false,
                    note = "Enter play mode for real-time frame timing data"
                });
            }

            float deltaTime = Time.deltaTime;
            float smoothDeltaTime = Time.smoothDeltaTime;
            float unscaledDeltaTime = Time.unscaledDeltaTime;
            float fps = 1.0f / unscaledDeltaTime;
            int frameCount = Time.frameCount;
            float realtimeSinceStartup = Time.realtimeSinceStartup;

            return new SuccessResponse("Frame timing", new
            {
                fps = Mathf.Round(fps * 10f) / 10f,
                deltaTimeMs = deltaTime * 1000f,
                smoothDeltaTimeMs = smoothDeltaTime * 1000f,
                unscaledDeltaTimeMs = unscaledDeltaTime * 1000f,
                frameCount,
                realtimeSinceStartup,
                timeScale = Time.timeScale
            });
        }
    }
}
