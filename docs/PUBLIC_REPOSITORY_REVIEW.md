# Public repository review

Reviewed for T-002 on 2026-08-03 against Unity 6000.3.18f1 and URP 17.3.0.

No third-party source code or packages are imported by this task.

## Decision summary

| Candidate | License | Useful ideas | Unity 6 / URP 17 risk | Decision |
|---|---|---|---|---|
| SunnyValleyStudio/Grid-Placement-System-Unity-2022 | MIT | Native Grid coordinate conversion, placement/removal state separation, preview object, pointer-over-UI guard | Written for Unity 2022 and solves a broader building-placement problem | Reference concepts only; do not copy or import |
| KyryloKuzyk/PrimeTween | Custom permissive game-use license | Placement pop, deletion shrink, camera reveal, UI fades and sequencing | Active project, but no Unity 6 compatibility statement was found in the reviewed README | Defer package installation until the feedback/reveal slice; install only through its supported package route if selected |
| chrisnolet/QuickOutline | MIT | Per-object hover and selection outline | Open repository issues report Unity 6 LTS and orthographic-camera problems; implementation is primarily aimed at world-space/VR use | Reject for this project |
| NullTale/OutlineFx | MIT | URP screen-space selection outline with softness and mask controls | URP renderer feature APIs are version-sensitive; Unity 6/URP 17 compatibility was not established | Keep as a later isolated experiment; do not import now |
| ColinLeung-NiloCat/UnityURPToonLitShaderExample | MIT | Toon lighting equation, controlled light/shadow bands, outline-related HLSL organization | Example shader code may depend on older URP include/API details; visual direction is not yet locked | Reference lighting concepts only; build a project-owned Shader Graph/HLSL baseline later |

## Evidence and rationale

### Grid placement reference

Repository: https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022

The repository describes a 3D placement system using Unity's native Grid component and separates placement, removal, preview, input, and grid data. Those boundaries are useful, but its building database and placement-state implementation exceed this game's discrete puzzle-domain needs. Our occupancy and projection logic remains pure C# and project-owned.

### PrimeTween

Repository: https://github.com/KyryloKuzyk/PrimeTween

PrimeTween provides transform, material, camera, UI, shake, delay, and sequence animation APIs. Its license permits use and modification in free and commercial games distributed in binary form, while restricting redistribution of its source/tarball as a repackaged product. It is the strongest candidate for the polished feedback layer, but it would add no value to the pure domain task and therefore is intentionally deferred.

### Outline choices

QuickOutline is not selected because its open issues include reports about Unity 6 LTS, runtime disabling, orthographic cameras, and distance-dependent width. OutlineFx has a more appropriate screen-space URP approach, but renderer-feature compatibility must be proven in an isolated Unity 6/URP 17 test before adoption.

For the first interactive slice, hover feedback should start with a project-owned ghost material and simple color/scale feedback. An outline package is optional, not foundational.

### Stylized shader reference

Repository: https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample

The example is MIT licensed and useful for understanding a compact toon-lighting implementation. It should not be copied before the art direction is tested with URP Lit/Shader Graph. The product target is a soft toy/optical-lab look, not necessarily hard cel shading.

## Import policy resulting from T-002

1. T-003 imports nothing.
2. Core puzzle logic never depends on tween or rendering packages.
3. PrimeTween may be proposed when placement and reveal animations begin.
4. OutlineFx requires a disposable compatibility test before package inclusion.
5. Any later import must update `THIRD_PARTY_NOTICES.md` with repository URL, pinned revision/version, license, files or package route, modifications, and purpose.

