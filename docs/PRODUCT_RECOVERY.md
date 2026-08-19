# Product recovery direction

## Decision

The validated vertical slice is a technical prototype, not a release-quality
game. The player product gate failed on 2026-08-09. Engineering success is no
longer accepted as evidence of product quality.

The recovery target is a tactile perspective-painting game: players freely
layer sculptural scenery until one framed camera view becomes an authored
illustration. Occlusion is a core verb, so scenery overlap must be legal.

## Interaction contract

- A picked piece always has a landable destination.
- Cursor overshoot meets a soft tray edge; it never creates a large red dead zone.
- Scenery may overlap because depth and occlusion form the target image.
- Release lands at the visible preview. Escape is the explicit cancel action.
- The player should not perform collision management on behalf of the engine.
- Feedback describes composition progress, not internal validity rules.

## Art-direction hypothesis

Use a handcrafted illustrated diorama rather than generic low-poly geometry:

- physical scene: painted wood, paper, ceramic and translucent resin;
- target view: a composed storybook landscape with controlled value grouping;
- palette: warm parchment, celadon, ink green, mineral blue and one vermilion accent;
- lighting: one warm key, broad cool fill, soft contact shadows and restrained bloom;
- UI: framed artwork and minimal curator marks, not a debug dashboard;
- motion: weighted pickup, magnetic edge stop, gentle settle and a deliberate reveal.

This combines three useful precedents without copying their surface treatment:

- Shadowmatic makes every puzzle belong to an atmosphere, room and musical idea:
  https://www.shadowmatic.com/
- Assemble with Care treats object manipulation, sound and handcrafted imagery as
  one tactile experience: https://www.assemblegame.com/
- Gorogoa makes layering illustrated views the puzzle language itself:
  https://www.gorogoa.com/

## Autonomous quality gates

No intermediate user test is requested until all gates pass:

1. Every movable piece can reach every meaningful composition region without a
   failed release or unexplained snap-back.
2. A first-time player can identify the target, live view and next useful action
   from the screen alone.
3. Automated screenshots at 16:9 and 16:10 show no clipped UI, debug visuals or
   accidental visual collisions.
4. The opening, manipulation loop and completion reveal form a coherent 30-second
   capture without narration.
5. EditMode, GPU PlayMode, Windows build and standalone smoke checks pass.
6. At least three internally authored puzzle compositions demonstrate that the
   mechanic creates different visual ideas rather than one repeated arrangement.

## Tooling research

`Besty0728/Unity-Skills` is the strongest candidate for closing the live-editor
observation loop. It exposes scene inspection, screenshots, tests and editor
operations through a local REST server and supports Unity 2022.3+. It is not yet
installed: adding an editor-control package is a project dependency decision and
will be evaluated separately against the existing deterministic builders.

Repository: https://github.com/Besty0728/Unity-Skills

`gamedev-skills/awesome-gamedev-agent-skills` is useful mainly as an art-direction
and asset-workflow reference. Installing its entire router would add excessive
instruction surface; only a narrowly selected visual-development skill should be
considered.

Repository: https://github.com/gamedev-skills/awesome-gamedev-agent-skills

## Visual keyframe

The first generated art-direction target is stored at:

`docs/art-direction/perspective-painting-keyframe-v1.png`

It is a quality and material-language reference, not a literal gameplay target.
The playable 3D composition must earn the same richness through real geometry,
materials, lighting and interaction rather than displaying this image as a fake
background.

T-015A visual-review captures are retained beside the keyframe:

- `docs/art-direction/t015a-build-view.png`
- `docs/art-direction/t015a-composition-view.png`
