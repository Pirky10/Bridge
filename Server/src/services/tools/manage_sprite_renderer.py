"""
Defines the manage_sprite_renderer tool for Unity 2D sprites.
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
        "Manages SpriteRenderers: create sprites, assign images, configure rendering, sorting. "
        "Actions: create, set_sprite, configure, set_sorting, get_sprite_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Sprite Renderer",
        destructiveHint=True,
    ),
)
async def manage_sprite_renderer(
    ctx: Context,
    action: Annotated[Literal[
        "create", "set_sprite", "configure", "set_sorting", "get_sprite_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    name: Annotated[str, "Name for new sprite object"] | None = None,
    sprite_path: Annotated[str, "Path to sprite asset (Assets/...)"] | None = None,
    position: Annotated[list[float], "Position [x, y, z]"] | None = None,
    color: Annotated[list[float], "Color [r, g, b] or [r, g, b, a] (0-1)"] | None = None,
    flip_x: Annotated[bool, "Flip sprite horizontally"] | None = None,
    flip_y: Annotated[bool, "Flip sprite vertically"] | None = None,
    sorting_layer: Annotated[str, "Sorting layer name"] | None = None,
    sorting_order: Annotated[int, "Sorting order (higher = on top)"] | None = None,
    draw_mode: Annotated[str, "Draw mode: Simple, Sliced, Tiled"] | None = None,
    mask_interaction: Annotated[str, "Mask interaction: None, VisibleInsideMask, VisibleOutsideMask"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "name": name,
        "sprite_path": sprite_path, "position": position, "color": color,
        "flip_x": flip_x, "flip_y": flip_y,
        "sorting_layer": sorting_layer, "sorting_order": sorting_order,
        "draw_mode": draw_mode, "mask_interaction": mask_interaction,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_sprite_renderer",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
