"""
Defines the manage_localization tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity Localization: locales, string tables, entries. Requires com.unity.localization. Actions: add_locale, create_string_table, set_string_entry, get_string_entry, add_localized_string, get_info.",
    annotations=ToolAnnotations(title="Manage Localization", destructiveHint=True),
)
async def manage_localization(
    ctx: Context,
    action: Annotated[Literal["add_locale", "create_string_table", "set_string_entry", "get_string_entry", "add_localized_string", "get_info"], "Action"],
    locale_code: Annotated[str, "Locale code (e.g. en, fr, es)"] | None = None,
    table_name: Annotated[str, "String table name"] | None = None,
    key: Annotated[str, "String entry key"] | None = None,
    value: Annotated[str, "String entry value"] | None = None,
    locale: Annotated[str, "Locale for entry"] | None = None,
    target: Annotated[str, "Target GameObject for LocalizeStringEvent"] | None = None,
    path: Annotated[str, "Asset save path"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)
    params_dict = {k: v for k, v in {
        "action": action, "locale_code": locale_code, "table_name": table_name,
        "key": key, "value": value, "locale": locale, "target": target, "path": path
    }.items() if v is not None}
    result = await send_with_unity_instance(async_send_command_with_retry, unity_instance, "manage_localization", params_dict)
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
