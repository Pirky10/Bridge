using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_mesh")]
    public static class ManageMesh
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
                    case "get_mesh_info": return GetMeshInfo(@params);
                    case "create_procedural_mesh": return CreateProceduralMesh(@params);
                    case "combine_meshes": return CombineMeshes(@params);
                    case "set_mesh_data": return SetMeshData(@params);
                    case "export_mesh_data": return ExportMeshData(@params);
                    default: return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"ManageMesh error: {e.Message}");
            }
        }

        private static object GetMeshInfo(JObject @params)
        {
            string target = @params["target"]?.ToString();
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' is required.");

            var go = GameObjectLookup.FindByTarget(new JValue(target), "by_id_or_name_or_path");
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            var mf = go.GetComponent<MeshFilter>();
            var smr = go.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);

            if (mesh == null) return new ErrorResponse($"No mesh found on '{go.name}'.");

            return new SuccessResponse($"Mesh info for '{go.name}'.", new
            {
                meshName = mesh.name,
                vertexCount = mesh.vertexCount,
                triangleCount = mesh.triangles.Length / 3,
                subMeshCount = mesh.subMeshCount,
                bounds = new { center = new float[] { mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.center.z },
                               size = new float[] { mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z } },
                hasNormals = mesh.normals != null && mesh.normals.Length > 0,
                hasUVs = mesh.uv != null && mesh.uv.Length > 0,
                hasTangents = mesh.tangents != null && mesh.tangents.Length > 0,
                hasColors = mesh.colors != null && mesh.colors.Length > 0,
                isReadable = mesh.isReadable
            });
        }

        private static object CreateProceduralMesh(JObject @params)
        {
            string meshType = @params["mesh_type"]?.ToString()?.ToLowerInvariant() ?? "plane";
            string name = @params["name"]?.ToString() ?? $"Procedural_{meshType}";
            int segments = @params["segments"]?.ToObject<int>() ?? 16;
            float radius = @params["radius"]?.ToObject<float>() ?? 1f;
            float width = @params["width"]?.ToObject<float>() ?? 1f;
            float height = @params["height"]?.ToObject<float>() ?? 1f;

            Mesh mesh = new Mesh { name = name };

            switch (meshType)
            {
                case "plane":
                    CreatePlaneMesh(mesh, width, height, segments);
                    break;
                case "circle":
                    CreateCircleMesh(mesh, radius, segments);
                    break;
                default:
                    CreatePlaneMesh(mesh, width, height, segments);
                    break;
            }

            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            return new SuccessResponse($"Created procedural {meshType} mesh '{name}'.", new
            {
                instanceID = go.GetInstanceID(),
                name = go.name,
                vertexCount = mesh.vertexCount,
                triangleCount = mesh.triangles.Length / 3
            });
        }

        private static void CreatePlaneMesh(Mesh mesh, float width, float height, int segments)
        {
            int vertCount = (segments + 1) * (segments + 1);
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segments * segments * 6];

            for (int z = 0; z <= segments; z++)
            {
                for (int x = 0; x <= segments; x++)
                {
                    int i = z * (segments + 1) + x;
                    verts[i] = new Vector3((float)x / segments * width - width / 2, 0, (float)z / segments * height - height / 2);
                    uvs[i] = new Vector2((float)x / segments, (float)z / segments);
                }
            }

            int tri = 0;
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int bl = z * (segments + 1) + x;
                    int br = bl + 1;
                    int tl = bl + segments + 1;
                    int tr = tl + 1;
                    tris[tri++] = bl; tris[tri++] = tl; tris[tri++] = tr;
                    tris[tri++] = bl; tris[tri++] = tr; tris[tri++] = br;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static void CreateCircleMesh(Mesh mesh, float radius, int segments)
        {
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var tris = new int[(segments - 1) * 3];

            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / (segments - 1) * Mathf.PI * 2;
                verts[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                uvs[i + 1] = new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f);
            }

            for (int i = 0; i < segments - 1; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static object CombineMeshes(JObject @params)
        {
            var targets = @params["targets"]?.ToObject<List<string>>();
            if (targets == null || targets.Count < 2)
                return new ErrorResponse("'targets' must contain at least 2 GameObjects.");

            string name = @params["name"]?.ToString() ?? "CombinedMesh";
            bool mergeSubmeshes = @params["merge_submeshes"]?.ToObject<bool>() ?? true;

            var combines = new List<CombineInstance>();
            foreach (var t in targets)
            {
                var go = GameObjectLookup.FindByTarget(new JValue(t), "by_id_or_name_or_path");
                if (go == null) continue;
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                combines.Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    transform = mf.transform.localToWorldMatrix
                });
            }

            if (combines.Count == 0)
                return new ErrorResponse("No valid meshes found to combine.");

            var combined = new Mesh { name = name };
            combined.CombineMeshes(combines.ToArray(), mergeSubmeshes);

            var newGo = new GameObject(name);
            var newMf = newGo.AddComponent<MeshFilter>();
            var newMr = newGo.AddComponent<MeshRenderer>();
            newMf.sharedMesh = combined;
            newMr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            Undo.RegisterCreatedObjectUndo(newGo, $"Combine Meshes into {name}");

            return new SuccessResponse($"Combined {combines.Count} meshes into '{name}'.", new
            {
                instanceID = newGo.GetInstanceID(),
                vertexCount = combined.vertexCount,
                triangleCount = combined.triangles.Length / 3
            });
        }

        private static object SetMeshData(JObject @params)
        {
            string target = @params["target"]?.ToString();
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' is required.");

            var go = GameObjectLookup.FindByTarget(new JValue(target), "by_id_or_name_or_path");
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = go.AddComponent<MeshFilter>();
                if (go.GetComponent<MeshRenderer>() == null)
                    go.AddComponent<MeshRenderer>();
            }

            var verts = @params["vertices"]?.ToObject<float[][]>();
            var tris = @params["triangles"]?.ToObject<int[]>();

            if (verts == null || tris == null)
                return new ErrorResponse("'vertices' and 'triangles' are required.");

            var mesh = new Mesh { name = @params["name"]?.ToString() ?? "CustomMesh" };
            mesh.vertices = verts.Select(v => new Vector3(v[0], v[1], v[2])).ToArray();
            mesh.triangles = tris;

            var normals = @params["normals"]?.ToObject<float[][]>();
            if (normals != null)
                mesh.normals = normals.Select(n => new Vector3(n[0], n[1], n[2])).ToArray();
            else
                mesh.RecalculateNormals();

            var uvs = @params["uvs"]?.ToObject<float[][]>();
            if (uvs != null)
                mesh.uv = uvs.Select(u2 => new Vector2(u2[0], u2[1])).ToArray();

            mesh.RecalculateBounds();
            Undo.RecordObject(mf, "Set Mesh Data");
            mf.sharedMesh = mesh;

            return new SuccessResponse($"Set mesh data on '{go.name}'.", new
            {
                vertexCount = mesh.vertexCount,
                triangleCount = mesh.triangles.Length / 3
            });
        }

        private static object ExportMeshData(JObject @params)
        {
            string target = @params["target"]?.ToString();
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'target' is required.");

            var go = GameObjectLookup.FindByTarget(new JValue(target), "by_id_or_name_or_path");
            if (go == null) return new ErrorResponse($"GameObject '{target}' not found.");

            var mf = go.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) return new ErrorResponse($"No mesh on '{go.name}'.");

            bool includeVerts = @params["include_vertices"]?.ToObject<bool>() ?? false;

            var data = new Dictionary<string, object>
            {
                ["meshName"] = mesh.name,
                ["vertexCount"] = mesh.vertexCount,
                ["triangleCount"] = mesh.triangles.Length / 3,
                ["subMeshCount"] = mesh.subMeshCount,
                ["bounds"] = new { center = new float[] { mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.center.z },
                                   size = new float[] { mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z } }
            };

            if (includeVerts && mesh.isReadable)
            {
                data["vertices"] = mesh.vertices.Select(v => new float[] { v.x, v.y, v.z }).ToArray();
                data["triangles"] = mesh.triangles;
            }

            return new SuccessResponse($"Exported mesh data for '{go.name}'.", data);
        }
    }
}
