"""
manage_editor_window tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages custom Unity Editor windows: create, dock, focus, close, and add UI elements. Actions: create_window, close_window, focus_window, list_windows, add_ui_element.",
    annotations=ToolAnnotations(title="Manage Editor Window", destructiveHint=True),
)
async def manage_editor_window(
    ctx: Context,
    action: Annotated[Literal["create_window", "close_window", "focus_window", "list_windows", "add_ui_element"], "Action to perform"],
    window_name: Annotated[str, "Unique identifier or title for the window"] | None = None,
    title: Annotated[str, "Display title for the window"] | None = None,
    element_type: Annotated[str, "Type of UI element to add (e.g. Button, Label, TextField)"] | None = None,
    element_properties: Annotated[dict[str, Any], "Properties for the UI element"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action,
        "window_name": window_name,
        "title": title,
        "element_type": element_type,
        "element_properties": element_properties
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_editor_window", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
