---
name: unity-cli
description: Control Unity Editor, install modules, run builds and tests using Unity CLI and MCP.
---

# Unity CLI Integration Skill

Use Unity CLI (`unity`) to control Unity Editors, run builds, execute tests, and manage projects.

## Common Operations

### 1. Check Version & Status
```powershell
unity --version
unity doctor
unity editors
```

### 2. Open Current Project
```powershell
unity open .
```

### 3. Run Build & Tests
```powershell
unity test .
unity build .
```

### 4. Manage Modules & Editors
```powershell
unity modules list <version>
unity install-modules -e <version> -m <module>
```

### 5. MCP Integration
Unity MCP Stdio server configured in `C:\Users\PC\.gemini\config\mcp_config.json`.
Command: `unity mcp`
