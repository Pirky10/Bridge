"""
manage_scriptable_object_editor tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Creates and manages custom editors for ScriptableObjects to enhance visualization and editing. Actions: create_so_editor, list_so_editors, apply_custom_layout.",
    annotations=ToolAnnotations(title="Manage ScriptableObject Editor", destructiveHint=True),
)
async def manage_scriptable_object_editor(
    ctx: Context,
    action: Annotated[Literal["create_so_editor", "list_so_editors", "apply_custom_layout"], "Action to perform"],
    so_type: Annotated[str, "Type name of the ScriptableObject"] | None = None,
    save_path: Annotated[str, "Asset path for the generated editor script"] | None = None,
    layout_template: Annotated[str, "Optional template for the custom layout"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action,
        "so_type": so_type,
        "save_path": save_path,
        "layout_template": layout_template
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_scriptable_object_editor", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
