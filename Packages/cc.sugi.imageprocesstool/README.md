# sugi.cc Image Process Tool

Unity Editor extension package for node-based image processing workflows.

## Current Status
- Package scaffold is created.
- Graph data model is implemented (`nodes`, `edges`, `port definitions`, `parameters`).
- Editor window is available from `Tools/sugi.cc/Image Process Tool`.
- Shader operator node can sync ports from assigned shader properties.
- Basic graph topology validation (cycle/missing-node checks) is available.

## Requirements
- Unity 6.3 LTS (`6000.3`)
- URP (`com.unity.render-pipelines.universal` 17.3.0)
- Shader Graph (`com.unity.shadergraph` 17.3.0)

## Install in Other Projects
1. Open `Window > Package Manager`.
2. Click `+` and choose one of the following:
   - `Add package from git URL...`
   - `Add package from disk...` (select this package's `package.json`)

Example git URL format:
`https://<your-repo-url>.git?path=/Packages/cc.sugi.imageprocesstool`

## Quick Start
1. Open `Tools/sugi.cc/Image Process Tool`.
2. Create a graph asset with `New`.
3. Add nodes (`Source`, `Shader`, `Output`).
4. Assign shader to shader node in the node list.
5. Click `Sync Shader Ports` to auto-generate input ports and default parameter values.
6. Configure `edges` and click `Validate Order`.

## Folder Structure
- `Runtime/`: data and runtime-safe classes.
- `Editor/`: editor-only UI and tooling.

## Next Implementation Targets
1. Dedicated GraphView UI for visual node editing.
2. Type-safe edge validation (port type and direction checks).
3. Graph execution pipeline with RenderTexture intermediates.
4. Per-node output save (PNG/EXR).

