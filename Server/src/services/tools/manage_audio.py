"""
Defines the manage_audio tool for Unity audio operations.
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
        "Manages Unity audio: AudioSource, AudioListener, playback controls, clip assignment, volume. "
        "Actions: add_source, configure_source, add_listener, play, stop, pause, set_clip, set_volume, get_audio_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Audio",
        destructiveHint=True,
    ),
)
async def manage_audio(
    ctx: Context,
    action: Annotated[Literal[
        "add_source", "configure_source", "add_listener",
        "play", "stop", "pause",
        "set_clip", "set_volume", "get_audio_info"
    ], "Action to perform."],

    # Common
    target: Annotated[str, "Target GameObject name"] | None = None,

    # Source config
    volume: Annotated[float, "Volume (0-1)"] | None = None,
    pitch: Annotated[float, "Pitch multiplier"] | None = None,
    loop: Annotated[bool, "Whether audio loops"] | None = None,
    play_on_awake: Annotated[bool, "Play on awake"] | None = None,
    mute: Annotated[bool, "Mute the source"] | None = None,
    spatial_blend: Annotated[float, "Spatial blend (0=2D, 1=3D)"] | None = None,
    min_distance: Annotated[float, "Minimum distance for 3D audio"] | None = None,
    max_distance: Annotated[float, "Maximum distance for 3D audio"] | None = None,
    priority: Annotated[int, "Audio priority (0=highest, 256=lowest)"] | None = None,
    rolloff_mode: Annotated[str, "Audio rolloff: Logarithmic, Linear, Custom"] | None = None,

    # Clip
    clip_path: Annotated[str, "Path to AudioClip asset (Assets/...)"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action,
        "target": target,
        "volume": volume,
        "pitch": pitch,
        "loop": loop,
        "play_on_awake": play_on_awake,
        "mute": mute,
        "spatial_blend": spatial_blend,
        "min_distance": min_distance,
        "max_distance": max_distance,
        "priority": priority,
        "rolloff_mode": rolloff_mode,
        "clip_path": clip_path,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_audio",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
