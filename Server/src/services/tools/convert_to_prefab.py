"""convert_to_prefab tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Smart prefab operations: create prefab from scene object, create variants, unpack, apply/revert overrides. Actions: create_prefab, create_variant, unpack, apply_overrides, revert_overrides, get_info.",
    annotations=ToolAnnotations(title="Convert To Prefab", destructiveHint=True),
)
async def convert_to_prefab(
    ctx: Context,
    action: Annotated[Literal["create_prefab", "create_variant", "unpack", "apply_overrides", "revert_overrides", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject"] | None = None,
    save_path: Annotated[str, "Save path for prefab"] | None = None,
    base_prefab_path: Annotated[str, "Base prefab for variant"] | None = None,
    variant_path: Annotated[str, "Save path for variant"] | None = None,
    completely: Annotated[bool, "Unpack completely (vs outermost)"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {"action": action, "target": target, "save_path": save_path, "base_prefab_path": base_prefab_path, "variant_path": variant_path, "completely": completely}.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "convert_to_prefab", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
