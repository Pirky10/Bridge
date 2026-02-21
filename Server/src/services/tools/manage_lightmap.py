"""
Defines the manage_lightmap tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity lightmap baking: set GI static, configure lightmapper, bake (async/sync), clear, per-renderer params. Actions: set_static, configure_settings, bake, clear, set_lightmap_parameters, get_info.",
    annotations=ToolAnnotations(title="Manage Lightmap", destructiveHint=True),
)
async def manage_lightmap(
    ctx: Context,
    action: Annotated[Literal["set_static", "configure_settings", "bake", "clear", "set_lightmap_parameters", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject"] | None = None,
    contribute_gi: Annotated[bool, "Mark as Contribute GI"] | None = None,
    include_children: Annotated[bool, "Apply to children"] | None = None,
    lightmap_scale: Annotated[float, "Scale in lightmap"] | None = None,
    indirect_resolution: Annotated[float, "Indirect resolution"] | None = None,
    direct_resolution: Annotated[float, "Direct resolution (lightmap resolution)"] | None = None,
    max_lightmap_size: Annotated[int, "Max lightmap size"] | None = None,
    bounces: Annotated[int, "Max bounces"] | None = None,
    lightmapper: Annotated[str, "Lightmapper: progressive_cpu, progressive_gpu"] | None = None,
    compress_lightmaps: Annotated[bool, "Compress lightmaps"] | None = None,
    ambient_occlusion: Annotated[bool, "Enable ambient occlusion"] | None = None,
    ao_max_distance: Annotated[float, "AO max distance"] | None = None,
    receive_gi: Annotated[str, "Receive GI mode: lightmaps, lightprobes"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    params_dict = {k: v for k, v in {
        "action": action, "target": target, "contribute_gi": contribute_gi,
        "include_children": include_children, "lightmap_scale": lightmap_scale,
        "indirect_resolution": indirect_resolution, "direct_resolution": direct_resolution,
        "max_lightmap_size": max_lightmap_size, "bounces": bounces,
        "lightmapper": lightmapper, "compress_lightmaps": compress_lightmaps,
        "ambient_occlusion": ambient_occlusion, "ao_max_distance": ao_max_distance,
        "receive_gi": receive_gi,
    }.items() if v is not None}
    result = await send_with_unity_instance(async_send_command_with_retry, unity_instance, "manage_lightmap", params_dict)
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
