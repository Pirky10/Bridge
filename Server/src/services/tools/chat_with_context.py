"""
Contextual AI chat for Unity projects.
Gathers project context to enable more informed responses.
"""
from typing import Annotated, Any

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    description=(
        "Provides contextual AI chat with awareness of the Unity project. "
        "Gathers relevant project context (hierarchy, scripts, settings, logs) "
        "to answer questions with project-specific knowledge."
    ),
    annotations=ToolAnnotations(
        title="Chat With Context",
        readOnlyHint=True,
    ),
)
async def chat_with_context(
    ctx: Context,
    question: Annotated[str, "Question or topic to discuss"],
    context_sources: Annotated[str, "Context to gather: hierarchy, scripts, settings, logs, all"] | None = None,
    script_path: Annotated[str, "Specific script to include as context"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    sources = (context_sources or "all").lower().split(",")
    sources = [s.strip() for s in sources]
    gather_all = "all" in sources

    context_data = {}

    if gather_all or "hierarchy" in sources:
        try:
            hierarchy = await send_with_unity_instance(
                async_send_command_with_retry, unity_instance,
                "get_hierarchy", {}
            )
            context_data["scene_hierarchy"] = hierarchy
        except Exception:
            pass

    if gather_all or "logs" in sources:
        try:
            logs = await send_with_unity_instance(
                async_send_command_with_retry, unity_instance,
                "get_logs", {}
            )
            context_data["console_logs"] = logs
        except Exception:
            pass

    if script_path:
        try:
            script = await send_with_unity_instance(
                async_send_command_with_retry, unity_instance,
                "read_file", {"path": script_path}
            )
            context_data["script_content"] = script
        except Exception:
            pass

    chat = {
        "question": question,
        "project_context": context_data,
        "instructions": (
            "Answer the question using the gathered Unity project context. "
            "Be specific to this project — reference actual GameObjects, scripts, "
            "and components found in the context. If suggesting code changes, "
            "use the project's existing patterns and naming conventions."
        ),
    }

    return {"success": True, "message": "Context gathered for chat", "data": chat}
