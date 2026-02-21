"""
Defines the manage_physics tool for Unity physics operations.
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
        "Manages Unity physics: Rigidbodies, Colliders, Physics Materials, Joints, gravity, and raycasting. "
        "Actions: add_rigidbody, configure_rigidbody, add_collider, configure_collider, "
        "create_physics_material, add_joint, configure_joint, set_gravity, raycast, get_physics_info."
    ),
    annotations=ToolAnnotations(
        title="Manage Physics",
        destructiveHint=True,
    ),
)
async def manage_physics(
    ctx: Context,
    action: Annotated[Literal[
        "add_rigidbody", "configure_rigidbody",
        "add_collider", "configure_collider",
        "create_physics_material",
        "add_joint", "configure_joint",
        "set_gravity", "raycast", "get_physics_info"
    ], "Action to perform."],

    # Common
    target: Annotated[str, "Target GameObject name or path"] | None = None,

    # Rigidbody
    mass: Annotated[float, "Rigidbody mass"] | None = None,
    drag: Annotated[float, "Linear drag"] | None = None,
    angular_drag: Annotated[float, "Angular drag"] | None = None,
    use_gravity: Annotated[bool, "Whether rigidbody uses gravity"] | None = None,
    is_kinematic: Annotated[bool, "Whether rigidbody is kinematic"] | None = None,
    interpolation: Annotated[str, "Interpolation mode: None, Interpolate, Extrapolate"] | None = None,
    collision_detection: Annotated[str, "Collision detection: Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative"] | None = None,
    constraints: Annotated[list[str], "Rigidbody constraints list e.g. ['FreezePositionX', 'FreezeRotationY']"] | None = None,

    # Collider
    collider_type: Annotated[str, "Collider type: box, sphere, capsule, mesh"] | None = None,
    is_trigger: Annotated[bool, "Whether collider is a trigger"] | None = None,
    size: Annotated[list[float], "Box collider size [x, y, z]"] | None = None,
    center: Annotated[list[float], "Collider center [x, y, z]"] | None = None,
    radius: Annotated[float, "Sphere/capsule collider radius"] | None = None,
    height: Annotated[float, "Capsule collider height"] | None = None,
    direction: Annotated[int, "Capsule direction (0=X, 1=Y, 2=Z)"] | None = None,
    convex: Annotated[bool, "Whether mesh collider is convex"] | None = None,
    physics_material: Annotated[str, "Path to physics material asset"] | None = None,

    # Physics Material creation
    path: Annotated[str, "Path to create physics material (Assets/...)"] | None = None,
    dynamic_friction: Annotated[float, "Dynamic friction (0-1)"] | None = None,
    static_friction: Annotated[float, "Static friction (0-1)"] | None = None,
    bounciness: Annotated[float, "Bounciness (0-1)"] | None = None,
    friction_combine: Annotated[str, "Friction combine mode: Average, Minimum, Maximum, Multiply"] | None = None,
    bounce_combine: Annotated[str, "Bounce combine mode: Average, Minimum, Maximum, Multiply"] | None = None,

    # Joint
    joint_type: Annotated[str, "Joint type: fixed, hinge, spring, character, configurable"] | None = None,
    connected_body: Annotated[str, "Connected body GameObject name"] | None = None,
    break_force: Annotated[float, "Force needed to break the joint"] | None = None,
    break_torque: Annotated[float, "Torque needed to break the joint"] | None = None,
    axis: Annotated[list[float], "Joint axis [x, y, z]"] | None = None,
    spring_force: Annotated[float, "Spring force for spring joints"] | None = None,
    damper: Annotated[float, "Damper value for spring joints"] | None = None,

    # Gravity
    gravity: Annotated[list[float], "Global gravity vector [x, y, z]"] | None = None,

    # Raycast
    origin: Annotated[list[float], "Raycast origin [x, y, z]"] | None = None,
    direction_vec: Annotated[list[float], "Raycast direction [x, y, z]"] | None = None,
    max_distance: Annotated[float, "Maximum raycast distance"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action,
        "target": target,
        "mass": mass,
        "drag": drag,
        "angular_drag": angular_drag,
        "use_gravity": use_gravity,
        "is_kinematic": is_kinematic,
        "interpolation": interpolation,
        "collision_detection": collision_detection,
        "constraints": constraints,
        "collider_type": collider_type,
        "is_trigger": is_trigger,
        "size": size,
        "center": center,
        "radius": radius,
        "height": height,
        "direction": direction,
        "convex": convex,
        "physics_material": physics_material,
        "path": path,
        "dynamic_friction": dynamic_friction,
        "static_friction": static_friction,
        "bounciness": bounciness,
        "friction_combine": friction_combine,
        "bounce_combine": bounce_combine,
        "joint_type": joint_type,
        "connected_body": connected_body,
        "break_force": break_force,
        "break_torque": break_torque,
        "axis": axis,
        "spring_force": spring_force,
        "damper": damper,
        "gravity": gravity,
        "origin": origin,
        "direction": direction_vec,
        "max_distance": max_distance,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_physics",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
