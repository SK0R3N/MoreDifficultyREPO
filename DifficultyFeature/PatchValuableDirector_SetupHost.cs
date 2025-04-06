using BepInEx.Logging;
using HarmonyLib;
using REPOLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using static MyMOD.DifficultyManager;

namespace MyMOD
{
    [HarmonyPatch(typeof(ValuableDirector), "SetupHost")]
    public static class Patch_ValuableDirector_SetupHost
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.RunManagerPatch");

        public static void Prefix(ValuableDirector __instance)
        {
            var difficulty = DifficultyManager.CurrentDifficulty;
            float multiplier = DifficultyManager3.GetValuableMultiplier(difficulty);

            Log.LogInfo($"[Valuables] Applying valuable multiplier x{multiplier} for difficulty {difficulty}");

            // Apply multipliers to the max amount fields
            __instance.totalMaxAmountCurve = ScaleCurve(__instance.totalMaxAmountCurve, multiplier);
            __instance.tinyMaxAmountCurve = ScaleCurve(__instance.tinyMaxAmountCurve, multiplier);
            __instance.smallMaxAmountCurve = ScaleCurve(__instance.smallMaxAmountCurve, multiplier);
            __instance.mediumMaxAmountCurve = ScaleCurve(__instance.mediumMaxAmountCurve, multiplier);
            __instance.bigMaxAmountCurve = ScaleCurve(__instance.bigMaxAmountCurve, multiplier);
            __instance.wideMaxAmountCurve = ScaleCurve(__instance.wideMaxAmountCurve, multiplier);
            __instance.tallMaxAmountCurve = ScaleCurve(__instance.tallMaxAmountCurve, multiplier);
            __instance.veryTallMaxAmountCurve = ScaleCurve(__instance.veryTallMaxAmountCurve, multiplier);
        }

        private static AnimationCurve ScaleCurve(AnimationCurve original, float multiplier)
        {
            Keyframe[] keys = original.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= multiplier;
            }
            return new AnimationCurve(keys);
        }
    }

    [HarmonyPatch(typeof(EnemyDirector), "AmountSetup")]
    public static class Patch_EnemyDirector_AmountSetup
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.RunManagerPatch");

        public static bool Prefix(EnemyDirector __instance)
        {
            Log.LogInfo("[Difficulty] Custom Enemy Setup Launched");

            List<EnemySetup> selectedEnemies = new List<EnemySetup>();

            int completed = RunManager.instance.levelsCompleted;
            var difficulty = DifficultyManager.CurrentDifficulty;

            int targetCount = GetTargetEnemyCount(completed, difficulty);

            Log.LogInfo($"[Difficulty] Level Completed: {completed}, Target Enemy Count: {targetCount}");

            // Choisir les ennemis dans les listes 1 à 3 selon progression
            AddEnemies(__instance.enemiesDifficulty1, selectedEnemies, completed, 1);
            AddEnemies(__instance.enemiesDifficulty2, selectedEnemies, completed, 3);
            AddEnemies(__instance.enemiesDifficulty3, selectedEnemies, completed, 5);

            // Shuffle + Truncate si nécessaire
            selectedEnemies.Shuffle();
            if (selectedEnemies.Count > targetCount)
                selectedEnemies = selectedEnemies.GetRange(0, targetCount);

            // Injecter dans la liste privée "enemyList"
            FieldInfo enemyListField = AccessTools.Field(typeof(EnemyDirector), "enemyList");
            enemyListField.SetValue(__instance, selectedEnemies);

            __instance.totalAmount = selectedEnemies.Count;

            foreach(var enemy in selectedEnemies)
            {
                Log.LogInfo($"[Difficulty]  enemy list: {enemy.name}");
            }

            Log.LogInfo($"[Difficulty] Final enemy list count: {selectedEnemies.Count}");

            return false; // skip vanilla
        }

        private static int GetTargetEnemyCount(int completed, DifficultyManager.DifficultyLevel difficulty)
        {
            int baseCount = 2 + completed; // exemple : +1 ennemi par niveau terminé
            switch (difficulty)
            {
                case DifficultyManager.DifficultyLevel.Hard: return baseCount + 1;
                case DifficultyManager.DifficultyLevel.Hardcore: return baseCount + 3;
                case DifficultyManager.DifficultyLevel.Nightmare: return baseCount + 5;
                case DifficultyManager.DifficultyLevel.IsThatEvenPossible: return baseCount + 8;
                case DifficultyManager.DifficultyLevel.Custom: return baseCount * DifficultyManager.EnemyMultiplier;
                default: return baseCount;
            }
        }

        private static void AddEnemies(List<EnemySetup> sourceList, List<EnemySetup> target, int completed, int tier)
        {
            foreach (var enemy in sourceList)
            {
                if (enemy == null) continue;

                if (enemy.levelsCompletedCondition)
                {
                    if (completed < enemy.levelsCompletedMin || completed > enemy.levelsCompletedMax)
                        continue;
                }

                // On autorise par défaut une seule occurrence
                target.Add(enemy);
            }
        }
    }


    [HarmonyPatch(typeof(ShopManager), "ShopInitialize")]
    public static class Patch_ShopManager_ShopInitialize
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.ShopPatch");

        public static void Prefix(ShopManager __instance)
        {
            float multiplier = DifficultyManager3.GetShopPriceMultiplier(DifficultyManager.CurrentDifficulty);
            __instance.itemValueMultiplier = multiplier * 4;
            Log.LogError($"{__instance.itemValueMultiplier}");

            Log.LogInfo($"[Difficulty] Shop price multiplier applied: x{multiplier}");
        }
    }


    public static class DifficultyScaler
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.RunManagerPatch");

        public static void ApplyDifficultyScaling()
        {
            var difficulty = DifficultyManager.CurrentDifficulty;

            // Valuable multiplier
            float valuableMultiplier = DifficultyManager3.GetValuableMultiplier(difficulty);
            Log.LogInfo($"[Valuables] Applying valuable multiplier x{valuableMultiplier} for difficulty {difficulty}");
        }
    }

    public static class DifficultyManager3
    {
        public static DifficultyManager.DifficultyLevel CurrentDifficulty = DifficultyManager.CurrentDifficulty;


        public static float GetValuableMultiplier(DifficultyLevel difficulty) => difficulty switch
        {
            DifficultyManager.DifficultyLevel.Hard => 2f,
            DifficultyManager.DifficultyLevel.Hardcore => 3f,
            DifficultyManager.DifficultyLevel.Nightmare => 4f,
            DifficultyManager.DifficultyLevel.IsThatEvenPossible => 5f,
            DifficultyManager.DifficultyLevel.Custom => DifficultyManager.ValuableMultiplier,
            _ => 1f
        };

        public static float GetShopPriceMultiplier(DifficultyLevel difficulty) => difficulty switch
        {
            DifficultyManager.DifficultyLevel.Hard => 1.5f,
            DifficultyManager.DifficultyLevel.Hardcore => 2f,
            DifficultyManager.DifficultyLevel.Nightmare => 2.5f,
            DifficultyManager.DifficultyLevel.IsThatEvenPossible => 3f,
            DifficultyManager.DifficultyLevel.Custom => DifficultyManager.ShopMultiplier,
            _ => 1f
        };
    }

}
