"""find_references tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Finds references to assets, components, and scripts. Also finds missing/broken references. Actions: find_asset_references, find_component_usage, find_script_references, find_missing_references.",
    annotations=ToolAnnotations(title="Find References", readOnlyHint=True),
)
async def find_references(
    ctx: Context,
    action: Annotated[Literal["find_asset_references", "find_component_usage", "find_script_references", "find_missing_references"], "Action"],
    asset_path: Annotated[str, "Asset path to find references for"] | None = None,
    component_type: Annotated[str, "Component type name"] | None = None,
    script_path: Annotated[str, "Script path"] | None = None,
    search_scene: Annotated[bool, "Search in current scene"] | None = None,
    search_prefabs: Annotated[bool, "Search in prefabs"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in locals().items() if v is not None and k not in ("ctx", "u", "self")}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "find_references", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
