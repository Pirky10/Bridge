"""
Defines the manage_terrain tool for Unity terrain operations.
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
        "Creates and manages Unity terrains: heightmaps, textures, trees, details, painting. "
        "Actions: create, set_height, set_size, set_texture, add_tree_prototype, add_detail_prototype, "
        "paint_texture, get_terrain_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Terrain",
        destructiveHint=True,
    ),
)
async def manage_terrain(
    ctx: Context,
    action: Annotated[Literal[
        "create", "set_height", "set_size", "set_texture",
        "add_tree_prototype", "add_detail_prototype",
        "paint_texture", "get_terrain_info"
    ], "Action to perform."],

    target: Annotated[str, "Target terrain GameObject name"] | None = None,
    name: Annotated[str, "Name for new terrain"] | None = None,
    width: Annotated[float, "Terrain width or paint width"] | None = None,
    length: Annotated[float, "Terrain length"] | None = None,
    height: Annotated[float, "Terrain height or paint height"] | None = None,
    heightmap_resolution: Annotated[int, "Heightmap resolution (power-of-2 + 1, e.g. 513)"] | None = None,
    data_path: Annotated[str, "Path to save TerrainData asset"] | None = None,
    position: Annotated[list[float], "Terrain position [x, y, z]"] | None = None,
    height_value: Annotated[float, "Height value to set"] | None = None,
    x: Annotated[int, "X coordinate on heightmap/alphamap"] | None = None,
    y: Annotated[int, "Y coordinate on heightmap/alphamap"] | None = None,
    height_count: Annotated[int, "Height region size"] | None = None,
    texture_path: Annotated[str, "Path to terrain texture (Assets/...)"] | None = None,
    normal_path: Annotated[str, "Path to normal map texture"] | None = None,
    tile_width: Annotated[float, "Texture tile width"] | None = None,
    tile_height: Annotated[float, "Texture tile height"] | None = None,
    layer_path: Annotated[str, "Path to save TerrainLayer asset"] | None = None,
    prefab_path: Annotated[str, "Path to tree/detail prefab"] | None = None,
    bend_factor: Annotated[float, "Tree bend factor"] | None = None,
    min_width: Annotated[float, "Detail min width"] | None = None,
    max_width: Annotated[float, "Detail max width"] | None = None,
    min_height: Annotated[float, "Detail min height"] | None = None,
    max_height: Annotated[float, "Detail max height"] | None = None,
    layer_index: Annotated[int, "Texture layer index for painting"] | None = None,
    strength: Annotated[float, "Paint strength (0-1)"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "name": name,
        "width": width, "length": length, "height": height,
        "heightmap_resolution": heightmap_resolution, "data_path": data_path,
        "position": position, "height_value": height_value,
        "x": x, "y": y, "height_count": height_count,
        "texture_path": texture_path, "normal_path": normal_path,
        "tile_width": tile_width, "tile_height": tile_height,
        "layer_path": layer_path, "prefab_path": prefab_path,
        "bend_factor": bend_factor,
        "min_width": min_width, "max_width": max_width,
        "min_height": min_height, "max_height": max_height,
        "layer_index": layer_index, "strength": strength,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_terrain",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
