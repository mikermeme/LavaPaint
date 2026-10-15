namespace LavaPaint;

using UnityEngine;

// Small generated icons for the Hoe menu entries, so no assets are needed.
static class LavaIcons {
  const int Size = 64;

  static readonly Color RedLava = new(0.80f, 0.08f, 0.04f);
  static readonly Color OrangeLava = new(1f, 0.55f, 0.05f);
  static readonly Color Hot = new(1f, 0.90f, 0.40f);
  static readonly Color Ground = new(0.20f, 0.15f, 0.14f);
  static readonly Color Ember = new(1f, 0.50f, 0.10f);
  static readonly Color Chalk = new(0.86f, 0.86f, 0.88f);
  static readonly Color Cross = new(0.55f, 0.10f, 0.10f);
  static readonly Color ShallowSnow = new(0.50f, 0.60f, 0.74f);
  static readonly Color DeepSnow = new(0.97f, 0.98f, 1f);
  static readonly Color SnowSymbol = new(0.15f, 0.25f, 0.45f);

  public static Sprite Create(BrushDefinition definition) {
    LavaPainter.LavaSettings settings = definition.Settings != null ? definition.Settings() : default;
    bool hard = definition.Kind == BrushKind.Eraser || settings.HardEdged;

    Texture2D texture = new(Size, Size, TextureFormat.RGBA32, false) {
      filterMode = FilterMode.Bilinear,
      wrapMode = TextureWrapMode.Clamp,
    };

    Color[] pixels = new Color[Size * Size];
    float center = (Size - 1) * 0.5f;
    float radius = Size * 0.45f;

    for (int y = 0; y < Size; y++) {
      for (int x = 0; x < Size; x++) {
        float dx = x - center;
        float dy = y - center;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);
        float edge = hard ? Mathf.Clamp01(radius - distance + 0.5f) : Mathf.Clamp01(1f - distance / radius);
        float centerBias = Mathf.Clamp01(1f - distance / radius);

        Color color = definition.Kind switch {
          BrushKind.Eraser => EraserColor(dx, dy, distance, radius),
          BrushKind.Embers => EmbersColor(x, y, centerBias),
          BrushKind.Custom => CustomColor(settings, centerBias),
          BrushKind.Snow => SnowColor(definition, dx, dy, distance, radius),
          _ => Color.Lerp((settings.Red ?? 0f) < 0.5f ? RedLava : OrangeLava, Hot, 0.35f * centerBias),
        };

        color.a = edge;
        pixels[y * Size + x] = color;
      }
    }

    texture.SetPixels(pixels);
    texture.Apply();

    return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
  }

  static Color EmbersColor(int x, int y, float centerBias) {
    Color color = Color.Lerp(Ground, Ember, 0.3f * centerBias);
    if ((x * 7 + y * 13) % 11 == 0 && centerBias > 0.2f) {
      color = Ember;
    }

    return color;
  }

  static Color EraserColor(float dx, float dy, float distance, float radius) {
    bool onCross = distance < radius * 0.8f && (Mathf.Abs(dx - dy) < 2.5f || Mathf.Abs(dx + dy) < 2.5f);
    return onCross ? Cross : Chalk;
  }

  // The configured color. A channel that is not written is shown as mid gray.
  static Color CustomColor(LavaPainter.LavaSettings settings, float centerBias) {
    Color color = new(settings.Red ?? 0.5f, settings.Green ?? 0.5f, settings.Blue ?? 0.5f);
    return Color.Lerp(color, Color.white, 0.12f * centerBias);
  }

  static Color SnowColor(BrushDefinition definition, float dx, float dy, float distance, float radius) {
    float arm = radius * 0.55f;
    bool onSymbol =
        (definition.IconSymbol == '+' && ((Mathf.Abs(dx) < 2.5f && Mathf.Abs(dy) < arm) ||
                                          (Mathf.Abs(dy) < 2.5f && Mathf.Abs(dx) < arm))) ||
        (definition.IconSymbol == '-' && Mathf.Abs(dy) < 2.5f && Mathf.Abs(dx) < arm);

    return onSymbol ? SnowSymbol : Color.Lerp(ShallowSnow, DeepSnow, definition.IconLevel);
  }
}
