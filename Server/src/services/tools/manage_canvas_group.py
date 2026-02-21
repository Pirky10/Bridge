"""
Defines the manage_canvas_group tool for Unity CanvasGroup.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity CanvasGroup: alpha, interactable, blocksRaycasts, ignoreParentGroups. Actions: add, configure, remove, get_info.",
    annotations=ToolAnnotations(title="Manage Canvas Group", destructiveHint=True),
)
async def manage_canvas_group(
    ctx: Context,
    action: Annotated[Literal["add", "configure", "remove", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject"] | None = None,
    alpha: Annotated[float, "Alpha (0-1)"] | None = None,
    interactable: Annotated[bool, "Is interactable"] | None = None,
    blocks_raycasts: Annotated[bool, "Blocks raycasts"] | None = None,
    ignore_parent_groups: Annotated[bool, "Ignore parent groups"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    params_dict = {k: v for k, v in {"action": action, "target": target, "alpha": alpha, "interactable": interactable, "blocks_raycasts": blocks_raycasts, "ignore_parent_groups": ignore_parent_groups}.items() if v is not None}
    result = await send_with_unity_instance(async_send_command_with_retry, unity_instance, "manage_canvas_group", params_dict)
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
