"""manage_assembly_definitions tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages .asmdef assembly definitions: create, add references, get info, list all. Speeds up compilation for large projects.",
    annotations=ToolAnnotations(title="Manage Assembly Definitions", destructiveHint=True),
)
async def manage_assembly_definitions(
    ctx: Context,
    action: Annotated[Literal["create", "add_reference", "get_info", "list"], "Action"],
    name: Annotated[str, "Assembly name"] | None = None,
    folder: Annotated[str, "Folder path"] | None = None,
    root_namespace: Annotated[str, "Root namespace"] | None = None,
    references: Annotated[list[str], "Assembly references"] | None = None,
    allow_unsafe: Annotated[bool, "Allow unsafe code"] | None = None,
    auto_referenced: Annotated[bool, "Auto referenced"] | None = None,
    asmdef_path: Annotated[str, "Path to existing .asmdef"] | None = None,
    reference: Annotated[str, "Single reference to add"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in locals().items() if v is not None and k not in ("ctx", "u", "self")}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_assembly_definitions", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
