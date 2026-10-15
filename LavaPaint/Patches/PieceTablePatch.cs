namespace LavaPaint;

using System;

using HarmonyLib;

[HarmonyPatch(typeof(PieceTable))]
static class PieceTablePatch {
  static bool _loggedError;

  [HarmonyPostfix]
  [HarmonyPatch(nameof(PieceTable.UpdateAvailable))]
  static void UpdateAvailablePostfix(PieceTable __instance) {
    try {
      LavaMenu.Inject(__instance);
    } catch (Exception exception) {
      // Never break the vanilla build menu, log once.
      if (!_loggedError) {
        _loggedError = true;
        LavaPaint.LogError($"Adding the lava menu entries failed: {exception}");
      }
    }
  }
}
