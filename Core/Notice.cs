namespace LavaPaint;

using UnityEngine;

static class Notice {
  const float Cooldown = 3f;

  static float _lastTime = -999f;

  public static void WrongBiome(BiomeFilter biome) {
    Show($"This brush only works in the {Name(biome)}.");
  }

  public static string Name(BiomeFilter biome) {
    switch (biome) {
      case BiomeFilter.AshLands:
        return "Ashlands";
      case BiomeFilter.DeepNorth:
        return "Deep North";
      default:
        return "world";
    }
  }

  static void Show(string message) {
    if (Time.time - _lastTime < Cooldown) {
      return;
    }

    _lastTime = Time.time;

    Player player = Player.m_localPlayer;
    if (player) {
      player.Message(MessageHud.MessageType.TopLeft, message);
    }
  }
}
