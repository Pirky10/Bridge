<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/images/logo-header-dark.png">
    <img alt="MCP for Unity" src="docs/images/logo-header-light.png" width="400">
  </picture>
</p>

<div align="center">

[English](README.md) <img src="docs/images/connector.svg" alt="↔" height="14"> [简体中文](docs/i18n/README-zh.md) &nbsp;&nbsp;&nbsp;|&nbsp;&nbsp;&nbsp; [Discord](https://discord.gg/y4p8KfzrN4) <img src="docs/images/connector.svg" alt="↔" height="14"> [Wiki](https://coplaydev.github.io/unity-mcp/)

#### Proudly sponsored and maintained by [Aura](https://www.tryaura.dev/) — the AI assistant for Unreal & Unity.
##### And don't miss [Godot AI](https://github.com/hi-godot/godot-ai), the new open source project from the makers of MCP for Unity.

</div>

<p align="center"><b>Create your Unity apps with LLMs.</b> MCP for Unity bridges AI assistants — Claude, Codex, VS Code, local LLMs, and more — with your Unity Editor via <a href="https://modelcontextprotocol.io/introduction">Model Context Protocol</a>. Give your LLM the tools to manage assets, control scenes, edit scripts, run tests, and automate your game dev workflows.</p>

<p align="center">
  <img alt="MCP for Unity building a scene" src="docs/images/building_scene.gif">
</p>

---

<!-- recent-updates:start -->
<details>
<summary><strong>Recent Updates</strong></summary>

* **[v10.0.0](https://github.com/CoplayDev/unity-mcp/releases/tag/v10.0.0)** (2026-06-30)
* **[v9.7.3](https://github.com/CoplayDev/unity-mcp/releases/tag/v9.7.3)** (2026-06-15)
* **[v9.7.1](https://github.com/CoplayDev/unity-mcp/releases/tag/v9.7.1)** (2026-05-24)
* **[v9.7.0](https://github.com/CoplayDev/unity-mcp/releases/tag/v9.7.0)** (2026-05-22)
* **[v9.6.8](https://github.com/CoplayDev/unity-mcp/releases/tag/v9.6.8)** (2026-04-27)

Full history: [Release Notes](https://coplaydev.github.io/unity-mcp/releases).

</details>
<!-- recent-updates:end -->

---

## What it does

Control the Unity Editor in natural language from any MCP client — create scenes & GameObjects, edit C# scripts, manage assets, run tests, profile, and build. 47 focused MCP tool entrypoints, any client, free & MIT.

**[Browse the full tool catalog →](https://coplaydev.github.io/unity-mcp/reference/tools/)**

---

## Quickstart

**Requirements:** Unity **2021.3 LTS → 6.x** · Python **3.10+** (via [`uv`](https://docs.astral.sh/uv/)). Works with **any MCP client** — Claude Desktop & Code, Cursor, VS Code, Windsurf, Cline, Gemini CLI, and more.

### Available Tools
`apply_text_edits` • `batch_execute` • `create_script` • `debug_request_context` • `delete_script` • `execute_custom_tool` • `execute_menu_item` • `find_gameobjects` • `find_in_file` • `get_sha` • `get_test_job` • `manage_animation` • `manage_asset` • `manage_build` • `manage_camera` • `manage_components` • `manage_editor` • `manage_gameobject` • `manage_graphics` • `manage_material` • `manage_packages` • `manage_physics` • `manage_prefabs` • `manage_probuilder` • `manage_profiler` • `manage_scene` • `manage_script` • `manage_script_capabilities` • `manage_scriptable_object` • `manage_shader` • `manage_texture` • `manage_tools` • `manage_ui` • `manage_vfx` • `read_console` • `refresh_unity` • `run_tests` • `script_apply_edits` • `set_active_instance` • `unity_docs` • `unity_reflect` • `validate_script`

### Available Resources
`cameras` • `custom_tools` • `renderer_features` • `rendering_stats` • `volumes` • `editor_active_tool` • `editor_prefab_stage` • `editor_selection` • `editor_state` • `editor_windows` • `gameobject` • `gameobject_api` • `gameobject_component` • `gameobject_components` • `get_tests` • `get_tests_for_mode` • `menu_items` • `prefab_api` • `prefab_hierarchy` • `prefab_info` • `project_info` • `project_layers` • `project_tags` • `tool_groups` • `unity_instances`

**Performance Tip:** Use `batch_execute` for multiple operations — it's 10-100x faster than individual calls!
</details>

<details>
<summary><strong>Manual Configuration</strong></summary>

If auto-setup doesn't work, add this to your MCP client's config file:

**HTTP (default — works with Claude Desktop, Cursor, Windsurf):**
```json
{
  "mcpServers": {
    "unityMCP": {
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

**VS Code:**
```json
{
  "servers": {
    "unityMCP": {
      "type": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

<details>
<summary>Stdio configuration (uvx)</summary>

**macOS/Linux:**
```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "uvx",
      "args": ["--from", "bridge-ai-unity-mcp", "mcp-for-unity", "--transport", "stdio"]
    }
  }
}
```

**Windows:**
```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "C:/Users/YOUR_USERNAME/AppData/Local/Microsoft/WinGet/Links/uvx.exe",
      "args": ["--from", "bridge-ai-unity-mcp", "mcp-for-unity", "--transport", "stdio"]
    }
  }
}
```
</details>
</details>

<details>
<summary><strong>Multiple Unity Instances</strong></summary>

MCP for Unity supports multiple Unity Editor instances. To target a specific one:

1. Ask your LLM to check the `unity_instances` resource
2. Use `set_active_instance` with the `Name@hash` (e.g., `MyProject@abc123`)
3. All subsequent tools route to that instance
</details>

<details>
<summary><strong>Roslyn Script Validation (Advanced)</strong></summary>

For **Strict** validation that catches undefined namespaces, types, and methods:

1. Install [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)
2. `Window > NuGet Package Manager` → Install `Microsoft.CodeAnalysis` v5.0
3. Also install `SQLitePCLRaw.core` and `SQLitePCLRaw.bundle_e_sqlite3` v3.0.2
4. Add `USE_ROSLYN` to `Player Settings > Scripting Define Symbols`
5. Restart Unity

  <details>
  <summary>One-click installer (recommended)</summary>

  Open `Window > MCP for Unity`, scroll to the **Runtime Code Execution (Roslyn)** section in the Scripts/Validation tab, and click **Install Roslyn DLLs**. This downloads the required NuGet packages and places the DLLs in `Assets/Plugins/Roslyn/` automatically.

  You can also run it from the menu: `Window > MCP For Unity > Install Roslyn DLLs`.
  </details>

  <details>
  <summary>Manual DLL installation (if the installer isn't available)</summary>

  1. Download `Microsoft.CodeAnalysis.CSharp.dll` and dependencies from [NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/)
  2. Place DLLs in `Assets/Plugins/Roslyn/` folder
  3. Ensure .NET compatibility settings are correct
  4. Add `USE_ROSLYN` to Scripting Define Symbols
  5. Restart Unity
  </details>
</details>

<details>
<summary><strong>Troubleshooting</strong></summary>

* **Unity Bridge Not Connecting:** Check `Window > MCP for Unity` status, restart Unity
* **Server Not Starting:** Verify `uv --version` works, check the terminal for errors
* **Client Not Connecting:** Ensure the HTTP server is running and the URL matches your config

**Detailed setup guides:**
* [Fix Unity MCP and Cursor, VSCode & Windsurf](https://github.com/CoplayDev/unity-mcp/wiki/1.-Fix-Unity-MCP-and-Cursor,-VSCode-&-Windsurf) — uv/Python installation, PATH issues
* [Fix Unity MCP and Claude Code](https://github.com/CoplayDev/unity-mcp/wiki/2.-Fix-Unity-MCP-and-Claude-Code) — Claude CLI installation
* [Common Setup Problems](https://github.com/CoplayDev/unity-mcp/wiki/3.-Common-Setup-Problems) — macOS dyld errors, FAQ

Still stuck? [Open an Issue](https://github.com/CoplayDev/unity-mcp/issues) or [Join Discord](https://discord.gg/y4p8KfzrN4)
</details>

<details>
<summary><strong>Contributing</strong></summary>

See [README-DEV.md](docs/development/README-DEV.md) for development setup. For custom tools, see [CUSTOM_TOOLS.md](docs/reference/CUSTOM_TOOLS.md).

1. Fork → Create issue → Branch (`feature/your-idea`) → Make changes → PR
</details>

<details>
<summary><strong>Telemetry & Privacy</strong></summary>

Anonymous, privacy-focused telemetry (no code, no project names, no personal data). Opt out with `DISABLE_TELEMETRY=true`. See [TELEMETRY.md](docs/reference/TELEMETRY.md).
</details>

<details>
<summary><strong>Security</strong></summary>

Network defaults are intentionally fail-closed:
* **HTTP Local** allows loopback-only hosts by default (`127.0.0.1`, `localhost`, `::1`).
* Bind-all interfaces (`0.0.0.0`, `::`) require explicit opt-in in **Advanced Settings** via **Allow LAN Bind (HTTP Local)**.
* **HTTP Remote** requires `https://` by default.
* Plaintext `http://` for remote endpoints requires explicit opt-in via **Allow Insecure Remote HTTP**.
</details>

---

## Community

- [Discord](https://discord.gg/y4p8KfzrN4) — chat with maintainers and other contributors
- [Issues](https://github.com/CoplayDev/unity-mcp/issues) — bugs and feature requests
- [Discussions](https://github.com/CoplayDev/unity-mcp/discussions) — design ideas and broader questions
- Security: see [SECURITY.md](SECURITY.md) for private reporting

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Branch off `beta`, not `main`. The full dev setup, testing, and release process live in the [Contributing](https://coplaydev.github.io/unity-mcp/contributing/dev-setup) docs.

## Advanced

- **Multiple Unity instances** — [Multi-Instance Routing](https://coplaydev.github.io/unity-mcp/guides/multi-instance)
- **Tool groups (vfx / animation / ui / testing / etc.)** — [Tool Groups](https://coplaydev.github.io/unity-mcp/guides/tool-groups)
- **v10 asset generation and upgrade notes** — [v10 Migration](https://coplaydev.github.io/unity-mcp/migrations/v10)
- **Roslyn script validation** — [Roslyn Validation](https://coplaydev.github.io/unity-mcp/guides/roslyn)
- **Remote-hosted server with auth** — [Remote Server Auth](https://coplaydev.github.io/unity-mcp/guides/remote-server-auth)

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=CoplayDev/unity-mcp&type=Date)](https://www.star-history.com/#CoplayDev/unity-mcp&Date)

## Citation

If MCP for Unity helped your research, please cite it.

```bibtex
@inproceedings{wu2025mcpunity,
  author    = {Wu, Shutong and Barnett, Justin P.},
  title     = {{MCP-Unity}: {Protocol-Driven} Framework for Interactive {3D} Authoring},
  year      = {2025},
  isbn      = {9798400721366},
  publisher = {Association for Computing Machinery},
  address   = {New York, NY, USA},
  url       = {https://doi.org/10.1145/3757376.3771417},
  doi       = {10.1145/3757376.3771417},
  series    = {SA Technical Communications '25}
}
```

## Unity AI Tools by Aura

Aura offers 2 AI tools for Unity:
- **MCP for Unity** is available freely under the MIT license.
- **Aura for Unity** is a premium Unity/Unreal AI assistant built for game devs.

## Disclaimer

This project is a free and open-source tool for the Unity Editor, and is not affiliated with Unity Technologies.

---

**License:** MIT — see [LICENSE](LICENSE).
