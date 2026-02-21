"""
Defines the manage_physics_2d tool for Unity 2D physics operations.
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
        "Manages Unity 2D physics: Rigidbody2D, Collider2D (Box, Circle, Capsule, Polygon, Edge, Composite), "
        "Joint2D (Fixed, Distance, Spring, Hinge, Slider, Wheel, Friction, Relative, Target), "
        "PhysicsMaterial2D, Effector2D (Area, Buoyancy, Point, Platform, Surface), raycasting, gravity. "
        "Actions: add_rigidbody2d, configure_rigidbody2d, add_collider2d, configure_collider2d, "
        "add_joint2d, create_physics_material_2d, add_effector2d, raycast2d, set_gravity2d, get_physics2d_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Physics 2D",
        destructiveHint=True,
    ),
)
async def manage_physics_2d(
    ctx: Context,
    action: Annotated[Literal[
        "add_rigidbody2d", "configure_rigidbody2d",
        "add_collider2d", "configure_collider2d",
        "add_joint2d", "create_physics_material_2d",
        "add_effector2d", "raycast2d",
        "set_gravity2d", "get_physics2d_info"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name"] | None = None,

    # Rigidbody2D
    body_type: Annotated[str, "Body type: Dynamic, Kinematic, Static"] | None = None,
    mass: Annotated[float, "Rigidbody mass"] | None = None,
    linear_drag: Annotated[float, "Linear drag"] | None = None,
    angular_drag: Annotated[float, "Angular drag"] | None = None,
    gravity_scale: Annotated[float, "Gravity scale multiplier"] | None = None,
    freeze_rotation: Annotated[bool, "Freeze Z rotation"] | None = None,
    simulated: Annotated[bool, "Whether body is simulated"] | None = None,
    collision_detection: Annotated[str, "Collision detection: Discrete, Continuous"] | None = None,
    interpolation: Annotated[str, "Interpolation: None, Interpolate, Extrapolate"] | None = None,

    # Collider2D
    collider_type: Annotated[str, "Box, Circle, Capsule, Polygon, Edge, Composite"] | None = None,
    size: Annotated[list[float], "Collider size [w, h]"] | None = None,
    radius: Annotated[float, "Circle collider radius"] | None = None,
    offset: Annotated[list[float], "Collider offset [x, y]"] | None = None,
    is_trigger: Annotated[bool, "Is trigger collider"] | None = None,
    direction: Annotated[str, "Capsule direction: Vertical, Horizontal"] | None = None,
    material_path: Annotated[str, "PhysicsMaterial2D asset path"] | None = None,

    # Joint2D
    joint_type: Annotated[str, "Fixed, Distance, Spring, Hinge, Slider, Wheel, Friction, Relative, Target"] | None = None,
    connected_to: Annotated[str, "Connected GameObject name"] | None = None,
    enable_collision: Annotated[bool, "Enable collision between connected bodies"] | None = None,
    frequency: Annotated[float, "Spring joint frequency"] | None = None,
    damping_ratio: Annotated[float, "Spring joint damping ratio"] | None = None,

    # PhysicsMaterial2D
    path: Annotated[str, "Asset path for new physics material"] | None = None,
    bounciness: Annotated[float, "Material bounciness (0-1)"] | None = None,
    friction: Annotated[float, "Material friction"] | None = None,

    # Effector2D
    effector_type: Annotated[str, "Area, Buoyancy, Point, Platform, Surface"] | None = None,
    force_angle: Annotated[float, "Area effector force angle"] | None = None,
    force_magnitude: Annotated[float, "Effector force magnitude"] | None = None,
    surface_level: Annotated[float, "Buoyancy surface level"] | None = None,
    density: Annotated[float, "Buoyancy density"] | None = None,
    speed: Annotated[float, "Surface effector speed"] | None = None,

    # Raycast
    origin: Annotated[list[float], "Raycast origin [x, y]"] | None = None,
    ray_direction: Annotated[list[float], "Raycast direction [x, y]"] | None = None,
    distance: Annotated[float, "Raycast max distance"] | None = None,
    layer_mask: Annotated[int, "Raycast layer mask"] | None = None,

    # Gravity
    gravity: Annotated[list[float], "2D gravity [x, y]"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target,
        "body_type": body_type, "mass": mass,
        "linear_drag": linear_drag, "angular_drag": angular_drag,
        "gravity_scale": gravity_scale, "freeze_rotation": freeze_rotation,
        "simulated": simulated, "collision_detection": collision_detection,
        "interpolation": interpolation,
        "collider_type": collider_type, "size": size, "radius": radius,
        "offset": offset, "is_trigger": is_trigger, "direction": direction,
        "material_path": material_path,
        "joint_type": joint_type, "connected_to": connected_to,
        "enable_collision": enable_collision,
        "frequency": frequency, "damping_ratio": damping_ratio,
        "path": path, "bounciness": bounciness, "friction": friction,
        "effector_type": effector_type, "force_angle": force_angle,
        "force_magnitude": force_magnitude, "surface_level": surface_level,
        "density": density, "speed": speed,
        "origin": origin, "direction": ray_direction, "distance": distance,
        "layer_mask": layer_mask, "gravity": gravity,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_physics_2d",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
