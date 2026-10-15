namespace LavaPaint;

using HarmonyLib;

[HarmonyPatch(typeof(Terminal))]
static class TerminalPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(Terminal.InitTerminal))]
  static void InitTerminalPostfix() {
    LavaCommand.Register();
  }
}
