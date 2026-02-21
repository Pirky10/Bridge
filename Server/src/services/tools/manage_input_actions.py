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
        "Actions: create_asset, add_action_map, add_action, add_binding, get_info, assign_to_player_input, "
        "remove_action_map, remove_action, remove_binding, modify_action, modify_binding, "
        "add_composite_binding, set_interactions, set_processors."
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
        "add_binding", "get_info", "assign_to_player_input",
        "remove_action_map", "remove_action", "remove_binding",
        "modify_action", "modify_binding",
        "add_composite_binding", "set_interactions", "set_processors"
    ], "Action to perform."],

    path: Annotated[str, "Path to .inputactions asset (Assets/...)"] | None = None,
    map_name: Annotated[str, "Action map name"] | None = None,
    action_name: Annotated[str, "Input action name"] | None = None,
    action_type: Annotated[str, "Action type: Button, Value, PassThrough"] | None = None,
    binding_path: Annotated[str, "Binding path (e.g., '<Keyboard>/w', '<Gamepad>/leftStick')"] | None = None,
    target: Annotated[str, "Target GameObject for PlayerInput assignment"] | None = None,
    default_map: Annotated[str, "Default action map name"] | None = None,

    # modify_action / modify_binding
    new_name: Annotated[str, "New name for renaming an action or map"] | None = None,
    new_action_type: Annotated[str, "New action type for modify_action (Button, Value, PassThrough)"] | None = None,
    new_binding_path: Annotated[str, "New binding path for modify_binding"] | None = None,
    binding_index: Annotated[int, "Index of binding to modify or remove (0-based)"] | None = None,

    # add_composite_binding
    composite_type: Annotated[str, "Composite type (e.g. '2DVector', '1DAxis', 'ButtonWithOneModifier')"] | None = None,
    composite_parts: Annotated[dict[str, str] | None, "Dict of part name to binding path, e.g. {'up': '<Keyboard>/w', 'down': '<Keyboard>/s'}"] | None = None,

    # set_interactions / set_processors
    interactions: Annotated[str, "Interaction string to set on action/binding (e.g. 'Hold(duration=0.5)')"] | None = None,
    processors: Annotated[str, "Processor string to set on action/binding (e.g. 'InvertVector2(invertX=true)')"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "path": path, "map_name": map_name,
        "action_name": action_name, "action_type": action_type,
        "binding_path": binding_path, "target": target,
        "default_map": default_map,
        "new_name": new_name, "new_action_type": new_action_type,
        "new_binding_path": new_binding_path, "binding_index": binding_index,
        "composite_type": composite_type, "composite_parts": composite_parts,
        "interactions": interactions, "processors": processors,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_input_actions",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
