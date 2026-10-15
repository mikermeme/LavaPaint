# LavaPaint

Client-only, vanilla-compatible BepInEx mod that paints the terrain paint mask: Ashlands lava, Deep North snow, or any color you set.

The paint mask is a per-vertex RGBA texture. In the Ashlands, alpha above 0.6 is lava (`Heightmap.IsLava`, not in biome-edge zones). In the Deep North, green is the snow depth (`GetCultivationMask`). Vanilla paint cannot carry a custom color and always keeps alpha, so the mod writes the `TerrainComp` paint arrays directly as the ZDO owner (same pattern as TerrainBlueprint's paste). The saved terrain data stores full RGBA per vertex, so other clients, vanilla ones included, load and render it.

## Hoe menu

Equip the Hoe and open the build menu. Fourteen entries are added after the vanilla ones. The brush size is that of the vanilla Hoe piece they are cloned from (Miadhoe's radius applies too). The other Hoe tools are untouched.

| Entry | Paints | Where |
|---|---|---|
| Lava (Red / Orange, Soft / Hard) | `0,0,0,alpha` or `1,0,0,alpha` (`Lava / alpha`, default 1) | Ashlands |
| Embers (Soft / Hard) | alpha only (`Embers / alpha`, default 0.24), the ground texture stays. Below 0.6 it glows but is not lava | Ashlands |
| Custom Paint (Soft / Hard) | the RGBA from the `Custom` config section, any channel can be switched off with `writeRed` etc. | any biome |
| Snow: Clear | green (snow depth) set to 0: shallow snow, buildable | Deep North |
| Snow: Deep | green set to `Snow / deepDepth` (0.4) | Deep North |
| Snow: Very Deep | green set to `Snow / veryDeepDepth` (1.0) | Deep North |
| Snow Drift (+) | adds `Snow / step` (0.25) to the snow depth per stroke | Deep North |
| Snow Shovel (-) | removes `Snow / step` from the snow depth per stroke | Deep North |
| Paint Eraser | puts all four channels back to worldgen (ground, lava, snow). Height is not changed | any biome |

Hard paints every vertex in the brush at full strength, soft fades out linearly over the radius. Snow brushes use `Snow / hardEdged` (default soft). The custom brush icon shows the configured color, re-equip the Hoe to refresh it after a change.

Snow facts from the 1.0 source: green above 0.12 is deep snow, above 0.6 very deep, the ground height rises with green, and most pieces cannot be built where green is above 0.15 (`Player.m_deepSnowBuildHeight`). The snow brushes only write green, like vanilla Cultivate in the Deep North.

The entries are clones of a vanilla Hoe paint piece without a network view, so nothing new is registered on the server. When one is placed the vanilla terrain op is swapped for the brush stroke. Ward and no-build rules apply as for any Hoe piece.

## Console

```
lava zone[=<rings>] | radius=<m>
    alpha=<0-1> red=<0-1> green=<0-1> blue=<0-1>  (or style=red|orange)
    only  additive  biome=any|ashlands|deepnorth  soft|hard
lava reset zone[=<rings>] | radius=<m>
lava erase zone[=<rings>] | radius=<m>
lava undo
```

Without `only` the command paints lava: red from `style`, green 0, blue 0, alpha from config, Ashlands only. `only` writes just the channels you give and leaves the rest. `additive` makes the channel values deltas (-1 to 1) that are added to the terrain. `biome=` sets where it may paint (default `ashlands`).

Examples:

```
lava radius=8 red=0.5                                     lava with a mixed red channel
lava radius=10 green=0.4 only biome=deepnorth             snow depth 0.4
lava radius=10 green=0.25 only additive biome=deepnorth   add snow
lava radius=6 red=0 green=0 blue=0 alpha=1 biome=any      the vanilla "nothing" color anywhere
```

`zone` paints the zone you stand in, `zone=1` also paints the 8 neighbors. `reset` puts alpha back to worldgen and leaves the other channels. `erase` puts all paint back to worldgen (any biome by default). `undo` restores the zones changed by the last paint, reset or erase (32 steps, this session only).

## Config

`Hoe Menu / showInHoeMenu`, `Lava / alpha`, `Embers / alpha`, `Custom / red, green, blue, alpha, writeRed, writeGreen, writeBlue, writeAlpha`, `Snow / deepDepth, veryDeepDepth, step, hardEdged`, `Command / style, hardEdged`, `Debug / logHoeTable` (logs the Hoe pieces and which one is cloned, on by default), `Debug / logIntercepted`.

## Limits

- Lava and embers stay in the Ashlands and snow stays in the Deep North, because the channels mean different things per biome and biomes come from worldgen on every client (a client mod cannot change them). The custom brush and the eraser work in any biome.
- Lava in biome-edge Ashlands zones is painted with a warning: the game does not count it as lava there (damage, footsteps).
- Taking ownership of terrain data can be undone by server-side mods.
- The vanilla `optterrain` command re-syncs alpha from worldgen and will remove painted lava near the player.
- The Recent and Favorites tabs of the build menu remember the entry names. They are ignored when the mod is not loaded.

## Build

Same setup as Sharpie and Cryptanalyst: `dotnet build -p:GamePath="D:\Games\Valheim"`. Needs the publicized `assembly_valheim` and `assembly_utils`.
