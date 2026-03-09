"""
Defines the manage_audio_mixer tool for Unity audio mixer operations.
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
        "Manages Unity AudioMixer: create mixer assets, add groups, set volume/parameters, "
        "create snapshots, expose parameters, assign groups to AudioSources. "
        "Uses AudioMixerController (Editor API) for full programmatic control. "
        "Actions: create, add_group, set_volume, set_float, get_float, "
        "create_snapshot, expose_parameter, assign_to_source, get_mixer_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Audio Mixer",
        destructiveHint=True,
    ),
)
async def manage_audio_mixer(
    ctx: Context,
    action: Annotated[Literal[
        "create", "add_group", "set_volume", "set_float",
        "get_float", "create_snapshot", "expose_parameter",
        "assign_to_source", "get_mixer_info"
    ], "Action to perform."],

    mixer_path: Annotated[str, "Path to AudioMixer asset (Assets/...)"] | None = None,
    path: Annotated[str, "Path for new mixer asset (e.g., Assets/Audio/Main.mixer)"] | None = None,
    group_name: Annotated[str, "Mixer group name"] | None = None,
    parent_group: Annotated[str, "Parent group name (default: Master)"] | None = None,
    parameter_name: Annotated[str, "Exposed parameter name"] | None = None,
    exposed_name: Annotated[str, "Name for exposed parameter"] | None = None,
    volume: Annotated[float, "Volume (0-1, converted to dB)"] | None = None,
    value: Annotated[float, "Raw float parameter value"] | None = None,
    target: Annotated[str, "Target GameObject with AudioSource"] | None = None,
    snapshot_name: Annotated[str, "Name for new snapshot"] | None = None,
    auto_expose_volume: Annotated[bool, "Auto-expose volume when adding group (default: true)"] | None = None,

) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "mixer_path": mixer_path, "path": path,
        "group_name": group_name, "parent_group": parent_group,
        "parameter_name": parameter_name, "exposed_name": exposed_name,
        "volume": volume, "value": value, "target": target,
        "snapshot_name": snapshot_name,
        "auto_expose_volume": auto_expose_volume,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_audio_mixer",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
