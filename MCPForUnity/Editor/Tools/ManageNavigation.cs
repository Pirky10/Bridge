using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_navigation", AutoRegister = false)]
    public static class ManageNavigation
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
                    case "add_navmesh_surface":
                        return AddNavMeshSurface(@params, p);
                    case "add_agent":
                        return AddAgent(@params, p);
                    case "configure_agent":
                        return ConfigureAgent(@params, p);
                    case "add_obstacle":
                        return AddObstacle(@params, p);
                    case "configure_obstacle":
                        return ConfigureObstacle(@params, p);
                    case "add_offmesh_link":
                        return AddOffMeshLink(@params, p);
                    case "get_navigation_info":
                        return GetNavigationInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: add_navmesh_surface, add_agent, configure_agent, add_obstacle, configure_obstacle, add_offmesh_link, get_navigation_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object AddNavMeshSurface(JObject @params, ToolParams p)
        {
            string target = p.Get("target");

            GameObject go;
            if (!string.IsNullOrEmpty(target))
            {
                go = GameObject.Find(target);
                if (go == null)
                    return new ErrorResponse($"GameObject '{target}' not found.");
            }
            else
            {
                string name = p.Get("name", "NavMesh Surface");
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create NavMesh Surface");
            }

            // Mark the object as navigation static
            go.isStatic = true;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Prepared '{go.name}' for NavMesh (set NavigationStatic flag). Use Window > AI > Navigation to bake the NavMesh.", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                navigationStatic = true
            });
        }

        private static object AddAgent(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            if (go.GetComponent<NavMeshAgent>() != null)
                return new ErrorResponse($"'{target}' already has a NavMeshAgent.");

            Undo.RecordObject(go, "Add NavMeshAgent");
            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();

            float? speed = p.GetFloat("speed");
            float? angularSpeed = p.GetFloat("angular_speed");
            float? acceleration = p.GetFloat("acceleration");
            float? stoppingDistance = p.GetFloat("stopping_distance");
            float? radius = p.GetFloat("radius");
            float? height = p.GetFloat("height");
            float? baseOffset = p.GetFloat("base_offset");

            if (speed.HasValue) agent.speed = speed.Value;
            if (angularSpeed.HasValue) agent.angularSpeed = angularSpeed.Value;
            if (acceleration.HasValue) agent.acceleration = acceleration.Value;
            if (stoppingDistance.HasValue) agent.stoppingDistance = stoppingDistance.Value;
            if (radius.HasValue) agent.radius = radius.Value;
            if (height.HasValue) agent.height = height.Value;
            if (baseOffset.HasValue) agent.baseOffset = baseOffset.Value;

            bool autoTraverseOffMeshLink = p.GetBool("auto_traverse_offmesh_link", true);
            agent.autoTraverseOffMeshLink = autoTraverseOffMeshLink;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added NavMeshAgent to '{target}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                speed = agent.speed,
                radius = agent.radius,
                height = agent.height
            });
        }

        private static object ConfigureAgent(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            if (agent == null)
                return new ErrorResponse($"No NavMeshAgent on '{target}'.");

            Undo.RecordObject(agent, "Configure NavMeshAgent");

            float? speed = p.GetFloat("speed");
            float? angularSpeed = p.GetFloat("angular_speed");
            float? acceleration = p.GetFloat("acceleration");
            float? stoppingDistance = p.GetFloat("stopping_distance");
            float? radius = p.GetFloat("radius");
            float? height = p.GetFloat("height");
            float? baseOffset = p.GetFloat("base_offset");

            if (speed.HasValue) agent.speed = speed.Value;
            if (angularSpeed.HasValue) agent.angularSpeed = angularSpeed.Value;
            if (acceleration.HasValue) agent.acceleration = acceleration.Value;
            if (stoppingDistance.HasValue) agent.stoppingDistance = stoppingDistance.Value;
            if (radius.HasValue) agent.radius = radius.Value;
            if (height.HasValue) agent.height = height.Value;
            if (baseOffset.HasValue) agent.baseOffset = baseOffset.Value;

            if (p.Has("auto_traverse_offmesh_link"))
                agent.autoTraverseOffMeshLink = p.GetBool("auto_traverse_offmesh_link", agent.autoTraverseOffMeshLink);

            int? avoidancePriority = p.GetInt("avoidance_priority");
            if (avoidancePriority.HasValue) agent.avoidancePriority = avoidancePriority.Value;

            // Set destination
            JToken destToken = p.GetRaw("destination");
            if (destToken != null)
            {
                var dest = destToken.ToObject<float[]>();
                if (dest != null && dest.Length >= 3)
                    agent.SetDestination(new Vector3(dest[0], dest[1], dest[2]));
            }

            EditorUtility.SetDirty(agent);

            return new SuccessResponse($"Configured NavMeshAgent on '{target}'", new
            {
                speed = agent.speed,
                radius = agent.radius,
                height = agent.height,
                stoppingDistance = agent.stoppingDistance
            });
        }

        private static object AddObstacle(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            if (go.GetComponent<NavMeshObstacle>() != null)
                return new ErrorResponse($"'{target}' already has a NavMeshObstacle.");

            Undo.RecordObject(go, "Add NavMeshObstacle");
            NavMeshObstacle obstacle = go.AddComponent<NavMeshObstacle>();

            bool carve = p.GetBool("carve", true);
            obstacle.carving = carve;

            string shape = p.Get("shape", "box");
            switch (shape.ToLowerInvariant())
            {
                case "capsule":
                    obstacle.shape = NavMeshObstacleShape.Capsule;
                    break;
                default:
                    obstacle.shape = NavMeshObstacleShape.Box;
                    break;
            }

            JToken sizeToken = p.GetRaw("size");
            if (sizeToken != null)
            {
                var size = sizeToken.ToObject<float[]>();
                if (size != null && size.Length >= 3)
                    obstacle.size = new Vector3(size[0], size[1], size[2]);
            }

            JToken centerToken = p.GetRaw("center");
            if (centerToken != null)
            {
                var center = centerToken.ToObject<float[]>();
                if (center != null && center.Length >= 3)
                    obstacle.center = new Vector3(center[0], center[1], center[2]);
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added NavMeshObstacle to '{target}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                carving = obstacle.carving,
                shape = obstacle.shape.ToString()
            });
        }

        private static object ConfigureObstacle(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            NavMeshObstacle obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
                return new ErrorResponse($"No NavMeshObstacle on '{target}'.");

            Undo.RecordObject(obstacle, "Configure NavMeshObstacle");

            if (p.Has("carve")) obstacle.carving = p.GetBool("carve", obstacle.carving);

            JToken sizeToken = p.GetRaw("size");
            if (sizeToken != null)
            {
                var size = sizeToken.ToObject<float[]>();
                if (size != null && size.Length >= 3)
                    obstacle.size = new Vector3(size[0], size[1], size[2]);
            }

            EditorUtility.SetDirty(obstacle);

            return new SuccessResponse($"Configured NavMeshObstacle on '{target}'");
        }

        private static object AddOffMeshLink(JObject @params, ToolParams p)
        {
            var startResult = p.GetRequired("start_object");
            var startError = startResult.GetOrError(out string startName);
            if (startError != null) return startError;

            var endResult = p.GetRequired("end_object");
            var endError = endResult.GetOrError(out string endName);
            if (endError != null) return endError;

            GameObject startGo = GameObject.Find(startName);
            if (startGo == null)
                return new ErrorResponse($"Start object '{startName}' not found.");

            GameObject endGo = GameObject.Find(endName);
            if (endGo == null)
                return new ErrorResponse($"End object '{endName}' not found.");

            Undo.RecordObject(startGo, "Add OffMeshLink");
#pragma warning disable CS0618 // OffMeshLink is deprecated but NavMeshLink requires the AI Navigation package
            OffMeshLink link = startGo.AddComponent<OffMeshLink>();
            link.startTransform = startGo.transform;
            link.endTransform = endGo.transform;

            bool biDirectional = p.GetBool("bi_directional", true);
            link.biDirectional = biDirectional;

            EditorUtility.SetDirty(startGo);

            return new SuccessResponse($"Added OffMeshLink from '{startName}' to '{endName}'", new
            {
                start = startGo.name,
                end = endGo.name,
                biDirectional = link.biDirectional
            });
#pragma warning restore CS0618
        }

        private static object GetNavigationInfo(JObject @params, ToolParams p)
        {
            string target = p.Get("target");

            if (!string.IsNullOrEmpty(target))
            {
                GameObject go = GameObject.Find(target);
                if (go == null)
                    return new ErrorResponse($"GameObject '{target}' not found.");

                var info = new Dictionary<string, object>();
                info["name"] = go.name;
                info["instanceId"] = go.GetInstanceID();

                NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    info["navMeshAgent"] = new
                    {
                        speed = agent.speed,
                        angularSpeed = agent.angularSpeed,
                        acceleration = agent.acceleration,
                        stoppingDistance = agent.stoppingDistance,
                        radius = agent.radius,
                        height = agent.height,
                        baseOffset = agent.baseOffset,
                        enabled = agent.enabled
                    };
                }

                NavMeshObstacle obstacle = go.GetComponent<NavMeshObstacle>();
                if (obstacle != null)
                {
                    info["navMeshObstacle"] = new
                    {
                        carving = obstacle.carving,
                        shape = obstacle.shape.ToString(),
                        size = new { x = obstacle.size.x, y = obstacle.size.y, z = obstacle.size.z }
                    };
                }

                return new SuccessResponse($"Navigation info for '{target}'", info);
            }

            // Global navigation info
            var agents = UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            var obstacles = UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None);

            return new SuccessResponse("Navigation scene info", new
            {
                agentCount = agents.Length,
                obstacleCount = obstacles.Length,
                agents = System.Array.ConvertAll(agents, a => new { name = a.gameObject.name, instanceId = a.gameObject.GetInstanceID(), speed = a.speed }),
                obstacles = System.Array.ConvertAll(obstacles, o => new { name = o.gameObject.name, instanceId = o.gameObject.GetInstanceID(), carving = o.carving })
            });
        }
    }
}
