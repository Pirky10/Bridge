using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_constraints", AutoRegister = false)]
    public static class ManageConstraints
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
                    case "add_position":
                        return AddPositionConstraint(@params, p);
                    case "add_rotation":
                        return AddRotationConstraint(@params, p);
                    case "add_scale":
                        return AddScaleConstraint(@params, p);
                    case "add_aim":
                        return AddAimConstraint(@params, p);
                    case "add_parent":
                        return AddParentConstraint(@params, p);
                    case "add_look_at":
                        return AddLookAtConstraint(@params, p);
                    case "configure":
                        return Configure(@params, p);
                    case "get_constraints_info":
                        return GetConstraintsInfo(@params, p);
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Valid actions: add_position, add_rotation, add_scale, add_aim, add_parent, add_look_at, configure, get_constraints_info");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        private static GameObject FindTarget(ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            if (targetResult.GetOrError(out string target) != null) return null;
            return GameObject.Find(target);
        }

        private static ConstraintSource CreateSource(ToolParams p, string sourceParamName = "source")
        {
            string sourceName = p.Get(sourceParamName);
            float weight = p.GetFloat("weight") ?? 1f;
            var source = new ConstraintSource();
            if (!string.IsNullOrEmpty(sourceName))
            {
                GameObject sourceGo = GameObject.Find(sourceName);
                if (sourceGo != null)
                    source.sourceTransform = sourceGo.transform;
            }
            source.weight = weight;
            return source;
        }

        private static object AddPositionConstraint(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            Undo.RecordObject(go, "Add Position Constraint");
            PositionConstraint constraint = go.AddComponent<PositionConstraint>();

            var source = CreateSource(p);
            if (source.sourceTransform != null)
                constraint.AddSource(source);

            bool activate = p.GetBool("activate", true);
            constraint.constraintActive = activate;

            if (p.Has("lock_x")) constraint.translationAxis = UpdateAxis(constraint.translationAxis, Axis.X, p.GetBool("lock_x", true));
            if (p.Has("lock_y")) constraint.translationAxis = UpdateAxis(constraint.translationAxis, Axis.Y, p.GetBool("lock_y", true));
            if (p.Has("lock_z")) constraint.translationAxis = UpdateAxis(constraint.translationAxis, Axis.Z, p.GetBool("lock_z", true));

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added PositionConstraint to '{targetName}'", new
            {
                name = go.name,
                sourceCount = constraint.sourceCount,
                active = constraint.constraintActive
            });
        }

        private static object AddRotationConstraint(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            Undo.RecordObject(go, "Add Rotation Constraint");
            RotationConstraint constraint = go.AddComponent<RotationConstraint>();

            var source = CreateSource(p);
            if (source.sourceTransform != null)
                constraint.AddSource(source);

            bool activate = p.GetBool("activate", true);
            constraint.constraintActive = activate;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added RotationConstraint to '{targetName}'", new
            {
                name = go.name,
                sourceCount = constraint.sourceCount,
                active = constraint.constraintActive
            });
        }

        private static object AddScaleConstraint(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            Undo.RecordObject(go, "Add Scale Constraint");
            ScaleConstraint constraint = go.AddComponent<ScaleConstraint>();

            var source = CreateSource(p);
            if (source.sourceTransform != null)
                constraint.AddSource(source);

            bool activate = p.GetBool("activate", true);
            constraint.constraintActive = activate;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added ScaleConstraint to '{targetName}'", new
            {
                name = go.name,
                sourceCount = constraint.sourceCount,
                active = constraint.constraintActive
            });
        }

        private static object AddAimConstraint(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            Undo.RecordObject(go, "Add Aim Constraint");
            AimConstraint constraint = go.AddComponent<AimConstraint>();

            var source = CreateSource(p);
            if (source.sourceTransform != null)
                constraint.AddSource(source);

            bool activate = p.GetBool("activate", true);
            constraint.constraintActive = activate;

            // Aim axis
            JToken aimToken = p.GetRaw("aim_vector");
            if (aimToken != null)
            {
                var v = aimToken.ToObject<float[]>();
                if (v != null && v.Length >= 3)
                    constraint.aimVector = new Vector3(v[0], v[1], v[2]);
            }

            JToken upToken = p.GetRaw("up_vector");
            if (upToken != null)
            {
                var v = upToken.ToObject<float[]>();
                if (v != null && v.Length >= 3)
                    constraint.upVector = new Vector3(v[0], v[1], v[2]);
            }

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added AimConstraint to '{targetName}'", new
            {
                name = go.name,
                sourceCount = constraint.sourceCount,
                active = constraint.constraintActive
            });
        }

        private static object AddParentConstraint(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            Undo.RecordObject(go, "Add Parent Constraint");
            ParentConstraint constraint = go.AddComponent<ParentConstraint>();

            var source = CreateSource(p);
            if (source.sourceTransform != null)
                constraint.AddSource(source);

            bool activate = p.GetBool("activate", true);
            constraint.constraintActive = activate;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added ParentConstraint to '{targetName}'", new
            {
                name = go.name,
                sourceCount = constraint.sourceCount,
                active = constraint.constraintActive
            });
        }

        private static object AddLookAtConstraint(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            Undo.RecordObject(go, "Add LookAt Constraint");
            LookAtConstraint constraint = go.AddComponent<LookAtConstraint>();

            var source = CreateSource(p);
            if (source.sourceTransform != null)
                constraint.AddSource(source);

            bool activate = p.GetBool("activate", true);
            constraint.constraintActive = activate;

            float? roll = p.GetFloat("roll");
            if (roll.HasValue) constraint.roll = roll.Value;

            EditorUtility.SetDirty(go);

            return new SuccessResponse($"Added LookAtConstraint to '{targetName}'", new
            {
                name = go.name,
                sourceCount = constraint.sourceCount,
                active = constraint.constraintActive
            });
        }

        private static object Configure(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            string constraintType = p.Get("constraint_type", "position");

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            switch (constraintType.ToLowerInvariant())
            {
                case "position":
                    var pc = go.GetComponent<PositionConstraint>();
                    if (pc == null) return new ErrorResponse($"No PositionConstraint on '{targetName}'.");
                    Undo.RecordObject(pc, "Configure Constraint");
                    if (p.Has("activate")) pc.constraintActive = p.GetBool("activate", pc.constraintActive);
                    if (p.Has("weight")) pc.weight = p.GetFloat("weight") ?? pc.weight;
                    EditorUtility.SetDirty(pc);
                    return new SuccessResponse($"Configured PositionConstraint on '{targetName}'");

                case "rotation":
                    var rc = go.GetComponent<RotationConstraint>();
                    if (rc == null) return new ErrorResponse($"No RotationConstraint on '{targetName}'.");
                    Undo.RecordObject(rc, "Configure Constraint");
                    if (p.Has("activate")) rc.constraintActive = p.GetBool("activate", rc.constraintActive);
                    if (p.Has("weight")) rc.weight = p.GetFloat("weight") ?? rc.weight;
                    EditorUtility.SetDirty(rc);
                    return new SuccessResponse($"Configured RotationConstraint on '{targetName}'");

                case "scale":
                    var sc = go.GetComponent<ScaleConstraint>();
                    if (sc == null) return new ErrorResponse($"No ScaleConstraint on '{targetName}'.");
                    Undo.RecordObject(sc, "Configure Constraint");
                    if (p.Has("activate")) sc.constraintActive = p.GetBool("activate", sc.constraintActive);
                    if (p.Has("weight")) sc.weight = p.GetFloat("weight") ?? sc.weight;
                    EditorUtility.SetDirty(sc);
                    return new SuccessResponse($"Configured ScaleConstraint on '{targetName}'");

                default:
                    return new ErrorResponse($"Unknown constraint type: {constraintType}. Valid: position, rotation, scale, aim, parent, lookat");
            }
        }

        private static object GetConstraintsInfo(JObject @params, ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string targetName);
            if (targetError != null) return targetError;

            GameObject go = GameObject.Find(targetName);
            if (go == null)
                return new ErrorResponse($"GameObject '{targetName}' not found.");

            var constraints = new List<object>();

            foreach (var c in go.GetComponents<IConstraint>())
            {
                var comp = c as Component;
                constraints.Add(new
                {
                    type = comp.GetType().Name,
                    active = c.constraintActive,
                    sourceCount = c.sourceCount,
                    weight = c.weight
                });
            }

            return new SuccessResponse($"Found {constraints.Count} constraints on '{targetName}'", new
            {
                name = go.name,
                constraints
            });
        }

        private static Axis UpdateAxis(Axis current, Axis flag, bool enabled)
        {
            if (enabled) return current | flag;
            return current & ~flag;
        }
    }
}
