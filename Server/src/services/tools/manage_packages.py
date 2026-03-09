"""
Defines the manage_packages tool for Unity Package Manager operations.
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
        "Manages Unity packages via the Package Manager. "
        "Actions: list, install, remove, search, get_info, install_git_package."
    ),
    annotations=ToolAnnotations(
        title="Manage Packages",
        destructiveHint=True,
    ),
)
async def manage_packages(
    ctx: Context,
    action: Annotated[Literal[
        "list", "install", "remove", "search", "get_info", "install_git_package"
    ], "Action to perform."],

    package_id: Annotated[str, "Package identifier (e.g., com.unity.inputsystem, com.unity.cinemachine@3.0.0)"] | None = None,
    query: Annotated[str, "Search query for package search"] | None = None,
    offline_mode: Annotated[bool, "List only locally cached packages"] | None = None,
    git_url: Annotated[str, "Git repository URL for install_git_package (e.g. 'https://github.com/user/repo.git' or 'https://github.com/user/repo.git#branch')"] | None = None,

) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action,
        "package_id": package_id,
        "query": query,
        "offline_mode": offline_mode,
        "gitUrl": git_url,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_packages",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
