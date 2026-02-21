"""
AI-powered code review for Unity C# scripts.
Reads script content and returns structured review context.
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
        "Reviews Unity C# scripts for quality, performance, best practices, and bugs. "
        "Reads the script content from the project and analyzes it. "
        "Checks for: Unity anti-patterns, performance issues, null reference risks, "
        "memory leaks, incorrect API usage, and suggests improvements."
    ),
    annotations=ToolAnnotations(
        title="Review Code",
        readOnlyHint=True,
    ),
)
async def review_code(
    ctx: Context,
    script_path: Annotated[str, "Path to the script to review (Assets/Scripts/...)"],
    focus_areas: Annotated[str, "Specific areas to focus on: performance, security, readability, best_practices, bugs"] | None = None,
    context: Annotated[str, "Additional context about what the code does"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    # Read the script content via Unity
    try:
        script_result = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "read_file", {"path": script_path}
        )
    except Exception as e:
        return {"success": False, "message": f"Could not read script: {e}"}

    review = {
        "script_path": script_path,
        "focus_areas": focus_areas or "all",
        "review_checklist": {
            "unity_best_practices": [
                "Proper use of MonoBehaviour lifecycle (Awake, Start, Update)",
                "Avoiding Find() calls in Update",
                "Caching component references",
                "Using SerializeField instead of public fields",
                "Proper use of coroutines vs async/await",
            ],
            "performance": [
                "No allocations in hot paths (Update, FixedUpdate)",
                "Using object pooling where appropriate",
                "Avoiding string concatenation in loops",
                "Proper use of Physics.Raycast (non-alloc versions)",
                "Camera.main caching",
            ],
            "common_bugs": [
                "Null reference risks (missing null checks)",
                "Race conditions in initialization order",
                "Missing Unsubscribe from events",
                "Incorrect use of == vs .Equals for Unity objects",
                "Missing Dispose/cleanup calls",
            ],
        },
        "script_content": script_result,
        "instructions": (
            "Review this Unity C# script against the checklist above. "
            "Provide specific line-by-line feedback with severity levels "
            "(critical, warning, suggestion) and concrete fix examples."
        ),
    }

    return {"success": True, "message": f"Script loaded for review: {script_path}", "data": review}
