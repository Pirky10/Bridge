using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_physics_2d", AutoRegister = false)]
    public static class ManagePhysics2D
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
                    case "add_rigidbody2d":
                        return AddRigidbody2D(@params, p);
                    case "configure_rigidbody2d":
                        return ConfigureRigidbody2D(@params, p);
                    case "add_collider2d":
                        return AddCollider2D(@params, p);
                    case "configure_collider2d":
                        return ConfigureCollider2D(@params, p);
                    case "add_joint2d":
                        return AddJoint2D(@params, p);
                    case "create_physics_material_2d":
                        return CreatePhysicsMaterial2D(@params, p);
                    case "add_effector2d":
                        return AddEffector2D(@params, p);
                    case "raycast2d":
                        return Raycast2D(@params, p);
                    case "set_gravity2d":
                        return SetGravity2D(@params, p);
                    case "get_physics2d_info":
                        return GetPhysics2DInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid: add_rigidbody2d, configure_rigidbody2d, add_collider2d, configure_collider2d, add_joint2d, create_physics_material_2d, add_effector2d, raycast2d, set_gravity2d, get_physics2d_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static object AddRigidbody2D(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");
            if (go.GetComponent<Rigidbody2D>() != null)
                return new ErrorResponse($"'{target}' already has a Rigidbody2D.");

            Undo.RecordObject(go, "Add Rigidbody2D");
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();

            string bodyType = p.Get("body_type", "Dynamic");
            if (Enum.TryParse<RigidbodyType2D>(bodyType, true, out var bt))
                rb.bodyType = bt;

            float? mass = p.GetFloat("mass");
            if (mass.HasValue) rb.mass = mass.Value;

            float? linearDrag = p.GetFloat("linear_drag");
            if (linearDrag.HasValue) rb.linearDamping = linearDrag.Value;

            float? angularDrag = p.GetFloat("angular_drag");
            if (angularDrag.HasValue) rb.angularDamping = angularDrag.Value;

            float? gravityScale = p.GetFloat("gravity_scale");
            if (gravityScale.HasValue) rb.gravityScale = gravityScale.Value;

            if (p.Has("freeze_rotation")) rb.freezeRotation = p.GetBool("freeze_rotation", false);

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added Rigidbody2D to '{target}'", new
            {
                bodyType = rb.bodyType.ToString(),
                mass = rb.mass,
                gravityScale = rb.gravityScale
            });
        }

        private static object ConfigureRigidbody2D(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) return new ErrorResponse($"No Rigidbody2D on '{target}'.");

            Undo.RecordObject(rb, "Configure Rigidbody2D");

            string bodyType = p.Get("body_type");
            if (!string.IsNullOrEmpty(bodyType) && Enum.TryParse<RigidbodyType2D>(bodyType, true, out var bt))
                rb.bodyType = bt;

            float? mass = p.GetFloat("mass");
            if (mass.HasValue) rb.mass = mass.Value;

            float? linearDrag = p.GetFloat("linear_drag");
            if (linearDrag.HasValue) rb.linearDamping = linearDrag.Value;

            float? angularDrag = p.GetFloat("angular_drag");
            if (angularDrag.HasValue) rb.angularDamping = angularDrag.Value;

            float? gravityScale = p.GetFloat("gravity_scale");
            if (gravityScale.HasValue) rb.gravityScale = gravityScale.Value;

            if (p.Has("freeze_rotation")) rb.freezeRotation = p.GetBool("freeze_rotation", rb.freezeRotation);
            if (p.Has("simulated")) rb.simulated = p.GetBool("simulated", rb.simulated);

            string collisionDetection = p.Get("collision_detection");
            if (!string.IsNullOrEmpty(collisionDetection) && Enum.TryParse<CollisionDetectionMode2D>(collisionDetection, true, out var cd))
                rb.collisionDetectionMode = cd;

            string interpolation = p.Get("interpolation");
            if (!string.IsNullOrEmpty(interpolation) && Enum.TryParse<RigidbodyInterpolation2D>(interpolation, true, out var interp))
                rb.interpolation = interp;

            EditorUtility.SetDirty(rb);

            return new SuccessResponse($"Configured Rigidbody2D on '{target}'", new
            {
                bodyType = rb.bodyType.ToString(), mass = rb.mass,
                gravityScale = rb.gravityScale, simulated = rb.simulated
            });
        }

        private static object AddCollider2D(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            string colliderType = p.Get("collider_type", "BoxCollider2D");

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add Collider2D");
            Collider2D collider;

            switch (colliderType.ToLowerInvariant())
            {
                case "boxcollider2d": case "box":
                    var box = go.AddComponent<BoxCollider2D>();
                    JToken sizeToken = p.GetRaw("size");
                    if (sizeToken != null)
                    {
                        var s = sizeToken.ToObject<float[]>();
                        if (s != null && s.Length >= 2) box.size = new Vector2(s[0], s[1]);
                    }
                    collider = box;
                    break;

                case "circlecollider2d": case "circle":
                    var circle = go.AddComponent<CircleCollider2D>();
                    float? radius = p.GetFloat("radius");
                    if (radius.HasValue) circle.radius = radius.Value;
                    collider = circle;
                    break;

                case "capsulecollider2d": case "capsule":
                    var capsule = go.AddComponent<CapsuleCollider2D>();
                    JToken capSizeToken = p.GetRaw("size");
                    if (capSizeToken != null)
                    {
                        var s = capSizeToken.ToObject<float[]>();
                        if (s != null && s.Length >= 2) capsule.size = new Vector2(s[0], s[1]);
                    }
                    string direction = p.Get("direction", "Vertical");
                    if (Enum.TryParse<CapsuleDirection2D>(direction, true, out var dir))
                        capsule.direction = dir;
                    collider = capsule;
                    break;

                case "polygoncollider2d": case "polygon":
                    collider = go.AddComponent<PolygonCollider2D>();
                    break;

                case "edgecollider2d": case "edge":
                    collider = go.AddComponent<EdgeCollider2D>();
                    break;

                case "compositecollider2d": case "composite":
                    collider = go.AddComponent<CompositeCollider2D>();
                    break;

                default:
                    return new ErrorResponse($"Unknown collider type: {colliderType}. Valid: BoxCollider2D, CircleCollider2D, CapsuleCollider2D, PolygonCollider2D, EdgeCollider2D, CompositeCollider2D");
            }

            if (p.Has("is_trigger")) collider.isTrigger = p.GetBool("is_trigger", false);

            JToken offsetToken = p.GetRaw("offset");
            if (offsetToken != null)
            {
                var o = offsetToken.ToObject<float[]>();
                if (o != null && o.Length >= 2) collider.offset = new Vector2(o[0], o[1]);
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added {collider.GetType().Name} to '{target}'", new
            {
                type = collider.GetType().Name,
                isTrigger = collider.isTrigger,
                offset = new { x = collider.offset.x, y = collider.offset.y }
            });
        }

        private static object ConfigureCollider2D(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            string colliderType = p.Get("collider_type", "BoxCollider2D");

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Collider2D collider = null;
            switch (colliderType.ToLowerInvariant())
            {
                case "boxcollider2d": case "box":
                    collider = go.GetComponent<BoxCollider2D>(); break;
                case "circlecollider2d": case "circle":
                    collider = go.GetComponent<CircleCollider2D>(); break;
                case "capsulecollider2d": case "capsule":
                    collider = go.GetComponent<CapsuleCollider2D>(); break;
                case "polygoncollider2d": case "polygon":
                    collider = go.GetComponent<PolygonCollider2D>(); break;
                default:
                    collider = go.GetComponent<Collider2D>(); break;
            }

            if (collider == null) return new ErrorResponse($"No {colliderType} on '{target}'.");

            Undo.RecordObject(collider, "Configure Collider2D");

            if (p.Has("is_trigger")) collider.isTrigger = p.GetBool("is_trigger", collider.isTrigger);

            JToken offsetToken = p.GetRaw("offset");
            if (offsetToken != null)
            {
                var o = offsetToken.ToObject<float[]>();
                if (o != null && o.Length >= 2) collider.offset = new Vector2(o[0], o[1]);
            }

            // Type-specific configuration
            if (collider is BoxCollider2D boxCol)
            {
                JToken sizeToken = p.GetRaw("size");
                if (sizeToken != null)
                {
                    var s = sizeToken.ToObject<float[]>();
                    if (s != null && s.Length >= 2) boxCol.size = new Vector2(s[0], s[1]);
                }
            }
            else if (collider is CircleCollider2D circleCol)
            {
                float? radius = p.GetFloat("radius");
                if (radius.HasValue) circleCol.radius = radius.Value;
            }

            // Physics material
            string materialPath = p.Get("material_path");
            if (!string.IsNullOrEmpty(materialPath))
            {
                string sanitized = AssetPathUtility.SanitizeAssetPath(materialPath);
                PhysicsMaterial2D mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(sanitized);
                if (mat != null) collider.sharedMaterial = mat;
            }

            EditorUtility.SetDirty(collider);

            return new SuccessResponse($"Configured {collider.GetType().Name} on '{target}'");
        }

        private static object AddJoint2D(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            string jointType = p.Get("joint_type", "FixedJoint2D");

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add Joint2D");
            Joint2D joint;

            switch (jointType.ToLowerInvariant())
            {
                case "fixedjoint2d": case "fixed":
                    joint = go.AddComponent<FixedJoint2D>(); break;
                case "distancejoint2d": case "distance":
                    joint = go.AddComponent<DistanceJoint2D>(); break;
                case "springjoint2d": case "spring":
                    var spring = go.AddComponent<SpringJoint2D>();
                    float? frequency = p.GetFloat("frequency");
                    if (frequency.HasValue) spring.frequency = frequency.Value;
                    float? dampingRatio = p.GetFloat("damping_ratio");
                    if (dampingRatio.HasValue) spring.dampingRatio = dampingRatio.Value;
                    joint = spring;
                    break;
                case "hingejoint2d": case "hinge":
                    joint = go.AddComponent<HingeJoint2D>(); break;
                case "sliderjoint2d": case "slider":
                    joint = go.AddComponent<SliderJoint2D>(); break;
                case "wheeljoint2d": case "wheel":
                    joint = go.AddComponent<WheelJoint2D>(); break;
                case "frictionjoint2d": case "friction":
                    joint = go.AddComponent<FrictionJoint2D>(); break;
                case "relativejoint2d": case "relative":
                    joint = go.AddComponent<RelativeJoint2D>(); break;
                case "targetjoint2d": case "target_joint":
                    joint = go.AddComponent<TargetJoint2D>(); break;
                default:
                    return new ErrorResponse($"Unknown joint type: {jointType}. Valid: FixedJoint2D, DistanceJoint2D, SpringJoint2D, HingeJoint2D, SliderJoint2D, WheelJoint2D, FrictionJoint2D, RelativeJoint2D, TargetJoint2D");
            }

            // Connect to another body
            string connectedTo = p.Get("connected_to");
            if (!string.IsNullOrEmpty(connectedTo))
            {
                GameObject connectedGo = GameObject.Find(connectedTo);
                if (connectedGo != null)
                {
                    Rigidbody2D connectedRb = connectedGo.GetComponent<Rigidbody2D>();
                    if (connectedRb != null && joint is AnchoredJoint2D anchoredJoint)
                        anchoredJoint.connectedBody = connectedRb;
                }
            }

            if (p.Has("enable_collision"))
                joint.enableCollision = p.GetBool("enable_collision", false);

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added {joint.GetType().Name} to '{target}'", new
            {
                type = joint.GetType().Name,
                enableCollision = joint.enableCollision
            });
        }

        private static object CreatePhysicsMaterial2D(JObject @params, ToolParams p)
        {
            var pathResult = p.GetRequired("path");
            var pathError = pathResult.GetOrError(out string path);
            if (pathError != null) return pathError;

            string sanitized = AssetPathUtility.SanitizeAssetPath(path);
            if (!sanitized.EndsWith(".physicsMaterial2D", StringComparison.OrdinalIgnoreCase) &&
                !sanitized.EndsWith(".physicsmaterial2d", StringComparison.OrdinalIgnoreCase))
                sanitized += ".physicsMaterial2D";

            var material = new PhysicsMaterial2D();

            float? bounciness = p.GetFloat("bounciness");
            if (bounciness.HasValue) material.bounciness = bounciness.Value;

            float? friction = p.GetFloat("friction");
            if (friction.HasValue) material.friction = friction.Value;

            AssetDatabase.CreateAsset(material, sanitized);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created PhysicsMaterial2D at '{sanitized}'", new
            {
                path = sanitized,
                bounciness = material.bounciness,
                friction = material.friction
            });
        }

        private static object AddEffector2D(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return targetError;

            string effectorType = p.Get("effector_type", "AreaEffector2D");

            GameObject go = GameObject.Find(target);
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            Undo.RecordObject(go, "Add Effector2D");
            Effector2D effector;

            switch (effectorType.ToLowerInvariant())
            {
                case "areaeffector2d": case "area":
                    var area = go.AddComponent<AreaEffector2D>();
                    float? forceAngle = p.GetFloat("force_angle");
                    if (forceAngle.HasValue) area.forceAngle = forceAngle.Value;
                    float? forceMagnitude = p.GetFloat("force_magnitude");
                    if (forceMagnitude.HasValue) area.forceMagnitude = forceMagnitude.Value;
                    effector = area;
                    break;
                case "buoyancyeffector2d": case "buoyancy":
                    var buoy = go.AddComponent<BuoyancyEffector2D>();
                    float? surfaceLevel = p.GetFloat("surface_level");
                    if (surfaceLevel.HasValue) buoy.surfaceLevel = surfaceLevel.Value;
                    float? density = p.GetFloat("density");
                    if (density.HasValue) buoy.density = density.Value;
                    effector = buoy;
                    break;
                case "pointeffector2d": case "point":
                    var point = go.AddComponent<PointEffector2D>();
                    float? fMag = p.GetFloat("force_magnitude");
                    if (fMag.HasValue) point.forceMagnitude = fMag.Value;
                    effector = point;
                    break;
                case "platformeffector2d": case "platform":
                    effector = go.AddComponent<PlatformEffector2D>();
                    break;
                case "surfaceeffector2d": case "surface":
                    var surface = go.AddComponent<SurfaceEffector2D>();
                    float? speed = p.GetFloat("speed");
                    if (speed.HasValue) surface.speed = speed.Value;
                    effector = surface;
                    break;
                default:
                    return new ErrorResponse($"Unknown effector type: {effectorType}. Valid: AreaEffector2D, BuoyancyEffector2D, PointEffector2D, PlatformEffector2D, SurfaceEffector2D");
            }

            // Ensure any existing collider is set to use by effector
            Collider2D col = go.GetComponent<Collider2D>();
            if (col != null)
            {
                Undo.RecordObject(col, "Set Used By Effector");
                col.usedByEffector = true;
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added {effector.GetType().Name} to '{target}'", new
            {
                type = effector.GetType().Name
            });
        }

        private static object Raycast2D(JObject @params, ToolParams p)
        {
            JToken originToken = p.GetRaw("origin");
            JToken directionToken = p.GetRaw("direction");

            if (originToken == null || directionToken == null)
                return new ErrorResponse("'origin' [x,y] and 'direction' [x,y] required.");

            var orig = originToken.ToObject<float[]>();
            var dir = directionToken.ToObject<float[]>();

            if (orig == null || orig.Length < 2 || dir == null || dir.Length < 2)
                return new ErrorResponse("'origin' and 'direction' must be [x,y] arrays.");

            Vector2 origin = new Vector2(orig[0], orig[1]);
            Vector2 direction = new Vector2(dir[0], dir[1]);
            float distance = p.GetFloat("distance") ?? Mathf.Infinity;
            int layerMask = p.GetInt("layer_mask") ?? Physics2D.AllLayers;

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, layerMask);

            if (hit.collider != null)
            {
                return new SuccessResponse("Raycast2D hit", new
                {
                    hitObject = hit.collider.gameObject.name,
                    point = new { x = hit.point.x, y = hit.point.y },
                    normal = new { x = hit.normal.x, y = hit.normal.y },
                    distance = hit.distance
                });
            }

            return new SuccessResponse("Raycast2D no hit", new { hit = false });
        }

        private static object SetGravity2D(JObject @params, ToolParams p)
        {
            JToken gravToken = p.GetRaw("gravity");
            if (gravToken == null)
                return new ErrorResponse("'gravity' [x, y] required.");

            var g = gravToken.ToObject<float[]>();
            if (g == null || g.Length < 2)
                return new ErrorResponse("'gravity' must be [x, y].");

            Physics2D.gravity = new Vector2(g[0], g[1]);

            return new SuccessResponse($"Set 2D gravity to ({g[0]}, {g[1]})", new
            {
                gravity = new { x = Physics2D.gravity.x, y = Physics2D.gravity.y }
            });
        }

        private static object GetPhysics2DInfo(JObject @params, ToolParams p)
        {
            string target = p.Get("target");

            if (!string.IsNullOrEmpty(target))
            {
                GameObject go = GameObject.Find(target);
                if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

                var rb = go.GetComponent<Rigidbody2D>();
                var colliders = go.GetComponents<Collider2D>();
                var joints = go.GetComponents<Joint2D>();

                var colList = new List<object>();
                foreach (var c in colliders)
                    colList.Add(new { type = c.GetType().Name, isTrigger = c.isTrigger });

                var jointList = new List<object>();
                foreach (var j in joints)
                    jointList.Add(new { type = j.GetType().Name });

                return new SuccessResponse($"Physics2D info for '{target}'", new
                {
                    hasRigidbody2D = rb != null,
                    bodyType = rb != null ? rb.bodyType.ToString() : null,
                    mass = rb?.mass,
                    gravityScale = rb?.gravityScale,
                    colliders = colList,
                    joints = jointList
                });
            }

            return new SuccessResponse("Physics2D global settings", new
            {
                gravity = new { x = Physics2D.gravity.x, y = Physics2D.gravity.y },
                defaultContactOffset = Physics2D.defaultContactOffset,
                velocityIterations = Physics2D.velocityIterations,
                positionIterations = Physics2D.positionIterations
            });
        }
    }
}
