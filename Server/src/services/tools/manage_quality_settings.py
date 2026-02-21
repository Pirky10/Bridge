"""
Defines the manage_quality_settings tool for Unity quality settings.
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
        "Manages Unity quality settings: quality levels, VSync, shadows, anti-aliasing, textures. "
        "Actions: get_info, set_level, set_vsync, set_shadow_settings, set_anti_aliasing, set_texture_quality."
    ),
    annotations=ToolAnnotations(
        title="Manage Quality Settings",
        destructiveHint=True,
    ),
)
async def manage_quality_settings(
    ctx: Context,
    action: Annotated[Literal[
        "get_info", "set_level", "set_vsync",
        "set_shadow_settings", "set_anti_aliasing", "set_texture_quality"
    ], "Action to perform."],

    level: Annotated[int, "Quality level index"] | None = None,
    level_name: Annotated[str, "Quality level name (e.g., 'Ultra', 'High', 'Medium')"] | None = None,
    vsync_count: Annotated[int, "VSync count (0=off, 1=every vblank, 2=every other)"] | None = None,
    shadow_distance: Annotated[float, "Shadow draw distance"] | None = None,
    shadow_resolution: Annotated[str, "Shadow resolution: Low, Medium, High, VeryHigh"] | None = None,
    shadow_quality: Annotated[str, "Shadow quality: Disable, HardOnly, All"] | None = None,
    shadow_cascades: Annotated[int, "Shadow cascades (0, 2, or 4)"] | None = None,
    anti_aliasing: Annotated[int, "Anti-aliasing: 0=off, 2=2x, 4=4x, 8=8x MSAA"] | None = None,
    texture_limit: Annotated[int, "Texture mipmap limit (0=full, 1=half, 2=quarter)"] | None = None,
    anisotropic_filtering: Annotated[str, "Anisotropic filtering: Disable, Enable, ForceEnable"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "level": level, "level_name": level_name,
        "vsync_count": vsync_count, "shadow_distance": shadow_distance,
        "shadow_resolution": shadow_resolution, "shadow_quality": shadow_quality,
        "shadow_cascades": shadow_cascades, "anti_aliasing": anti_aliasing,
        "texture_limit": texture_limit, "anisotropic_filtering": anisotropic_filtering,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_quality_settings",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
