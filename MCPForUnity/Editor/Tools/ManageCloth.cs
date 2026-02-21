using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_cloth", AutoRegister = false)]
    public static class ManageCloth
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
                    case "add": return AddCloth(@params, p);
                    case "configure": return ConfigureCloth(@params, p);
                    case "set_constraints": return SetConstraints(@params, p);
                    case "remove": return RemoveCloth(@params, p);
                    case "get_info": return GetInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: add, configure, set_constraints, remove, get_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object AddCloth(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            if (go.GetComponent<Cloth>() != null)
                return new ErrorResponse($"'{target}' already has a Cloth component.");

            SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr == null)
                return new ErrorResponse($"'{target}' needs a SkinnedMeshRenderer for Cloth.");

            Undo.RecordObject(go, "Add Cloth");
            Cloth cloth = go.AddComponent<Cloth>();

            float? bendingStiffness = p.GetFloat("bending_stiffness");
            if (bendingStiffness.HasValue) cloth.bendingStiffness = bendingStiffness.Value;

            float? stretchingStiffness = p.GetFloat("stretching_stiffness");
            if (stretchingStiffness.HasValue) cloth.stretchingStiffness = stretchingStiffness.Value;

            float? damping = p.GetFloat("damping");
            if (damping.HasValue) cloth.damping = damping.Value;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added Cloth to '{target}'", new
            {
                name = go.name,
                vertexCount = cloth.vertices.Length
            });
        }

        private static object ConfigureCloth(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Cloth cloth = go.GetComponent<Cloth>();
            if (cloth == null) return new ErrorResponse($"No Cloth on '{target}'.");

            Undo.RecordObject(cloth, "Configure Cloth");

            float? bendingStiffness = p.GetFloat("bending_stiffness");
            if (bendingStiffness.HasValue) cloth.bendingStiffness = bendingStiffness.Value;

            float? stretchingStiffness = p.GetFloat("stretching_stiffness");
            if (stretchingStiffness.HasValue) cloth.stretchingStiffness = stretchingStiffness.Value;

            float? damping = p.GetFloat("damping");
            if (damping.HasValue) cloth.damping = damping.Value;

            float? friction = p.GetFloat("friction");
            if (friction.HasValue) cloth.friction = friction.Value;

            float? worldVelocityScale = p.GetFloat("world_velocity_scale");
            if (worldVelocityScale.HasValue) cloth.worldVelocityScale = worldVelocityScale.Value;

            float? worldAccelerationScale = p.GetFloat("world_acceleration_scale");
            if (worldAccelerationScale.HasValue) cloth.worldAccelerationScale = worldAccelerationScale.Value;

            float? sleepThreshold = p.GetFloat("sleep_threshold");
            if (sleepThreshold.HasValue) cloth.sleepThreshold = sleepThreshold.Value;

            if (p.Has("use_gravity")) cloth.useGravity = p.GetBool("use_gravity", true);

            JToken extAccel = p.GetRaw("external_acceleration");
            if (extAccel != null)
            {
                var v = extAccel.ToObject<float[]>();
                if (v != null && v.Length >= 3)
                    cloth.externalAcceleration = new Vector3(v[0], v[1], v[2]);
            }

            JToken randomAccel = p.GetRaw("random_acceleration");
            if (randomAccel != null)
            {
                var v = randomAccel.ToObject<float[]>();
                if (v != null && v.Length >= 3)
                    cloth.randomAcceleration = new Vector3(v[0], v[1], v[2]);
            }

            EditorUtility.SetDirty(cloth);

            return new SuccessResponse($"Configured Cloth on '{target}'");
        }

        private static object SetConstraints(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Cloth cloth = go.GetComponent<Cloth>();
            if (cloth == null) return new ErrorResponse($"No Cloth on '{target}'.");

            Undo.RecordObject(cloth, "Set Cloth Constraints");

            float maxDistance = p.GetFloat("max_distance") ?? 10f;
            float collisionSphereDistance = p.GetFloat("collision_sphere_distance") ?? 0f;

            // Apply uniform constraints to all vertices
            ClothSkinningCoefficient[] coefficients = cloth.coefficients;
            for (int i = 0; i < coefficients.Length; i++)
            {
                coefficients[i].maxDistance = maxDistance;
                coefficients[i].collisionSphereDistance = collisionSphereDistance;
            }

            // Pin specific vertices if indices provided
            JToken pinnedToken = p.GetRaw("pinned_vertices");
            if (pinnedToken != null)
            {
                var indices = pinnedToken.ToObject<int[]>();
                if (indices != null)
                {
                    foreach (int idx in indices)
                    {
                        if (idx >= 0 && idx < coefficients.Length)
                            coefficients[idx].maxDistance = 0f; // Pin
                    }
                }
            }

            cloth.coefficients = coefficients;
            EditorUtility.SetDirty(cloth);

            return new SuccessResponse($"Set constraints on '{target}' ({coefficients.Length} vertices)", new
            {
                vertexCount = coefficients.Length,
                maxDistance,
                collisionSphereDistance
            });
        }

        private static object RemoveCloth(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Cloth cloth = go.GetComponent<Cloth>();
            if (cloth == null) return new ErrorResponse($"No Cloth on '{target}'.");

            Undo.DestroyObjectImmediate(cloth);

            return new SuccessResponse($"Removed Cloth from '{target}'");
        }

        private static object GetInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Cloth cloth = go.GetComponent<Cloth>();
            if (cloth == null) return new ErrorResponse($"No Cloth on '{target}'.");

            return new SuccessResponse("Cloth info", new
            {
                name = go.name,
                vertexCount = cloth.vertices.Length,
                bendingStiffness = cloth.bendingStiffness,
                stretchingStiffness = cloth.stretchingStiffness,
                damping = cloth.damping,
                friction = cloth.friction,
                useGravity = cloth.useGravity,
                worldVelocityScale = cloth.worldVelocityScale,
                worldAccelerationScale = cloth.worldAccelerationScale,
                sleepThreshold = cloth.sleepThreshold,
                externalAcceleration = new[] { cloth.externalAcceleration.x, cloth.externalAcceleration.y, cloth.externalAcceleration.z },
                colliderCount = cloth.capsuleColliders.Length + cloth.sphereColliders.Length
            });
        }
    }
}
