# SugiCho Image Process Tool

Unity Editor extension package for node-based image processing workflows.

## Current Status
- Package scaffold is created.
- Minimal editor window is available from `Tools/SugiCho/Image Process Tool`.
- Runtime graph asset placeholder is available.

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

## Folder Structure
- `Runtime/`: data and runtime-safe classes.
- `Editor/`: editor-only UI and tooling.

## Next Implementation Targets
1. Graph view UI (node creation, connection, validation).
2. Shader property reflection and dynamic port generation.
3. Graph execution pipeline with RenderTexture intermediates.
4. Per-node output save (PNG/EXR).

