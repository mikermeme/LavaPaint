namespace LavaPaint;

using System;
using System.Collections.Generic;

using HarmonyLib;

using UnityEngine;

// The game assembly is only publicized at compile time, so private members are reached through reflection.
static class PrivateAccess {
  public static readonly AccessTools.FieldRef<TerrainComp, ZNetView> TerrainCompNetView =
      AccessTools.FieldRefAccess<TerrainComp, ZNetView>("m_nview");

  public static readonly AccessTools.FieldRef<TerrainComp, Color[]> TerrainCompPaintMask =
      AccessTools.FieldRefAccess<TerrainComp, Color[]>("m_paintMask");

  public static readonly AccessTools.FieldRef<TerrainComp, bool[]> TerrainCompModifiedPaint =
      AccessTools.FieldRefAccess<TerrainComp, bool[]>("m_modifiedPaint");

  public static readonly Action<TerrainComp> TerrainCompCheckLoad =
      AccessTools.MethodDelegate<Action<TerrainComp>>(AccessTools.Method(typeof(TerrainComp), "CheckLoad"));

  public static readonly Action<TerrainComp, bool> TerrainCompSave =
      AccessTools.MethodDelegate<Action<TerrainComp, bool>>(
          AccessTools.Method(typeof(TerrainComp), "Save", [typeof(bool)]));

  public static readonly AccessTools.FieldRef<PieceTable, List<List<Piece>>> PieceTableAvailableByCategory =
      AccessTools.FieldRefAccess<PieceTable, List<List<Piece>>>("m_availablePiecesByCategory");
}
