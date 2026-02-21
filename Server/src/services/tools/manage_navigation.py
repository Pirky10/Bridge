"""
Defines the manage_navigation tool for Unity AI navigation.
"""
from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    description=(
        "Manages Unity AI Navigation: NavMeshAgent, NavMeshObstacle, OffMeshLink, NavMesh surfaces. "
        "Actions: add_navmesh_surface, add_agent, configure_agent, add_obstacle, configure_obstacle, add_offmesh_link, get_navigation_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Navigation",
        destructiveHint=True,
    ),
)
async def manage_navigation(
    ctx: Context,
    action: Annotated[Literal[
        "add_navmesh_surface", "add_agent", "configure_agent",
        "add_obstacle", "configure_obstacle",
        "add_offmesh_link", "get_navigation_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    name: Annotated[str, "Name for new navigation object"] | None = None,
    speed: Annotated[float, "Agent movement speed"] | None = None,
    angular_speed: Annotated[float, "Agent angular speed"] | None = None,
    acceleration: Annotated[float, "Agent acceleration"] | None = None,
    stopping_distance: Annotated[float, "Agent stopping distance"] | None = None,
    radius: Annotated[float, "Agent/obstacle radius"] | None = None,
    height: Annotated[float, "Agent height"] | None = None,
    base_offset: Annotated[float, "Agent base offset"] | None = None,
    auto_traverse_offmesh_link: Annotated[bool, "Auto traverse off-mesh links"] | None = None,
    avoidance_priority: Annotated[int, "Agent avoidance priority (0-99)"] | None = None,
    destination: Annotated[list[float], "Agent destination [x, y, z]"] | None = None,
    carve: Annotated[bool, "Whether obstacle carves NavMesh"] | None = None,
    shape: Annotated[str, "Obstacle shape: box, capsule"] | None = None,
    size: Annotated[list[float], "Obstacle size [x, y, z]"] | None = None,
    center: Annotated[list[float], "Obstacle center [x, y, z]"] | None = None,
    start_object: Annotated[str, "Start GameObject for OffMeshLink"] | None = None,
    end_object: Annotated[str, "End GameObject for OffMeshLink"] | None = None,
    bi_directional: Annotated[bool, "Whether OffMeshLink is bi-directional"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "name": name,
        "speed": speed, "angular_speed": angular_speed,
        "acceleration": acceleration, "stopping_distance": stopping_distance,
        "radius": radius, "height": height, "base_offset": base_offset,
        "auto_traverse_offmesh_link": auto_traverse_offmesh_link,
        "avoidance_priority": avoidance_priority, "destination": destination,
        "carve": carve, "shape": shape, "size": size, "center": center,
        "start_object": start_object, "end_object": end_object,
        "bi_directional": bi_directional,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_navigation",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
