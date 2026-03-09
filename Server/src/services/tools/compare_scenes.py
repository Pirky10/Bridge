"""
compare_scenes tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Compares two Unity scene files and finds differences in GameObjects, components, and properties. Actions: compare, list_differences, generate_diff_report.",
    annotations=ToolAnnotations(title="Compare Scenes"),
)
async def compare_scenes(
    ctx: Context,
    action: Annotated[Literal["compare", "list_differences", "generate_diff_report"], "Action to perform"],
    source_scene: Annotated[str, "Asset path to the source scene file"],
    target_scene: Annotated[str, "Asset path to the target scene to compare against"],
    compare_options: Annotated[dict[str, Any], "Optional flags for comparison (e.g. ignore_transform, deep_compare)"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action,
        "source_scene": source_scene,
        "target_scene": target_scene,
        "compare_options": compare_options
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "compare_scenes", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
