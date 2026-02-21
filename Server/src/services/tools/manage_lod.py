"""
Defines the manage_lod tool for Unity LOD Group operations.
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
        "Manages Unity LOD Groups: add LOD groups, configure levels, assign renderers, set fade modes. "
        "Actions: add_lod_group, configure, set_lod_level, get_lod_info."
    ),
    annotations=ToolAnnotations(
        title="Manage LOD",
        destructiveHint=True,
    ),
)
async def manage_lod(
    ctx: Context,
    action: Annotated[Literal[
        "add_lod_group", "configure", "set_lod_level", "get_lod_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    lod_count: Annotated[int, "Number of LOD levels (default 3)"] | None = None,
    levels: Annotated[list, "LOD level thresholds as array of floats or objects"] | None = None,
    level: Annotated[int, "LOD level index to configure"] | None = None,
    threshold: Annotated[float, "Screen transition height (0-1) for LOD level"] | None = None,
    renderer_source: Annotated[str, "Child object name to get renderers from"] | None = None,
    fade_mode: Annotated[str, "Fade mode: None, CrossFade, SpeedTree"] | None = None,
    animate_cross_fading: Annotated[bool, "Enable animated cross-fading"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target,
        "lod_count": lod_count, "levels": levels,
        "level": level, "threshold": threshold,
        "renderer_source": renderer_source,
        "fade_mode": fade_mode,
        "animate_cross_fading": animate_cross_fading,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_lod",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
