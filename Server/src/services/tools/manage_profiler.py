"""
Defines the manage_profiler tool for Unity profiler operations.
"""
from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    description=(
        "Queries Unity profiler data: performance stats, memory usage, frame timing, recording. "
        "Actions: get_stats, start_recording, stop_recording, get_memory_info, get_frame_timing."
    ),
    annotations=ToolAnnotations(
        title="Manage Profiler",
        readOnlyHint=True,
    ),
)
async def manage_profiler(
    ctx: Context,
    action: Annotated[Literal[
        "get_stats", "start_recording", "stop_recording",
        "get_memory_info", "get_frame_timing"
    ], "Action to perform."],

    log_file: Annotated[str, "Path for profiler log file (for start_recording)"] | None = None,

) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict = {"action": action}
    if log_file is not None:
        params_dict["log_file"] = log_file

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_profiler",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
