"""
AI-powered performance optimization for Unity projects.
Gathers profiler data and suggests optimizations.
"""
from typing import Annotated, Any

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    description=(
        "Analyzes Unity project performance and suggests optimizations. "
        "Gathers profiler data, analyzes scripts for common performance issues, "
        "and recommends specific improvements for rendering, physics, scripting, "
        "memory, and asset loading."
    ),
    annotations=ToolAnnotations(
        title="Optimize Performance",
        readOnlyHint=True,
    ),
)
async def optimize_performance(
    ctx: Context,
    area: Annotated[str, "Area to optimize: rendering, physics, scripting, memory, loading, all, list_high_poly"] | None = None,
    script_path: Annotated[str, "Specific script to analyze for performance"] | None = None,
    target_platform: Annotated[str, "Target platform: PC, Mobile, Console, VR"] | None = None,
    target_fps: Annotated[int, "Target FPS"] | None = None,
    poly_threshold: Annotated[int, "Polygon threshold for list_high_poly (default: 10000)"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    # Handle list_high_poly as a direct Unity code execution
    if (area or "").lower() == "list_high_poly":
        threshold = poly_threshold or 10000
        code = f"""
var results = new System.Collections.Generic.List<object>();
foreach (var mf in UnityEngine.Object.FindObjectsOfType<UnityEngine.MeshFilter>())
{{
    if (mf.sharedMesh != null && mf.sharedMesh.triangles.Length / 3 >= {threshold})
    {{
        results.Add(new {{ name = mf.gameObject.name, path = GetPath(mf.transform),
            triangles = mf.sharedMesh.triangles.Length / 3, vertices = mf.sharedMesh.vertexCount,
            meshName = mf.sharedMesh.name }});
    }}
}}
string GetPath(UnityEngine.Transform t) {{
    string p = t.name;
    while (t.parent != null) {{ t = t.parent; p = t.name + "/" + p; }}
    return p;
}}
return new {{ success = true, threshold = {threshold}, count = results.Count, objects = results }};
"""
        result = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "execute_code", {
                "action": "run",
                "code": code,
            }
        )
        return result if isinstance(result, dict) else {"success": False, "message": str(result)}

    context_data = {}

    # Get profiler data
    try:
        profiler_data = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "manage_profiler", {"action": "get_stats"}
        )
        context_data["profiler"] = profiler_data
    except Exception:
        pass

    # Get memory info
    try:
        memory_data = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "manage_profiler", {"action": "get_memory_info"}
        )
        context_data["memory"] = memory_data
    except Exception:
        pass

    # Get quality settings
    try:
        quality_data = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "manage_quality_settings", {"action": "get_info"}
        )
        context_data["quality_settings"] = quality_data
    except Exception:
        pass

    # Read script if specified
    if script_path:
        try:
            script = await send_with_unity_instance(
                async_send_command_with_retry, unity_instance,
                "read_file", {"path": script_path}
            )
            context_data["script_content"] = script
        except Exception:
            pass

    optimization = {
        "area": area or "all",
        "target_platform": target_platform or "PC",
        "target_fps": target_fps or 60,
        "optimization_checklist": {
            "rendering": [
                "Reduce draw calls (batching, atlasing)",
                "LOD groups for complex models",
                "Occlusion culling",
                "Shader complexity (mobile-friendly shaders)",
                "Light baking vs real-time",
                "Shadow distance/resolution tuning",
            ],
            "physics": [
                "Simplified collision meshes",
                "Appropriate Fixed Timestep",
                "Layer-based collision matrix",
                "Physics.RaycastNonAlloc instead of Raycast",
                "Rigidbody sleep thresholds",
            ],
            "scripting": [
                "Cache GetComponent results",
                "Avoid Find/FindObjectOfType in loops",
                "Use object pooling",
                "Minimize GC allocations",
                "Use Jobs/Burst for heavy computation",
                "Proper Coroutine management",
            ],
            "memory": [
                "Texture compression settings",
                "Audio import settings (load type, compression)",
                "Asset bundle strategy",
                "Addressables for large projects",
                "Unload unused assets",
            ],
        },
        "project_context": context_data,
        "instructions": (
            f"Analyze performance data for a {target_platform or 'PC'} project targeting "
            f"{target_fps or 60} FPS. Focus on: {area or 'all areas'}. "
            "Provide specific, actionable optimizations with expected impact levels."
        ),
    }

    return {"success": True, "message": "Performance analysis ready", "data": optimization}
