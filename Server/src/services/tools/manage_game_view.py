"""
manage_game_view tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages the Unity Game View: aspect ratio, scale, and play mode controls. Actions: set_aspect_ratio, set_scale, toggle_maximize_on_play, toggle_mute_audio, get_game_view_info.",
    annotations=ToolAnnotations(title="Manage Game View"),
)
async def manage_game_view(
    ctx: Context,
    action: Annotated[Literal["set_aspect_ratio", "set_scale", "toggle_maximize_on_play", "toggle_mute_audio", "get_game_view_info"], "Action to perform"],
    aspect_ratio: Annotated[str, "Aspect ratio string (e.g. '16:9', '4:3', 'Free Aspect')"] | None = None,
    scale: Annotated[float, "Zoom scale for the game view"] | None = None,
    enabled: Annotated[bool, "Toggle state"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action,
        "aspect_ratio": aspect_ratio,
        "scale": scale,
        "enabled": enabled
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_game_view", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
