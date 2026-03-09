"""manage_layer_collision tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages the Physics layer collision matrix. Actions: set_collision, ignore_collision, get_matrix, reset_all.",
    annotations=ToolAnnotations(title="Manage Layer Collision", destructiveHint=True),
)
async def manage_layer_collision(
    ctx: Context,
    action: Annotated[Literal["set_collision", "ignore_collision", "get_matrix", "reset_all"], "Action"],
    layer1: Annotated[str, "First layer name"] | None = None,
    layer2: Annotated[str, "Second layer name"] | None = None,
    collide: Annotated[bool, "Whether layers should collide"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {"action": action, "layer1": layer1, "layer2": layer2, "collide": collide}.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_layer_collision", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
