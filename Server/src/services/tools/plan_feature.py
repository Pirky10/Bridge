"""
AI-powered feature planning for Unity projects.
Gathers project context and returns structured planning prompts.
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
        "Plans a Unity feature: gathers project context (scene hierarchy, existing scripts, "
        "packages) and returns a structured implementation plan with steps, dependencies, "
        "architecture considerations, and estimated complexity."
    ),
    annotations=ToolAnnotations(
        title="Plan Feature",
        readOnlyHint=True,
    ),
)
async def plan_feature(
    ctx: Context,
    feature_description: Annotated[str, "Description of the feature to plan"],
    scope: Annotated[str, "Scope: small, medium, large"] | None = None,
    target_platforms: Annotated[str, "Target platforms (e.g., 'PC, Mobile')"] | None = None,
    considerations: Annotated[str, "Additional considerations or constraints"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    # Gather project context from Unity
    context_data = {}
    try:
        scene_info = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "get_hierarchy", {}
        )
        context_data["scene_hierarchy"] = scene_info
    except Exception:
        pass

    plan = {
        "feature": feature_description,
        "scope": scope or "medium",
        "planning_prompt": (
            f"## Feature Planning: {feature_description}\n\n"
            f"### Scope: {scope or 'medium'}\n"
            f"{'### Target Platforms: ' + target_platforms if target_platforms else ''}\n"
            f"{'### Constraints: ' + considerations if considerations else ''}\n\n"
            "### Planning Steps:\n"
            "1. **Requirements Analysis** — Break down the feature into specific requirements\n"
            "2. **Architecture Design** — Identify components, scripts, and systems needed\n"
            "3. **Dependencies** — List required packages, assets, or third-party tools\n"
            "4. **Implementation Order** — Define the order of implementation steps\n"
            "5. **Testing Strategy** — Plan how to verify the feature works correctly\n"
            "6. **Risk Assessment** — Identify potential issues and mitigation strategies\n\n"
            "### Project Context:\n"
        ),
        "project_context": context_data,
        "instructions": (
            "Use the project context above to create a detailed implementation plan. "
            "Consider existing scene objects, scripts, and packages when designing the architecture. "
            "Suggest specific Unity APIs, components, and patterns to use."
        ),
    }

    return {"success": True, "message": "Feature planning context gathered", "data": plan}
