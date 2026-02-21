"""
Defines the manage_video_player tool for Unity video playback.
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
        "Manages Unity VideoPlayer: add, configure, set clip/URL, playback controls, render modes. "
        "Actions: add, configure, set_clip, set_url, play, stop, pause, set_render_mode, get_video_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Video Player",
        destructiveHint=True,
    ),
)
async def manage_video_player(
    ctx: Context,
    action: Annotated[Literal[
        "add", "configure", "set_clip", "set_url",
        "play", "stop", "pause",
        "set_render_mode", "get_video_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    clip_path: Annotated[str, "Path to VideoClip asset (Assets/...)"] | None = None,
    url: Annotated[str, "Video URL for streaming"] | None = None,
    loop: Annotated[bool, "Loop video playback"] | None = None,
    play_on_awake: Annotated[bool, "Play on awake"] | None = None,
    playback_speed: Annotated[float, "Playback speed multiplier"] | None = None,
    volume: Annotated[float, "Audio volume (0-1)"] | None = None,
    skip_on_drop: Annotated[bool, "Skip frames when lagging"] | None = None,
    render_mode: Annotated[str, "Render mode: CameraFarPlane, CameraNearPlane, RenderTexture, MaterialOverride, APIOnly"] | None = None,
    width: Annotated[int, "RenderTexture width"] | None = None,
    height: Annotated[int, "RenderTexture height"] | None = None,
    render_texture_path: Annotated[str, "Path to save RenderTexture asset"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target,
        "clip_path": clip_path, "url": url,
        "loop": loop, "play_on_awake": play_on_awake,
        "playback_speed": playback_speed, "volume": volume,
        "skip_on_drop": skip_on_drop,
        "render_mode": render_mode,
        "width": width, "height": height,
        "render_texture_path": render_texture_path,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_video_player",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
