"""
manage_mesh tool — Read, create, and combine meshes.
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
        "Manages Unity meshes: read mesh data (vertices, triangles, bounds), create procedural meshes, "
        "combine meshes, export mesh info. "
        "Actions: get_mesh_info, create_procedural_mesh, combine_meshes, set_mesh_data, export_mesh_data."
    ),
    annotations=ToolAnnotations(title="Manage Mesh", destructiveHint=True),
)
async def manage_mesh(
    ctx: Context,
    action: Annotated[Literal[
        "get_mesh_info", "create_procedural_mesh", "combine_meshes",
        "set_mesh_data", "export_mesh_data"
    ], "Action to perform."],

    target: Annotated[str, "Target GameObject name/path/ID"] | None = None,
    targets: Annotated[list[str], "List of GameObjects to combine (combine_meshes)"] | None = None,
    name: Annotated[str, "Name for created mesh or combined object"] | None = None,
    mesh_type: Annotated[str, "Procedural mesh type: plane, circle, ring, cylinder, torus"] | None = None,
    vertices: Annotated[list[list[float]], "Vertex positions [[x,y,z], ...] for set_mesh_data"] | None = None,
    triangles: Annotated[list[int], "Triangle indices for set_mesh_data"] | None = None,
    normals: Annotated[list[list[float]], "Vertex normals [[x,y,z], ...] for set_mesh_data"] | None = None,
    uvs: Annotated[list[list[float]], "UV coordinates [[u,v], ...] for set_mesh_data"] | None = None,
    segments: Annotated[int, "Number of segments for procedural meshes"] | None = None,
    radius: Annotated[float, "Radius for procedural meshes"] | None = None,
    inner_radius: Annotated[float, "Inner radius for ring/torus"] | None = None,
    width: Annotated[float, "Width for plane"] | None = None,
    height: Annotated[float, "Height/length for plane/cylinder"] | None = None,
    merge_submeshes: Annotated[bool, "Merge submeshes when combining"] | None = None,
    include_vertices: Annotated[bool, "Include vertex data in export (can be large)"] | None = None,
) -> dict[str, Any]:
    u = get_unity_instance_from_context(ctx)
    p = {k: v for k, v in {
        "action": action, "target": target, "targets": targets, "name": name,
        "mesh_type": mesh_type, "vertices": vertices, "triangles": triangles,
        "normals": normals, "uvs": uvs, "segments": segments, "radius": radius,
        "inner_radius": inner_radius, "width": width, "height": height,
        "merge_submeshes": merge_submeshes, "include_vertices": include_vertices,
    }.items() if v is not None}
    r = await send_with_unity_instance(async_send_command_with_retry, u, "manage_mesh", p)
    return r if isinstance(r, dict) else {"success": False, "message": str(r)}
