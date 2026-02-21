"""
Defines the manage_addressables tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity Addressables: init, mark assets addressable, create groups, set labels/addresses, build. Requires com.unity.addressables. Actions: init, mark_addressable, create_group, set_label, set_address, build, get_info.",
    annotations=ToolAnnotations(title="Manage Addressables", destructiveHint=True),
)
async def manage_addressables(
    ctx: Context,
    action: Annotated[Literal["init", "mark_addressable", "create_group", "set_label", "set_address", "build", "get_info"], "Action"],
    asset_path: Annotated[str, "Asset path to mark/label"] | None = None,
    group_name: Annotated[str, "Group name"] | None = None,
    group: Annotated[str, "Target group for mark_addressable"] | None = None,
    label: Annotated[str, "Label to set"] | None = None,
    address: Annotated[str, "Addressable address/key"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    params_dict = {k: v for k, v in {"action": action, "asset_path": asset_path, "group_name": group_name, "group": group, "label": label, "address": address}.items() if v is not None}
    result = await send_with_unity_instance(async_send_command_with_retry, unity_instance, "manage_addressables", params_dict)
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
