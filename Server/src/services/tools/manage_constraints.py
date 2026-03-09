"""
Defines the manage_constraints tool for Unity animation constraints.
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
        "Manages Unity animation constraints: Position, Rotation, Scale, Aim, Parent, LookAt. "
        "Actions: add_position, add_rotation, add_scale, add_aim, add_parent, add_look_at, configure, get_constraints_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Constraints",
        destructiveHint=True,
    ),
)
async def manage_constraints(
    ctx: Context,
    action: Annotated[Literal[
        "add_position", "add_rotation", "add_scale",
        "add_aim", "add_parent", "add_look_at",
        "configure", "get_constraints_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    source: Annotated[str, "Source GameObject name for constraint"] | None = None,
    weight: Annotated[float, "Source weight (0-1)"] | None = None,
    activate: Annotated[bool, "Whether to activate the constraint"] | None = None,
    constraint_type: Annotated[str, "Constraint type for configure: position, rotation, scale, aim, parent, lookat"] | None = None,
    lock_x: Annotated[bool, "Lock X axis"] | None = None,
    lock_y: Annotated[bool, "Lock Y axis"] | None = None,
    lock_z: Annotated[bool, "Lock Z axis"] | None = None,
    aim_vector: Annotated[list[float], "Aim direction [x, y, z]"] | None = None,
    up_vector: Annotated[list[float], "Up direction [x, y, z]"] | None = None,
    roll: Annotated[float, "LookAt constraint roll angle"] | None = None,

) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "source": source,
        "weight": weight, "activate": activate,
        "constraint_type": constraint_type,
        "lock_x": lock_x, "lock_y": lock_y, "lock_z": lock_z,
        "aim_vector": aim_vector, "up_vector": up_vector, "roll": roll,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_constraints",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
