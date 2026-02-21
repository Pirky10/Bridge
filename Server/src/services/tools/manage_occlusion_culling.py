"""
Defines the manage_occlusion_culling tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity occlusion culling: set static flags, create OcclusionAreas, bake, clear, set portals. Actions: set_static, set_area, bake, clear, set_portal, get_info.",
    annotations=ToolAnnotations(title="Manage Occlusion Culling", destructiveHint=True),
)
async def manage_occlusion_culling(
    ctx: Context,
    action: Annotated[Literal["set_static", "set_area", "bake", "clear", "set_portal", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject"] | None = None,
    name: Annotated[str, "Name for new OcclusionArea"] | None = None,
    occluder_static: Annotated[bool, "Mark as occluder"] | None = None,
    occludee_static: Annotated[bool, "Mark as occludee"] | None = None,
    include_children: Annotated[bool, "Apply flags to children too"] | None = None,
    center: Annotated[list[float], "Area center [x,y,z]"] | None = None,
    size: Annotated[list[float], "Area size [x,y,z]"] | None = None,
    smallest_occluder: Annotated[float, "Smallest occluder size"] | None = None,
    smallest_hole: Annotated[float, "Smallest hole size"] | None = None,
    backface_threshold: Annotated[float, "Backface threshold"] | None = None,
    open: Annotated[bool, "Portal open state"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    params_dict = {k: v for k, v in locals().items() if v is not None and k not in ("ctx", "unity_instance", "self")}
    result = await send_with_unity_instance(async_send_command_with_retry, unity_instance, "manage_occlusion_culling", params_dict)
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
