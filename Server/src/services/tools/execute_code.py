"""
Defines the execute_code tool for running arbitrary C# in the Unity Editor.
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
        "Executes arbitrary C# code in the Unity Editor context. "
        "Actions: run (execute a code block), evaluate (evaluate an expression), "
        "run_static_method (call an existing static method by type and name). "
        "WARNING: Do NOT use this to create, modify, or delete script files "
        "(no System.IO.File calls for .cs files). Use create_script, delete_script, "
        "script_apply_edits, or apply_text_edits instead."
    ),
    annotations=ToolAnnotations(
        title="Execute Code",
        destructiveHint=True,
    ),
)
async def execute_code(
    ctx: Context,
    action: Annotated[Literal[
        "run", "evaluate", "run_static_method"
    ], "Action to perform."],

    code: Annotated[str, "C# code to run (for 'run' action). Can be statements or full class."] | None = None,
    expression: Annotated[str, "C# expression to evaluate (for 'evaluate' action)"] | None = None,
    type_name: Annotated[str, "Fully-qualified type name (for 'run_static_method')"] | None = None,
    method_name: Annotated[str, "Static method name (for 'run_static_method')"] | None = None,
    arguments: Annotated[list, "Method arguments as array (for 'run_static_method')"] | None = None,

) -> dict[str, Any]:
    unity_instance = get_unity_instance_from_context(ctx)

    params_dict = {
        "action": action,
        "code": code,
        "expression": expression,
        "type_name": type_name,
        "method_name": method_name,
        "arguments": arguments,
    }

    params_dict = {k: v for k, v in params_dict.items() if v is not None}

    result = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "execute_code",
        params_dict,
    )

    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
