"""
manage_gizmos tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Debug gizmos: create visual debug objects (spheres, cubes, lines), generate gizmo drawer scripts, toggle gizmo visibility. Actions: create_debug_sphere, create_debug_cube, create_debug_line, add_gizmo_drawer, set_gizmo_enabled, get_info.",
    annotations=ToolAnnotations(title="Manage Gizmos", destructiveHint=True),
)
async def manage_gizmos(
    ctx: Context,
    action: Annotated[Literal["create_debug_sphere", "create_debug_cube", "create_debug_line", "add_gizmo_drawer", "set_gizmo_enabled", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject"] | None = None,
    name: Annotated[str, "Name for debug object"] | None = None,
    position: Annotated[list[float], "Position [x,y,z]"] | None = None,
    scale: Annotated[list[float], "Scale [x,y,z]"] | None = None,
    radius: Annotated[float, "Radius for spheres"] | None = None,
    color: Annotated[list[float], "Color [r,g,b] or [r,g,b,a]"] | None = None,
    start: Annotated[list[float], "Line start [x,y,z]"] | None = None,
    end: Annotated[list[float], "Line end [x,y,z]"] | None = None,
    width: Annotated[float, "Line width"] | None = None,
    gizmo_type: Annotated[str, "Gizmo type: wire_sphere, sphere, wire_cube, cube, ray"] | None = None,
    component_type: Annotated[str, "Component type for toggling gizmos"] | None = None,
    enabled: Annotated[bool, "Enable/disable gizmo"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in locals().items() if v is not None and k not in ("ctx", "u", "self")}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_gizmos", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
