namespace LavaPaint;

using System;
using System.Collections.Generic;

using UnityEngine;

// Which zones a stroke may change. A zone matches when any of its four corner biomes is the biome. Paint channels mean
// different things per biome (alpha is lava in the Ashlands and the vegetation mask elsewhere, green is snow depth in
// the Deep North and cultivated ground elsewhere), so the lava and snow brushes stay inside their biome.
enum BiomeFilter {
  Any,
  AshLands,
  DeepNorth,
}

// The paint mask is the per-vertex RGBA texture of the terrain. Lava is alpha above 0.6 in the Ashlands
// (Heightmap.IsLava: not a biome-edge zone), snow depth is green in the Deep North. Vanilla paint operations cannot
// carry a custom color over RPC and keep alpha, so this writes the TerrainComp paint arrays directly as the ZDO
// owner. The saved data carries full RGBA per modified vertex, which vanilla clients load and render like any other
// terrain paint.
static class LavaPainter {
  public sealed class Result {
    public int Zones;
    public int Vertices;
    public int EdgeZones;
    public int SkippedZones;
    public int UnloadedZones;
    public int NotOwned;
  }

  // A null channel is left as it is on the terrain. When Additive is set the channel values are deltas that are added
  // to the terrain (scaled by the brush strength) and clamped to 0-1, like vanilla Cultivate in the Deep North.
  public readonly struct LavaSettings {
    public readonly float? Alpha;
    public readonly float? Red;
    public readonly float? Green;
    public readonly float? Blue;
    public readonly bool HardEdged;
    public readonly bool Additive;

    public LavaSettings(
        float? alpha, float? red, float? green, float? blue, bool hardEdged, bool additive = false) {
      Alpha = alpha;
      Red = red;
      Green = green;
      Blue = blue;
      HardEdged = hardEdged;
      Additive = additive;
    }

    public static LavaSettings Lava(float red, bool hardEdged) {
      return new LavaSettings(PluginConfig.LavaAlpha.Value, red, 0f, 0f, hardEdged);
    }

    // Alpha only, so the ground keeps its texture and just glows.
    public static LavaSettings Embers(bool hardEdged) {
      return new LavaSettings(PluginConfig.EmbersAlpha.Value, null, null, null, hardEdged);
    }

    public static LavaSettings Custom(bool hardEdged) {
      return new LavaSettings(
          PluginConfig.CustomWriteAlpha.Value ? PluginConfig.CustomAlpha.Value : null,
          PluginConfig.CustomWriteRed.Value ? PluginConfig.CustomRed.Value : null,
          PluginConfig.CustomWriteGreen.Value ? PluginConfig.CustomGreen.Value : null,
          PluginConfig.CustomWriteBlue.Value ? PluginConfig.CustomBlue.Value : null,
          hardEdged);
    }

    // Snow depth is the green channel only.
    public static LavaSettings SnowDepth(float depth) {
      return new LavaSettings(null, null, depth, null, PluginConfig.SnowHardEdged.Value);
    }

    public static LavaSettings SnowDelta(float delta) {
      return new LavaSettings(null, null, delta, null, PluginConfig.SnowHardEdged.Value, additive: true);
    }

    public static LavaSettings CommandDefaults() {
      return new LavaSettings(
          PluginConfig.LavaAlpha.Value,
          PluginConfig.CommandStyle.Value == LavaStyle.Orange ? 1f : 0f,
          0f,
          0f,
          PluginConfig.CommandHardEdged.Value);
    }
  }

  sealed class Snapshot {
    public Vector3 Position;
    public Color[] PaintMask;
    public bool[] ModifiedPaint;
  }

  delegate Color Modifier(Heightmap heightmap, int index, Color current, float strength);

  const int MaxUndoSteps = 32;
  const float RectEpsilon = 0.01f;

  static readonly List<List<Snapshot>> _undoStack = [];
  static readonly List<int> _indices = [];
  static readonly List<float> _strengths = [];

  public static int UndoSteps => _undoStack.Count;

  public static bool Matches(Heightmap heightmap, BiomeFilter biome) {
    switch (biome) {
      case BiomeFilter.AshLands:
        return heightmap.HaveBiome(Heightmap.Biome.AshLands);
      case BiomeFilter.DeepNorth:
        return heightmap.HaveBiome(Heightmap.Biome.DeepNorth);
      default:
        return true;
    }
  }

  // Brush path (Hoe menu, radius command). Mirrors vanilla's half-cell shift and snaps the brush center to a vertex.
  public static Result PaintCircle(
      Vector3 position, float radius, bool halfOffset, LavaSettings settings, BiomeFilter biome) {
    return Circle(position, radius, halfOffset, settings.HardEdged, MakePaintModifier(settings), false, biome);
  }

  // Alpha back to worldgen, other channels untouched.
  public static Result ResetCircle(Vector3 position, float radius, bool halfOffset, bool hardEdged, BiomeFilter biome) {
    return Circle(position, radius, halfOffset, hardEdged, MakeRestoreModifier(false), false, biome);
  }

  // All four channels back to worldgen. Fully covered vertices are unmarked, so they are vanilla paint again.
  public static Result EraseCircle(Vector3 position, float radius, bool halfOffset, bool hardEdged, BiomeFilter biome) {
    return Circle(position, radius, halfOffset, hardEdged, MakeRestoreModifier(true), true, biome);
  }

  public static Result PaintZones(Heightmap origin, int rings, LavaSettings settings, BiomeFilter biome) {
    return Zones(origin, rings, MakePaintModifier(settings), false, biome);
  }

  public static Result ResetZones(Heightmap origin, int rings, BiomeFilter biome) {
    return Zones(origin, rings, MakeRestoreModifier(false), false, biome);
  }

  public static Result EraseZones(Heightmap origin, int rings, BiomeFilter biome) {
    return Zones(origin, rings, MakeRestoreModifier(true), true, biome);
  }

  static Result Circle(
      Vector3 position,
      float radius,
      bool halfOffset,
      bool hardEdged,
      Modifier modifier,
      bool clearModified,
      BiomeFilter biome) {
    Heightmap origin = Heightmap.FindHeightmap(position);
    if (!origin) {
      return new Result();
    }

    Vector3 shifted = position;
    if (halfOffset) {
      shifted.x -= 0.5f;
      shifted.z -= 0.5f;
    }

    origin.WorldToVertexMask(shifted, out int centerX, out int centerY);
    Vector3 center = origin.VertexMaskToWorld(centerX, centerY);
    float safeRadius = Mathf.Max(radius, 0.01f);

    List<Heightmap> heightmaps = [];
    Heightmap.FindHeightmap(center, safeRadius + 1f, heightmaps);

    float StrengthAt(Vector3 point) {
      float distance = Utils.DistanceXZ(point, center);
      if (distance > safeRadius) {
        return 0f;
      }

      return hardEdged ? 1f : Mathf.Max(0f, 1f - distance / safeRadius);
    }

    return Apply(heightmaps, StrengthAt, modifier, clearModified, biome, center, safeRadius + 1f);
  }

  // Paints every paint mask vertex of the zone block. Paint vertices on a zone border exist in both zones, so the
  // surrounding ring of loaded neighbors is visited too and only the vertices inside the block are written.
  static Result Zones(Heightmap origin, int rings, Modifier modifier, bool clearModified, BiomeFilter biome) {
    float scale = origin.m_scale;
    float size = origin.m_width * scale;
    Vector3 center = origin.transform.position;

    float half = rings * size + size * 0.5f;
    float minX = center.x - half - 0.5f * scale - RectEpsilon;
    float maxX = center.x + half - 0.5f * scale + RectEpsilon;
    float minZ = center.z - half - 0.5f * scale - RectEpsilon;
    float maxZ = center.z + half - 0.5f * scale + RectEpsilon;

    List<Heightmap> heightmaps = [];
    int loaded = 0;
    int expected = (2 * rings + 1) * (2 * rings + 1);

    for (int dz = -rings - 1; dz <= rings + 1; dz++) {
      for (int dx = -rings - 1; dx <= rings + 1; dx++) {
        Heightmap heightmap = Heightmap.FindHeightmap(center + new Vector3(dx * size, 0f, dz * size));
        if (!heightmap || heightmaps.Contains(heightmap)) {
          continue;
        }

        heightmaps.Add(heightmap);
        if (Math.Abs(dx) <= rings && Math.Abs(dz) <= rings) {
          loaded++;
        }
      }
    }

    float StrengthAt(Vector3 point) {
      return point.x >= minX && point.x <= maxX && point.z >= minZ && point.z <= maxZ ? 1f : 0f;
    }

    Result result = Apply(heightmaps, StrengthAt, modifier, clearModified, biome, center, half * 1.5f + size);
    result.UnloadedZones = expected - loaded;
    return result;
  }

  static Result Apply(
      List<Heightmap> heightmaps,
      Func<Vector3, float> strengthAt,
      Modifier modifier,
      bool clearModified,
      BiomeFilter biome,
      Vector3 grassCenter,
      float grassRadius) {
    Result result = new();
    List<Snapshot> snapshots = [];

    foreach (Heightmap heightmap in heightmaps) {
      int pitch = heightmap.m_width + 1;

      _indices.Clear();
      _strengths.Clear();

      for (int y = 0; y < pitch; y++) {
        for (int x = 0; x < pitch; x++) {
          float strength = strengthAt(heightmap.VertexMaskToWorld(x, y));
          if (strength > 0f) {
            _indices.Add(y * pitch + x);
            _strengths.Add(strength);
          }
        }
      }

      if (_indices.Count == 0) {
        continue;
      }

      if (!Matches(heightmap, biome)) {
        result.SkippedZones++;
        continue;
      }

      TerrainComp terrainComp = heightmap.GetAndCreateTerrainCompiler();
      if (!terrainComp) {
        continue;
      }

      ZNetView netView = PrivateAccess.TerrainCompNetView(terrainComp);
      if (!netView || !netView.IsValid()) {
        continue;
      }

      // Pull the latest ZDO data before taking over, then become the owner so Save() persists.
      PrivateAccess.TerrainCompCheckLoad(terrainComp);
      if (!netView.IsOwner()) {
        netView.ClaimOwnership();
      }

      if (!netView.IsOwner()) {
        result.NotOwned++;
        continue;
      }

      Color[] paintMask = PrivateAccess.TerrainCompPaintMask(terrainComp);
      bool[] modifiedPaint = PrivateAccess.TerrainCompModifiedPaint(terrainComp);
      if (paintMask == null || modifiedPaint == null || paintMask.Length != pitch * pitch ||
          modifiedPaint.Length != pitch * pitch) {
        continue;
      }

      snapshots.Add(
          new Snapshot {
            Position = heightmap.transform.position,
            PaintMask = (Color[]) paintMask.Clone(),
            ModifiedPaint = (bool[]) modifiedPaint.Clone(),
          });

      for (int i = 0; i < _indices.Count; i++) {
        int index = _indices[i];
        Color current = heightmap.GetPaintMask(index % pitch, index / pitch);
        paintMask[index] = modifier(heightmap, index, current, _strengths[i]);
        modifiedPaint[index] = !(clearModified && _strengths[i] >= 1f);
      }

      result.Zones++;
      result.Vertices += _indices.Count;

      // Only lava cares about biome-edge zones: IsLava ignores them.
      if (biome == BiomeFilter.AshLands && heightmap.IsBiomeEdge()) {
        result.EdgeZones++;
      }

      PrivateAccess.TerrainCompSave(terrainComp, false);
      heightmap.Poke(1, true);
    }

    if (result.Zones > 0) {
      if (ClutterSystem.instance) {
        ClutterSystem.instance.ResetGrass(grassCenter, grassRadius);
      }

      PushUndo(snapshots);
    }

    return result;
  }

  // Restores the terrain data of the zones touched by the latest paint, reset or erase.
  public static int Undo() {
    if (_undoStack.Count == 0) {
      return -1;
    }

    List<Snapshot> snapshots = _undoStack[_undoStack.Count - 1];
    _undoStack.RemoveAt(_undoStack.Count - 1);

    int restored = 0;
    foreach (Snapshot snapshot in snapshots) {
      Heightmap heightmap = Heightmap.FindHeightmap(snapshot.Position);
      if (!heightmap) {
        continue;
      }

      TerrainComp terrainComp = TerrainComp.FindTerrainCompiler(heightmap.transform.position);
      if (!terrainComp) {
        continue;
      }

      ZNetView netView = PrivateAccess.TerrainCompNetView(terrainComp);
      if (!netView || !netView.IsValid()) {
        continue;
      }

      PrivateAccess.TerrainCompCheckLoad(terrainComp);
      if (!netView.IsOwner()) {
        netView.ClaimOwnership();
      }

      Color[] paintMask = PrivateAccess.TerrainCompPaintMask(terrainComp);
      bool[] modifiedPaint = PrivateAccess.TerrainCompModifiedPaint(terrainComp);
      if (!netView.IsOwner() || paintMask == null || modifiedPaint == null ||
          paintMask.Length != snapshot.PaintMask.Length || modifiedPaint.Length != snapshot.ModifiedPaint.Length) {
        continue;
      }

      Array.Copy(snapshot.PaintMask, paintMask, paintMask.Length);
      Array.Copy(snapshot.ModifiedPaint, modifiedPaint, modifiedPaint.Length);

      PrivateAccess.TerrainCompSave(terrainComp, false);
      heightmap.Poke(1, true);

      if (ClutterSystem.instance) {
        ClutterSystem.instance.ResetGrass(
            heightmap.transform.position, heightmap.m_width * heightmap.m_scale * 0.75f);
      }

      restored++;
    }

    return restored;
  }

  static void PushUndo(List<Snapshot> snapshots) {
    if (snapshots.Count == 0) {
      return;
    }

    _undoStack.Add(snapshots);
    if (_undoStack.Count > MaxUndoSteps) {
      _undoStack.RemoveAt(0);
    }
  }

  static float Channel(float current, float? target, float strength, bool additive) {
    if (!target.HasValue) {
      return current;
    }

    return additive
        ? Mathf.Clamp01(current + target.Value * strength)
        : Mathf.Lerp(current, target.Value, strength);
  }

  static Modifier MakePaintModifier(LavaSettings settings) {
    return (_, _, current, strength) => {
      current.r = Channel(current.r, settings.Red, strength, settings.Additive);
      current.g = Channel(current.g, settings.Green, strength, settings.Additive);
      current.b = Channel(current.b, settings.Blue, strength, settings.Additive);
      current.a = Channel(current.a, settings.Alpha, strength, settings.Additive);
      return current;
    };
  }

  // Moves the paint toward the worldgen value: alpha only, or all channels.
  static Modifier MakeRestoreModifier(bool allChannels) {
    Dictionary<Heightmap, Color[]> baseMasks = [];

    return (heightmap, index, current, strength) => {
      if (!baseMasks.TryGetValue(heightmap, out Color[] baseMask)) {
        baseMask =
            HeightmapBuilder.instance.RequestTerrainSync(
                heightmap.transform.position,
                heightmap.m_width,
                heightmap.m_scale,
                heightmap.IsDistantLod,
                WorldGenerator.instance).m_baseMask;
        baseMasks[heightmap] = baseMask;
      }

      Color original = baseMask[index];
      if (allChannels) {
        return Color.Lerp(current, original, strength);
      }

      current.a = Mathf.Lerp(current.a, original.a, strength);
      return current;
    };
  }
}
