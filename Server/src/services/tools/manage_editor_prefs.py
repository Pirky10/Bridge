"""
Defines the manage_editor_prefs tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages EditorPrefs and PlayerPrefs: get, set, delete, check existence. Supports string, int, float, bool types.",
    annotations=ToolAnnotations(title="Manage Editor Prefs", destructiveHint=True),
)
async def manage_editor_prefs(
    ctx: Context,
    action: Annotated[Literal["get_editor_pref", "set_editor_pref", "delete_editor_pref", "has_editor_pref", "get_player_pref", "set_player_pref", "delete_player_pref", "delete_all_player_prefs"], "Action"],
    key: Annotated[str, "Preference key"] | None = None,
    value: Annotated[str, "Value to set (as string)"] | None = None,
    value_type: Annotated[str, "Type: string, int, float, bool"] | None = None,
) -> dict[str, Any]:
    unity = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {"action": action, "key": key, "value": value, "value_type": value_type}.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, unity, "manage_editor_prefs", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
