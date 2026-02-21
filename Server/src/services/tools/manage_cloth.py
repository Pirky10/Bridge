"""
Defines the manage_cloth tool for Unity Cloth simulation.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity Cloth simulation: add, configure stiffness/damping/gravity, set vertex constraints, pin vertices. Actions: add, configure, set_constraints, remove, get_info.",
    annotations=ToolAnnotations(title="Manage Cloth", destructiveHint=True),
)
async def manage_cloth(
    ctx: Context,
    action: Annotated[Literal["add", "configure", "set_constraints", "remove", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject name"] | None = None,
    bending_stiffness: Annotated[float, "Bending stiffness (0-1)"] | None = None,
    stretching_stiffness: Annotated[float, "Stretching stiffness (0-1)"] | None = None,
    damping: Annotated[float, "Damping (0-1)"] | None = None,
    friction: Annotated[float, "Friction (0-1)"] | None = None,
    use_gravity: Annotated[bool, "Use gravity"] | None = None,
    world_velocity_scale: Annotated[float, "World velocity scale"] | None = None,
    world_acceleration_scale: Annotated[float, "World acceleration scale"] | None = None,
    sleep_threshold: Annotated[float, "Sleep threshold"] | None = None,
    external_acceleration: Annotated[list[float], "External acceleration [x,y,z]"] | None = None,
    random_acceleration: Annotated[list[float], "Random acceleration [x,y,z]"] | None = None,
    max_distance: Annotated[float, "Max vertex distance for constraints"] | None = None,
    collision_sphere_distance: Annotated[float, "Collision sphere distance"] | None = None,
    pinned_vertices: Annotated[list[int], "Vertex indices to pin (maxDistance=0)"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    params_dict = {k: v for k, v in {
        "action": action, "target": target, "bending_stiffness": bending_stiffness,
        "stretching_stiffness": stretching_stiffness, "damping": damping, "friction": friction,
        "use_gravity": use_gravity, "world_velocity_scale": world_velocity_scale,
        "world_acceleration_scale": world_acceleration_scale, "sleep_threshold": sleep_threshold,
        "external_acceleration": external_acceleration, "random_acceleration": random_acceleration,
        "max_distance": max_distance, "collision_sphere_distance": collision_sphere_distance,
        "pinned_vertices": pinned_vertices,
    }.items() if v is not None}
    result = await send_with_unity_instance(async_send_command_with_retry, unity_instance, "manage_cloth", params_dict)
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
