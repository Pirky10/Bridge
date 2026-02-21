"""
Defines the manage_render_pipeline tool for Unity URP/HDRP/Built-in settings.
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
        "Manages Unity Render Pipeline settings: query pipeline info, switch pipelines, "
        "configure URP/HDRP settings, set color space, configure per-camera rendering. "
        "Actions: get_info, set_render_pipeline, configure_urp, configure_hdrp, "
        "set_color_space, configure_camera_rendering."
    ),
    annotations=ToolAnnotations(
        title="Manage Render Pipeline",
        destructiveHint=True,
    ),
)
async def manage_render_pipeline(
    ctx: Context,
    action: Annotated[Literal[
        "get_info", "set_render_pipeline", "configure_urp",
        "configure_hdrp", "set_color_space", "configure_camera_rendering"
    ], "Action to perform."],

    target: Annotated[str, "Camera GameObject for configure_camera_rendering"] | None = None,
    pipeline_path: Annotated[str, "Path to RenderPipelineAsset (null = built-in)"] | None = None,
    color_space: Annotated[str, "Color space: Linear or Gamma"] | None = None,

    # URP settings
    hdr: Annotated[bool, "Enable HDR"] | None = None,
    msaa: Annotated[int, "MSAA sample count (1=off, 2, 4, 8)"] | None = None,
    render_scale: Annotated[float, "Render scale (0.1-2.0)"] | None = None,
    shadow_distance: Annotated[float, "Shadow draw distance"] | None = None,
    shadow_cascades: Annotated[int, "Number of shadow cascades"] | None = None,
    soft_shadows: Annotated[bool, "Enable soft shadows"] | None = None,

    # Camera rendering
    dynamic_resolution: Annotated[bool, "Enable dynamic resolution"] | None = None,
    occlusion_culling: Annotated[bool, "Enable occlusion culling"] | None = None,
    rendering_path: Annotated[str, "Rendering path: Forward, Deferred, VertexLit"] | None = None,
    render_type: Annotated[str, "URP camera type: Base, Overlay"] | None = None,
    post_processing: Annotated[bool, "Enable post-processing on URP camera"] | None = None,
    anti_aliasing: Annotated[int, "URP anti-aliasing mode (0=None, 1=FXAA, 2=SMAA)"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action, "target": target,
        "pipeline_path": pipeline_path, "color_space": color_space,
        "hdr": hdr, "msaa": msaa, "render_scale": render_scale,
        "shadow_distance": shadow_distance, "shadow_cascades": shadow_cascades,
        "soft_shadows": soft_shadows,
        "dynamic_resolution": dynamic_resolution,
        "occlusion_culling": occlusion_culling,
        "rendering_path": rendering_path,
        "render_type": render_type, "post_processing": post_processing,
        "anti_aliasing": anti_aliasing,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_render_pipeline",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
