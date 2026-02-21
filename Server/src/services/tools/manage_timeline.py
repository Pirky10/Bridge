"""
Defines the manage_timeline tool for Unity Timeline operations.
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
        "Manages Unity Timeline assets. Requires com.unity.timeline package. "
        "Actions: create_asset, add_track, add_clip, set_clip_timing, bind_track, get_timeline_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Timeline",
        destructiveHint=True,
    ),
)
async def manage_timeline(
    ctx: Context,
    action: Annotated[Literal[
        "create_asset", "add_track", "add_clip",
        "set_clip_timing", "bind_track", "get_timeline_info"
    ], "Action to perform."],

    path: Annotated[str, "Path to .playable timeline asset"] | None = None,
    target: Annotated[str, "GameObject with PlayableDirector"] | None = None,
    track_type: Annotated[str, "Track type: AnimationTrack, AudioTrack, ActivationTrack, GroupTrack"] | None = None,
    track_name: Annotated[str, "Track name"] | None = None,
    clip_name: Annotated[str, "Clip display name"] | None = None,
    clip_path: Annotated[str, "Path to audio clip asset for AudioTrack"] | None = None,
    clip_index: Annotated[int, "Clip index in track"] | None = None,
    start: Annotated[float, "Clip start time in seconds"] | None = None,
    duration: Annotated[float, "Clip or timeline duration in seconds"] | None = None,
    bind_target: Annotated[str, "GameObject to bind to track"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "path": path, "target": target,
        "track_type": track_type, "track_name": track_name,
        "clip_name": clip_name, "clip_path": clip_path,
        "clip_index": clip_index, "start": start,
        "duration": duration, "bind_target": bind_target,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_timeline",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
