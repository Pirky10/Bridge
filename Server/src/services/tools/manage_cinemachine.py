"""
Defines the manage_cinemachine tool for Unity Cinemachine virtual cameras.
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
        "Manages Unity Cinemachine virtual cameras. Supports both Cinemachine 2.x and 3.x. "
        "Requires com.unity.cinemachine package. "
        "Actions: add_brain, create_virtual_camera, configure_virtual_camera, set_follow, "
        "set_look_at, create_freelook, create_state_driven, set_priority, get_cinemachine_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Cinemachine",
        destructiveHint=True,
    ),
)
async def manage_cinemachine(
    ctx: Context,
    action: Annotated[Literal[
        "add_brain", "create_virtual_camera", "configure_virtual_camera",
        "set_follow", "set_look_at", "create_freelook",
        "create_state_driven", "set_priority", "get_cinemachine_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    name: Annotated[str, "Name for new camera object"] | None = None,
    position: Annotated[list[float], "Position [x, y, z]"] | None = None,
    rotation: Annotated[list[float], "Euler rotation [x, y, z]"] | None = None,
    follow: Annotated[str, "Follow target GameObject name"] | None = None,
    look_at: Annotated[str, "LookAt target GameObject name"] | None = None,
    priority: Annotated[int, "Camera priority (higher = active)"] | None = None,
    fov: Annotated[float, "Field of view"] | None = None,
    default_blend: Annotated[float, "Brain default blend time"] | None = None,
    body_type: Annotated[str, "Body component type (e.g., CinemachineTransposer, CinemachineFramingTransposer)"] | None = None,
    noise_profile: Annotated[str, "Noise profile name (e.g., Handheld_wideangle_mild)"] | None = None,
    amplitude_gain: Annotated[float, "Noise amplitude gain"] | None = None,
    frequency_gain: Annotated[float, "Noise frequency gain"] | None = None,

) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "name": name,
        "position": position, "rotation": rotation,
        "follow": follow, "look_at": look_at,
        "priority": priority, "fov": fov,
        "default_blend": default_blend, "body_type": body_type,
        "noise_profile": noise_profile,
        "amplitude_gain": amplitude_gain,
        "frequency_gain": frequency_gain,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_cinemachine",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
