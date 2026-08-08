# Architecture baseline

The Unity project and Cycle 1 voxel slice are complete. Cycle 2 adds a separate painting-composition path while retaining the verified voxel code and scenes as technical evidence. The pivot is recorded in `DECISIONS.md` and planned in `CYCLE_02_PAINTING_RECONSTRUCTION.md`.

## Target stack

- Unity 6000.3.18f1
- Universal Render Pipeline
- C#
- New Input System only if the URP template includes it cleanly; otherwise keep the first interaction layer minimal.

## Runtime boundaries

### Cycle 2 composition domain

Project-owned C# data and comparison logic:

- stable piece identifiers and authored piece constraints;
- target silhouette and object-ID buffers;
- immutable comparison inputs and results;
- overall silhouette IoU;
- per-piece visible coverage and overlap;
- weighted composition score and pass thresholds.

Array comparison and score aggregation must remain independent from GameObjects, cameras, RenderTextures, input, UI, and beauty materials. GPU capture and readback belong to the Unity-facing presentation layer.

### Cycle 2 composition presentation

Unity-facing behaviour:

- perspective composition camera and three-quarter build camera;
- beauty, object-ID, and silhouette rendering;
- asynchronous low-resolution readback;
- piece selection and constrained transform manipulation;
- reference painting and live viewfinder;
- diagnostic-to-player feedback translation;
- completion lock and camera reveal.

The new composition scene must not depend on the voxel occupancy grid. Shared visual utilities, rounded forms, palette, build pipeline, and test infrastructure may be reused when their assumptions still hold.

### Cycle 2 content

The first painting definition owns:

- the required piece roster and stable IDs;
- initial and solved transforms;
- transform constraints;
- beauty reference and hidden comparison artifacts;
- score weights, critical pieces, and tolerant thresholds.

The hidden solved scene is the source from which target artifacts are generated. Runtime code must not hard-code the one landscape's piece names in controller branches.

### Cycle 1 voxel domain (retained)

Pure C# projection and puzzle state:

- discrete grid occupancy;
- placement validity;
- projection calculation;
- target comparison;
- match result;
- undo command data.

The domain must not depend on GameObjects, MonoBehaviours, rendering, input, or UI.

### Cycle 1 voxel presentation (retained)

Unity-facing behaviour:

- grid/world coordinate conversion;
- piece views and previews;
- pointer input;
- camera and reveal sequence;
- projection-board rendering;
- audio and animation feedback.

### Cycle 1 voxel content (retained)

Puzzle definitions and visual settings should be authorable without changing projection code.

## Initial scene structure

The structure below documents Cycle 1. Cycle 2 uses a separate `PaintingPrototype.unity` scene with `PaintingSession`, `SceneryRoot`, `ReferenceFrame`, `LiveViewfinder`, `CompositionCamera`, `BuildCamera`, and `Lighting` roots. Exact child structure is fixed in the T-008 contract after visual composition design.

```text
GameRoot
├── PuzzleSession
├── GridStage
│   ├── PlacementSurface
│   ├── PieceRoot
│   └── PreviewRoot
├── ProjectionBoard
├── Cameras
│   ├── BuildCamera
│   └── RevealTarget
├── Lighting
└── UI
```

## Dependency policy

Start with Unity and URP only. Public repositories are references, not automatic dependencies. Any imported code requires license review, version-compatibility review, attribution, and a narrow reason for inclusion.
