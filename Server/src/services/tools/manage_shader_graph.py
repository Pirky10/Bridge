"""
manage_shader_graph tool.
"""
from typing import Annotated, Any, Literal
from fastmcp import Context
from mcp.types import ToolAnnotations
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

@mcp_for_unity_tool(
    description="Manages Unity Shader Graphs: create graphs, add nodes, and connect properties. Requires com.unity.shadergraph. Actions: create_shader_graph, add_node, connect_nodes, set_property, get_graph_info, set_master_node.",
    annotations=ToolAnnotations(title="Manage Shader Graph", destructiveHint=True),
)
async def manage_shader_graph(
    ctx: Context,
    action: Annotated[Literal["create_shader_graph", "add_node", "connect_nodes", "set_property", "get_graph_info", "set_master_node"], "Action to perform"],
    graph_path: Annotated[str, "Asset path to the shader graph file"] | None = None,
    node_type: Annotated[str, "Type of node to add"] | None = None,
    property_name: Annotated[str, "Name of the graph property"] | None = None,
    property_value: Annotated[Any, "Value for the property"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action,
        "graph_path": graph_path,
        "node_type": node_type,
        "property_name": property_name,
        "property_value": property_value
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_shader_graph", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
