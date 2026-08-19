# Task board

Exactly one task may be marked `IN PROGRESS`.

## T-033 - Unified permissions and gallery art conversion

Status: DONE

Owner: Codex.

Goal: make Moon Garden completable without G by exposing its required rotation
and pavilion controls, then convert all four galleries to one coherent celadon
hero-asset family in a single candidate.

Completion report:

- Enabled Q/E from Moon Garden onward and added direct stage-three regression
  for pavilion selection, movement, depth and rotation; locked-stage APIs still
  reject selection and assist.
- Updated Moon Garden's Chinese introduction and pause help to disclose Q/E.
- Promoted the sculpted mountains, continuous cloud pines, connected pavilion
  and detailed arched bridge to all four galleries while preserving authored
  transforms, root colliders and image-based completion.
- Regenerated all affected beauty, Object-ID, silhouette and nine reviewed
  baseline images together after inspecting every build/composition view.
- Verification: EditMode 153/153, GPU PlayMode 35/35, authored primary and
  secondary answers, nine-frame visual regression, correct painting Windows
  build and 15-second standalone smoke all passed.
- Known limitation: the project-owned procedural assets are now coherent
  across the product, but a later store-page-quality pass still benefits from
  hand-authored DCC models and texture painting.
- Suggested next task: player-test Moon Garden without G, then judge the whole
  four-gallery candidate rather than reviewing isolated objects.

## T-032 - Moon Garden depth curriculum

Status: DONE

Owner: Codex.

Goal: replace the second gallery's simultaneous depth guesses with a staged
bridge, mountain-depth and pavilion-occlusion lesson while preserving visual
equivalence and keeping rotation locked.

Completion report:

- Added one shared curriculum state used by permissions, guidance and the
  completion reveal; future-stage pieces cannot be selected or assisted.
- Kept locked pieces at the authored solution until their stage begins, then
  restored their stored unsolved pose so earlier lessons remain readable.
- Preserved tolerant image-based completion and prevented overall score from
  completing the gallery before the pavilion stage is established.
- Verification: focused curriculum 1/1, EditMode 153/153, GPU PlayMode 35/35,
  nine-frame visual regression, Windows build and 15-second standalone smoke
  all passed.
- Known limitation: the second gallery still needs the user's final difficulty
  and learning-flow judgment; automated checks establish correctness, not fun.
- Suggested next task: player-test Moon Garden without using G and decide
  whether the mountain stage needs fewer cells, stronger depth labels or a
  shorter acceptance window.

## T-029 - Unified four-piece hero asset family

Status: DONE

Owner: Codex.

Goal: rebuild Mist Valley's bridge, pavilion, main mountain and cloud pine as
one visually coherent celadon collectible family without changing interaction
difficulty.

Completion report:

- Locked one shared production reference and implemented deterministic V3
  bridge, pavilion, six-mass mountain and continuous-branch cloud-pine assets.
- Preserved simplified root BoxColliders, lattice placement, transforms and
  visual-equivalent completion policy while regenerating first-gallery targets.
- Reviewed and stored both fixed build and composition frames; all nine visual
  regression frames passed, with zero drift in the other three galleries.
- Verification: EditMode 153/153, GPU PlayMode 32/32, successful Windows build
  with zero warnings, and 15-second standalone smoke with no exception match.
- Known limitation: these are production-oriented project-owned procedural
  assets, not hand-sculpted Blender models; T-027 remains available for future
  DCC replacement without changing gameplay.
- Suggested next task: player review of overall asset quality and first-level
  readability before applying this family language to later galleries.

## T-027 - Production art infrastructure

Status: DONE

Owner: Codex.

Goal: establish the reusable DCC import contract, fixed-camera visual
regression gate and conservative generated-asset cleanup before production art
replaces the procedural benchmark.

Completion report:

- Added the constrained `Assets/Art/SourceModels` import boundary and validator,
  plus the Blender-to-Unity scale, origin, naming, collision and LOD contract.
- Added nine reviewed camera baselines, tolerant pixel comparison, numeric
  report and heatmap outputs with editor and batch entry points.
- Removed eleven unreferenced T-026 V1 mesh assets after source-name and GUID
  checks; documented the remaining 28-mesh procedural kit and recovery commit.
- Verification: model validation passed; nine regenerated visual frames passed;
  EditMode 153/153 and GPU PlayMode 32/32 passed; Windows build succeeded with
  zero warnings; 15-second standalone smoke produced no runtime exception match.
- Known limitation: this creates the production pipeline but does not create
  Blender-authored hero models; current T-026 art remains the procedural fallback.
- Suggested next task: after the player reviews T-026, author and integrate only
  `HERO_ArchBridge_v001` as the first end-to-end DCC asset trial.

## T-001 — Generate the Unity URP project skeleton

Status: DONE

Owner: Claude Code / DeepSeek, after Codex activates the task.

Goal: turn this documentation folder into a valid Unity 6000.3.18f1 URP project without implementing gameplay.

Acceptance criteria:

- Unity opens the project without compilation errors.
- URP is installed and assigned as the active render pipeline.
- Generated folders are ignored by Git.
- Project contains the top-level asset folders `Art`, `Audio`, `Content`, `Prefabs`, `Scenes`, `Scripts`, `Settings`, and `Tests`.
- A minimal bootstrap scene opens without missing references.
- No third-party package is added.

Completion report:

- Files changed: Unity project metadata in `Packages` and `ProjectSettings`; project bootstrap tooling in `Assets/Scripts/Editor`; generated URP assets in `Assets/Settings/URP`; generated scene `Assets/Scenes/Bootstrap.unity`; top-level asset folders and Git ignore rules.
- Verification: Unity 6000.3.18f1 imported the project and exited with code 0. URP 17.3.0 is both the default and active render pipeline. `Bootstrap.unity` opens with one root, and `GameRoot`, `BuildCamera`, and `Directional Light` were found. Missing script references: 0.
- Fix applied during review: replaced the inaccessible URP 17 call `UniversalRendererData.Create()` with `ScriptableObject.CreateInstance<UniversalRendererData>()`.
- Known limitations: this is an empty technical bootstrap scene; no visual treatment or gameplay has been implemented. Windows IL2CPP support is not yet installed and is not required for editor iteration.
- Suggested next task: Codex completes T-002 before implementation of T-003 begins.

## T-002 — Public repository evidence review

Status: DONE

Owner: Codex.

Goal: evaluate candidate repositories for grid interaction, tweening, outlines, and stylized URP rendering. Record exact licenses, compatible versions, useful concepts, and import/no-import decisions.

Completion report:

- Reviewed grid placement, tweening, outline, and stylized URP shader candidates.
- Recorded license and compatibility evidence in `docs/PUBLIC_REPOSITORY_REVIEW.md`.
- Imported no source code, package, or asset.
- Decision: keep T-003 project-owned and dependency-free; defer PrimeTween; reject QuickOutline; retain OutlineFx and the toon shader only as later isolated visual references.

## T-003 — Pure grid and projection domain

Status: DONE

Owner: Claude Code / DeepSeek.

Goal: implement and test project-owned grid occupancy and single-axis projection logic after the project skeleton is accepted.

Acceptance criteria:

- Runtime domain code lives under `Assets/Scripts/Domain` and has no `UnityEngine`, scene, input, rendering, or UI dependency.
- A value-type integer grid coordinate supports equality and stable hashing.
- A fixed-size 3D occupancy grid validates dimensions and coordinates, prevents duplicate placement, supports removal/query/clear, and exposes occupied count without leaking its internal array.
- Projection along the Z axis returns a 2D X/Y map where a cell is occupied when any depth at that X/Y is occupied.
- A target comparison returns matching-cell count, total-cell count, normalized match ratio, and exact-match state; mismatched dimensions fail explicitly.
- Public APIs validate null and invalid arguments with predictable exceptions or result values.
- EditMode tests cover construction validation, bounds, duplicate placement, removal, clearing, depth collapse, empty projection, exact match, partial match, and dimension mismatch.
- Unity batch compilation and EditMode tests complete without errors.
- No third-party dependency is added.

Implementation guidance:

- Suggested namespace: `PerspectivePuzzle.Domain`.
- Prefer immutable result/value types and straightforward loops over clever abstractions.
- Do not implement MonoBehaviours, GameObjects, controls, visuals, serialization, undo UI, levels, or multiple projection axes in this task.

Completion report:

- Added dependency-free domain assembly under `Assets/Scripts/Domain`.
- Implemented immutable integer coordinates, bounded 3D occupancy, immutable 2D maps, Z-axis projection, target creation with defensive copying, and match comparison.
- Added EditMode tests under `Assets/Tests/EditMode`.
- Codex review fixed an incorrect test re-placement coordinate and added the public `ProjectionMap2D.FromCells` target-data factory.
- Verification: Unity 6000.3.18f1 compiled successfully; 47 EditMode tests passed, 0 failed, 0 skipped.
- No third-party dependency was added.

## T-004 — Stylized visual foundation

Status: DONE

Owner: Claude Code / DeepSeek for implementation; Codex for visual review.

Goal: create a product-looking, non-interactive hero scene that proves the tabletop-toy and optical-laboratory art direction before gameplay presentation is built.

Acceptance criteria:

- Create a separate `Assets/Scenes/VisualPrototype.unity`; do not degrade or repurpose `Bootstrap.unity`.
- Use an orthographic three-quarter composition designed for a 16:9 frame.
- Establish a warm off-white environment, a raised miniature stage, low-saturation teal construction pieces, and one restrained warm accent.
- Construction pieces must not look like untouched default Unity cubes; use a project-owned rounded/beveled mesh or another intentional toy-like silhouette.
- Include a physical projection board that reads clearly in the composition.
- Use project-owned URP materials, soft shadows, ambient treatment, and restrained post-processing appropriate to a cozy optical exhibit.
- Add no third-party package or downloaded asset.
- Provide an editor/batch-safe way to build the scene deterministically and render a 1280x720 PNG review image under `Logs/VisualReviews`.
- Unity batch compilation and scene generation must exit with code 0.
- Codex must inspect the rendered image before marking the task done.

Out of scope:

- Placement input, puzzle logic integration, UI, text instructions, animation, audio, particles, multiple rooms, and final production art.

Notes:

- Product brief and screenshot acceptance criteria are complete in `docs/VISUAL_FOUNDATION.md`.
- DeepSeek completed the rounded-box mesh factory and deterministic scene builder at max effort; Codex reviewed both.
- Codex added a deterministic GPU-backed review capture utility, inspected multiple rendered frames, and granted visual acceptance.

Completion report:

- Files changed: `Assets/Scenes/VisualPrototype.unity`; project-owned rounded-box meshes and URP materials plus the volume profile under `Assets/Art` (`Meshes`, `Materials`, `VisualPrototypeProfile.asset`); editor utilities `Assets/Scripts/Editor/RoundedBoxMeshFactory.cs`, `VisualPrototypeSceneBuilder.cs`, and `VisualPrototypeCapture.cs`.
- Verification (2026-08-04): Unity 6000.3.18f1 batch scene build and GPU capture both exited with code 0 with no C# or shader compilation errors; EditMode tests re-run in batch mode: 47 passed, 0 failed, 0 skipped. `Logs/VisualReviews/VisualPrototype.png` (1280x720) shows the bright physical target board with teal/coral cross, teal rounded construction pieces, coral accent, orthographic three-quarter composition, stage, and shadows; Codex inspected the image and accepted the visual.
- Known limitations: non-interactive hero proof; placement input, puzzle logic integration, UI, text instructions, animation, audio, particles, and multiple rooms remain out of scope per the T-004 out-of-scope list.
- Suggested next task: Codex defines the next gameplay-presentation task on top of the accepted visual foundation.

## T-005 — Playable placement loop

Status: DONE

Owner: Claude Code / DeepSeek for implementation; Codex for final playability review.

Goal: turn the accepted visual foundation and pure projection domain into one immediately understandable playable puzzle scene.

Acceptance criteria:

- Create `Assets/Scenes/PlayablePrototype.unity` without degrading `VisualPrototype.unity` or `Bootstrap.unity`.
- Use the existing `PerspectivePuzzle.Domain` types as the source of truth for a 5x5x3 occupancy grid, Z projection, and target comparison; do not duplicate projection rules in MonoBehaviours.
- Provide a deterministic editor/batch scene builder so the playable scene can be recreated without manual inspector work.
- Show a clearly readable 5x5 placement surface integrated into the accepted tabletop stage.
- Pointer hover shows a snapped translucent preview and whether placement is valid.
- Left click places one rounded teal piece; right click removes the pointed piece; duplicate and out-of-bounds placement cannot desynchronize domain state and GameObjects.
- Number keys 1, 2, and 3 select the active depth layer with a small in-world indicator; controls must not require a free camera.
- `Z` performs one-step undo for both placement and removal.
- The physical projection board displays target, current projection, overlap, and mismatch using an immediately distinguishable restrained palette after every action.
- Exact match locks placement and produces a simple camera/board reveal; do not use a plain `Success` popup.
- Add focused EditMode tests for coordinate conversion and command/undo state that are not already covered by domain tests.
- Unity batch scene generation, compilation, and all EditMode tests succeed. Add no package or downloaded asset.

Out of scope:

- Multiple levels, save data, audio, settings, touch controls, free-camera controls, multiple piece shapes, tween packages, final animation polish, and multi-axis projection.

Implementation guidance:

- Runtime presentation code belongs under `Assets/Scripts/Presentation`; editor-only deterministic construction remains under `Assets/Scripts/Editor`.
- Keep the scene narrow: one target pattern and one placement loop. Prefer clear direct code over framework building.
- Reuse the accepted palette, rounded meshes, materials, camera language, and physical-board concept from T-004.

Completion report:

- Added the presentation layer under `Assets/Scripts/Presentation`: grid/world mapping, reversible placement history, puzzle session orchestration, snapped preview, layer indicator, piece views, pointer/keyboard input, projection-board state rendering, runtime session source, and non-text camera reveal.
- Added deterministic `PlayablePrototype.unity` construction, validation, and 1280x720 GPU review capture under `Assets/Scripts/Editor` and `Logs/VisualReviews/PlayablePrototype.png`.
- Codex corrected the generated grid origin from Z `+1.32` to `-1.32`; the final 5x5 tile centers and their full tile edges lie inside the 3.7x3.7 puzzle slab. Codex inspected the final image and accepted the aligned tabletop composition and readable physical projection board.
- Verification: Unity 6000.3.18f1 scene build, validation, and capture exited with code 0. EditMode: 80 passed, 0 failed, 0 skipped. PlayMode: 1 passed, 0 failed, 0 skipped; it loads the real scene, verifies all 25 placement-surface raycasts, places the seven target cells, confirms exact-match lock/reveal, and verifies camera arrival.
- Known limitation: automated tests do not inject real operating-system mouse and keyboard events, so subjective click feel remains a human playtest concern rather than a functional blocker.
- Suggested next task: package a human-playable build and add restrained onboarding/interaction polish without expanding puzzle scope.

## T-006 — Human playtest build and interaction polish

Status: DONE

Owner: Claude Code / DeepSeek for implementation; Codex for build and visual review.

Goal: make the completed one-puzzle loop understandable and pleasant enough for the user to launch and judge directly without opening the Unity editor.

Acceptance criteria:

- Add a restrained in-world control legend that clearly communicates left-click place, right-click remove, 1/2/3 layer selection, and Z undo without covering the puzzle or projection board.
- Make the active layer unmistakable at a glance using the existing physical layer indicator; avoid a conventional settings-style UI panel.
- Add small project-owned placement and removal motion feedback using direct coroutines or simple interpolation; no tween package.
- Preserve immediate domain mutations and scene/view synchronization throughout feedback animations; rapid valid input must not create duplicate or orphaned views.
- Add `R` to reset the current puzzle and restore board, pieces, reveal state, camera, and input without reloading the application.
- Extend focused EditMode or PlayMode coverage for reset and rapid place/remove synchronization.
- Deterministically regenerate and visually review `PlayablePrototype.unity` after the polish.
- Produce a Windows x86_64 Mono development build under `Builds/Windows` when the installed Unity modules support it; otherwise record the exact missing module without changing to IL2CPP.
- All EditMode and PlayMode tests remain green. No package, downloaded asset, additional level, audio, or settings screen.

Completion report:

- Added a physical in-world control plaque, clearer active-layer presentation, direct placement/removal motion feedback, and `R` reset covering session, history, views, projection board, reveal state, camera, and input.
- Added reset and rapid synchronization coverage. Verification: EditMode 81 passed, 0 failed, 0 skipped; PlayMode 3 passed, 0 failed, 0 skipped.
- Codex rejected the first mirrored/overlapping legend, replaced it with a separate rounded light plaque, regenerated the scene, and visually accepted the final 1280x720 capture.
- Added deterministic Windows x86_64 Mono Development build tooling. Unity build result: Succeeded, exit code 0, 0 warnings; output `Builds/Windows/PerspectivePuzzle.exe` with its data folder (about 150.9 MiB reported build size).
- Launch smoke test: the generated player remained alive for ten seconds and initialized Mono, legacy input, PhysX, and D3D11 on the GTX 1050 Ti without runtime exceptions in `Logs/T006_player_smoke.log`; the smoke-test process was then intentionally closed.
- Known limitation: subjective mouse feel still needs the user's direct playtest; automated PlayMode coverage verifies scene behavior but does not inject operating-system mouse/keyboard events.
- Suggested next task: add the remaining two short puzzle definitions and a minimal physical transition between three puzzles, reusing the same room and systems.

## T-007 — Three-puzzle vertical slice

Status: DONE

Owner: Claude Code / DeepSeek for implementation; Codex for progression and visual review.

Goal: fulfill the product brief's three-short-puzzle promise inside the existing polished room without creating menus or additional environments.

Acceptance criteria:

- Represent puzzle targets as a small project-owned content definition separate from projection/domain rules and from hard-coded controller branches.
- Ship exactly three readable 5x5 target patterns with a deliberate difficulty curve; each pattern must be non-empty and visually distinct on the physical board.
- Start on puzzle one. After its reveal, communicate that the player can continue using one simple existing input (prefer Space or left click) and transition to the next target without loading a new room.
- Transition clears occupancy, history, pieces, preview and prior reveal state; restores the build camera; updates the physical target board and a restrained three-step progress indicator.
- `R` resets only the current puzzle. Completing puzzle three produces a final non-text visual hold and does not wrap silently to puzzle one.
- Rapid input during reveal/transition cannot mutate the locked session or create orphaned views.
- Add focused tests for content validity, ordered progression, per-puzzle reset, transition cleanup and final completion lock.
- Regenerate/capture the playable scene, keep all EditMode and PlayMode tests green, rebuild the Windows Mono player, and smoke-launch it.
- No menus, saves, additional rooms, packages, audio, story, procedural generation, or multiple projection axes.

Completion report:

- Added project-owned `PuzzleContent` with exactly three distinct validated 5x5 targets, ordered `PuzzleProgression`, physical three-step progress indication, and same-room Space transitions.
- Transitions clear occupancy, history, piece views and previews; refresh the board; restore camera/reveal/input; and preserve `R` as current-puzzle reset. Puzzle three ends locked and never wraps.
- Updated deterministic scene construction and validation. Codex visually accepted the final 1280x720 capture: grid and board remain clear, the three progress pips read at the stage edge, and the physical control plaque includes the Space hint without obstructing play.
- Verification: EditMode 93 passed, 0 failed, 0 skipped; PlayMode 7 passed, 0 failed, 0 skipped, including progression cleanup, per-puzzle reset, final lock and rapid-input protection.
- Rebuilt Windows x86_64 Mono Development player successfully (about 150.96 MiB reported size, 0 warnings) and smoke-launched it for ten seconds. The player remained alive and `Logs/T007_player_smoke.log` contained no exception, null-reference, missing-reference or error matches.
- Deliverable: `Builds/Windows/PerspectivePuzzle.exe` and its adjacent `PerspectivePuzzle_Data`/Mono folders.
- Next required evidence: direct human playtest for subjective click feel, puzzle interest, readability and product-level visual impression.

## Cycle 2: Painting reconstruction pivot

The detailed sequence and gates are defined in `docs/CYCLE_02_PAINTING_RECONSTRUCTION.md`. These tasks are PLANNED; none is active until Codex writes the bounded current contract to `tasks/ACTIVE.md`.

### T-008 — Solved landscape and target artifacts

Status: DONE

Goal: build one non-interactive hidden solved diorama with meaningful foreground, middle-ground, and background separation; render its beauty reference, object-ID buffer, and silhouette buffer; prove that the designated perspective view reads as a composed illustration while the three-quarter view reads as a physical arrangement.

Gate: user and Codex approve the paired front/three-quarter stills before interaction work begins.

Completion report:

- Added deterministic `PaintingPrototype.unity`, its editor scene builder/capture utility, eight exact solved-scenery roots, perspective Composition Camera, unobstructed Build Camera, physical Reference Frame, and project-owned generated meshes/materials.
- Codex rejected the first blocked build camera and raw composition, performed two bounded visual repair iterations, and retained the cleaner celadon landscape arrangement after comparing actual GPU captures.
- Generated `PaintingPrototype_Build.png`, `PaintingPrototype_Composition.png`, and target artifacts `MistValleyBridge_Beauty.png` (1280x720), `MistValleyBridge_ObjectId.png` (256x144), and `MistValleyBridge_Silhouette.png` (256x144).
- Verified all eight required exact 24-bit object-ID colors are present. Builder validation and all five GPU captures completed. A pre-existing Unity package-cache `DirectoryNotFoundException` for a Collections test DLL is logged during editor startup but did not prevent scene build, validation, capture, or artifact import.
- Known limitation: the beauty render is an intentionally temporary low-poly/celadon visual baseline, not final production art.

### T-009 — Composition truth and tolerant scorer

Status: DONE

Goal: implement project-owned target metadata and a deterministic scorer using overall silhouette overlap, per-piece object-ID overlap, and occlusion-sensitive coverage. Keep scoring independent from the beauty material and expose diagnostic results without making percentages the final UI.

Gate: known solved transforms pass; deliberate near/far, horizontal, vertical, and occlusion errors reduce the correct score component; asynchronous sampling does not stall play.

Progress report — T-009A:

- Added immutable packed-RGB composition buffers, policy validation, ordered per-piece metrics, silhouette IoU, identity accuracy, mean piece IoU, minimum coverage, normalized weighted scoring, exact-match state, and tolerant pass state in pure C#.
- Focused EditMode verification: 28 passed, 0 failed, 0 skipped.
- Completed by T-009B runtime capture, deterministic scene wiring, and real-scene calibration.

Progress report — T-009B1/B2:

- Added project-owned asynchronous CommandBuffer + AsyncGPUReadback Object-ID evaluation, exact ordered piece tagging, fixed-resolution 256x144 sampling, and a minimal project-owned URP Object-ID shader.
- Deterministic scene builder now wires all eight painting pieces, target texture, Composition Camera and evaluator; machine masks retain their original NPOT dimensions and import readable/uncompressed/non-sRGB.
- Calibrated GPU row orientation, fixed sampling projection against window-aspect drift, and handled forward/reversed Z buffers so runtime occlusion matches the captured target pixel-for-pixel.
- Final focused PlayMode verification: solved composition plus horizontal, vertical, near/far and explicit occlusion perturbation/recovery passed (3 passed, 0 failed).
- Final pure-domain EditMode regression: 28 passed, 0 failed, 0 skipped. No temporary GPU diagnostics remain.

### T-010 — Perspective-piece manipulation

Status: IN PROGRESS

Goal: replace grid placement in the new scene with selection, target-camera-plane dragging, depth adjustment, constrained rotation, reset, and undo for fixed-size scenery pieces inside authored bounds.

Gate: a player can reproduce the solved composition without editing transforms in the Unity Inspector, and depth visibly changes apparent size.

Progress report — T-010A:

- Added a one-piece Arch Bridge interaction slice: Build Camera raycast selection, restrained non-destructive highlight, stable camera-plane dragging, Composition Camera depth adjustment, authored bounds, one-step undo, reset and deselect.
- The deterministic builder creates a fitted bridge BoxCollider, dedicated `PaintingPiece` layer, authored transform metadata and a wired manipulation controller without changing accepted scene visuals.
- Isolated machine Object-ID color from beauty `_BaseColor` property blocks so selection feedback cannot corrupt composition scoring.
- Focused manipulation PlayMode verification: 2 passed, 0 failed. Full PlayMode regression: 12 passed, 0 failed.
- Hands-on T-010A mouse feel review accepted by the user.

Progress report — T-010B:

- Rolled selection, camera-plane dragging, Composition Camera depth adjustment, reset, highlight and one-step undo out to all eight ordered scenery pieces.
- Added nearest-valid-collider overlap selection via `Physics.RaycastAll`, selection switching, and constrained Q/E rotation in 15-degree steps clamped to +/-45 degrees from each piece's authored yaw.
- The deterministic builder now fits one robust root-local BoxCollider to every piece by transforming all eight corners of every renderer world AABB, wires the ordered piece array, and preserves the explicit Arch Bridge compatibility API.
- Fixed the conservative manipulation bounds for the robust tree-cluster collider extents and rebuilt `PaintingPrototype.unity`.
- Focused T-010B PlayMode verification: 5 passed, 0 failed. Full PlayMode regression: 15 passed, 0 failed.
- Remaining T-010 work: author an intentionally unsolved but readable starting layout, then perform hands-on multi-piece feel review and tune bounds/sensitivity only from observed friction.

Progress report — T-010C:

- The scene builder now captures the accepted solved authored transform first, then saves a deterministic, readable unsolved layout for all eight pieces using bounded translations and modest 15-degree-step yaw offsets.
- The saved starting composition fails clearly (observed weighted score about 0.1507), while resetting all eight pieces through the public controller API restores the exact authored transforms and the strong passing composition.
- Builder validation now checks the deterministic start table, authored scale preservation, movement/depth bounds, and real mesh-corner coverage by each root collider after rotation.
- Updated T-010 manipulation tests and older T-009 scorer tests to recover solved baselines through public per-piece reset rather than assuming the saved scene starts solved.
- Focused T-010C verification: 5 passed, 0 failed. Full PlayMode regression: 16 passed, 0 failed.
- Remaining T-010 work: hands-on review of the eight-piece start layout, selection visibility and manipulation feel; tune only observed friction before closing T-010.

Hands-on correction — T-010D:

- User found a visible pavilion roof/column gap and strong southwest/southeast pointer-drag resistance.
- Joined pavilion columns, roof and finial exactly; recaptured solved beauty, silhouette and Object-ID targets from a temporary authored solution while preserving the saved unsolved start.
- Changed pointer dragging from a camera-facing plane to the horizontal tabletop XZ plane, preserving root height and removing the asymmetric world-Y clamp that blocked screen-south movement. Wheel depth remains independent.
- Full PlayMode regression after rebuilt scene and recaptured targets: 16 passed, 0 failed.

Hands-on correction — T-010E:

- User found right-side pieces still had less travel because the shared absolute boundary gave edge pieces unequal remaining room.
- Every piece now receives the same authored-relative horizontal allowance: +/-2.5 world units on X/Z, with the existing common +/-2.0 Composition Camera depth limit; a wider outer safety volume only prevents leaving the installation.
- Added explicit all-eight-piece positive/negative travel assertions. Focused manipulation verification: 6 passed, 0 failed; full PlayMode regression: 16 passed, 0 failed.

Interaction rethink — T-010F:

- Rejected authored-centered world-space travel cages: the puzzle is now controlled in composition space, matching its goal of reconstructing a 2D painting from 3D pieces.
- Pointer dragging uses a plane parallel to the Composition Camera image plane and controls target-image X/Y; the wheel alone controls Composition Camera depth.
- Every piece root can traverse the same normalized canvas rectangle (5%–95% on both axes) and the same absolute depth interval (4.5–12). The enlarged world bounds are emergency numerical safety only.
- Replaced world-distance assertions with all-eight-piece target-camera corner accessibility tests. Focused manipulation verification: 6 passed, 0 failed; full PlayMode regression: 16 passed, 0 failed.

Physical placement cycle — T-010G:

- Replaced free composition-space dragging with a physical pickup transaction on one shared lake surface: lifted weighted follow, persistent landing ghost, valid/invalid feedback, settle/return and one-step undo.
- Placement validity is size-aware: the complete rotated/scaled root BoxCollider footprint must remain inside the common tabletop rectangle and must not overlap another scenery piece.
- Q/E rotates during carry and refreshes validity immediately; Escape returns exactly to the pickup pose; invalid drops preserve prior undo history.
- Added transparent URP valid/invalid preview materials without third-party packages. Preview meshes reuse source shared meshes but contain no colliders, rigidbodies or source material mutation.
- Rebuilt the Sun as a physical weighted base, support post and camera-facing disc ornament, then rebuilt the scene and recaptured beauty, silhouette and Object-ID references.
- Visual review confirms all eight pieces read as physical tabletop objects in the unsolved build view.
- Full GPU-backed PlayMode regression: 18 passed, 0 failed, 0 skipped (including two new physical-placement transaction tests).
- Remaining T-010 gate: direct hands-on feel review and small tuning only; no further interaction architecture change unless the playtest disproves this model.

Hands-on tuning — T-010G.1:

- User accepted pickup weight and placement feel, but found the legal travel area too small and overlapping pieces difficult to target.
- Expanded the shared legal surface from the coloured lake to the full inset inner tray, preserving complete-footprint containment and the same strong red invalid boundary.
- Replaced nearest-depth overlap selection with visual-intent scoring: among ray hits, the piece whose projected renderer bounds centre is closest to the pointer wins, with depth used only as a stable tie-breaker. Broad fitted mountain/tree colliders no longer automatically steal clicks aimed at smaller pieces.
- Rebuilt the deterministic scene. Full GPU-backed PlayMode regression remains 18 passed, 0 failed, 0 skipped.

### T-011 — Reference, live viewfinder, and guidance

Status: IN PROGRESS

Goal: present the complete reference painting and live composition view clearly inside the exhibit; convert scorer diagnostics into restrained, readable near-match feedback and optional per-piece guidance.

Gate: the objective and the most incorrect region are understandable without exposing the hidden ID colors or a coordinate grid.

Progress report — T-011A:

- Added a warm-ivory screen-space curator rail with the full target beauty and a live 640x360 Composition Camera beauty view.
- Replaced the physical Reference Frame placeholder material with the captured target painting.
- Converted score output into a smooth nonnumeric progress line, four calm state messages and a single worst-coverage `Focus: [piece]` hint.
- Kept all machine Object-ID colours, coordinate grids and raw percentages hidden from players.
- Added deterministic builder validation and PlayMode coverage for target/live resources, camera wiring, guidance copy and solved-state response.
- Full GPU-backed PlayMode regression: 19 passed, 0 failed, 0 skipped.
- Remaining gate: hands-on visual/readability review before marking T-011 complete.

Internal QA — T-011B:

- Balanced Canvas scaling between width and height so the curator rail stays readable without dominating 4:3, 16:9, 16:10 or ultrawide layouts.
- Added focus-hint hysteresis: a new piece must remain materially worse after a minimum hold window before replacing the current hint, preventing close scores from producing distracting label flicker.
- Rebuilt the scene and retained a clean 19/19 GPU PlayMode regression.

### T-012 — Painting reveal and tactile polish

Status: COMPLETE

Goal: deliver the signature moment: lock the solved arrangement, move the build camera into the composition viewpoint, visually settle the live scene into the painting, then optionally reveal the unusual spatial arrangement from the side.

Gate: a 20–30 second capture communicates the premise without explanatory narration.

Completion report — T-012:

- Completion requires both a real pointer pickup and a stable passing score, preventing transient samples or test/reset helpers from accidentally firing the reveal.
- Locks manipulation and safely clears any selected/carried visual state before presentation begins.
- Smoothly carries the Build Camera into the exact Composition Camera pose, fades the curator rail, holds the finished painting under a restrained `PAINTING ALIGNED` wash, then moves to a side perspective with `PERSPECTIVE REVEALED` before leaving the physical construction visible.
- Uses unscaled deterministic smoothstep timing with no tween dependency and preserves the Composition Camera/evaluator resources.
- Added direct PlayMode coverage for input lock, exact painting-view alignment and final physical-perspective reveal.
- Full GPU-backed PlayMode regression: 20 passed, 0 failed, 0 skipped.

### T-013 — Cycle 2 validation build

Status: COMPLETE

Goal: run focused EditMode/PlayMode coverage, deterministic visual captures, Windows build and smoke launch, followed by a direct user playtest of the one-painting slice.

Gate: decide from human evidence whether to create a second painting, revise manipulation/scoring, or stop the direction before building content breadth.

Validation report — T-013:

- Added a deterministic Windows x64 development-build command that includes only the painting prototype scene and verifies the executable plus data directory.
- Fixed a player-only shader stripping failure by serializing the Object-ID shader directly on the composition evaluator; scene validation now rejects a missing explicit reference before a build is attempted.
- Rebuilt the scene and passed the complete GPU-backed PlayMode suite: 20 passed, 0 failed, 0 skipped.
- Passed the complete EditMode suite: 121 passed, 0 failed, 0 skipped.
- Rebuilt and launched the standalone Windows player. It remained responsive throughout the smoke window and its player log contained no missing-shader, null-reference or runtime exception.
- Windows handoff artifact: `Builds/WindowsPainting/PerspectivePainting.exe`.
- Engineering validation is complete. The remaining gate is intentionally human: judge the interaction and reveal as a player before expanding content.

### T-014 — Product recovery: frictionless composition

Status: COMPLETE

Goal: replace collision-driven placement with predictable visual composition. Every pointer drag must retain a landable preview, tray edges must behave as soft stops, and scenery overlap must be legal because occlusion is the puzzle language.

Gate: automated all-piece reachability and overlap tests pass, visual captures show no red placement dead zones during normal play, and a standalone build completes repeated pick/place actions without snap-back.

Completion report — T-014:

- Pointer overshoot now meets a footprint-aware soft tray edge and remains landable.
- Scenery overlap is legal, matching the core occlusion-composition mechanic.
- The real visual-intent pointer resolver is public and directly regression-tested, so broad decorative colliders cannot silently make visible pieces unselectable.
- Placement and full GPU PlayMode coverage pass after the interaction change.

### T-015 — Handcrafted diorama art rebuild

Status: COMPLETE

Goal: replace the empty low-poly test installation with a coherent collectible painting table: walnut frame, parchment backdrop, celadon water, layered painted mountain reliefs, detailed trees, pavilion, bridge rails, atmospheric distance layers and restrained gallery lighting.

Progress report — T-015A:

- Added the generated art-direction keyframe under `docs/art-direction` as the visual quality reference.
- Rebuilt the table and backdrop as framed walnut-and-gold physical furniture.
- Replaced cone-only mountains with reusable extruded painted-relief meshes and layered glaze/ink accents.
- Added layered tree crowns, wood trunks, a jade pavilion roof, structural lintels, bridge rails and gold focal accents.
- Added soft key shadows and static distant paper-cut mountain layers.
- Regenerated beauty, Object-ID and silhouette targets from the new authored solution.
- Full validation: 121 EditMode and 21 GPU PlayMode tests passed; Windows build succeeded and its standalone smoke process remained responsive with a clean runtime log.

Completion report — T-015 through T-018 release candidate:

- Replaced the abrupt prototype entry with a restrained museum opening, chapter title and concise interaction language.
- Added reset, undo, completion hold, next-gallery hand-off and final collection-complete state.
- Added two further authored occlusion compositions, Moon Garden and Red Cliffs, each with its own beauty, silhouette and Object-ID target and deterministic scene.
- Added a reusable build/capture pipeline that creates and validates all three galleries from one art and interaction baseline.
- DeepSeek v4 Flash supplied focused flow tests from the shared task contract; Codex reviewed and integrated them.
- Final validation: 121/121 EditMode and 24/24 GPU PlayMode tests passed. Windows build succeeded with zero build warnings, and the standalone player remained alive and responsive through the smoke window with no runtime exception match.
- Release candidate: `Builds/WindowsPainting/PerspectivePainting.exe`.

### T-019–T-023 — Solvability, creative guidance, and dual-view validation

Status: COMPLETE

- Added pure per-piece visual diagnostics for normalized direction, apparent size/depth, principal-axis rotation, occlusion and near alignment without comparing authored transforms.
- Rebuilt the curriculum: Mist Valley exposes four pieces with depth/rotation locked, Moon Garden exposes six with depth enabled, and Red Cliffs exposes all eight controls.
- Added selected-piece gold target outlines, hold-H directional clues, active-piece-only focus, and non-snapping near-alignment confirmation.
- Added Twin Seal as a fourth gallery with a primary painting plus a right-side four-piece silhouette goal. Completion requires both independent goals and reveals primary, secondary, then physical construction.
- DeepSeek v4 Flash max supplied diagnostic and curriculum test breadth under bounded task contracts; Codex reviewed, integrated and ran Unity acceptance.
- Final validation: 148/148 EditMode and 28/28 GPU PlayMode tests passed. The Windows build contains all four galleries, reported success with zero build warnings, and remained responsive through a 15-second standalone smoke window without runtime exception matches.
- Release candidate: `Builds/WindowsPainting/PerspectivePainting.exe`.
# T-038 — Public repository and playtest release preparation

Status: IN PROGRESS

Prepare accurate public documentation, audit the repository for credentials and oversized/generated content, confirm repository visibility and licensing with the product owner, publish the source to GitHub, attach a clean Windows x64 playtest ZIP to `v0.1.0-playtest`, verify the remote artifacts, and only then remove regenerable local caches.

Active contract: `tasks/ACTIVE.md`. Publishing details: `docs/PUBLISHING_ZH.md`.

---
