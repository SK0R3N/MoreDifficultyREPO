using HarmonyLib;
using MyMOD;
using static MyMOD.DifficultyManager;
using System.Reflection;
using UnityEngine;
using BepInEx.Logging;

[HarmonyPatch(typeof(LevelGenerator), "Start")]
public static class Patch_LevelGeneratorStart
{

    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.RunManagerPatch");

    [HarmonyPostfix]
    public static void Postfix(LevelGenerator __instance)
    {
        var difficulty = DifficultyManager.CurrentDifficulty;
        var level = RunManager.instance.levelCurrent;

        if (level == null)
            return;
        float passageMultiplier = difficulty switch
        {
            DifficultyLevel.Normal => 1f,
            DifficultyLevel.Hard => 1f,
            DifficultyLevel.Hardcore => 2f,
            DifficultyLevel.Nightmare => 2f,
            DifficultyLevel.IsThatEvenPossible => 3f,
            DifficultyLevel.Custom => 1f,
            _ => 1f
        };

        float deadEndMultiplier = passageMultiplier; // même multiplicateur que pour les passages

        // Applique les multiplicateurs
        level.PassageMaxAmount = Mathf.RoundToInt(1*(int)difficulty);
        __instance.GetType().GetField("DeadEndAmount", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(__instance, Mathf.RoundToInt(1));

        Log.LogInfo($"[Difficulty] PassageMaxAmount x{passageMultiplier} => {level.PassageMaxAmount}");
        Log.LogInfo($"[Difficulty] DeadEndAmount x{deadEndMultiplier}");
    }
}
