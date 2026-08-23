# Interior ghost pathfinding edge case

## Case

The recycling-pit BNV source contains two lower stair ghosts whose base edges lie inside the horizontal footprint of the adjoining flat rooms instead of on their outer boundaries:

| Ghost | Stair room | Flat room | Base midpoint (X, Y, Z) |
| --- | --- | --- | --- |
| `ghost_130` | `room_100` | `room_96` | approximately `(75.756, 0, 262.357)` |
| `ghost_132` | `room_103` | `room_94` | approximately `(-75.756, 0, 262.357)` |

The room and ghost geometry is in `state_bnv.dae`. `state_env.dae` and `state_furn.dae` contribute geometry weights to ordinary grids, but room adjacency comes from the BNV and ghost geometry.

The lobby landing also has two upper SAT transitions:

| Ghost | Stair room | Flat room | Base midpoint (X, Y, Z) |
| --- | --- | --- | --- |
| `ghost_131` | `room_102` | `room_91` | approximately `(131.182, 21, 230.357)` |
| `ghost_133` | `room_101` | `room_90` | approximately `(-131.182, 21, 230.357)` |

Their adjacencies were serialized correctly after the logical-room repair, but the ordinary landing-grid rasterizer allowed impassable geometry to sever the projected SAT outlet in `room_90`.

## Binary comparison

The current and original `level11_Final.dat`/`.raw` pairs were exported, and their AKEV pathfinding grids were decoded. AKVA grid runs store the weight in the low nibble and the run length in the high nibble; a zero high nibble uses the following byte as an extended run length.

The relevant weights are:

| Value | Meaning | Extracted TGA color (BGRA) |
| ---: | --- | --- |
| 2 | Border1 | `E6 D8 AD FF` |
| 3 | Border2 | `FA CE 87 FF` |
| 5 | Border3 | `FF 00 00 FF` |
| 6 | Border4 | `80 00 00 FF` |
| 8 | Danger | `00 A5 FF FF` |
| 9 | Impassable | red in the in-game/debug interpretation |

Both original ramp BNVs have 15 by 15 grids with this exact distribution:

| Weight | Tiles |
| ---: | ---: |
| Border1 (2) | 19 |
| Border2 (3) | 28 |
| Border3 (5) | 32 |
| Border4 (6) | 31 |
| Danger (8) | 115 |

The 110 non-Danger tiles form the projected blue stair corridor visible in the original level. The broken OniSplit result instead left the affected ramp area blocked/red.

## Root causes

### Logical rooms were split by polygon

`state_bnv.dae` contains 151 room nodes, but the old builder created one BNV for every floor polygon and produced 155 AKVA rooms. The nodes responsible for the four extra rooms were:

| Room node | Floor polygons |
| --- | ---: |
| `room_95` | 3 |
| `room_97` | 3 |
| `room_100` | 2 |

A DAE node is one logical BNV even when its floor is represented by several polygons. Splitting those polygons shifted subsequent room indices and reduced each generated BSP to only one part of the intended volume. In particular, source `room_100` became two AKVA rooms, and source `room_103` no longer had its source index in the binary.

The near-duplicate cleanup in `RoomDaeReader` could also delete both vertices around a short edge. That collapsed the intended `room_103` outline to a triangle and further damaged containment tests.

### Interior ghosts need an overlap test

The old adjacency probe sampled one point on each side of the ghost plane. That works for a ghost on a shared boundary, but an interior ghost can have both side probes inside the larger flat room. The raised midpoint of the ghost's base edge is inside both overlapping BNVs and is therefore the correct first test for this case.

OniSplit now pairs all distinct rooms containing that midpoint. If it cannot form a pair there, it retains the old opposite-side probe as the boundary-ghost fallback. The existing height and volume ordering still resolves cases with more than two containing rooms.

### Stair grids require the two SATs

The actual SAT pairs for these ramps are:

| Stair room | Lower SAT | Upper SAT |
| --- | --- | --- |
| `room_100` | `ghost_130` | `ghost_136` |
| `room_103` | `ghost_132` | `ghost_138` |

`ghost_131` and `ghost_133` are not the upper SATs for these two BNVs.

Bungie's `Imp_Path.c` does not rasterize ordinary level geometry for a stair BNV. It:

1. Initializes the temporary grid to Danger.
2. Finds one `StairsDown` SAT and one `StairsUp` SAT whose base points belong to the BNV.
3. Converts the four SAT base points with `MUrFloat_Round_To_Int`.
4. Extends the SAT quad by approximately four grid squares at each end.
5. Rasterizes the quad as two inclusive scanline triangles with Clear weight.
6. Expands four border layers from Danger into the Clear corridor.

OniSplit previously rasterized stair BNVs through the ordinary floor/geometry path. The replacement follows the importer algorithm, including round-to-nearest coordinate conversion and Bungie's inclusive triangle scanline behavior. Truncating coordinates or using the generic polygon scanner moves corridor edges by one tile and does not reproduce the original byte layout.

### Flat landing grids require SAT outlets

Bungie's `PHiRasterizeSAT` also projects every stair SAT 15 world units toward the adjoining stair BNV center in the flat room's grid. It writes Stairs weight 7 as two triangles, then places an Impassable endpoint with SemiPassable neighbors at each end of the SAT base edge.

OniSplit now reproduces that projection for non-stair rooms. It first uses Bungie's normal weight precedence. If geometry selected by OniSplit's coarser BNV intersection test leaves an Impassable cell across the outlet centerline, it redraws that outlet as Stairs before adding the protected endpoints. This fallback repairs the severed `room_90` to `room_101` route without indiscriminately overriding obstacles in already-connected outlets.

## Implementation

- `RoomDaeReader` copies each COLLADA node's unique ID to its polygons. `RoomBuilder` groups floor polygons by source file and node ID (falling back to the object name only for legacy inputs), builds one logical room, and creates a union BSP from the component floor prisms. Distinct nodes with the same display name therefore remain distinct BNVs.
- `Room` retains and exports all component floor polygons while keeping a representative floor plane for room ordering and stair calculations. It also retains each component prism BSP so the 10-unit geometry-admission tolerance is applied independently to every part of a grouped room.
- `RoomDaeReader` removes short-edge duplicate vertices one at a time and removes collinear vertices without erasing both corners.
- `RoomBuilder.ConnectRooms` tries midpoint overlap pairing before the legacy side probes, then classifies the lowest and highest stair adjacencies as `StairsDown` and `StairsUp` SATs.
- `RoomGridBuilder` creates stair grids from those two SAT base edges, applies the four Danger border passes, filters ordinary geometry through tolerant BNV admission, and emits SAT outlets into adjoining flat rooms.
- `RoomGridRasterizer` uses Bungie's rounded coordinates, four-tile extension, inclusive two-part triangle rasterizer, 15-unit landing projection, endpoint protection, and blocked-centerline fallback.

## Validation

OniSplit was built with:

```text
dotnet build OniSplit.csproj -c Debug --no-restore
```

`-grid:create` was run with `state_bnv.dae`, `state_env.dae`, and `state_furn.dae`. It read exactly 151 rooms and emitted no missing-adjacency warnings for `ghost_131`, `ghost_133`, `ghost_136`, or `ghost_138`.

A complete level was then imported with the patched `-create:level`, the resulting `AKEVstate.oni` was extracted again, and its persisted binary data was checked:

| Ghost | Binary quad | Binary BNV adjacency records |
| --- | ---: | --- |
| `ghost_130` | 29241 | 100 and 96 |
| `ghost_131` | 29242 | 102 and 91 |
| `ghost_132` | 29243 | 103 and 94 |
| `ghost_133` | 29244 | 101 and 90 |
| `ghost_136` | 29247 | 100 and 91 |
| `ghost_138` | 29249 | 103 and 90 |

The four stair-room grids remain byte-identical to the original archive. The landing grids are not byte-identical because some ordinary border and obstacle weights still differ, but both projected outlets are continuous and the target room is substantially closer to the original:

| Grid | Matching decoded pixels | Patched SHA-256 |
| --- | ---: | --- |
| `bnv_grid_090.tga` | 208 / 225 | `82B5602E27B944FFE3B11E6D967685F66CCE1DA3A0FD77C2D0FFB8202B228976` |
| `bnv_grid_091.tga` | 206 / 225 | `52997EAB1CB8CF8D028950D02E6CE69DEB4D05166FB1BD5A41E09ADBDE57B9FD` |
| `bnv_grid_100.tga` | 225 / 225 | `9A9EDCD866F860E1354273BCDAC1E8EAD5BB5A098DB1D110B3809A981C917B5B` |
| `bnv_grid_101.tga` | 306 / 306 | `7F79875AE017B06D8AA5D17F0AAB4CEA73CF7CCD4483D5C47BC9A4179F68A70E` |
| `bnv_grid_102.tga` | 306 / 306 | `86CC87216789F8CE3F859441BFD3634D223F02042452F36CEE0B182C315B13B4` |
| `bnv_grid_103.tga` | 225 / 225 | `0FB152B89F1A5BE0DDB78C7817B82E19642899F6D8DD89193B59752D923658C9` |

The rebuilt `level11_Final.dat`/`.raw` pair has SHA-256 values `8FFB2219889FEEB5C93827534D3863C92D236414462D355B53C58B4DE42850A8` and `1B746DCD40CF95BD4A678A35464ECF3D648D72EA44444AAABCDB5F9E1C392434`. Its exported `AKEVstate.oni` has SHA-256 `C9BAC9103922A85DA6C54733C6CF04AFF50AF38FC5B6D01ED5D213B4F90CF145`.

This verifies the two interior lower transitions, the upper `room_90` to `room_101` transition through `ghost_133`, its mirrored `room_91` to `room_102` transition through `ghost_131`, and their persisted pathfinding weights after complete archive serialization.
