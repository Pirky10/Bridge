"""duplicate_scene_setup tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Duplicates scene setup: clone entire scenes, copy specific objects between scenes. Actions: duplicate, copy_objects, get_info.",
    annotations=ToolAnnotations(title="Duplicate Scene Setup", destructiveHint=True),
)
async def duplicate_scene_setup(
    ctx: Context,
    action: Annotated[Literal["duplicate", "copy_objects", "get_info"], "Action"],
    new_scene_path: Annotated[str, "Path for duplicated scene"] | None = None,
    target_scene_path: Annotated[str, "Target scene for copy_objects"] | None = None,
    objects: Annotated[list[str], "GameObject names to copy"] | None = None,
    open_new: Annotated[bool, "Open the new scene after duplication"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {"action": action, "new_scene_path": new_scene_path, "target_scene_path": target_scene_path, "objects": objects, "open_new": open_new}.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "duplicate_scene_setup", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
