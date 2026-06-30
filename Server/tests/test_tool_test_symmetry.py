"""Symmetry guard: every MCP tool module must have test coverage.

Enforces the CLAUDE.md rule that "Every new feature needs tests". A tool module
(any file under ``services/tools/`` that exposes an ``@mcp_for_unity_tool``) is
considered covered when a ``test_*.py`` file under ``Server/tests/`` references it
by module name -- a dedicated ``test_<module>.py`` or a characterization test
counts. Non-``test_*.py`` scripts (e.g. ``tests/e2e/bridge_smoke.py``) are not
scanned, so a tool exercised only there still needs a unit/characterization test.

Tools that are genuinely untested today live in ``KNOWN_UNTESTED`` so this guard
fails for *new* untested tools without forcing a full backfill first. SHRINK the
quarantine list as coverage lands -- a stale entry (a module that now *has*
coverage) also fails the guard, so the list can only get smaller, never grow
silently.
"""
import re
from pathlib import Path

import pytest

TOOLS_DIR = Path(__file__).resolve().parents[1] / "src" / "services" / "tools"
TESTS_DIR = Path(__file__).resolve().parent

# Modules under tools/ that are infrastructure, not user-facing tool surfaces.
NOT_TOOLS = {"__init__", "utils", "preflight", "debug_request_context"}

# Tool modules without any test reference today. Do NOT add to this list for new
# tools -- new tools must ship with a test. Remove an entry once coverage lands.
KNOWN_UNTESTED = {
    "capture_screenshot",
    "chat_with_context",
    "compare_scenes",
    "convert_to_prefab",
    "debug_issue",
    "duplicate_scene_setup",
    "execute_menu_item",
    "explain_code",
    "find_references",
    "generate_documentation",
    "manage_2d_tools",
    "manage_addressables",
    "manage_assembly_definitions",
    "manage_asset_bundle",
    "manage_audio",
    "manage_audio_mixer",
    "manage_canvas_group",
    "manage_cinemachine",
    "manage_cloth",
    "manage_constraints",
    "manage_custom_editor",
    "manage_editor_prefs",
    "manage_editor_window",
    "manage_game_view",
    "manage_git",
    "manage_gizmos",
    "manage_input_actions",
    "manage_layer_collision",
    "manage_layout",
    "manage_lighting",
    "manage_lightmap",
    "manage_localization",
    "manage_lod",
    "manage_mesh",
    "manage_navigation",
    "manage_occlusion_culling",
    "manage_particle_system",
    "manage_physics_2d",
    "manage_post_processing",
    "manage_presets",
    "manage_project_settings",
    "manage_quality_settings",
    "manage_render_pipeline",
    "manage_rendering_layers",
    "manage_scene_view",
    "manage_scriptable_object_editor",
    "manage_scriptable_objects_bulk",
    "manage_selection",
    "manage_shader",
    "manage_shader_graph",
    "manage_sorting_layers",
    "manage_splines",
    "manage_sprite_renderer",
    "manage_tags_layers",
    "manage_terrain",
    "manage_timeline",
    "manage_tools",
    "manage_undo",
    "manage_video_player",
    "manage_visual_scripting",
    "optimize_performance",
    "plan_feature",
    "review_code",
    "search_documentation",
    "suggest_architecture",
    "validate_project",
    "web_search",
}


def _tool_modules() -> list[str]:
    mods = []
    for path in sorted(TOOLS_DIR.glob("*.py")):
        if path.stem in NOT_TOOLS:
            continue
        if "@mcp_for_unity_tool" in path.read_text(encoding="utf-8"):
            mods.append(path.stem)
    return mods


def _is_referenced(stem: str) -> bool:
    pattern = re.compile(rf"\b{re.escape(stem)}\b")
    for test_file in TESTS_DIR.rglob("test_*.py"):
        if test_file.resolve() == Path(__file__).resolve():
            continue
        if pattern.search(test_file.read_text(encoding="utf-8")):
            return True
    return False


@pytest.mark.parametrize("module", _tool_modules())
def test_every_tool_module_has_test_coverage(module):
    if module in KNOWN_UNTESTED:
        pytest.skip(f"{module} is quarantined in KNOWN_UNTESTED; add a test and remove it")
    assert _is_referenced(module), (
        f"Tool module '{module}' has no test referencing it. Every tool needs a "
        f"test (CLAUDE.md). Add a Server/tests/test_{module}.py, or -- only if you "
        f"have a justified reason -- add '{module}' to KNOWN_UNTESTED."
    )


@pytest.mark.parametrize("module", sorted(KNOWN_UNTESTED))
def test_quarantine_list_has_no_stale_entries(module):
    assert module in set(_tool_modules()), (
        f"KNOWN_UNTESTED entry '{module}' is not a real tool module -- remove it."
    )
    assert not _is_referenced(module), (
        f"'{module}' now has test coverage -- remove it from KNOWN_UNTESTED so the "
        f"quarantine list stays honest."
    )
