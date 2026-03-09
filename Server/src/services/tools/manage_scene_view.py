"""
manage_scene_view tool — Control Unity Scene View camera and settings.
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
        "Controls Unity Scene View: camera position/rotation, frame selection, 2D/3D mode, "
        "scene lighting, draw modes, orthographic toggle. "
        "Actions: frame_selection, look_at, set_camera_position, set_2d_mode, "
        "set_scene_lighting, set_draw_mode, set_orthographic, get_scene_view_info."
    ),
    annotations=ToolAnnotations(title="Manage Scene View", destructiveHint=True),
)
async def manage_scene_view(
    ctx: Context,
    action: Annotated[Literal[
        "frame_selection", "look_at", "set_camera_position",
        "set_2d_mode", "set_scene_lighting", "set_draw_mode",
        "set_orthographic", "get_scene_view_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject to look at or frame"] | None = None,
    position: Annotated[list[float], "Camera pivot position [x, y, z]"] | None = None,
    rotation: Annotated[list[float], "Camera rotation [x, y, z] euler angles"] | None = None,
    size: Annotated[float, "Camera distance/zoom size"] | None = None,
    enabled: Annotated[bool, "Enable/disable for 2D mode and scene lighting"] | None = None,
    draw_mode: Annotated[str, "Draw mode: Textured, Wireframe, TexturedWire, ShadedWireframe, Shaded"] | None = None,
    orthographic: Annotated[bool, "Enable/disable orthographic projection"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action, "target": target, "position": position,
        "rotation": rotation, "size": size, "enabled": enabled,
        "draw_mode": draw_mode, "orthographic": orthographic,
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_scene_view", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
