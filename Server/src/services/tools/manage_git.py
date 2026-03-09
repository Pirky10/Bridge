"""
Git operations for Unity projects.
Uses subprocess to run git commands on the project directory.
"""
import asyncio
import os
from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    unity_target=None,
    description=(
        "Manages Git operations for the Unity project: status, diff, log, "
        "branch management, staging, committing, stashing, and more. "
        "Operates on the Unity project directory. Does NOT push or pull "
        "to avoid accidental remote changes — use the terminal for those."
    ),
    annotations=ToolAnnotations(
        title="Manage Git",
        destructiveHint=True,
    ),
)
async def manage_git(
    ctx: Context,
    action: Annotated[Literal[
        "status", "diff", "log", "branch_list", "branch_create",
        "branch_switch", "stage", "unstage", "commit",
        "stash", "stash_pop", "reset_file", "blame", "show"
    ], "Git action to perform."],

    path: Annotated[str, "File path for file-specific operations"] | None = None,
    message: Annotated[str, "Commit message (for commit action)"] | None = None,
    branch_name: Annotated[str, "Branch name (for branch_create/branch_switch)"] | None = None,
    num_entries: Annotated[int, "Number of log entries (default: 10)"] | None = None,
    commit_hash: Annotated[str, "Commit hash (for show action)"] | None = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    # Get the project path from Unity
    try:
        project_info = await send_with_unity_instance(
            async_send_command_with_retry, unity_instance,
            "get_project_info", {}
        )
        project_path = None
        if isinstance(project_info, dict):
            project_path = project_info.get("data", {}).get("projectPath") or project_info.get("projectPath")
    except Exception:
        project_path = None

    if not project_path:
        # Fallback: try common Unity project detection
        return {"success": False, "message": "Could not determine project path. Ensure Unity is connected."}

    try:
        result = await _run_git_action(action, project_path, path, message, branch_name, num_entries, commit_hash)
        return result
    except Exception as e:
        return {"success": False, "message": f"Git error: {e}"}


async def _run_git(project_path: str, *args: str) -> tuple[str, str, int]:
    """Run a git command and return stdout, stderr, returncode."""
    proc = await asyncio.create_subprocess_exec(
        "git", *args,
        cwd=project_path,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
    )
    stdout, stderr = await asyncio.wait_for(proc.communicate(), timeout=30)
    return stdout.decode("utf-8", errors="replace"), stderr.decode("utf-8", errors="replace"), proc.returncode


async def _run_git_action(action, project_path, path, message, branch_name, num_entries, commit_hash):
    """Execute the specific git action."""

    if action == "status":
        stdout, stderr, rc = await _run_git(project_path, "status", "--porcelain", "-b")
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}

        lines = stdout.strip().split("\n") if stdout.strip() else []
        branch_line = lines[0] if lines else ""
        file_lines = lines[1:] if len(lines) > 1 else []

        staged = [l for l in file_lines if l and l[0] in "MADRCU"]
        modified = [l for l in file_lines if l and len(l) > 1 and l[1] in "MADRCU"]
        untracked = [l for l in file_lines if l.startswith("??")]

        return {
            "success": True,
            "message": "Git status",
            "data": {
                "branch": branch_line,
                "staged_count": len(staged),
                "modified_count": len(modified),
                "untracked_count": len(untracked),
                "files": file_lines[:50],  # Cap at 50
            },
        }

    elif action == "diff":
        args = ["diff", "--stat"]
        if path:
            args.append(path)
        stdout, stderr, rc = await _run_git(project_path, *args)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}

        # Also get the actual diff (limited)
        args2 = ["diff"]
        if path:
            args2.append(path)
        stdout2, _, _ = await _run_git(project_path, *args2)

        # Truncate long diffs
        diff_text = stdout2[:15000] if len(stdout2) > 15000 else stdout2

        return {
            "success": True,
            "message": "Git diff",
            "data": {"stat": stdout, "diff": diff_text},
        }

    elif action == "log":
        n = num_entries or 10
        stdout, stderr, rc = await _run_git(
            project_path, "log", f"-{n}",
            "--format=%H|%an|%ar|%s", "--no-merges"
        )
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}

        entries = []
        for line in stdout.strip().split("\n"):
            if "|" in line:
                parts = line.split("|", 3)
                entries.append({
                    "hash": parts[0][:8],
                    "full_hash": parts[0],
                    "author": parts[1] if len(parts) > 1 else "",
                    "date": parts[2] if len(parts) > 2 else "",
                    "message": parts[3] if len(parts) > 3 else "",
                })

        return {
            "success": True,
            "message": f"Last {n} commits",
            "data": {"entries": entries},
        }

    elif action == "branch_list":
        stdout, stderr, rc = await _run_git(project_path, "branch", "-a", "--format=%(refname:short)|%(upstream:short)|%(HEAD)")
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}

        branches = []
        for line in stdout.strip().split("\n"):
            if line:
                parts = line.split("|")
                branches.append({
                    "name": parts[0],
                    "upstream": parts[1] if len(parts) > 1 and parts[1] else None,
                    "current": parts[2].strip() == "*" if len(parts) > 2 else False,
                })

        return {
            "success": True,
            "message": f"Found {len(branches)} branches",
            "data": {"branches": branches},
        }

    elif action == "branch_create":
        if not branch_name:
            return {"success": False, "message": "'branch_name' required."}
        stdout, stderr, rc = await _run_git(project_path, "branch", branch_name)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": f"Created branch '{branch_name}'"}

    elif action == "branch_switch":
        if not branch_name:
            return {"success": False, "message": "'branch_name' required."}
        stdout, stderr, rc = await _run_git(project_path, "checkout", branch_name)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": f"Switched to branch '{branch_name}'"}

    elif action == "stage":
        target = path or "."
        stdout, stderr, rc = await _run_git(project_path, "add", target)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": f"Staged: {target}"}

    elif action == "unstage":
        target = path or "."
        stdout, stderr, rc = await _run_git(project_path, "reset", "HEAD", target)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": f"Unstaged: {target}"}

    elif action == "commit":
        if not message:
            return {"success": False, "message": "'message' required for commit."}
        stdout, stderr, rc = await _run_git(project_path, "commit", "-m", message)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": f"Committed: {message}", "data": {"output": stdout}}

    elif action == "stash":
        msg = message or "MCP auto-stash"
        stdout, stderr, rc = await _run_git(project_path, "stash", "push", "-m", msg)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": "Changes stashed", "data": {"output": stdout}}

    elif action == "stash_pop":
        stdout, stderr, rc = await _run_git(project_path, "stash", "pop")
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": "Stash popped", "data": {"output": stdout}}

    elif action == "reset_file":
        if not path:
            return {"success": False, "message": "'path' required for reset_file."}
        stdout, stderr, rc = await _run_git(project_path, "checkout", "--", path)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": f"Reset file: {path}"}

    elif action == "blame":
        if not path:
            return {"success": False, "message": "'path' required for blame."}
        stdout, stderr, rc = await _run_git(project_path, "blame", "--porcelain", path)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        # Truncate blame output
        if len(stdout) > 10000:
            stdout = stdout[:10000] + "\n... (truncated)"
        return {"success": True, "message": f"Blame for {path}", "data": {"blame": stdout}}

    elif action == "show":
        ref = commit_hash or "HEAD"
        stdout, stderr, rc = await _run_git(project_path, "show", "--stat", ref)
        if rc != 0:
            return {"success": False, "message": f"Git error: {stderr}"}
        return {"success": True, "message": f"Commit {ref}", "data": {"show": stdout}}

    else:
        return {"success": False, "message": f"Unknown action: {action}"}
