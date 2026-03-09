"""
manage_rendering_layers tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages URP/HDRP rendering layer masks: set per-renderer/per-light layer masks, rename layers, get info. Actions: set_rendering_layer, rename_layer, set_light_layers, get_info.",
    annotations=ToolAnnotations(title="Manage Rendering Layers", destructiveHint=True),
)
async def manage_rendering_layers(
    ctx: Context,
    action: Annotated[Literal["set_rendering_layer", "rename_layer", "set_light_layers", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject"] | None = None,
    rendering_layer_mask: Annotated[int, "Rendering layer bitmask"] | None = None,
    layer_index: Annotated[int, "Layer index (0-31) for rename"] | None = None,
    layer_name: Annotated[str, "New name for rendering layer"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {"action": action, "target": target, "rendering_layer_mask": rendering_layer_mask, "layer_index": layer_index, "layer_name": layer_name}.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_rendering_layers", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
