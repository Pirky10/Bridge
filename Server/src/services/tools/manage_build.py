"""
Defines the manage_build tool for Unity build operations.
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
        "Manages Unity build settings and operations. "
        "Actions: get_settings, set_target_platform, add_scene, remove_scene, build, "
        "set_scripting_backend, set_company_name, set_product_name."
    ),
    annotations=ToolAnnotations(
        title="Manage Build",
        destructiveHint=True,
    ),
)
async def manage_build(
    ctx: Context,
    action: Annotated[Literal[
        "get_settings", "set_target_platform", "add_scene", "remove_scene",
        "build", "set_scripting_backend", "set_company_name", "set_product_name"
    ], "Action to perform."],

    platform: Annotated[str, "Build target: StandaloneWindows64, StandaloneOSX, Android, iOS, WebGL, etc."] | None = None,
    scene_path: Annotated[str, "Scene path (Assets/Scenes/MyScene.unity)"] | None = None,
    enabled: Annotated[bool, "Whether scene is enabled in build"] | None = None,
    output_path: Annotated[str, "Build output path"] | None = None,
    development: Annotated[bool, "Development build"] | None = None,
    auto_run: Annotated[bool, "Auto run after build"] | None = None,
    backend: Annotated[str, "Scripting backend: Mono2x, IL2CPP"] | None = None,
    name: Annotated[str, "Company or product name"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "platform": platform,
        "scene_path": scene_path, "enabled": enabled,
        "output_path": output_path, "development": development,
        "auto_run": auto_run, "backend": backend, "name": name,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_build",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
