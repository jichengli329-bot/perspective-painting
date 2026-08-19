# Production source models

This folder is the only automatic DCC-model import boundary. Put reviewed FBX
exports here; do not put `.blend` working files inside Unity's `Assets` tree.

Required filename: `ROLE_Name_v###.fbx`, where ROLE is `HERO`, `PROP`, or
`ENV`. Example: `HERO_ArchBridge_v001.fbx`.

Unity automatically disables embedded materials, animation, lights and
cameras, preserves authored normals, calculates Mikk tangents, uses meter-scale
geometry, and optimizes non-readable runtime meshes. Run
`Tools/PerspectivePuzzle/Production Art/Validate Source Models` before commit.

See `docs/PRODUCTION_ART_PIPELINE_ZH.md` for the complete Blender-side contract.
