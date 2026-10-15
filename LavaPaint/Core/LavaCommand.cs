namespace LavaPaint;

using System.Globalization;

using UnityEngine;

static class LavaCommand {
  const int MaxRings = 5;
  const float MaxRadius = 128f;

  static bool _registered;

  public static void Register() {
    if (_registered) {
      return;
    }

    _registered = true;
    new Terminal.ConsoleCommand(
        "lava",
        "Paints terrain paint (lava, snow, any color). Run 'lava' for usage.",
        Execute);
  }

  static void Execute(Terminal.ConsoleEventArgs args) {
    Terminal terminal = args.Context;

    if (!PluginConfig.IsModEnabled.Value) {
      terminal.AddString("LavaPaint is disabled in its config.");
      return;
    }

    if (args.Length < 2) {
      PrintUsage(terminal);
      return;
    }

    bool undo = false;
    bool reset = false;
    bool erase = false;
    bool only = false;
    bool additive = false;
    bool zone = false;
    int rings = 0;
    float radius = 0f;
    float? alpha = null;
    float? red = null;
    float? green = null;
    float? blue = null;
    bool? hardEdged = null;
    BiomeFilter? biome = null;

    for (int i = 1; i < args.Length; i++) {
      string key = args[i].ToLowerInvariant();
      string value = null;

      int equals = key.IndexOf('=');
      if (equals >= 0) {
        value = key.Substring(equals + 1);
        key = key.Substring(0, equals);
      }

      switch (key) {
        case "undo":
          undo = true;
          break;
        case "reset":
          reset = true;
          break;
        case "erase":
          erase = true;
          break;
        case "only":
          only = true;
          break;
        case "additive":
          additive = true;
          break;
        case "soft":
          hardEdged = false;
          break;
        case "hard":
          hardEdged = true;
          break;
        case "zone":
          zone = true;
          if (value != null && (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rings) ||
                                rings < 0 || rings > MaxRings)) {
            terminal.AddString($"lava: zone rings must be 0-{MaxRings}.");
            return;
          }

          break;
        case "radius":
          if (!TryParseFloat(value, out radius) || radius <= 0f || radius > MaxRadius) {
            terminal.AddString($"lava: radius must be a number up to {MaxRadius}.");
            return;
          }

          break;
        case "alpha":
          if (!TryParseChannel(value, out float alphaValue)) {
            terminal.AddString("lava: alpha must be 0-1 (-1 to 1 with additive).");
            return;
          }

          alpha = alphaValue;
          break;
        case "red":
          if (!TryParseChannel(value, out float redValue)) {
            terminal.AddString("lava: red must be 0-1 (-1 to 1 with additive).");
            return;
          }

          red = redValue;
          break;
        case "green":
          if (!TryParseChannel(value, out float greenValue)) {
            terminal.AddString("lava: green must be 0-1 (-1 to 1 with additive).");
            return;
          }

          green = greenValue;
          break;
        case "blue":
          if (!TryParseChannel(value, out float blueValue)) {
            terminal.AddString("lava: blue must be 0-1 (-1 to 1 with additive).");
            return;
          }

          blue = blueValue;
          break;
        case "style":
          if (value == "red") {
            red = 0f;
          } else if (value == "orange") {
            red = 1f;
          } else {
            terminal.AddString("lava: style must be red or orange.");
            return;
          }

          break;
        case "biome":
          if (value == "any") {
            biome = BiomeFilter.Any;
          } else if (value == "ashlands") {
            biome = BiomeFilter.AshLands;
          } else if (value == "deepnorth") {
            biome = BiomeFilter.DeepNorth;
          } else {
            terminal.AddString("lava: biome must be any, ashlands or deepnorth.");
            return;
          }

          break;
        default:
          terminal.AddString($"lava: unknown option '{args[i]}'.");
          PrintUsage(terminal);
          return;
      }
    }

    if (undo) {
      int restored = LavaPainter.Undo();
      terminal.AddString(
          restored < 0 ? "lava: nothing to undo." : $"lava: undid the last change ({restored} zone(s) restored).");
      return;
    }

    if (!zone && radius <= 0f) {
      PrintUsage(terminal);
      return;
    }

    if (!additive && (alpha < 0f || red < 0f || green < 0f || blue < 0f)) {
      terminal.AddString("lava: negative channel values need the additive option.");
      return;
    }

    Player player = Player.m_localPlayer;
    if (!player) {
      terminal.AddString("lava: no player in the world.");
      return;
    }

    // Painting stays in the Ashlands unless told otherwise, restoring paint works anywhere.
    BiomeFilter filter = biome ?? (reset || erase ? BiomeFilter.Any : BiomeFilter.AshLands);

    bool hard = hardEdged ?? PluginConfig.CommandHardEdged.Value;
    LavaPainter.LavaSettings settings;

    if (only) {
      // Only the channels that were given, the rest of the paint stays as it is.
      settings = new LavaPainter.LavaSettings(alpha, red, green, blue, hard, additive);
    } else {
      LavaPainter.LavaSettings defaults = LavaPainter.LavaSettings.CommandDefaults();
      settings =
          new LavaPainter.LavaSettings(
              alpha ?? defaults.Alpha,
              red ?? defaults.Red,
              green ?? defaults.Green,
              blue ?? defaults.Blue,
              hard,
              additive);
    }

    Vector3 position = player.transform.position;
    LavaPainter.Result result;
    string verb;

    if (zone) {
      Heightmap origin = Heightmap.FindHeightmap(position);
      if (!origin) {
        terminal.AddString("lava: no terrain loaded at your position.");
        return;
      }

      if (erase) {
        result = LavaPainter.EraseZones(origin, rings, filter);
        verb = "erased";
      } else if (reset) {
        result = LavaPainter.ResetZones(origin, rings, filter);
        verb = "reset";
      } else {
        result = LavaPainter.PaintZones(origin, rings, settings, filter);
        verb = "painted";
      }
    } else {
      if (erase) {
        result = LavaPainter.EraseCircle(position, radius, false, hard, filter);
        verb = "erased";
      } else if (reset) {
        result = LavaPainter.ResetCircle(position, radius, false, hard, filter);
        verb = "reset";
      } else {
        result = LavaPainter.PaintCircle(position, radius, false, settings, filter);
        verb = "painted";
      }
    }

    Report(terminal, verb, result, filter);
  }

  static void Report(Terminal terminal, string verb, LavaPainter.Result result, BiomeFilter filter) {
    if (result.Zones == 0) {
      terminal.AddString(
          result.SkippedZones > 0
              ? $"lava: nothing changed, the biome filter is {Notice.Name(filter)} (use biome=any to paint anywhere)."
              : "lava: nothing changed.");
    } else {
      terminal.AddString($"lava: {verb} {result.Vertices} vertices in {result.Zones} zone(s). Use 'lava undo' to revert.");
    }

    if (result.EdgeZones > 0) {
      terminal.AddString(
          $"<color=orange>lava: warning, {result.EdgeZones} zone(s) are biome-edge zones. The game does not count " +
          "lava there as lava (no lava damage or footstep), it may only look like it.</color>");
    }

    if (result.SkippedZones > 0 && result.Zones > 0) {
      terminal.AddString($"lava: skipped {result.SkippedZones} zone(s) outside the {Notice.Name(filter)}.");
    }

    if (result.UnloadedZones > 0) {
      terminal.AddString($"lava: {result.UnloadedZones} zone(s) in range were not loaded and were not painted.");
    }

    if (result.NotOwned > 0) {
      terminal.AddString(
          $"<color=orange>lava: could not take ownership of {result.NotOwned} zone(s), they were not changed.</color>");
    }
  }

  // Channels are -1 to 1 here. Negative values are only valid as deltas, which is checked once all options are read.
  static bool TryParseChannel(string value, out float result) {
    return TryParseFloat(value, out result) && result <= 1f && result >= -1f;
  }

  static bool TryParseFloat(string value, out float result) {
    result = 0f;
    return value != null &&
        float.TryParse(value.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
  }

  static void PrintUsage(Terminal terminal) {
    terminal.AddString("lava zone[=<rings>] | radius=<m> - paint your zone (+ rings of neighbors) or a circle around you");
    terminal.AddString("   channels: alpha=<0-1> red=<0-1> green=<0-1> blue=<0-1> or style=red|orange");
    terminal.AddString("   default: lava (red from style, green=0, blue=0, alpha from config), Ashlands only");
    terminal.AddString("   only: write just the channels given (e.g. 'green=0.4 only biome=deepnorth' sets snow depth)");
    terminal.AddString("   additive: channel values are deltas, -1 to 1 (e.g. 'green=0.25 only additive biome=deepnorth')");
    terminal.AddString("   biome=any|ashlands|deepnorth - where it may paint (default ashlands), soft|hard - edge");
    terminal.AddString("lava reset zone[=<rings>] | radius=<m> - alpha back to worldgen, other channels stay");
    terminal.AddString("lava erase zone[=<rings>] | radius=<m> - all paint back to worldgen (any biome)");
    terminal.AddString("lava undo - revert the last paint, reset or erase");
    terminal.AddString("The Hoe menu has the lava, embers, custom, snow and eraser brushes.");
  }
}
