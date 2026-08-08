# Product brief

## Current direction

Cycle 1 proved the Unity pipeline, tabletop visual language, placement reliability, projection tests, progression, and Windows delivery. Its 5x5 voxel puzzles are now classified as a completed technical prototype rather than the intended product experience.

Cycle 2 pivots the core experience to reconstructing a complete stylized painting from a fixed perspective camera by arranging recognizable three-dimensional scenery. See `CYCLE_02_PAINTING_RECONSTRUCTION.md` for the active product plan.

## Working title

Perspective Puzzle

## Player fantasy

The player handles a beautiful miniature optical toy. By arranging three-dimensional pieces inside a constrained space, they create a recognizable two-dimensional image from a designated viewpoint.

## Product promise

This must feel like a game and a desirable object, not a technical voxel demo. Every placement should be readable and satisfying, and completing a puzzle should produce a strong visual reveal.

## Cycle 2 vertical-slice goal

Create one polished room with one authored landscape-composition puzzle that demonstrates:

- a stylized tabletop-toy and optical-laboratory visual identity;
- a complete framed reference image rather than a boolean grid;
- recognizable 3D scenery distributed across foreground, middle ground, and background;
- direct screen-plane movement, perspective-depth movement, and constrained rotation;
- fixed-size physical pieces whose apparent size changes through perspective rather than free scaling;
- a live viewfinder from the designated composition camera;
- tolerant comparison of silhouette, per-piece coverage, and occlusion using hidden machine-readable render buffers;
- undo;
- legible near-match feedback without exposing a raw technical pixel grid;
- a completion sequence that moves into the designated viewpoint and resolves the diorama into the reference painting.

The first composition is an original stylized landscape generated from a hidden solved 3D scene. It is not a generic external photograph or a famous painting.

## Visual direction

- Warm off-white miniature stage.
- Rounded toy-like pieces rather than default Unity cubes.
- Low-saturation teal interaction color with restrained warm accent colors.
- Soft contact shadows and clean silhouettes.
- Three-quarter camera for construction and a separate perspective composition camera.
- A framed reference painting and a live optical viewfinder integrated into the scene.
- Minimal interface; information should feel part of the physical exhibit where practical.

## Experience principles

1. A player should understand the objective from the screen without reading instructions.
2. Placement must be faster than thinking about placement.
3. Every action needs immediate visual feedback.
4. Near-success should feel increasingly legible, not increasingly frustrating.
5. Depth must create meaningful apparent-size and occlusion decisions.
6. Success is a reveal, not a text popup.

## Explicitly out of scope for the first slice

- Online features and accounts.
- User-generated levels.
- Save system and settings menu.
- Story campaign.
- Procedural level generation.
- More than one finished painting puzzle.
- Mobile controls.
- Simultaneous three-view matching.
- Complex free-camera controls.
- Arbitrary photo import or general-purpose computer vision.
- Free scaling of puzzle pieces.
