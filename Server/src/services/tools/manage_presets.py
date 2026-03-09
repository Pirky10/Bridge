"""manage_presets tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Save/load component presets. Save configurations from any component and apply to others. Actions: save, apply, list, get_info.",
    annotations=ToolAnnotations(title="Manage Presets", destructiveHint=True),
)
async def manage_presets(
    ctx: Context,
    action: Annotated[Literal["save", "apply", "list", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject"] | None = None,
    component_type: Annotated[str, "Component type to save preset from"] | None = None,
    save_path: Annotated[str, "Path to save preset asset"] | None = None,
    preset_path: Annotated[str, "Path to existing preset"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {"action": action, "target": target, "component_type": component_type, "save_path": save_path, "preset_path": preset_path}.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_presets", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
