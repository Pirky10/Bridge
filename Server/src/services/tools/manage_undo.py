"""
manage_undo tool — Undo/redo operations and history.
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
        "Manages Unity undo/redo operations: perform undo/redo, get undo history, "
        "group undo operations, clear undo stack. "
        "Actions: undo, redo, get_undo_history, begin_group, end_group, clear_undo, collapse_group."
    ),
    annotations=ToolAnnotations(title="Manage Undo", destructiveHint=True),
)
async def manage_undo(
    ctx: Context,
    action: Annotated[Literal[
        "undo", "redo", "get_undo_history",
        "begin_group", "end_group", "clear_undo", "collapse_group"
    ], "Action to perform."],

    group_name: Annotated[str, "Name for undo group (begin_group, collapse_group)"] | None = None,
    steps: Annotated[int, "Number of undo/redo steps to perform (default 1)"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action, "group_name": group_name, "steps": steps,
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_undo", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
