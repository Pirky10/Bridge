"""
manage_splines tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity Splines: create splines, add/remove/set knots, and query spline info. Requires com.unity.splines. Actions: create_spline, add_knot, remove_knot, set_knot, set_spline_type, get_spline_info.",
    annotations=ToolAnnotations(title="Manage Splines", destructiveHint=True),
)
async def manage_splines(
    ctx: Context,
    action: Annotated[Literal["create_spline", "add_knot", "remove_knot", "set_knot", "set_spline_type", "get_spline_info"], "Action to perform"],
    target: Annotated[str, "Target GameObject or Spline component reference"] | None = None,
    position: Annotated[list[float], "Position [x, y, z] for the knot"] | None = None,
    rotation: Annotated[list[float], "Rotation [x, y, z, w] for the knot"] | None = None,
    tangent_in: Annotated[list[float], "In-tangent [x, y, z]"] | None = None,
    tangent_out: Annotated[list[float], "Out-tangent [x, y, z]"] | None = None,
    knot_index: Annotated[int, "Zero-based index of the knot"] | None = None,
    spline_type: Annotated[Literal["CatmullRom", "Bezier", "Linear"], "Type of spline interpolation"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action,
        "target": target,
        "position": position,
        "rotation": rotation,
        "tangent_in": tangent_in,
        "tangent_out": tangent_out,
        "knot_index": knot_index,
        "spline_type": spline_type
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_splines", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
