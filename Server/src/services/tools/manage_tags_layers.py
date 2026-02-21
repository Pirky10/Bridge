"""
Defines the manage_tags_layers tool for Unity tags and layers.
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
        "Manages Unity tags, layers, and sorting layers. "
        "Actions: list_tags, add_tag, remove_tag, set_tag, list_layers, set_layer, set_layer_name, list_sorting_layers, add_sorting_layer."
    ),
    annotations=ToolAnnotations(
        title="Manage Tags & Layers",
        destructiveHint=True,
    ),
)
async def manage_tags_layers(
    ctx: Context,
    action: Annotated[Literal[
        "list_tags", "add_tag", "remove_tag", "set_tag",
        "list_layers", "set_layer", "set_layer_name",
        "list_sorting_layers", "add_sorting_layer"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    tag: Annotated[str, "Tag name"] | None = None,
    layer_index: Annotated[int, "Layer index (0-31)"] | None = None,
    layer_name: Annotated[str, "Layer name"] | None = None,
    name: Annotated[str, "Name for layer or sorting layer"] | None = None,
    recursive: Annotated[bool, "Apply layer to all children"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "tag": tag,
        "layer_index": layer_index, "layer_name": layer_name,
        "name": name, "recursive": recursive,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_tags_layers",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
