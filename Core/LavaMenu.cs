namespace LavaPaint;

using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

using static PluginConfig;

// Adds the brushes (lava, embers, custom paint, snow, eraser) to the Hoe build menu.
//
// Each brush is a clone of a vanilla Hoe paint piece, kept in an inactive container. The clone keeps the vanilla ghost
// (brush circle), stamina use and swing animation. It has no ZNetView, so nothing is registered on the network, and
// when it is placed TerrainOpPatch swaps the vanilla terrain op for the brush stroke.
//
// BuildUi lists PieceTable.m_availablePieces, and PieceTable.GetPieceIndex searches the private per-category lists,
// so the pieces are added to both after every UpdateAvailable.
static class LavaMenu {
  const string DeepNorthOnly = " Deep North only.";
  const string AshlandsOnly = " Ashlands only.";

  static readonly BrushDefinition[] _definitions = [
      new("lavapaint_red_soft", "Lava (Red, Soft)",
          "Paints red Ashlands lava. The edge fades out over the brush radius." + AshlandsOnly,
          BrushKind.Lava, BiomeFilter.AshLands, () => LavaPainter.LavaSettings.Lava(0f, false)),
      new("lavapaint_red_hard", "Lava (Red, Hard)",
          "Paints red Ashlands lava. Everything inside the brush radius becomes lava." + AshlandsOnly,
          BrushKind.Lava, BiomeFilter.AshLands, () => LavaPainter.LavaSettings.Lava(0f, true)),
      new("lavapaint_orange_soft", "Lava (Orange, Soft)",
          "Paints orange Ashlands lava. The edge fades out over the brush radius." + AshlandsOnly,
          BrushKind.Lava, BiomeFilter.AshLands, () => LavaPainter.LavaSettings.Lava(1f, false)),
      new("lavapaint_orange_hard", "Lava (Orange, Hard)",
          "Paints orange Ashlands lava. Everything inside the brush radius becomes lava." + AshlandsOnly,
          BrushKind.Lava, BiomeFilter.AshLands, () => LavaPainter.LavaSettings.Lava(1f, true)),

      new("lavapaint_embers_soft", "Embers (Soft)",
          "Heats the ground without making it lava, and keeps the ground texture. The edge fades out." +
          AshlandsOnly,
          BrushKind.Embers, BiomeFilter.AshLands, () => LavaPainter.LavaSettings.Embers(false)),
      new("lavapaint_embers_hard", "Embers (Hard)",
          "Heats the ground without making it lava, and keeps the ground texture." + AshlandsOnly,
          BrushKind.Embers, BiomeFilter.AshLands, () => LavaPainter.LavaSettings.Embers(true)),

      new("lavapaint_custom_soft", "Custom Paint (Soft)",
          "Paints the RGBA color set in the config (Custom section), in any biome. The edge fades out.",
          BrushKind.Custom, BiomeFilter.Any, () => LavaPainter.LavaSettings.Custom(false)),
      new("lavapaint_custom_hard", "Custom Paint (Hard)",
          "Paints the RGBA color set in the config (Custom section), in any biome.",
          BrushKind.Custom, BiomeFilter.Any, () => LavaPainter.LavaSettings.Custom(true)),

      new("lavapaint_snow_clear", "Snow: Clear",
          "Sets the snow depth to 0: shallow snow where most pieces can be built." + DeepNorthOnly,
          BrushKind.Snow, BiomeFilter.DeepNorth, () => LavaPainter.LavaSettings.SnowDepth(0f), iconLevel: 0f),
      new("lavapaint_snow_deep", "Snow: Deep",
          "Sets the snow depth to the configured value (0.4): deep snow. Most pieces cannot be built in snow deeper " +
          "than 0.15." + DeepNorthOnly,
          BrushKind.Snow, BiomeFilter.DeepNorth, () => LavaPainter.LavaSettings.SnowDepth(SnowDeepDepth.Value),
          iconLevel: 0.6f),
      new("lavapaint_snow_verydeep", "Snow: Very Deep",
          "Sets the snow depth to the configured value (1.0): very deep snow, the deepest ground height." +
          DeepNorthOnly,
          BrushKind.Snow, BiomeFilter.DeepNorth, () => LavaPainter.LavaSettings.SnowDepth(SnowVeryDeepDepth.Value),
          iconLevel: 1f),
      new("lavapaint_snow_add", "Snow Drift (+)",
          "Piles up snow: every stroke adds to the snow depth (0.25 by default), so you can build drifts up " +
          "gradually." + DeepNorthOnly,
          BrushKind.Snow, BiomeFilter.DeepNorth, () => LavaPainter.LavaSettings.SnowDelta(SnowStep.Value),
          iconLevel: 0.85f, iconSymbol: '+'),
      new("lavapaint_snow_remove", "Snow Shovel (-)",
          "Shovels snow: every stroke removes from the snow depth (0.25 by default)." + DeepNorthOnly,
          BrushKind.Snow, BiomeFilter.DeepNorth, () => LavaPainter.LavaSettings.SnowDelta(-SnowStep.Value),
          iconLevel: 0.3f, iconSymbol: '-'),

      new("lavapaint_eraser", "Paint Eraser",
          "Puts the terrain paint (ground texture, lava and snow depth) back to the original worldgen, in any " +
          "biome. Height is not changed.",
          BrushKind.Eraser, BiomeFilter.Any, null),
  ];

  static readonly Dictionary<string, BrushDefinition> _byId = [];

  static GameObject _container;
  static PieceTable _hoeTable;
  static List<Piece> _pieces;
  static bool _failed;

  static LavaMenu() {
    foreach (BrushDefinition definition in _definitions) {
      _byId[definition.Id] = definition;
    }
  }

  public static bool TryGetDefinition(string id, out BrushDefinition definition) {
    return _byId.TryGetValue(id ?? "", out definition);
  }

  public static void Inject(PieceTable table) {
    if (!IsModEnabled.Value || !ShowInHoeMenu.Value || !table || !IsHoeTable(table)) {
      return;
    }

    List<Piece> pieces = GetPieces(table);
    List<List<Piece>> byCategory = PrivateAccess.PieceTableAvailableByCategory(table);
    if (pieces == null || byCategory == null || byCategory.Count == 0) {
      return;
    }

    foreach (Piece piece in pieces) {
      if (!piece) {
        continue;
      }

      // The custom brush icon follows the configured color.
      LavaBrush brush = piece.GetComponent<LavaBrush>();
      if (brush && TryGetDefinition(brush.Id, out BrushDefinition definition) &&
          definition.Kind == BrushKind.Custom) {
        piece.m_icon = definition.GetIcon();
      }

      if (piece.m_category == Piece.PieceCategory.All) {
        foreach (List<Piece> list in byCategory) {
          list.Add(piece);
        }
      } else {
        int category = (int) piece.m_category;
        byCategory[category >= 0 && category < byCategory.Count ? category : 0].Add(piece);
      }

      table.m_availablePieces.Add(piece);
      table.m_enabledPieces.Add(piece);
    }
  }

  static bool IsHoeTable(PieceTable table) {
    if (!_hoeTable) {
      _hoeTable = FindHoeTable();
    }

    if (_hoeTable) {
      return table == _hoeTable;
    }

    // The Hoe item was not found, so fall back to any table that has terrain op pieces.
    return table.m_pieces.Exists(prefab => prefab && prefab.GetComponentInChildren<TerrainOp>(true));
  }

  static PieceTable FindHoeTable() {
    ObjectDB objectDB = ObjectDB.instance;
    if (!objectDB) {
      return null;
    }

    GameObject hoe = objectDB.GetItemPrefab("Hoe");
    ItemDrop itemDrop = hoe ? hoe.GetComponent<ItemDrop>() : null;
    return itemDrop ? itemDrop.m_itemData.m_shared.m_buildPieces : null;
  }

  static List<Piece> GetPieces(PieceTable table) {
    if (_pieces != null && _pieces.Count > 0 && _pieces.TrueForAll(piece => piece)) {
      return _pieces;
    }

    if (_failed) {
      return null;
    }

    GameObject template = FindTemplate(table);
    if (!template) {
      _failed = true;
      LavaPaint.LogError("No Hoe paint piece to clone, the menu entries are not available.");
      return null;
    }

    if (!_container) {
      _container = new GameObject("LavaPaint_Templates");
      _container.SetActive(false);
      UnityEngine.Object.DontDestroyOnLoad(_container);
    }

    List<Piece> pieces = [];
    foreach (BrushDefinition definition in _definitions) {
      Piece piece = CreatePiece(template, definition);
      if (piece) {
        pieces.Add(piece);
      }
    }

    _pieces = pieces;
    return pieces;
  }

  // Prefers a pure paint piece that has no ground requirement and spawns nothing.
  static GameObject FindTemplate(PieceTable table) {
    bool log = LogHoeTable.Value;
    StringBuilder builder = log ? new StringBuilder("Hoe build table pieces:") : null;

    GameObject best = null;
    int bestScore = -1;

    foreach (GameObject prefab in table.m_pieces) {
      if (!prefab) {
        continue;
      }

      Piece piece = prefab.GetComponent<Piece>();
      TerrainOp op = prefab.GetComponentInChildren<TerrainOp>(true);

      if (log) {
        builder.Append($"\n  {prefab.name}: piece={(piece ? piece.m_name : "-")}");
        if (op) {
          TerrainOp.Settings s = op.m_settings;
          builder.Append(
              $", terrainOp(paint={s.m_paintCleared}/{s.m_paintType} r{s.m_paintRadius}, level={s.m_level}, " +
              $"raise={s.m_raise}, smooth={s.m_smooth}, spawn={(op.m_spawnOnPlaced ? op.m_spawnOnPlaced.name : "-")})");
        }

        builder.Append($", znetview={prefab.GetComponentInChildren<ZNetView>(true) != null}");
        builder.Append($", terrainModifier={prefab.GetComponentInChildren<TerrainModifier>(true) != null}");
        if (piece) {
          builder.Append(
              $", cultivatedOnly={piece.m_cultivatedGroundOnly}, vegetationOnly={piece.m_vegetationGroundOnly}, " +
              $"category={piece.m_category}");
        }
      }

      if (!piece || !op || piece.m_repairPiece || piece.m_removePiece || !op.m_settings.m_paintCleared) {
        continue;
      }

      TerrainOp.Settings settings = op.m_settings;
      int score = 0;
      score += !settings.m_level && !settings.m_raise && !settings.m_smooth ? 4 : 0;
      score += !piece.m_cultivatedGroundOnly ? 2 : 0;
      score += !piece.m_vegetationGroundOnly ? 1 : 0;
      score += !op.m_spawnOnPlaced ? 1 : 0;

      if (score > bestScore) {
        bestScore = score;
        best = prefab;
      }
    }

    if (log) {
      builder.Append($"\nCloning: {(best ? best.name : "none")}");
      LavaPaint.LogInfo(builder.ToString());
    }

    return best;
  }

  static Piece CreatePiece(GameObject template, BrushDefinition definition) {
    // The container is inactive, so the clone does not Awake and nothing registers until it is placed.
    GameObject clone = UnityEngine.Object.Instantiate(template, _container.transform);
    clone.name = definition.Id;

    foreach (ZNetView netView in clone.GetComponentsInChildren<ZNetView>(true)) {
      UnityEngine.Object.DestroyImmediate(netView);
    }

    foreach (TerrainModifier modifier in clone.GetComponentsInChildren<TerrainModifier>(true)) {
      UnityEngine.Object.DestroyImmediate(modifier);
    }

    Piece piece = clone.GetComponent<Piece>();
    if (!piece) {
      UnityEngine.Object.Destroy(clone);
      return null;
    }

    clone.AddComponent<LavaBrush>().Id = definition.Id;

    piece.m_name = definition.Name;
    piece.m_description = definition.Description;
    piece.m_icon = definition.GetIcon();
    piece.m_enabled = true;
    piece.m_resources = Array.Empty<Piece.Requirement>();
    piece.m_craftingStation = null;
    piece.m_dlc = "";
    piece.m_repairPiece = false;
    piece.m_removePiece = false;
    piece.m_cultivatedGroundOnly = false;
    piece.m_vegetationGroundOnly = false;
    piece.m_requireDeepSnow = false;
    piece.m_noClipping = false;
    piece.m_onlyInBiome = Heightmap.Biome.None;

    return piece;
  }
}
