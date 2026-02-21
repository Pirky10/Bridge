"""
AI-powered debugging assistant for Unity projects.
Gathers error context and returns structured debugging info.
"""
from typing import Annotated, Any

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    unity_target=None,
    description=(
        "Debugs Unity issues by analyzing error messages, console logs, stack traces, "
        "and relevant script content. Identifies root causes and suggests fixes. "
        "Can analyze NullReferenceException, MissingComponentException, shader errors, "
        "physics issues, performance bottlenecks, and more."
    ),
    annotations=ToolAnnotations(
        title="Debug Issue",
        readOnlyHint=True,
    ),
)
async def debug_issue(
    ctx: Context,
    error_message: Annotated[str, "The error message or description of the issue"],
    script_path: Annotated[str, "Path to the relevant script"] | None = None,
    stack_trace: Annotated[str, "Stack trace if available"] | None = None,
    steps_to_reproduce: Annotated[str, "Steps to reproduce the issue"] | None = None,
    what_was_tried: Annotated[str, "What has already been tried to fix it"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    # Gather console logs from Unity
    context_data = {}
    try:
        logs = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "get_logs", {}
        )
        context_data["console_logs"] = logs
    except Exception:
        pass

    # Read the relevant script if provided
    if script_path:
        try:
            script = await send_with_unity_instance(
                async_send_command_with_retry, unity_instance,
                "read_file", {"path": script_path}
            )
            context_data["script_content"] = script
        except Exception:
            pass

    debug_info = {
        "error_message": error_message,
        "stack_trace": stack_trace,
        "script_path": script_path,
        "steps_to_reproduce": steps_to_reproduce,
        "what_was_tried": what_was_tried,
        "common_unity_errors": {
            "NullReferenceException": "Object reference not set — check if component exists, if Start() ran before Awake(), if object was destroyed",
            "MissingComponentException": "Component was removed or never added — use TryGetComponent or null check",
            "MissingReferenceException": "Object was destroyed but still referenced — unsubscribe events, clear references",
            "InvalidOperationException": "Wrong context — e.g., modifying collection during iteration",
            "IndexOutOfRangeException": "Array/list access out of bounds — check collection size",
        },
        "project_context": context_data,
        "instructions": (
            "Analyze the error in the context of a Unity project. "
            "Identify the root cause, explain why it happens, and provide "
            "a concrete fix with code examples. Check the console logs for "
            "related warnings/errors."
        ),
    }

    return {"success": True, "message": "Debug context gathered", "data": debug_info}
