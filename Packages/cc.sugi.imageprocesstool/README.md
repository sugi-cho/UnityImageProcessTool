# sugi.cc Image Process Tool

Unity Editor extension package for node-based image processing workflows.

## Current Status
- Package scaffold is created.
- Graph data model is implemented (`nodes`, `edges`, `port definitions`, `parameters`).
- GraphView-based node editor is available from `Tools/sugi.cc/Image Process Tool`.
- Shader operator node can sync ports from assigned shader properties.
- Basic graph topology validation (cycle/missing-node checks) is available.
- Graph execution is available for `Source -> Shader -> Output`.
- Graph edits auto-run the pipeline.
- Nodes with `out_rgba` output show foldable previews.
- Preview outputs can be saved per node as PNG/EXR/Texture2D Asset.

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
3. Add nodes (`Source`, `Shader`, `Output`) in the graph canvas.
   - You can also right-click the canvas to add nodes.
4. Assign source textures and shaders from each node inspector area.
5. Click `Sync Shader Ports` to auto-generate input ports and default parameter values.
6. Connect ports directly on the graph and click `Validate`.
7. Edit graph data (connect, parameter change, source/shader change) and auto-run will update node previews.
8. Open each node preview foldout and save output as PNG/EXR/Asset.

## Folder Structure
- `Runtime/`: data and runtime-safe classes.
- `Editor/`: editor-only UI and tooling.

## Next Implementation Targets
1. Dedicated GraphView UI for visual node editing.
2. Inline editor UI for edge creation/removal and port-level inspection.
3. Channel split/combine node execution.
4. Intermediate preview thumbnails in the editor.

