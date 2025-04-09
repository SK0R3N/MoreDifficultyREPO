using BepInEx;
using HarmonyLib;
using MyMOD;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static EnemyParent;

namespace DifficultyFeature
{
    public static class DifficultySaveManager
    {
        private static string savePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "DifficultySaves.json");
        private static Dictionary<string, string> difficultyData = new();

        static DifficultySaveManager()
        {
            Load();
        }

        public static void SaveDifficulty(string difficulty)
        {
            var directory = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            difficultyData[StatsManager.instance.saveFileCurrent] = difficulty;
            File.WriteAllText(savePath, JsonConvert.SerializeObject(difficultyData, Formatting.Indented));
        }

        public static string LoadDifficulty(string saveFileName)
        {
            Debug.Log(saveFileName);
            if (difficultyData.TryGetValue(saveFileName, out string diff))
                return diff;
            return "Normal"; // default fallback
        }

        private static void Load()
        {
            if (File.Exists(savePath))
            {
                var json = File.ReadAllText(savePath);
                difficultyData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new();
            }
        }
    }

    public static class DifficultySaveContext
    {
        public static string CurrentSaveFileName { get; private set; } = "";

        public static void SetCurrent(string name)
        {
            CurrentSaveFileName = name;
        }

        [HarmonyPatch(typeof(MenuPageSaves), nameof(MenuPageSaves.SaveFileSelected))]
        public class SaveFileSelectedPatch
        {
            static void Postfix(MenuPageSaves __instance)
            {
                string currentSave = Traverse.Create(__instance).Field<string>("currentSaveFileName").Value;
                Debug.LogError($"{currentSave}");

                DifficultySaveContext.SetCurrent(currentSave);

                string loadedDifficulty = DifficultySaveManager.LoadDifficulty(currentSave);
                Debug.Log($"[Difficulty] Difficulty for save {loadedDifficulty}");
                DifficultyManager.CurrentDifficulty = (DifficultyManager.DifficultyLevel)Enum.Parse(typeof(DifficultyManager.DifficultyLevel), loadedDifficulty);
                Debug.Log($"[Difficulty] Difficulty for save {loadedDifficulty}");
            }
        }

    }
}
