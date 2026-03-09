"""validate_project tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Validates Unity project: missing scripts, broken references, empty GameObjects, duplicate names, full health check. Actions: check_missing_scripts, check_missing_references, check_empty_gameobjects, check_duplicate_names, full_validation.",
    annotations=ToolAnnotations(title="Validate Project", readOnlyHint=True),
)
async def validate_project(
    ctx: Context,
    action: Annotated[Literal["check_missing_scripts", "check_missing_references", "check_empty_gameobjects", "check_duplicate_names", "full_validation"], "Action"],
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    r = await send_with_unity_instance(async_send_command_with_retry, u, "validate_project", {"action": action})
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
