"""manage_project_settings tool."""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity Player Settings: product/company name, version, icon, splash screen, resolution, scripting backend, API compatibility. Actions: set_product, set_company, set_version, set_icon, set_splash, set_resolution, set_scripting_backend, set_api_compatibility, get_info.",
    annotations=ToolAnnotations(title="Manage Project Settings", destructiveHint=True),
)
async def manage_project_settings(
    ctx: Context,
    action: Annotated[Literal["set_product", "set_company", "set_version", "set_icon", "set_splash", "set_resolution", "set_scripting_backend", "set_api_compatibility", "get_info"], "Action"],
    product_name: Annotated[str, "Product name"] | None = None,
    company_name: Annotated[str, "Company name"] | None = None,
    version: Annotated[str, "Bundle version"] | None = None,
    icon_path: Annotated[str, "Icon texture path"] | None = None,
    show_splash: Annotated[bool, "Show splash screen"] | None = None,
    splash_style: Annotated[str, "Splash style: light, dark"] | None = None,
    width: Annotated[int, "Default screen width"] | None = None,
    height: Annotated[int, "Default screen height"] | None = None,
    fullscreen: Annotated[bool, "Fullscreen mode"] | None = None,
    resizable: Annotated[bool, "Resizable window"] | None = None,
    backend: Annotated[str, "Scripting backend: mono, il2cpp"] | None = None,
    level: Annotated[str, "API level: net_standard, net_framework"] | None = None,
) -> dict[str, Any]:
    u = await get_unity_instance_from_context(ctx)
    p = {k: v for k, v in locals().items() if v is not None and k not in ("ctx", "u", "self")}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_project_settings", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
