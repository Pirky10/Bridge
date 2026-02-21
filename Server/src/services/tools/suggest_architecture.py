"""
AI-powered architecture suggestions for Unity projects.
Analyzes project structure and suggests patterns/architectures.
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
        "Suggests architecture patterns for Unity projects. Analyzes current project "
        "structure and recommends design patterns (MVC, ECS, Singleton, Observer, "
        "State Machine, Command, etc.), folder organization, and system design."
    ),
    annotations=ToolAnnotations(
        title="Suggest Architecture",
        readOnlyHint=True,
    ),
)
async def suggest_architecture(
    ctx: Context,
    project_type: Annotated[str, "Project type: 2D_platformer, 3D_fps, RPG, puzzle, simulation, VR, etc."],
    requirements: Annotated[str, "Key requirements or features the architecture should support"] | None = None,
    current_issues: Annotated[str, "Current architectural issues or pain points"] | None = None,
    team_size: Annotated[str, "Team size: solo, small (2-5), medium (5-15), large (15+)"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    # Gather project context
    context_data = {}
    try:
        hierarchy = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "get_hierarchy", {}
        )
        context_data["hierarchy"] = hierarchy
    except Exception:
        pass

    suggestion = {
        "project_type": project_type,
        "team_size": team_size or "solo",
        "common_patterns": {
            "2D_platformer": ["State Machine for player", "Observer for events", "Object Pool for projectiles"],
            "3D_fps": ["Component-based architecture", "State Machine for AI", "Command pattern for input", "Object Pool for bullets"],
            "RPG": ["Scriptable Object architecture", "Event-driven systems", "Inventory with Strategy pattern", "Dialog system with State Machine"],
            "puzzle": ["MVC pattern", "Command pattern for undo/redo", "Observer for UI updates"],
            "simulation": ["ECS architecture", "Event bus for systems", "Data-driven design"],
            "VR": ["State Machine for interactions", "Observer for hand tracking", "Object Pool for physics objects"],
        },
        "recommended_folder_structure": {
            "Assets/": {
                "Scripts/": {"Core/": "Singletons, managers", "Gameplay/": "Game mechanics", "UI/": "UI controllers", "Systems/": "Subsystems", "Data/": "ScriptableObjects, configs"},
                "Prefabs/": "Reusable game objects",
                "Scenes/": "Scene files",
                "Art/": {"Materials/", "Textures/", "Models/", "Animations/"},
                "Audio/": {"SFX/", "Music/", "Mixers/"},
                "Resources/": "Runtime-loaded assets",
                "ScriptableObjects/": "Data assets",
            }
        },
        "project_context": context_data,
        "requirements": requirements,
        "current_issues": current_issues,
        "instructions": (
            "Analyze the project context and suggest a comprehensive architecture. "
            "Include: design patterns, folder structure, key systems, data flow, "
            "and a migration plan if refactoring from an existing codebase."
        ),
    }

    return {"success": True, "message": "Architecture analysis ready", "data": suggestion}
