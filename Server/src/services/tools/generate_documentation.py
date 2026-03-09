"""
AI-powered documentation generator for Unity C# scripts.
Generates XML documentation, README sections, and API docs.
"""
from typing import Annotated, Any

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    unity_target=None,
    description=(
        "Generates documentation for Unity scripts and projects: XML doc comments, "
        "README files, API reference, usage examples, and architecture docs. "
        "Can document individual scripts, entire folders, or generate project-level docs."
    ),
    annotations=ToolAnnotations(
        title="Generate Documentation",
        readOnlyHint=True,
    ),
)
async def generate_documentation(
    ctx: Context,
    script_path: Annotated[str, "Path to script or folder to document"],
    doc_type: Annotated[str, "Type: xml_comments, readme, api_reference, usage_examples, architecture"] | None = None,
    output_format: Annotated[str, "Format: markdown, xml, html"] | None = None,
    include_examples: Annotated[bool, "Include usage examples"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    try:
        script_result = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "read_file", {"path": script_path}
        )
    except Exception as e:
        return {"success": False, "message": f"Could not read: {e}"}

    doc_config = {
        "script_path": script_path,
        "doc_type": doc_type or "xml_comments",
        "output_format": output_format or "markdown",
        "include_examples": include_examples if include_examples is not None else True,
        "script_content": script_result,
        "templates": {
            "xml_comments": (
                "Generate C# XML documentation comments (/// summary, param, returns, remarks) "
                "for all public classes, methods, properties, and fields. Follow Unity conventions."
            ),
            "readme": (
                "Generate a README.md section covering: overview, features, setup instructions, "
                "usage examples, API reference, and configuration options."
            ),
            "api_reference": (
                "Generate a complete API reference with: class hierarchy, method signatures, "
                "parameter descriptions, return values, exceptions, and cross-references."
            ),
            "usage_examples": (
                "Generate practical usage examples showing how to use this code in a Unity project. "
                "Include Inspector setup, scripting examples, and common configurations."
            ),
            "architecture": (
                "Generate architecture documentation: component diagram, data flow, "
                "dependencies, extension points, and design rationale."
            ),
        },
        "instructions": (
            f"Generate {doc_type or 'xml_comments'} documentation for this Unity script. "
            f"Output in {output_format or 'markdown'} format. "
            f"{'Include practical usage examples. ' if include_examples else ''}"
        ),
    }

    return {"success": True, "message": f"Ready to generate docs for {script_path}", "data": doc_config}
