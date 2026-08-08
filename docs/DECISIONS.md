# Decision log

## D-001: Shared-folder collaboration

Codex and Claude Code work in the same repository. Markdown specifications and the active task are the coordination protocol; source files and version-control diffs are the implementation record.

## D-002: Engine and renderer

Use Unity 6000.3.18f1 with URP. The project prioritizes stylized visuals and efficient iteration over photorealistic rendering.

## D-003: Vertical slice before feature breadth

Build one polished room and three puzzles before adding multiple viewpoints, editors, campaigns, or online features.

## D-004: Core logic ownership

Projection and puzzle-state logic remain project-owned, small, and testable. Public repositories may inform interaction and presentation but will not replace the core game model.

## D-005: Implementation model

Claude Code implementation work uses `deepseek-v4-flash` with `CLAUDE_CODE_EFFORT_LEVEL=max`. The `[1m]` model suffix is excluded because it caused calls to hang in the verified local setup.

## D-006: Painting reconstruction pivot

The completed 5x5 voxel slice is retained as technical evidence but is not the product target. The next slice uses recognizable 3D scenery to reconstruct one complete authored 2D composition from a designated perspective camera. Depth, apparent size, silhouette overlap, and occlusion are core puzzle variables.

## D-007: Authored target plus hidden truth

The first target painting is rendered from a hidden solved 3D arrangement. The project stores both a polished beauty reference and machine-readable object-ID/silhouette buffers. Runtime matching compares the hidden buffers with the player's composition; it does not attempt general image recognition or exact RGB comparison.

## D-008: Fixed physical scale in the first painting slice

Puzzle pieces keep authored physical sizes. Players move them in the composition plane, change their depth, and use constrained rotation. Free scaling is excluded from the first slice because it would bypass the intended forced-perspective reasoning.
