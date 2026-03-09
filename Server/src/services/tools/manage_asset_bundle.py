"""
manage_asset_bundle tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity Asset Bundles: assigning assets, building bundles for specific platforms, and listing bundle contents. Actions: assign_asset, remove_asset, build_bundles, list_bundles, get_bundle_info.",
    annotations=ToolAnnotations(title="Manage Asset Bundle", destructiveHint=True),
)
async def manage_asset_bundle(
    ctx: Context,
    action: Annotated[Literal["assign_asset", "remove_asset", "build_bundles", "list_bundles", "get_bundle_info"], "Action to perform"],
    asset_path: Annotated[str, "Asset path to assign or remove"] | None = None,
    bundle_name: Annotated[str, "Name of the Asset Bundle"] | None = None,
    platform: Annotated[str, "Target build platform (e.g. Android, iOS, StandaloneWindows64)"] | None = None,
    output_path: Annotated[str, "Path where bundles will be built"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action,
        "asset_path": asset_path,
        "bundle_name": bundle_name,
        "platform": platform,
        "output_path": output_path
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_asset_bundle", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
