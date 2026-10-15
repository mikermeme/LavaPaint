namespace LavaPaint;

using System.Reflection;

using BepInEx;
using BepInEx.Logging;

using HarmonyLib;

using static PluginConfig;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class LavaPaint : BaseUnityPlugin {
  public const string PluginGuid = "miker.valheim.lavapaint";
  public const string PluginName = "LavaPaint";
  public const string PluginVersion = "1.1.0";

  static ManualLogSource _logger;
  Harmony _harmony;

  void Awake() {
    _logger = Logger;
    BindConfig(Config);

    _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);

    // Covers the terminal having been initialized before this plugin patched it.
    if (Console.instance) {
      LavaCommand.Register();
    }
  }

  void OnDestroy() {
    _harmony?.UnpatchSelf();
  }

  public static void LogInfo(object o) => _logger.LogInfo(o);

  public static void LogError(object o) => _logger.LogError(o);
}
