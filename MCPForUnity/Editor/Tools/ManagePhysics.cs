using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_physics", AutoRegister = false)]
    public static class ManagePhysics
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
                    case "add_rigidbody":
                        return AddRigidbody(@params, p);
                    case "configure_rigidbody":
                        return ConfigureRigidbody(@params, p);
                    case "add_collider":
                        return AddCollider(@params, p);
                    case "configure_collider":
                        return ConfigureCollider(@params, p);
                    case "create_physics_material":
                        return CreatePhysicsMaterial(@params, p);
                    case "add_joint":
                        return AddJoint(@params, p);
                    case "configure_joint":
                        return ConfigureJoint(@params, p);
                    case "set_gravity":
                        return SetGravity(@params, p);
                    case "raycast":
                        return Raycast(@params, p);
                    case "get_physics_info":
                        return GetPhysicsInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: add_rigidbody, configure_rigidbody, add_collider, configure_collider, create_physics_material, add_joint, configure_joint, set_gravity, raycast, get_physics_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static GameObject FindTarget(ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target)) return null;
            return GameObject.Find(target);
        }

        private static object AddRigidbody(JObject @params, ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' parameter is required.");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            if (go.GetComponent<Rigidbody>() != null)
                return new ErrorResponse($"GameObject '{target}' already has a Rigidbody.");

            Undo.RecordObject(go, "Add Rigidbody");
            Rigidbody rb = go.AddComponent<Rigidbody>();

            float? mass = p.GetFloat("mass");
            float? drag = p.GetFloat("drag");
            float? angularDrag = p.GetFloat("angular_drag");
            bool useGravity = p.GetBool("use_gravity", true);
            bool isKinematic = p.GetBool("is_kinematic", false);

            if (mass.HasValue) rb.mass = mass.Value;
            if (drag.HasValue) rb.linearDamping = drag.Value;
            if (angularDrag.HasValue) rb.angularDamping = angularDrag.Value;
            rb.useGravity = useGravity;
            rb.isKinematic = isKinematic;

            string interpolation = p.Get("interpolation");
            if (!string.IsNullOrEmpty(interpolation) && Enum.TryParse<RigidbodyInterpolation>(interpolation, true, out var interp))
                rb.interpolation = interp;

            string collisionDetection = p.Get("collision_detection");
            if (!string.IsNullOrEmpty(collisionDetection) && Enum.TryParse<CollisionDetectionMode>(collisionDetection, true, out var cd))
                rb.collisionDetectionMode = cd;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added Rigidbody to '{target}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                mass = rb.mass,
                useGravity = rb.useGravity,
                isKinematic = rb.isKinematic
            });
        }

        private static object ConfigureRigidbody(JObject @params, ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' parameter is required.");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                return new ErrorResponse($"GameObject '{target}' has no Rigidbody component.");

            Undo.RecordObject(rb, "Configure Rigidbody");

            float? mass = p.GetFloat("mass");
            float? drag = p.GetFloat("drag");
            float? angularDrag = p.GetFloat("angular_drag");

            if (mass.HasValue) rb.mass = mass.Value;
            if (drag.HasValue) rb.linearDamping = drag.Value;
            if (angularDrag.HasValue) rb.angularDamping = angularDrag.Value;

            if (p.Has("use_gravity")) rb.useGravity = p.GetBool("use_gravity", rb.useGravity);
            if (p.Has("is_kinematic")) rb.isKinematic = p.GetBool("is_kinematic", rb.isKinematic);

            string interpolation = p.Get("interpolation");
            if (!string.IsNullOrEmpty(interpolation) && Enum.TryParse<RigidbodyInterpolation>(interpolation, true, out var interp))
                rb.interpolation = interp;

            string collisionDetection = p.Get("collision_detection");
            if (!string.IsNullOrEmpty(collisionDetection) && Enum.TryParse<CollisionDetectionMode>(collisionDetection, true, out var cd))
                rb.collisionDetectionMode = cd;

            JToken constraintsToken = p.GetRaw("constraints");
            if (constraintsToken != null)
            {
                RigidbodyConstraints constraints = RigidbodyConstraints.None;
                var constraintsList = constraintsToken.ToObject<string[]>();
                if (constraintsList != null)
                {
                    foreach (var c in constraintsList)
                    {
                        if (Enum.TryParse<RigidbodyConstraints>(c, true, out var constraint))
                            constraints |= constraint;
                    }
                }
                rb.constraints = constraints;
            }

            EditorUtility.SetDirty(rb);

            return new SuccessResponse($"Configured Rigidbody on '{target}'", new
            {
                mass = rb.mass,
                drag = rb.linearDamping,
                angularDrag = rb.angularDamping,
                useGravity = rb.useGravity,
                isKinematic = rb.isKinematic,
                interpolation = rb.interpolation.ToString(),
                collisionDetection = rb.collisionDetectionMode.ToString()
            });
        }

        private static object AddCollider(JObject @params, ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' parameter is required.");

            string colliderType = p.Get("collider_type", "box");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add Collider");

            Collider collider;
            switch (colliderType.ToLowerInvariant())
            {
                case "box":
                    var box = go.AddComponent<BoxCollider>();
                    JToken sizeToken = p.GetRaw("size");
                    if (sizeToken != null)
                    {
                        var size = sizeToken.ToObject<float[]>();
                        if (size != null && size.Length >= 3)
                            box.size = new Vector3(size[0], size[1], size[2]);
                    }
                    JToken centerToken = p.GetRaw("center");
                    if (centerToken != null)
                    {
                        var center = centerToken.ToObject<float[]>();
                        if (center != null && center.Length >= 3)
                            box.center = new Vector3(center[0], center[1], center[2]);
                    }
                    collider = box;
                    break;

                case "sphere":
                    var sphere = go.AddComponent<SphereCollider>();
                    float? radius = p.GetFloat("radius");
                    if (radius.HasValue) sphere.radius = radius.Value;
                    JToken sphereCenter = p.GetRaw("center");
                    if (sphereCenter != null)
                    {
                        var c = sphereCenter.ToObject<float[]>();
                        if (c != null && c.Length >= 3)
                            sphere.center = new Vector3(c[0], c[1], c[2]);
                    }
                    collider = sphere;
                    break;

                case "capsule":
                    var capsule = go.AddComponent<CapsuleCollider>();
                    float? capRadius = p.GetFloat("radius");
                    float? capHeight = p.GetFloat("height");
                    if (capRadius.HasValue) capsule.radius = capRadius.Value;
                    if (capHeight.HasValue) capsule.height = capHeight.Value;
                    int? direction = p.GetInt("direction");
                    if (direction.HasValue) capsule.direction = direction.Value;
                    collider = capsule;
                    break;

                case "mesh":
                    var meshCollider = go.AddComponent<MeshCollider>();
                    bool convex = p.GetBool("convex", false);
                    meshCollider.convex = convex;
                    collider = meshCollider;
                    break;

                default:
                    return new ErrorResponse($"Unknown collider type: {colliderType}. Valid types: box, sphere, capsule, mesh");
            }

            bool isTrigger = p.GetBool("is_trigger", false);
            collider.isTrigger = isTrigger;

            // Apply physics material
            string materialPath = p.Get("physics_material");
            if (!string.IsNullOrEmpty(materialPath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(materialPath);
                PhysicsMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(sanitized);
                if (mat != null)
                    collider.sharedMaterial = mat;
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added {colliderType} collider to '{target}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                colliderType = colliderType,
                isTrigger = collider.isTrigger
            });
        }

        private static object ConfigureCollider(JObject @params, ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' parameter is required.");

            string colliderType = p.Get("collider_type");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Collider collider = null;
            if (!string.IsNullOrEmpty(colliderType))
            {
                switch (colliderType.ToLowerInvariant())
                {
                    case "box": collider = go.GetComponent<BoxCollider>(); break;
                    case "sphere": collider = go.GetComponent<SphereCollider>(); break;
                    case "capsule": collider = go.GetComponent<CapsuleCollider>(); break;
                    case "mesh": collider = go.GetComponent<MeshCollider>(); break;
                }
            }
            else
            {
                collider = go.GetComponent<Collider>();
            }

            if (collider == null)
                return new ErrorResponse($"No matching collider found on '{target}'.");

            Undo.RecordObject(collider, "Configure Collider");

            if (p.Has("is_trigger")) collider.isTrigger = p.GetBool("is_trigger", collider.isTrigger);

            if (collider is BoxCollider box)
            {
                JToken sizeToken = p.GetRaw("size");
                if (sizeToken != null)
                {
                    var size = sizeToken.ToObject<float[]>();
                    if (size != null && size.Length >= 3)
                        box.size = new Vector3(size[0], size[1], size[2]);
                }
                JToken centerToken = p.GetRaw("center");
                if (centerToken != null)
                {
                    var center = centerToken.ToObject<float[]>();
                    if (center != null && center.Length >= 3)
                        box.center = new Vector3(center[0], center[1], center[2]);
                }
            }
            else if (collider is SphereCollider sphere)
            {
                float? radius = p.GetFloat("radius");
                if (radius.HasValue) sphere.radius = radius.Value;
            }
            else if (collider is CapsuleCollider capsule)
            {
                float? radius = p.GetFloat("radius");
                float? height = p.GetFloat("height");
                if (radius.HasValue) capsule.radius = radius.Value;
                if (height.HasValue) capsule.height = height.Value;
            }
            else if (collider is MeshCollider mesh)
            {
                if (p.Has("convex")) mesh.convex = p.GetBool("convex", mesh.convex);
            }

            string materialPath = p.Get("physics_material");
            if (!string.IsNullOrEmpty(materialPath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(materialPath);
                PhysicsMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(sanitized);
                if (mat != null)
                    collider.sharedMaterial = mat;
            }

            EditorUtility.SetDirty(collider);

            return new SuccessResponse($"Configured collider on '{target}'");
        }

        private static object CreatePhysicsMaterial(JObject @params, ToolParams p)
        {
            string path = p.Get("path");
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("'path' parameter is required (e.g., Assets/Physics/Bouncy.physicMaterial).");

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".physicMaterial", StringComparison.OrdinalIgnoreCase)
                && !sanitized.EndsWith(".physicsMaterial", StringComparison.OrdinalIgnoreCase))
            {
                sanitized += ".physicMaterial";
            }

            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(sanitized) != null)
                return new ErrorResponse($"Physics material already exists at '{sanitized}'.");

            PhysicsMaterial mat = new PhysicsMaterial();
            float? dynamicFriction = p.GetFloat("dynamic_friction");
            float? staticFriction = p.GetFloat("static_friction");
            float? bounciness = p.GetFloat("bounciness");

            if (dynamicFriction.HasValue) mat.dynamicFriction = dynamicFriction.Value;
            if (staticFriction.HasValue) mat.staticFriction = staticFriction.Value;
            if (bounciness.HasValue) mat.bounciness = bounciness.Value;

            string frictionCombine = p.Get("friction_combine");
            if (!string.IsNullOrEmpty(frictionCombine) && Enum.TryParse<PhysicsMaterialCombine>(frictionCombine, true, out var fc))
                mat.frictionCombine = fc;

            string bounceCombine = p.Get("bounce_combine");
            if (!string.IsNullOrEmpty(bounceCombine) && Enum.TryParse<PhysicsMaterialCombine>(bounceCombine, true, out var bc))
                mat.bounceCombine = bc;

            AssetDatabase.CreateAsset(mat, sanitized);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created physics material at '{sanitized}'", new
            {
                path = sanitized,
                dynamicFriction = mat.dynamicFriction,
                staticFriction = mat.staticFriction,
                bounciness = mat.bounciness
            });
        }

        private static object AddJoint(JObject @params, ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' parameter is required.");

            string jointType = p.Get("joint_type", "fixed");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add Joint");

            Joint joint;
            switch (jointType.ToLowerInvariant())
            {
                case "fixed":
                    joint = go.AddComponent<FixedJoint>();
                    break;
                case "hinge":
                    var hinge = go.AddComponent<HingeJoint>();
                    JToken axisToken = p.GetRaw("axis");
                    if (axisToken != null)
                    {
                        var axis = axisToken.ToObject<float[]>();
                        if (axis != null && axis.Length >= 3)
                            hinge.axis = new Vector3(axis[0], axis[1], axis[2]);
                    }
                    joint = hinge;
                    break;
                case "spring":
                    var spring = go.AddComponent<SpringJoint>();
                    float? springConst = p.GetFloat("spring_force");
                    float? damper = p.GetFloat("damper");
                    if (springConst.HasValue) spring.spring = springConst.Value;
                    if (damper.HasValue) spring.damper = damper.Value;
                    joint = spring;
                    break;
                case "character":
                    var charJoint = go.AddComponent<CharacterJoint>();
                    joint = charJoint;
                    break;
                case "configurable":
                    joint = go.AddComponent<ConfigurableJoint>();
                    break;
                default:
                    return new ErrorResponse($"Unknown joint type: {jointType}. Valid types: fixed, hinge, spring, character, configurable");
            }

            // Connect to another body
            string connectedBody = p.Get("connected_body");
            if (!string.IsNullOrEmpty(connectedBody))
            {
                GameObject connected = GameObject.Find(connectedBody);
                if (connected != null)
                {
                    Rigidbody connectedRb = connected.GetComponent<Rigidbody>();
                    if (connectedRb != null)
                        joint.connectedBody = connectedRb;
                }
            }

            float? breakForce = p.GetFloat("break_force");
            float? breakTorque = p.GetFloat("break_torque");
            if (breakForce.HasValue) joint.breakForce = breakForce.Value;
            if (breakTorque.HasValue) joint.breakTorque = breakTorque.Value;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added {jointType} joint to '{target}'", new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                jointType = jointType
            });
        }

        private static object ConfigureJoint(JObject @params, ToolParams p)
        {
            string target = p.Get("target");
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' parameter is required.");

            GameObject go = GameObject.Find(target);
            if (go == null)
                return new ErrorResponse($"GameObject '{target}' not found.");

            Joint joint = go.GetComponent<Joint>();
            if (joint == null)
                return new ErrorResponse($"No joint found on '{target}'.");

            Undo.RecordObject(joint, "Configure Joint");

            float? breakForce = p.GetFloat("break_force");
            float? breakTorque = p.GetFloat("break_torque");
            if (breakForce.HasValue) joint.breakForce = breakForce.Value;
            if (breakTorque.HasValue) joint.breakTorque = breakTorque.Value;

            string connectedBody = p.Get("connected_body");
            if (!string.IsNullOrEmpty(connectedBody))
            {
                GameObject connected = GameObject.Find(connectedBody);
                if (connected != null)
                {
                    Rigidbody connectedRb = connected.GetComponent<Rigidbody>();
                    if (connectedRb != null)
                        joint.connectedBody = connectedRb;
                }
            }

            EditorUtility.SetDirty(joint);

            return new SuccessResponse($"Configured joint on '{target}'");
        }

        private static object SetGravity(JObject @params, ToolParams p)
        {
            JToken gravityToken = p.GetRaw("gravity");
            if (gravityToken == null)
                return new ErrorResponse("'gravity' parameter is required as [x, y, z] array.");

            var gravity = gravityToken.ToObject<float[]>();
            if (gravity == null || gravity.Length < 3)
                return new ErrorResponse("'gravity' must be an array of 3 floats [x, y, z].");

            Physics.gravity = new Vector3(gravity[0], gravity[1], gravity[2]);

            return new SuccessResponse($"Set gravity to ({gravity[0]}, {gravity[1]}, {gravity[2]})", new
            {
                gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z }
            });
        }

        private static object Raycast(JObject @params, ToolParams p)
        {
            JToken originToken = p.GetRaw("origin");
            JToken directionToken = p.GetRaw("direction");

            if (originToken == null || directionToken == null)
                return new ErrorResponse("'origin' and 'direction' parameters are required as [x, y, z] arrays.");

            var origin = originToken.ToObject<float[]>();
            var direction = directionToken.ToObject<float[]>();

            if (origin == null || origin.Length < 3 || direction == null || direction.Length < 3)
                return new ErrorResponse("'origin' and 'direction' must be arrays of 3 floats [x, y, z].");

            float maxDistance = p.GetFloat("max_distance") ?? Mathf.Infinity;

            Vector3 rayOrigin = new Vector3(origin[0], origin[1], origin[2]);
            Vector3 rayDir = new Vector3(direction[0], direction[1], direction[2]).normalized;

            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, maxDistance))
            {
                return new SuccessResponse("Raycast hit", new
                {
                    hit = true,
                    point = new { x = hit.point.x, y = hit.point.y, z = hit.point.z },
                    normal = new { x = hit.normal.x, y = hit.normal.y, z = hit.normal.z },
                    distance = hit.distance,
                    gameObject = hit.collider.gameObject.name,
                    instanceId = hit.collider.gameObject.GetInstanceID(),
                    collider = hit.collider.GetType().Name
                });
            }

            return new SuccessResponse("Raycast missed", new { hit = false });
        }

        private static object GetPhysicsInfo(JObject @params, ToolParams p)
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

                Rigidbody rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    info["rigidbody"] = new
                    {
                        mass = rb.mass,
                        drag = rb.linearDamping,
                        angularDrag = rb.angularDamping,
                        useGravity = rb.useGravity,
                        isKinematic = rb.isKinematic,
                        interpolation = rb.interpolation.ToString(),
                        collisionDetection = rb.collisionDetectionMode.ToString(),
                        constraints = rb.constraints.ToString()
                    };
                }

                var colliders = go.GetComponents<Collider>();
                if (colliders.Length > 0)
                {
                    var colliderInfos = new List<object>();
                    foreach (var c in colliders)
                    {
                        var ci = new Dictionary<string, object>
                        {
                            { "type", c.GetType().Name },
                            { "isTrigger", c.isTrigger },
                            { "enabled", c.enabled }
                        };
                        if (c is BoxCollider box)
                        {
                            ci["size"] = new { x = box.size.x, y = box.size.y, z = box.size.z };
                            ci["center"] = new { x = box.center.x, y = box.center.y, z = box.center.z };
                        }
                        else if (c is SphereCollider sphere)
                        {
                            ci["radius"] = sphere.radius;
                            ci["center"] = new { x = sphere.center.x, y = sphere.center.y, z = sphere.center.z };
                        }
                        else if (c is CapsuleCollider capsule)
                        {
                            ci["radius"] = capsule.radius;
                            ci["height"] = capsule.height;
                            ci["direction"] = capsule.direction;
                        }
                        else if (c is MeshCollider mesh)
                        {
                            ci["convex"] = mesh.convex;
                        }
                        if (c.sharedMaterial != null)
                            ci["physicsMaterial"] = c.sharedMaterial.name;
                        colliderInfos.Add(ci);
                    }
                    info["colliders"] = colliderInfos;
                }

                var joints = go.GetComponents<Joint>();
                if (joints.Length > 0)
                {
                    var jointInfos = new List<object>();
                    foreach (var j in joints)
                    {
                        jointInfos.Add(new
                        {
                            type = j.GetType().Name,
                            connectedBody = j.connectedBody != null ? j.connectedBody.gameObject.name : null,
                            breakForce = j.breakForce,
                            breakTorque = j.breakTorque
                        });
                    }
                    info["joints"] = jointInfos;
                }

                return new SuccessResponse($"Physics info for '{target}'", info);
            }

            // Global physics info
            return new SuccessResponse("Global physics settings", new
            {
                gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                defaultSolverIterations = Physics.defaultSolverIterations,
                defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
                bounceThreshold = Physics.bounceThreshold,
                sleepThreshold = Physics.sleepThreshold,
                defaultContactOffset = Physics.defaultContactOffset
            });
        }
    }
}
