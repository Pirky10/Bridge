"""
manage_visual_scripting tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity Visual Scripting graphs: create graphs, add nodes, connect them, and assign to GameObjects. Requires com.unity.visualscripting. Actions: create_graph, add_node, connect_nodes, add_variable, get_graph_info, assign_graph.",
    annotations=ToolAnnotations(title="Manage Visual Scripting", destructiveHint=True),
)
async def manage_visual_scripting(
    ctx: Context,
    action: Annotated[Literal["create_graph", "add_node", "connect_nodes", "add_variable", "get_graph_info", "assign_graph"], "Action to perform"],
    graph_path: Annotated[str, "Asset path to the graph file"] | None = None,
    node_type: Annotated[str, "Type of node to add"] | None = None,
    from_node: Annotated[str, "ID of the source node"] | None = None,
    to_node: Annotated[str, "ID of the destination node"] | None = None,
    variable_name: Annotated[str, "Name of the variable"] | None = None,
    variable_value: Annotated[Any, "Value of the variable"] | None = None,
    target: Annotated[str, "GameObject to assign the graph to"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action,
        "graph_path": graph_path,
        "node_type": node_type,
        "from_node": from_node,
        "to_node": to_node,
        "variable_name": variable_name,
        "variable_value": variable_value,
        "target": target
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_visual_scripting", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
