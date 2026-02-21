"""
Defines the capture_screenshot tool for Unity screenshots.
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
        "Captures screenshots from Unity: game view, scene view, or specific camera. "
        "Actions: game_view, scene_view, camera."
    ),
    annotations=ToolAnnotations(
        title="Capture Screenshot",
        readOnlyHint=True,
    ),
)
async def capture_screenshot(
    ctx: Context,
    action: Annotated[Literal[
        "game_view", "scene_view", "camera"
    ], "Capture source: game_view, scene_view, or specific camera."],

    target: Annotated[str, "Camera GameObject name (for 'camera' action)"] | None = None,
    output_path: Annotated[str, "Output file path (defaults to Assets/Screenshots/)"] | None = None,
    width: Annotated[int, "Image width in pixels"] | None = None,
    height: Annotated[int, "Image height in pixels"] | None = None,
    super_size: Annotated[int, "Super-resolution multiplier for game_view capture"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action,
        "target": target,
        "output_path": output_path,
        "width": width,
        "height": height,
        "super_size": super_size,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "capture_screenshot",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
