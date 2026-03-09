"""
AI-powered code explanation for Unity C# scripts.
Reads script content and returns structured explanation context.
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
        "Explains Unity C# code: what it does, how it works, and why certain patterns "
        "are used. Can explain entire scripts, specific methods, Unity APIs, design "
        "patterns, and the relationship between components."
    ),
    annotations=ToolAnnotations(
        title="Explain Code",
        readOnlyHint=True,
    ),
)
async def explain_code(
    ctx: Context,
    script_path: Annotated[str, "Path to the script to explain (Assets/Scripts/...)"],
    specific_section: Annotated[str, "Specific method/class/section to focus on"] | None = None,
    detail_level: Annotated[str, "Detail level: beginner, intermediate, advanced"] | None = None,
    explain_unity_apis: Annotated[bool, "Also explain Unity-specific APIs used"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    try:
        script_result = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "read_file", {"path": script_path}
        )
    except Exception as e:
        return {"success": False, "message": f"Could not read script: {e}"}

    explanation = {
        "script_path": script_path,
        "detail_level": detail_level or "intermediate",
        "specific_section": specific_section,
        "explain_unity_apis": explain_unity_apis if explain_unity_apis is not None else True,
        "script_content": script_result,
        "instructions": (
            f"Explain this Unity C# script at a {detail_level or 'intermediate'} level. "
            f"{'Focus specifically on: ' + specific_section + '. ' if specific_section else ''}"
            "Cover: what the code does, how it works step-by-step, what Unity APIs "
            "it uses and why, any design patterns employed, and how it interacts "
            "with other components in the scene."
        ),
    }

    return {"success": True, "message": f"Script loaded for explanation: {script_path}", "data": explanation}
