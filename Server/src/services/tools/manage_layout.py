"""
Defines the manage_layout tool for Unity UI Layout components.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity UI Layout components: HorizontalLayoutGroup, VerticalLayoutGroup, GridLayoutGroup, ContentSizeFitter, LayoutElement. Actions: add_horizontal, add_vertical, add_grid, add_content_size_fitter, add_layout_element, configure, get_info.",
    annotations=ToolAnnotations(title="Manage Layout", destructiveHint=True),
)
async def manage_layout(
    ctx: Context,
    action: Annotated[Literal["add_horizontal", "add_vertical", "add_grid", "add_content_size_fitter", "add_layout_element", "configure", "get_info"], "Action"],
    target: Annotated[str, "Target GameObject"] | None = None,
    spacing: Annotated[float, "Spacing between elements"] | None = None,
    padding: Annotated[int, "Uniform padding"] | None = None,
    padding_left: Annotated[int, "Left padding"] | None = None,
    padding_right: Annotated[int, "Right padding"] | None = None,
    padding_top: Annotated[int, "Top padding"] | None = None,
    padding_bottom: Annotated[int, "Bottom padding"] | None = None,
    child_alignment: Annotated[str, "Child alignment (e.g. MiddleCenter, UpperLeft)"] | None = None,
    child_force_expand_width: Annotated[bool, "Force expand width"] | None = None,
    child_force_expand_height: Annotated[bool, "Force expand height"] | None = None,
    child_control_width: Annotated[bool, "Control child width"] | None = None,
    child_control_height: Annotated[bool, "Control child height"] | None = None,
    cell_size: Annotated[list[float], "Grid cell size [w, h]"] | None = None,
    start_corner: Annotated[str, "Grid start corner: UpperLeft, UpperRight, LowerLeft, LowerRight"] | None = None,
    start_axis: Annotated[str, "Grid start axis: Horizontal, Vertical"] | None = None,
    constraint: Annotated[str, "Grid constraint: Flexible, FixedColumnCount, FixedRowCount"] | None = None,
    constraint_count: Annotated[int, "Grid constraint count"] | None = None,
    horizontal_fit: Annotated[str, "ContentSizeFitter horizontal: Unconstrained, MinSize, PreferredSize"] | None = None,
    vertical_fit: Annotated[str, "ContentSizeFitter vertical: Unconstrained, MinSize, PreferredSize"] | None = None,
    min_width: Annotated[float, "LayoutElement min width"] | None = None,
    min_height: Annotated[float, "LayoutElement min height"] | None = None,
    preferred_width: Annotated[float, "LayoutElement preferred width"] | None = None,
    preferred_height: Annotated[float, "LayoutElement preferred height"] | None = None,
    flexible_width: Annotated[float, "LayoutElement flexible width"] | None = None,
    flexible_height: Annotated[float, "LayoutElement flexible height"] | None = None,
    ignore_layout: Annotated[bool, "LayoutElement ignore layout"] | None = None,
) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)
    params_dict = {k: v for k, v in locals().items() if v is not None and k not in ("ctx", "unity_instance", "self")}
    result = await send_with_unity_instance(async_send_command_with_retry, unity_instance, "manage_layout", params_dict)
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
