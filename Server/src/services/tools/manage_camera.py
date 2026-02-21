"""
Defines the manage_camera tool for Unity camera operations.
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
        "Creates and configures Unity cameras. "
        "Actions: create, configure, set_clear_flags, set_culling_mask, set_viewport, get_camera_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Camera",
        destructiveHint=True,
    ),
)
async def manage_camera(
    ctx: Context,
    action: Annotated[Literal[
        "create", "configure", "set_clear_flags",
        "set_culling_mask", "set_viewport", "get_camera_info"
    ], "Action to perform."],

    # Common
    target: Annotated[str, "Target camera GameObject name"] | None = None,
    name: Annotated[str, "Name for new camera"] | None = None,

    # create/configure
    position: Annotated[list[float], "Position [x, y, z]"] | None = None,
    rotation: Annotated[list[float], "Euler rotation [x, y, z]"] | None = None,
    field_of_view: Annotated[float, "Field of view in degrees"] | None = None,
    near_clip: Annotated[float, "Near clipping plane"] | None = None,
    far_clip: Annotated[float, "Far clipping plane"] | None = None,
    orthographic: Annotated[bool, "Whether camera is orthographic"] | None = None,
    orthographic_size: Annotated[float, "Orthographic size"] | None = None,
    depth: Annotated[float, "Camera depth (rendering order)"] | None = None,
    background_color: Annotated[list[float], "Background color [r, g, b] or [r, g, b, a]"] | None = None,

    # set_clear_flags
    flags: Annotated[str, "Clear flags: Skybox, SolidColor, Depth, Nothing"] | None = None,

    # set_culling_mask
    layers: Annotated[list[str], "Layer names for culling mask"] | None = None,
    mask: Annotated[int, "Raw integer culling mask"] | None = None,

    # set_viewport
    viewport: Annotated[list[float], "Viewport rect [x, y, width, height] normalized 0-1"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action,
        "target": target,
        "name": name,
        "position": position,
        "rotation": rotation,
        "field_of_view": field_of_view,
        "near_clip": near_clip,
        "far_clip": far_clip,
        "orthographic": orthographic,
        "orthographic_size": orthographic_size,
        "depth": depth,
        "background_color": background_color,
        "flags": flags,
        "layers": layers,
        "mask": mask,
        "viewport": viewport,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_camera",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
