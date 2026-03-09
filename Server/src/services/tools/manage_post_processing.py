"""manage_post_processing tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages URP/HDRP post-processing Volumes and overrides (Bloom, ColorAdjustments, Vignette, DepthOfField, MotionBlur, etc.). Actions: add_volume, add_override, configure_override, remove_override, set_volume_properties, get_info.",
    annotations=ToolAnnotations(title="Manage Post Processing", destructiveHint=True),
)
async def manage_post_processing(
    ctx: Context,
    action: Annotated[Literal["add_volume", "add_override", "configure_override", "remove_override", "set_volume_properties", "get_info"], "Action"],
    target: Annotated[str, "Target Volume GameObject"] | None = None,
    name: Annotated[str, "Name for new Volume"] | None = None,
    is_global: Annotated[bool, "Global volume"] | None = None,
    priority: Annotated[float, "Volume priority"] | None = None,
    weight: Annotated[float, "Volume weight (0-1)"] | None = None,
    blend_distance: Annotated[float, "Blend distance"] | None = None,
    profile_path: Annotated[str, "Profile asset path"] | None = None,
    override_type: Annotated[str, "Override type: Bloom, ColorAdjustments, Vignette, DepthOfField, MotionBlur, etc."] | None = None,
    intensity: Annotated[float, "Effect intensity"] | None = None,
    threshold: Annotated[float, "Bloom threshold"] | None = None,
    scatter: Annotated[float, "Bloom scatter"] | None = None,
    post_exposure: Annotated[float, "Post exposure"] | None = None,
    contrast: Annotated[float, "Contrast"] | None = None,
    saturation: Annotated[float, "Saturation"] | None = None,
    smoothness: Annotated[float, "Vignette smoothness"] | None = None,
    settings: Annotated[dict, "Generic settings dict {field: value}"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in locals().items() if v is not None and k not in ("ctx", "u", "self")}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_post_processing", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
