# Changelog

All notable changes to this package will be documented in this file.

## [0.1.0] - 2026-03-06
- Initial package scaffold.
- Added URP and Shader Graph dependencies in package manifest.
- Added runtime graph asset placeholder.
- Added editor window placeholder.
- Added README instructions for package reuse in other projects.
- Added graph core models (`node`, `edge`, `port`, `parameter`).
- Added topological sort validation utility for execution order.
- Added shader property to node-port synchronization utility.
- Updated editor window with graph creation, node add, sync, and validate actions.
- Added graph validation for edge type/direction and required inputs.
- Added graph executor (`Source`, `ShaderOperator`, `Output`).
- Added output export utility (PNG/EXR).
- Added run/export controls in the editor window.
- Replaced default inspector-like window with GraphView-based node editor.
- Added context menu node creation (right-click on graph canvas).
- Added connection compatibility filtering by port direction/type.
- Replaced manual run/save-all workflow with auto-run on graph data changes.
- Added per-node foldable preview UI for nodes with `out_rgba` output.
- Added per-node export actions for PNG/EXR/Asset from preview.
