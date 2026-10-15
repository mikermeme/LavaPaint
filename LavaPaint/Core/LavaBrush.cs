namespace LavaPaint;

using System;

using UnityEngine;

// Marks a cloned Hoe piece as one of this mod's brushes. Only the id is kept on the component, since that is what
// survives Object.Instantiate when the piece is placed.
public sealed class LavaBrush : MonoBehaviour {
  public string Id;

  // A piece can hold more than one TerrainOp, the stroke must only be applied once.
  public bool Handled;
}

enum BrushKind {
  Lava,
  Embers,
  Eraser,
  Custom,
  Snow,
}

sealed class BrushDefinition {
  public string Id { get; }
  public string Name { get; }
  public string Description { get; }
  public BrushKind Kind { get; }
  public BiomeFilter Biome { get; }

  // Resolved for every stroke, so config changes apply right away. Null for the eraser.
  public Func<LavaPainter.LavaSettings> Settings { get; }

  // Only used for the icon: snow depth shade (0-1) and a '+' or '-' symbol.
  public float IconLevel { get; }
  public char IconSymbol { get; }

  Sprite _icon;
  string _iconKey;

  public BrushDefinition(
      string id,
      string name,
      string description,
      BrushKind kind,
      BiomeFilter biome,
      Func<LavaPainter.LavaSettings> settings,
      float iconLevel = 0f,
      char iconSymbol = ' ') {
    Id = id;
    Name = name;
    Description = description;
    Kind = kind;
    Biome = biome;
    Settings = settings;
    IconLevel = iconLevel;
    IconSymbol = iconSymbol;
  }

  // The custom brush icon shows the configured color, so it is rebuilt when that changes.
  public Sprite GetIcon() {
    string key = "";
    if (Kind == BrushKind.Custom && Settings != null) {
      LavaPainter.LavaSettings s = Settings();
      key = $"{(s.Red ?? 0.5f):F2},{(s.Green ?? 0.5f):F2},{(s.Blue ?? 0.5f):F2},{s.HardEdged}";
    }

    if (!_icon || key != _iconKey) {
      _icon = LavaIcons.Create(this);
      _iconKey = key;
    }

    return _icon;
  }

  public LavaPainter.Result Apply(Vector3 position, float radius, bool halfOffset) {
    if (Kind == BrushKind.Eraser) {
      return LavaPainter.EraseCircle(position, radius, halfOffset, true, Biome);
    }

    return LavaPainter.PaintCircle(position, radius, halfOffset, Settings(), Biome);
  }
}
