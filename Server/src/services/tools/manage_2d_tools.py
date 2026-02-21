"""
Defines the manage_2d_tools tool for Unity 2D features (Tilemap, SpriteShape, SpriteMask).
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
        "Manages Unity 2D tools: Tilemaps (create, place tiles, fill areas, clear), "
        "SpriteShape, SpriteMask. Requires com.unity.2d.tilemap.extras for advanced tile types "
        "and com.unity.2d.spriteshape for SpriteShape. "
        "Actions: create_tilemap, set_tile, fill_area, clear_tilemap, configure_tilemap, "
        "create_sprite_shape, add_sprite_mask, get_tilemap_info."
    ),
    annotations=ToolAnnotations(
        title="Manage 2D Tools",
        destructiveHint=True,
    ),
)
async def manage_2d_tools(
    ctx: Context,
    action: Annotated[Literal[
        "create_tilemap", "set_tile", "fill_area", "clear_tilemap",
        "configure_tilemap", "create_sprite_shape",
        "add_sprite_mask", "get_tilemap_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    name: Annotated[str, "Name for new object"] | None = None,
    grid_name: Annotated[str, "Grid parent name for tilemap"] | None = None,
    position: Annotated[list[float], "Position [x, y, z]"] | None = None,

    # Tilemap
    tile_path: Annotated[str, "Path to tile asset (Assets/...)"] | None = None,
    x: Annotated[int, "Tile X position"] | None = None,
    y: Annotated[int, "Tile Y position"] | None = None,
    z: Annotated[int, "Tile Z position"] | None = None,
    start_x: Annotated[int, "Fill area start X"] | None = None,
    start_y: Annotated[int, "Fill area start Y"] | None = None,
    end_x: Annotated[int, "Fill area end X"] | None = None,
    end_y: Annotated[int, "Fill area end Y"] | None = None,
    add_collider: Annotated[bool, "Add TilemapCollider2D"] | None = None,
    sorting_layer: Annotated[str, "Sorting layer name"] | None = None,
    sorting_order: Annotated[int, "Sorting order"] | None = None,
    render_mode: Annotated[str, "Render mode: Individual, Chunk"] | None = None,
    orientation: Annotated[str, "Tilemap orientation: XY, XZ, YX, YZ, ZX, ZY"] | None = None,
    color: Annotated[list[float], "Tilemap tint color [r,g,b,a]"] | None = None,
    tile_anchor: Annotated[list[float], "Tile anchor [x,y,z]"] | None = None,

    # SpriteMask
    sprite_path: Annotated[str, "Path to sprite asset for mask"] | None = None,
    front_sorting_order: Annotated[int, "SpriteMask front sorting order"] | None = None,
    back_sorting_order: Annotated[int, "SpriteMask back sorting order"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "name": name,
        "grid_name": grid_name, "position": position,
        "tile_path": tile_path, "x": x, "y": y, "z": z,
        "start_x": start_x, "start_y": start_y,
        "end_x": end_x, "end_y": end_y,
        "add_collider": add_collider,
        "sorting_layer": sorting_layer, "sorting_order": sorting_order,
        "render_mode": render_mode, "orientation": orientation,
        "color": color, "tile_anchor": tile_anchor,
        "sprite_path": sprite_path,
        "front_sorting_order": front_sorting_order,
        "back_sorting_order": back_sorting_order,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_2d_tools",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
