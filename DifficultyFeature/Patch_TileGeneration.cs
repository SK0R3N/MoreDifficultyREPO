using BepInEx.Logging;
using HarmonyLib;
using REPOLib;
using SingularityGroup.HotReload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using static MyMOD.DifficultyManager;

namespace MyMOD
{
    [HarmonyPatch(typeof(LevelGenerator), "TileGeneration")]
    public static class Patch_TileGeneration
    {

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.RunManagerPatch");

        public static bool Prefix(LevelGenerator __instance, ref IEnumerator __result)
        {
            __result = CustomTileGen(__instance);
            return false;
        }
        private static IEnumerator CustomTileGen(LevelGenerator gen)
        {
            bool success = false;
            while (!success)
            {
                Log.LogInfo("[CustomTileGen] Override launched!");

                FieldInfo waitingField = AccessTools.Field(typeof(LevelGenerator), "waitingForSubCoroutine");
                waitingField.SetValue(gen, true);

                int width = gen.LevelWidth;
                int height = gen.LevelHeight;

                FieldInfo gridField = AccessTools.Field(typeof(LevelGenerator), "LevelGrid");
                var grid = new LevelGenerator.Tile[width, height];

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        grid[x, y] = new LevelGenerator.Tile { x = x, y = y, active = false, type = (Module.Type)(-1) };
                    }
                }

                // Special case: skip custom logic for shop level
                if (gen.Level == RunManager.instance.levelShop || gen.Level == RunManager.instance.levelArena)
                {
                    grid[width / 2, 0].active = true;
                    grid[width / 2, 0].first = true;

                    gen.Level.PassageMaxAmount = 0;
                    gen.DeadEndAmount = 1;
                    gen.ExtractionAmount = 0;

                    gridField.SetValue(gen, grid);
                    waitingField.SetValue(gen, false);
                    Log.LogInfo("[CustomTileGen] Shop level detected, using default configuration.");
                    yield break;
                }

                int moduleCount = DifficultyManager2.GetModifiedModuleAmount() + (RunManager.instance.levelsCompleted * 2);
                if (moduleCount > 30)
                {
                    moduleCount = 30;
                }
                Log.LogInfo($"[CustomTileGen] Using Module Count: {moduleCount}");

                var difficulty = DifficultyManager.CurrentDifficulty;

                Log.LogInfo($"[Difficulty] DeadEndAmount → {gen.Level.PassageMaxAmount}");
                Log.LogInfo($"[Difficulty] DeadEndAmount → {DifficultyManager2.GetPassageMultiplier(difficulty)}");
                gen.Level.PassageMaxAmount = Mathf.RoundToInt(gen.Level.PassageMaxAmount * DifficultyManager2.GetPassageMultiplier(difficulty));
                Log.LogInfo($"[Difficulty] PassageMaxAmount → {gen.Level.PassageMaxAmount}");

                Log.LogInfo($"[Difficulty] DeadEndAmount → {gen.DeadEndAmount}");
                Log.LogInfo($"[Difficulty] DeadEndAmount → {DifficultyManager2.GetDeadEndMultiplier(difficulty)}");
                gen.DeadEndAmount = Mathf.RoundToInt(gen.DeadEndAmount * DifficultyManager2.GetDeadEndMultiplier(difficulty));
                Log.LogInfo($"[Difficulty] DeadEndAmount → {gen.DeadEndAmount}");

                int baseExtraction = DifficultyManager2.GetFixedExtractionAmount(difficulty);

                int extraFromProgress = (RunManager.instance.levelsCompleted + 1) / 2;
                int maxExtraction = DifficultyManager2.GetExtractionCap(difficulty);
                gen.ExtractionAmount = Mathf.Min(baseExtraction + extraFromProgress, maxExtraction);
                Log.LogInfo($"[Difficulty] ExtractionAmount → {gen.ExtractionAmount} (Base {baseExtraction} + Progress)");

                // Setup module difficulty tiers based on current difficulty and level
                int completed = RunManager.instance.levelsCompleted;
                switch (difficulty)
                {
                    case DifficultyLevel.Normal:
                    case DifficultyLevel.Hard:
                        if (completed < 3)
                            RarityOverrideManager.Set(1f, 0f, 0f);
                        else if (completed < 6)
                            RarityOverrideManager.Set(0.7f, 0.3f, 0f);
                        else
                            RarityOverrideManager.Set(0.4f, 0.4f, 0.2f); ;
                        break;
                    case DifficultyLevel.Hardcore:
                        if (completed < 4)
                            RarityOverrideManager.Set(0.4f, 0.6f, 0f);
                        else
                            RarityOverrideManager.Set(0.2f, 0.5f, 0.3f);
                        break;
                    case DifficultyLevel.Nightmare:
                        RarityOverrideManager.Set(0.3f, 0.4f, 0.3f);
                        break;
                    case DifficultyLevel.IsThatEvenPossible:
                        RarityOverrideManager.Set(0.1f, 0.3f, 0.6f);
                        break;
                    case DifficultyLevel.Custom:
                        RarityOverrideManager.Set((float)DifficultyManager.PourcentageRoom1 / 100, (float)DifficultyManager.PourcentageRoom2 / 100, (float)DifficultyManager.PourcentageRoom3 / 100);
                        break;
                }

                int centerX = width / 2;
                int centerY = 0;
                grid[centerX, centerY].active = true;
                grid[centerX, centerY].first = true;
                moduleCount--;

                int currentX = centerX;
                int currentY = centerY;

                System.Random rand = new System.Random();
                int tentative = 0;
                int tentativeMax = 10;
                bool notworking = true;
                while (moduleCount > 0)
                {
                    int dx = 0, dy = 0;
                    int dir = rand.Next(4);
                    switch (dir)
                    {
                        case 0: dx = 1; break;
                        case 1: dx = -1; break;
                        case 2: dy = 1; break;
                        case 3: dy = -1; break;
                    }

                    int newX = currentX + dx;
                    int newY = currentY + dy;

                    Log.LogInfo($"[Difficulty] ModuleAmount → {moduleCount} Progress)");
                    if (newX >= 0 && newX < width && newY >= 0 && newY < height && !grid[newX, newY].active) //problématique
                    {
                        grid[newX, newY].active = true;
                        currentX = newX;
                        currentY = newY;
                        moduleCount--;
                    }
                    else
                    {
                        if (tentative >= tentativeMax)
                        {
                            Log.LogError($"[Difficulty] ModuleAmount → {moduleCount} Failed)");
                            moduleCount--;
                            success = false;
                            notworking = false;
                        }
                        tentative++;
                    }

                    yield return null;
                }

                if(notworking)
                {
                    success = true;
                }

                List<LevelGenerator.Tile> possibleExtractionTiles = new();
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (!grid[x, y].active)
                        {
                            int adjacent = 0;
                            if (x > 0 && grid[x - 1, y].active) adjacent++;
                            if (x < width - 1 && grid[x + 1, y].active) adjacent++;
                            if (y > 0 && grid[x, y - 1].active) adjacent++;
                            if (y < height - 1 && grid[x, y + 1].active) adjacent++;

                            if (adjacent == 1)
                                possibleExtractionTiles.Add(grid[x, y]);
                        }
                    }
                }

                List<LevelGenerator.Tile> extractionTiles = new();
                extractionTiles.Add(new LevelGenerator.Tile { x = width / 2, y = -1 });



                while (gen.ExtractionAmount > 0 && possibleExtractionTiles.Count > 0)
                {
                    LevelGenerator.Tile best = null;
                    float bestDist = -1f;

                    foreach (var tile in possibleExtractionTiles)
                    {
                        float minDist = float.MaxValue;
                        foreach (var used in extractionTiles)
                        {
                            float dist = Vector2.Distance(new Vector2(tile.x, tile.y), new Vector2(used.x, used.y));
                            if (dist < minDist) minDist = dist;
                        }

                        if (minDist > bestDist)
                        {
                            bestDist = minDist;
                            best = tile;
                        }
                    }

                    if (best != null)
                    {
                        SetSpecialTile(grid, best, Module.Type.Extraction);
                        extractionTiles.Add(best);
                        possibleExtractionTiles.Remove(best);
                        gen.ExtractionAmount--;
                    }

                    yield return null;
                }

                possibleExtractionTiles.RemoveAll(t => t.type == (Module.Type)(-1));

                while (gen.DeadEndAmount > 0 && possibleExtractionTiles.Count > 0)
                {
                    int index = rand.Next(possibleExtractionTiles.Count);
                    var tile = possibleExtractionTiles[index];
                    SetSpecialTile(grid, tile, Module.Type.DeadEnd);
                    possibleExtractionTiles.RemoveAt(index);
                    gen.DeadEndAmount--;
                    yield return null;
                }

                if (success) { 
                    PrintMiniMap(grid);
                    gridField.SetValue(gen, grid);
                    waitingField.SetValue(gen, false);
                    Log.LogInfo("[CustomTileGen] Tile generation completed.");
                }
            }

        }

        private static void SetSpecialTile(LevelGenerator.Tile[,] grid, LevelGenerator.Tile target, Module.Type type)
        {
            target.active = true;
            target.type = type;

            int x = target.x;
            int y = target.y;

            void RemoveNeighbor(int nx, int ny)
            {
                if (nx >= 0 && nx < grid.GetLength(0) && ny >= 0 && ny < grid.GetLength(1))
                {
                    grid[nx, ny].type = (Module.Type)(-1); // Invalide pour future sélection
                }
            }

            RemoveNeighbor(x - 1, y);
            RemoveNeighbor(x + 1, y);
            RemoveNeighbor(x, y - 1);
            RemoveNeighbor(x, y + 1);
        }

        private static void PrintMiniMap(LevelGenerator.Tile[,] grid)
        {
            int width = grid.GetLength(0);
            int height = grid.GetLength(1);
            StringBuilder sb = new();

            sb.AppendLine("[TileGen] --- MINI MAP ---");

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = grid[x, y];
                    if (!tile.active)
                    {
                        sb.Append(" . ");
                    }
                    else
                    {
                        switch (tile.type)
                        {
                            case Module.Type.Extraction: sb.Append(" E "); break;
                            case Module.Type.DeadEnd: sb.Append(" D "); break;
                            case Module.Type.Passage: sb.Append(" P "); break;
                            case Module.Type.Normal: sb.Append(" N "); break;
                            default:
                                if (tile.first) sb.Append(" S "); // Start
                                else sb.Append(" # ");
                                break;
                        }
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("[TileGen] ----------------");

            Log.LogInfo(sb.ToString());
        }


    }

    [HarmonyPatch]
    public static class Patch_ModuleGeneration_MoveNext
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.ModuleGenerationPatch");

        // On cible la méthode MoveNext() de la coroutine générée par le compilateur
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // On récupère le type de la coroutine compilée
            var nestedTypes = typeof(LevelGenerator).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var type in nestedTypes)
            {
                if (type.Name.Contains("ModuleGeneration") && typeof(IEnumerator).IsAssignableFrom(type))
                {
                    var moveNext = AccessTools.Method(type, "MoveNext");
                    if (moveNext != null)
                    {
                        return moveNext;
                    }
                }
            }
            Log.LogError("Could not find MoveNext for ModuleGeneration coroutine.");
            return null;
        }

        // Ce Postfix s'exécute pendant l'exécution de la coroutine
        public static void Postfix(object __instance)
        {
            // On récupère le LevelGenerator via le champ <>4__this (self de la coroutine)
            var genField = AccessTools.Field(__instance.GetType(), "<>4__this");
            if (genField == null)
            {
                Log.LogError("Could not find '<>4__this' field.");
                return;
            }

            var gen = genField.GetValue(__instance) as LevelGenerator;
            if (gen == null)
            {
                Log.LogError("LevelGenerator instance not found.");
                return;
            }

            if (RarityOverrideManager.Rarity1.HasValue)
            {
                gen.ModuleRarity1 = RarityOverrideManager.Rarity1.Value;
                gen.ModuleRarity2 = RarityOverrideManager.Rarity2.Value;
                gen.ModuleRarity3 = RarityOverrideManager.Rarity3.Value;

                Log.LogInfo($"[Patch_ModuleGeneration_MoveNext] Overridden rarities → {gen.ModuleRarity1}, {gen.ModuleRarity2}, {gen.ModuleRarity3}");
            }
            else
            {
                Log.LogInfo("[Patch_ModuleGeneration_MoveNext] No override set, using default values.");
            }
        }
    }

    [HarmonyPatch(typeof(LevelGenerator), "SpawnModule")]
    public static class Patch_SpawnModule_Log
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.SpawnModuleLogger");

        public static void Postfix(int x, int y, Vector3 position, Vector3 rotation, Module.Type type)
        {
            Log.LogInfo($"[SpawnModule] Spawned module at ({x},{y}) | Type: {type}");
        }
    }

    public static class RarityOverrideManager
    {
        public static float? Rarity1 = null;
        public static float? Rarity2 = null;
        public static float? Rarity3 = null;

        public static void Set(float r1, float r2, float r3)
        {
            Rarity1 = r1;
            Rarity2 = r2;
            Rarity3 = r3;
        }

        public static void Clear()
        {
            Rarity1 = Rarity2 = Rarity3 = null;
        }
    }


    public static class DifficultyManager2
    {

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("MyMOD.RunManagerPatch");


        public static int GetModifiedModuleAmount()
        {
            Log.LogInfo($"[CustomTileGen] Difficulty: {DifficultyManager.CurrentDifficulty}");
            switch (DifficultyManager.CurrentDifficulty)
            {
                case DifficultyManager.DifficultyLevel.Hard: return 8;
                case DifficultyManager.DifficultyLevel.Hardcore: return 10;
                case DifficultyManager.DifficultyLevel.Nightmare: return 12;
                case DifficultyManager.DifficultyLevel.IsThatEvenPossible: return 15;
                case DifficultyManager.DifficultyLevel.Custom: return CustomRoom();
                default: return 8;
            }
        }

        public static int CustomRoom()
        {
            if(DifficultyManager.ExtractionMultiplier <= 12 && DifficultyManager.ExtractionMultiplier >= 7)
            {
                return DifficultyManager.RoomNumber;
            } else if (DifficultyManager.ExtractionMultiplier < 7 && DifficultyManager.ExtractionMultiplier >= 4)
            {
                return 20;
            } else if (DifficultyManager.ExtractionMultiplier < 4 && DifficultyManager.ExtractionMultiplier >= 2)
            {
                return 15;
            } else
            {
                return 10;
            }

        }

        public static float GetPassageMultiplier(DifficultyLevel difficulty) => 1f + (int)difficulty * 2f;
        public static float GetDeadEndMultiplier(DifficultyLevel difficulty) => 1f + (int)difficulty * 2f;

        public static int GetFixedExtractionAmount(DifficultyLevel difficulty) => difficulty switch
        {
            DifficultyLevel.Normal => 0,
            DifficultyLevel.Hard => 1,
            DifficultyLevel.Hardcore => 2,
            DifficultyLevel.Nightmare => 3,
            DifficultyLevel.IsThatEvenPossible => 4,
            DifficultyLevel.Custom => DifficultyManager.ExtractionMultiplier,
            _ => 1
        };

        public static int GetExtractionCap(DifficultyLevel difficulty) => difficulty switch
        {
            DifficultyLevel.Normal => 3,
            DifficultyLevel.Hard => 5,
            DifficultyLevel.Hardcore => 7,
            DifficultyLevel.Nightmare => 9,
            DifficultyLevel.IsThatEvenPossible => 11,
            DifficultyLevel.Custom => DifficultyManager.ExtractionMaxMultiplier,
            _ => 4
        };
    }
}