"""
Defines the manage_lighting tool for Unity lighting operations.
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
        "Manages Unity lighting: create/configure lights, light probes, reflection probes, ambient settings. "
        "Actions: create_light, configure_light, add_light_probe_group, add_reflection_probe, set_ambient, get_lighting_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Lighting",
        destructiveHint=True,
    ),
)
async def manage_lighting(
    ctx: Context,
    action: Annotated[Literal[
        "create_light", "configure_light",
        "add_light_probe_group", "add_reflection_probe",
        "set_ambient", "get_lighting_info"
    ], "Action to perform."],

    target: Annotated[str, "Target light GameObject name"] | None = None,
    name: Annotated[str, "Name for new light/probe"] | None = None,
    light_type: Annotated[str, "Light type: Directional, Point, Spot, Area"] | None = None,
    position: Annotated[list[float], "Position [x, y, z]"] | None = None,
    rotation: Annotated[list[float], "Euler rotation [x, y, z]"] | None = None,
    color: Annotated[list[float], "Color [r, g, b] or [r, g, b, a] (0-1)"] | None = None,
    intensity: Annotated[float, "Light/ambient intensity"] | None = None,
    range: Annotated[float, "Light range"] | None = None,
    spot_angle: Annotated[float, "Spot light angle"] | None = None,
    shadows: Annotated[str, "Shadow type: None, Hard, Soft"] | None = None,
    shadow_strength: Annotated[float, "Shadow strength (0-1)"] | None = None,
    bounce_intensity: Annotated[float, "Indirect light multiplier"] | None = None,
    size: Annotated[list[float], "Probe size [x, y, z]"] | None = None,
    resolution: Annotated[int, "Reflection probe resolution"] | None = None,
    mode: Annotated[str, "Ambient mode (Skybox, Trilight, Flat) or probe mode (Baked, Realtime, Custom)"] | None = None,
    probe_positions: Annotated[list[list[float]], "Light probe positions [[x,y,z], ...]"] | None = None,
    sky_color: Annotated[list[float], "Ambient sky color [r, g, b]"] | None = None,
    equator_color: Annotated[list[float], "Ambient equator color [r, g, b]"] | None = None,
    ground_color: Annotated[list[float], "Ambient ground color [r, g, b]"] | None = None,
    fog: Annotated[bool, "Enable/disable fog"] | None = None,
    fog_color: Annotated[list[float], "Fog color [r, g, b]"] | None = None,
    fog_density: Annotated[float, "Fog density"] | None = None,

) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "name": name,
        "light_type": light_type, "position": position, "rotation": rotation,
        "color": color, "intensity": intensity, "range": range,
        "spot_angle": spot_angle, "shadows": shadows,
        "shadow_strength": shadow_strength, "bounce_intensity": bounce_intensity,
        "size": size, "resolution": resolution, "mode": mode,
        "probe_positions": probe_positions,
        "sky_color": sky_color, "equator_color": equator_color,
        "ground_color": ground_color,
        "fog": fog, "fog_color": fog_color, "fog_density": fog_density,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_lighting",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
