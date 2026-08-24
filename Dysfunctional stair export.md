# Dysfunctional stair export

## Summary

Oni uses sloped AGQG quads as invisible stair ramps. Decorative stair geometry remains normally textured in `*_env.dae`; only the actual slope quads belong in `*_env_markers.dae` with `_marker_stairs`. During import, the ramp causes intersecting decorative treads to receive `NoCharacterCollision`.

`Ghost`, `StairsUp`, and `StairsDown` are BNV-adjacency flags, not stair-ramp markers. The established marker rules therefore remain unchanged: ghost-like polygons are excluded, `Invisible` plus `Danger` is `_marker_danger`, and `Invisible` plus generic `Stairs` is `_marker_stairs`.

The generic `Stairs` flag alone is insufficient. Bungie's source AGQG uses it on visible stair decoration, ordinary walkable ramps, and a small number of visible quads which actually carry stair-ramp semantics.

## Bungie source evidence

Original `AKEVEnvWarehouse` contains 448 generic-`Stairs` polygons. Thirty-eight are the missing marker ramps; the other 410 must remain ordinary visible geometry:

| Kind | Count | Required export |
| --- | ---: | --- |
| Actual stair slope quads | 38 | `_env_markers.dae`, `_marker_stairs` |
| Decorative upward-facing treads | 400 | `_env.dae`, original material |
| Decorative undersides | 4 | `_env.dae`, original material |
| Ordinary exterior ramps | 6 | `_env.dae`, original material |

Each actual ramp is a broad sloped quad under ten tread bands. The tread positions cover normalized run coordinates `0.05` through `0.95` at `0.1` spacing, with matching riser geometry. Four similar downward-facing polygons lie six world units below a real ramp and must not be selected.

Original level 8 is the principal negative control. Its AGQG contains 37 existing environment stair-marker quads. Ordinary visible ramps remain regular environment geometry; narrow false candidates can overlap 17 bands but have width/run approximately `0.0333`. `tctf_script_601` is not a stair marker in the original extraction and must not be counted as one.

## Generic classifier

`Oni.Akira.StairRampClassifier` runs only when DAE extraction is invoked with `-getVanillaStairs`, after source materials have been attached and the established marker lookup has failed. Without the option, OniSplit retains its legacy marker detection. The classifier contains no AKEV, file, object, material, or polygon identity.

A visible fallback candidate must:

- have generic `Stairs`, exactly four vertices, and no `NoCharacterCollision`;
- not be ghost-like, dangerous, or invisible;
- have `abs(normal.Y)` in `[0.35, 0.995)`;
- have exactly two low and two high vertices;
- have rise greater than `1`, horizontal run greater than `1`, and width/run at least `0.25`;
- represent the upward-facing walkable surface after winding is considered.

The classifier builds a dedicated octree over full source world geometry. Unlike the room-grid index, its predicate retains `NoCharacterCollision` decoration. Results from overlapping leaves are deduplicated.

Cover geometry is clipped to the candidate footprint extruded vertically from `+0.1` through `+7.5` above its slope. A candidate requires:

- at least eight horizontal `NoCharacterCollision` bands (`abs(normal.Y) >= 0.95`);
- bands grouped by normalized run coordinate with tolerance `0.025`;
- normalized tread-position span at least `0.75`;
- aggregate tread-width coverage at least `80%`;
- tread-spacing coefficient of variation no greater than `0.25`;
- at least eight distinct vertical riser/intersection bands.

Normals are oriented consistently before the parallel test. A quad with normal dot at least `0.999` which overlaps above the candidate in the clipped corridor rejects it. This rejects the four level 1 undersides. Downward-facing unpaired backfaces are not marker ramps.

`PolygonUtils.ClipToPlane` keeps the positive half-space, so all six prism planes point inward. The source-polygon constructor now initializes its bounding box, allowing the dedicated octree to index loaded AGQG geometry correctly. `RoomGridBuilder` and its global stair-collision behavior are unchanged.

## Validation

`dotnet build OniSplit.slnx -c Debug` succeeds. Direct extraction from the original archives produced these fallback counts:

| Level | 1 | 2 | 3 | 4 | 6 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 18 | 19 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Fallback | 38 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Existing invisible stairs | 0 | 13 | 24 | 2 | 14 | 37 | 7 | 43 | 20 | 23 | 0 | 10 | 37 | 25 |

Level 1 retains all 410 visible generic-`Stairs` polygons. Level 8 retains its 37 original environment stair markers while ordinary ramps remain regular.

Full copied-project round trips produced:

| Level | Canonical generic `Stairs` | `Invisible` stair ramps | Stair ramps with `NoCharacterCollision` |
| ---: | ---: | ---: | ---: |
| 1 | 38 | 38 | 0 |
| 8 | 37 | 37 | 0 |

Direct archive extraction took approximately `0.4` to `1.7` seconds per listed level. Copied-project create/import round trips took approximately `3.1` seconds for level 1 and `3.5` seconds for level 8 on the validation machine.

No LevelRecyclerCore source or level project was changed.
