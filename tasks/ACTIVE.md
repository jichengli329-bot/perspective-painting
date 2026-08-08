# Active implementation cycle

Status: DONE

# T-011 — Reference, live viewfinder, and guidance

Delivered:

- A right-side warm-ivory curator rail presents the complete target painting and a live 16:9 beauty feed from the Composition Camera.
- The physical Reference Frame now carries the captured target painting instead of a neutral placeholder.
- Evaluator output is translated into a smoothly animated progress line and restrained state copy: Arrange the scene, Composition forming, Almost aligned, Painting aligned.
- One `Focus: [piece]` hint names the scenery piece with the lowest target coverage; no Object-ID colours, grid or raw percentage is exposed.
- The live RenderTexture is created and released safely at runtime, restoring any prior Composition Camera target on disable/destroy.
- Deterministic builder validation covers evaluator, camera, target/live images and ordered eight-piece display names.
- Full GPU-backed PlayMode regression: 19 passed, 0 failed, 0 skipped.

Remaining product gate:

- Hands-on visual review of rail size, readability, live-view latency and whether the single focus hint feels helpful without solving the puzzle for the player.
