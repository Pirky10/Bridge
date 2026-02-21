"""
Defines the manage_input_actions tool for Unity Input System.
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
        "Manages Unity Input System action assets. Requires com.unity.inputsystem package. "
        "Actions: create_asset, add_action_map, add_action, add_binding, get_info, assign_to_player_input."
    ),
    annotations=ToolAnnotations(
        title="Manage Input Actions",
        destructiveHint=True,
    ),
)
async def manage_input_actions(
    ctx: Context,
    action: Annotated[Literal[
        "create_asset", "add_action_map", "add_action",
        "add_binding", "get_info", "assign_to_player_input"
    ], "Action to perform."],

    path: Annotated[str, "Path to .inputactions asset (Assets/...)"] | None = None,
    map_name: Annotated[str, "Action map name"] | None = None,
    action_name: Annotated[str, "Input action name"] | None = None,
    action_type: Annotated[str, "Action type: Button, Value, PassThrough"] | None = None,
    binding_path: Annotated[str, "Binding path (e.g., '<Keyboard>/w', '<Gamepad>/leftStick')"] | None = None,
    target: Annotated[str, "Target GameObject for PlayerInput assignment"] | None = None,
    default_map: Annotated[str, "Default action map name"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "path": path, "map_name": map_name,
        "action_name": action_name, "action_type": action_type,
        "binding_path": binding_path, "target": target,
        "default_map": default_map,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_input_actions",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
