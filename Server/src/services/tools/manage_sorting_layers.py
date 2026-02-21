"""manage_sorting_layers tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages sorting layers for 2D rendering order. Actions: add, remove, reorder, get_info.",
    annotations=ToolAnnotations(title="Manage Sorting Layers", destructiveHint=True),
)
async def manage_sorting_layers(
    ctx: Context,
    action: Annotated[Literal["add", "remove", "reorder", "get_info"], "Action"],
    layer_name: Annotated[str, "Sorting layer name"] | None = None,
    order: Annotated[list[str], "New order of layer names for reorder"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {"action": action, "layer_name": layer_name, "order": order}.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_sorting_layers", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
