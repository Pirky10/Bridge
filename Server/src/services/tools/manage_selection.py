"""
manage_selection tool — Get/set Unity Editor selection.
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
        "Manages Unity Editor selection: get/set selected objects, select by type/tag/layer, "
        "clear selection, ping objects in hierarchy. "
        "Actions: get_selection, set_selection, select_all, select_by_type, select_by_tag, "
        "select_by_layer, clear_selection, ping_object."
    ),
    annotations=ToolAnnotations(title="Manage Selection", destructiveHint=True),
)
async def manage_selection(
    ctx: Context,
    action: Annotated[Literal[
        "get_selection", "set_selection", "select_all",
        "select_by_type", "select_by_tag", "select_by_layer",
        "clear_selection", "ping_object"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name/path/ID for set_selection or ping_object"] | None = None,
    targets: Annotated[list[str], "List of GameObject names/paths/IDs for set_selection (multiple)"] | None = None,
    component_type: Annotated[str, "Component type name for select_by_type (e.g. 'MeshRenderer')"] | None = None,
    tag: Annotated[str, "Tag name for select_by_tag"] | None = None,
    layer: Annotated[str, "Layer name or index for select_by_layer"] | None = None,
    add_to_selection: Annotated[bool, "If true, add to current selection instead of replacing"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action, "target": target, "targets": targets,
        "component_type": component_type, "tag": tag, "layer": layer,
        "add_to_selection": add_to_selection,
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_selection", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
