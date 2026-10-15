namespace LavaPaint;

using System;

using HarmonyLib;

using UnityEngine;

using static PluginConfig;

[HarmonyPatch(typeof(TerrainOp))]
static class TerrainOpPatch {
  // Runs for the real op spawned by Player.PlacePiece. Placement ghosts are created with m_forceDisableTerrainOps, and
  // pieces without a LavaBrush are vanilla and left alone.
  //
  // Radius mods such as Miadhoe change TerrainOp.m_settings in their own Awake prefix. This prefix must run after
  // them, so it reads the radius they set, and not the one of the cloned Hoe piece.
  [HarmonyPriority(Priority.Last)]
  [HarmonyAfter("redseiko.valheim.miadhoe")]
  [HarmonyPrefix]
  [HarmonyPatch(nameof(TerrainOp.Awake))]
  static bool AwakePrefix(TerrainOp __instance) {
    if (TerrainOp.m_forceDisableTerrainOps) {
      return true;
    }

    LavaBrush brush = __instance.GetComponentInParent<LavaBrush>();
    if (!brush) {
      return true;
    }

    if (brush.Handled) {
      return false;
    }

    brush.Handled = true;

    try {
      if (IsModEnabled.Value && LavaMenu.TryGetDefinition(brush.Id, out BrushDefinition definition)) {
        Vector3 position = __instance.transform.position;
        TerrainOp.Settings settings = __instance.m_settings;

        LavaPainter.Result result = definition.Apply(position, settings.m_paintRadius, settings.m_halfOffset);

        if (LogIntercepted.Value) {
          LavaPaint.LogInfo(
              $"{definition.Name}: radius {settings.m_paintRadius}, {result.Vertices} vertices in " +
              $"{result.Zones} zone(s) at {position} (skipped {result.SkippedZones}, not owned {result.NotOwned})");
        }

        if (result.Zones == 0 && result.SkippedZones > 0) {
          Notice.WrongBiome(definition.Biome);
        }

        __instance.m_onPlacedEffect.Create(position, Quaternion.identity);
      }
    } catch (Exception exception) {
      LavaPaint.LogError($"Lava brush failed: {exception}");
    }

    UnityEngine.Object.Destroy(brush.gameObject);
    return false;
  }
}
