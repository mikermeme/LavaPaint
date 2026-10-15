namespace LavaPaint;

using BepInEx.Configuration;

using ComfyLib;

public enum LavaStyle {
  // Paint mask 0,0,0,a
  Red,
  // Paint mask 1,0,0,a
  Orange,
}

public static class PluginConfig {
  public static ConfigEntry<bool> IsModEnabled { get; private set; }
  public static ConfigEntry<bool> ShowInHoeMenu { get; private set; }
  public static ConfigEntry<float> LavaAlpha { get; private set; }
  public static ConfigEntry<float> EmbersAlpha { get; private set; }
  public static ConfigEntry<float> CustomRed { get; private set; }
  public static ConfigEntry<float> CustomGreen { get; private set; }
  public static ConfigEntry<float> CustomBlue { get; private set; }
  public static ConfigEntry<float> CustomAlpha { get; private set; }
  public static ConfigEntry<bool> CustomWriteRed { get; private set; }
  public static ConfigEntry<bool> CustomWriteGreen { get; private set; }
  public static ConfigEntry<bool> CustomWriteBlue { get; private set; }
  public static ConfigEntry<bool> CustomWriteAlpha { get; private set; }
  public static ConfigEntry<float> SnowDeepDepth { get; private set; }
  public static ConfigEntry<float> SnowVeryDeepDepth { get; private set; }
  public static ConfigEntry<float> SnowStep { get; private set; }
  public static ConfigEntry<bool> SnowHardEdged { get; private set; }
  public static ConfigEntry<LavaStyle> CommandStyle { get; private set; }
  public static ConfigEntry<bool> CommandHardEdged { get; private set; }
  public static ConfigEntry<bool> LogHoeTable { get; private set; }
  public static ConfigEntry<bool> LogIntercepted { get; private set; }

  public static void BindConfig(ConfigFile config) {
    AcceptableValueRange<float> unit = new(0f, 1f);

    IsModEnabled =
        config.BindInOrder(
            "_Global",
            "isModEnabled",
            true,
            "Globally enable or disable this mod.");

    ShowInHoeMenu =
        config.BindInOrder(
            "Hoe Menu",
            "showInHoeMenu",
            true,
            "Add the brush entries to the Hoe build menu. Re-equip the Hoe to apply a change.");

    LavaAlpha =
        config.BindInOrder(
            "Lava",
            "alpha",
            1f,
            "Paint mask alpha of the lava entries and the default of the lava command. " +
            "The game treats anything above 0.6 as lava. Ashlands only.",
            unit);

    EmbersAlpha =
        config.BindInOrder(
            "Embers",
            "alpha",
            0.24f,
            "Paint mask alpha of the embers entries. Below 0.6 the ground glows but is not lava " +
            "(no damage, no lava footsteps). The ground texture is left as it is. Ashlands only.",
            unit);

    CustomRed =
        config.BindInOrder(
            "Custom",
            "red",
            1f,
            "Red channel of the custom brush. In the Ashlands the red, green and blue channels pick the ground " +
            "texture (and the lava look), in other biomes they mark dirt, cultivated and paved ground.",
            unit);

    CustomGreen =
        config.BindInOrder(
            "Custom",
            "green",
            0f,
            "Green channel of the custom brush. In the Deep North this is the snow depth.",
            unit);

    CustomBlue =
        config.BindInOrder(
            "Custom",
            "blue",
            0f,
            "Blue channel of the custom brush.",
            unit);

    CustomAlpha =
        config.BindInOrder(
            "Custom",
            "alpha",
            1f,
            "Alpha channel of the custom brush. In the Ashlands above 0.6 is lava, elsewhere it is the vegetation " +
            "mask (0 clears grass and bushes).",
            unit);

    CustomWriteRed =
        config.BindInOrder("Custom", "writeRed", true, "Uncheck to leave the red channel as it is on the terrain.");

    CustomWriteGreen =
        config.BindInOrder("Custom", "writeGreen", true, "Uncheck to leave the green channel as it is on the terrain.");

    CustomWriteBlue =
        config.BindInOrder("Custom", "writeBlue", true, "Uncheck to leave the blue channel as it is on the terrain.");

    CustomWriteAlpha =
        config.BindInOrder("Custom", "writeAlpha", true, "Uncheck to leave the alpha channel as it is on the terrain.");

    SnowDeepDepth =
        config.BindInOrder(
            "Snow",
            "deepDepth",
            0.4f,
            "Snow depth (green channel) of the Snow: Deep entry. Above 0.12 the ground counts as deep snow, and most " +
            "pieces cannot be built above 0.15. Deep North only.",
            unit);

    SnowVeryDeepDepth =
        config.BindInOrder(
            "Snow",
            "veryDeepDepth",
            1f,
            "Snow depth of the Snow: Very Deep entry. Above 0.6 the ground counts as very deep snow. Deep North only.",
            unit);

    SnowStep =
        config.BindInOrder(
            "Snow",
            "step",
            0.25f,
            "Snow depth added or removed by one stroke of the Snow Drift and Snow Shovel entries.",
            new AcceptableValueRange<float>(0.01f, 1f));

    SnowHardEdged =
        config.BindInOrder(
            "Snow",
            "hardEdged",
            false,
            "Hard edge paints every vertex in the radius at full strength. Unchecked gives a linear falloff, which " +
            "looks better for snow because the depth is blended across the vertices.");

    CommandStyle =
        config.BindInOrder(
            "Command",
            "style",
            LavaStyle.Red,
            "Default style of the lava command. Red paints 0,0,0,alpha. Orange paints 1,0,0,alpha.");

    CommandHardEdged =
        config.BindInOrder(
            "Command",
            "hardEdged",
            true,
            "Default edge of the lava command. Hard paints every vertex in range at full strength, " +
            "unchecked gives a linear falloff. The Hoe menu has separate soft and hard entries.");

    LogHoeTable =
        config.BindInOrder(
            "Debug",
            "logHoeTable",
            true,
            "Log the pieces of the Hoe build table and which one is cloned for the menu entries.");

    LogIntercepted =
        config.BindInOrder(
            "Debug",
            "logIntercepted",
            false,
            "Log each brush stroke that is handled by this mod.");
  }
}
