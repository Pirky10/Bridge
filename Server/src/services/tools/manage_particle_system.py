"""
Defines the manage_particle_system tool for Unity particle systems.
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
        "Manages Unity Particle Systems (Shuriken): creation, main/emission/shape/renderer/color/size modules. "
        "Actions: create, configure_main, configure_emission, configure_shape, configure_renderer, "
        "configure_color_over_lifetime, configure_size_over_lifetime, play, stop, get_particle_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Particle System",
        destructiveHint=True,
    ),
)
async def manage_particle_system(
    ctx: Context,
    action: Annotated[Literal[
        "create", "configure_main", "configure_emission", "configure_shape",
        "configure_renderer", "configure_color_over_lifetime",
        "configure_size_over_lifetime", "play", "stop", "get_particle_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,
    name: Annotated[str, "Name for new particle system"] | None = None,
    position: Annotated[list[float], "Position [x, y, z]"] | None = None,

    # Main module
    duration: Annotated[float, "System duration in seconds"] | None = None,
    start_lifetime: Annotated[float, "Particle lifetime"] | None = None,
    start_speed: Annotated[float, "Particle start speed"] | None = None,
    start_size: Annotated[float, "Particle start size"] | None = None,
    start_color: Annotated[list[float], "Start color [r,g,b] or [r,g,b,a]"] | None = None,
    start_rotation: Annotated[float, "Start rotation in degrees"] | None = None,
    max_particles: Annotated[int, "Maximum particles"] | None = None,
    looping: Annotated[bool, "Whether system loops"] | None = None,
    play_on_awake: Annotated[bool, "Play on awake"] | None = None,
    prewarm: Annotated[bool, "Prewarm the system"] | None = None,
    simulation_space: Annotated[str, "Simulation space: Local, World, Custom"] | None = None,
    gravity_modifier: Annotated[float, "Gravity multiplier"] | None = None,

    # Emission
    enabled: Annotated[bool, "Enable/disable module"] | None = None,
    rate_over_time: Annotated[float, "Emission rate over time"] | None = None,
    rate_over_distance: Annotated[float, "Emission rate over distance"] | None = None,
    bursts: Annotated[list, "Burst configs [{time, count, cycles, interval}, ...]"] | None = None,

    # Shape
    shape_type: Annotated[str, "Shape: Sphere, Hemisphere, Cone, Box, Circle, Edge, etc."] | None = None,
    radius: Annotated[float, "Shape radius"] | None = None,
    angle: Annotated[float, "Cone angle"] | None = None,
    arc: Annotated[float, "Shape arc"] | None = None,
    radius_thickness: Annotated[float, "Radius thickness (0=surface, 1=volume)"] | None = None,
    scale: Annotated[list[float], "Shape scale [x, y, z]"] | None = None,

    # Renderer
    render_mode: Annotated[str, "Render mode: Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh"] | None = None,
    material_path: Annotated[str, "Material asset path"] | None = None,
    sort_mode: Annotated[str, "Sort mode: None, Distance, OldestInFront, YoungestInFront"] | None = None,
    min_particle_size: Annotated[float, "Min particle size on screen"] | None = None,
    max_particle_size: Annotated[float, "Max particle size on screen"] | None = None,

    # Color/Size over lifetime
    end_color: Annotated[list[float], "End color [r,g,b,a]"] | None = None,
    start_scale: Annotated[float, "Start scale for size over lifetime"] | None = None,
    end_scale: Annotated[float, "End scale for size over lifetime"] | None = None,

    # Playback
    with_children: Annotated[bool, "Affect child systems"] | None = None,
    clear: Annotated[bool, "Clear particles on stop"] | None = None,

) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target, "name": name, "position": position,
        "duration": duration, "start_lifetime": start_lifetime,
        "start_speed": start_speed, "start_size": start_size,
        "start_color": start_color, "start_rotation": start_rotation,
        "max_particles": max_particles, "looping": looping,
        "play_on_awake": play_on_awake, "prewarm": prewarm,
        "simulation_space": simulation_space, "gravity_modifier": gravity_modifier,
        "enabled": enabled, "rate_over_time": rate_over_time,
        "rate_over_distance": rate_over_distance, "bursts": bursts,
        "shape_type": shape_type, "radius": radius, "angle": angle,
        "arc": arc, "radius_thickness": radius_thickness, "scale": scale,
        "render_mode": render_mode, "material_path": material_path,
        "sort_mode": sort_mode, "min_particle_size": min_particle_size,
        "max_particle_size": max_particle_size,
        "end_color": end_color, "start_scale": start_scale, "end_scale": end_scale,
        "with_children": with_children, "clear": clear,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_particle_system",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
