"""
manage_custom_editor tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Auto-generates Unity Editor scripts: CustomEditors for MonoBehaviours (inspects fields, creates SerializedProperty layout), PropertyDrawers for custom types. Actions: generate_editor, generate_property_drawer, list_custom_editors.",
    annotations=ToolAnnotations(title="Manage Custom Editor", destructiveHint=True),
)
async def manage_custom_editor(
    ctx: Context,
    action: Annotated[Literal["generate_editor", "generate_property_drawer", "list_custom_editors"], "Action"],
    target_script: Annotated[str, "Path to target MonoBehaviour script"] | None = None,
    target_type: Annotated[str, "Type name for property drawer"] | None = None,
    save_path: Annotated[str, "Save path for generated script"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {"action": action, "target_script": target_script, "target_type": target_type, "save_path": save_path}.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_custom_editor", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
