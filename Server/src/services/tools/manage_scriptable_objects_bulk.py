"""
manage_scriptable_objects_bulk tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Bulk ScriptableObject operations: create multiple instances from type, clone from template, bulk-set fields, list types and instances. Actions: create_instances, create_from_template, bulk_set_field, list_types, list_instances.",
    annotations=ToolAnnotations(title="Manage ScriptableObjects Bulk", destructiveHint=True),
)
async def manage_scriptable_objects_bulk(
    ctx: Context,
    action: Annotated[Literal["create_instances", "create_from_template", "bulk_set_field", "list_types", "list_instances"], "Action"],
    so_type: Annotated[str, "ScriptableObject type name"] | None = None,
    count: Annotated[int, "Number of instances"] | None = None,
    folder: Annotated[str, "Target folder"] | None = None,
    name_prefix: Annotated[str, "Name prefix for created assets"] | None = None,
    template_path: Annotated[str, "Template SO to clone from"] | None = None,
    field_name: Annotated[str, "Field name for bulk_set"] | None = None,
    field_value: Annotated[Any, "Value to set (int, float, string, bool)"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action, "so_type": so_type, "count": count, "folder": folder,
        "name_prefix": name_prefix, "template_path": template_path,
        "field_name": field_name, "field_value": field_value,
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_scriptable_objects_bulk", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
