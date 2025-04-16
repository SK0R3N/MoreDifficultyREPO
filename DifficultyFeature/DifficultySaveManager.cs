using BepInEx;
using HarmonyLib;
using Mono.Cecil.Cil;
using MyMOD;
using Newtonsoft.Json;
using Photon.Pun;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Xml;
using UnityEngine;
using static DifficultyFeature.Event;
using WebSocketSharp;
using static EnemyParent;
using static System.Collections.Specialized.BitVector32;
using static UnityEngine.ParticleSystem;
using static UnityEngine.UIElements.UxmlAttributeDescription;

namespace DifficultyFeature
{
    public static class DifficultySaveManager
    {
        private static string savePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "DifficultySaves.json");
        private static Dictionary<string, SaveData> saveData = new();

        // Classe pour structurer les données sauvegardées
        private class SaveData
        {
            public string Difficulty { get; set; } = "Normal";
            public string WalkieWinnerSteamID { get; set; } = null; // ID du gagnant
        }

        static DifficultySaveManager()
        {
            Load();
        }

        public static void SaveDifficulty(string difficulty)
        {
            string saveFileName = StatsManager.instance.saveFileCurrent;

            var directory = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!saveData.ContainsKey(saveFileName))
            {
                saveData[saveFileName] = new SaveData();
            }
            saveData[saveFileName].Difficulty = difficulty;
            SaveToFile();
        }

        public static void SaveWalkieWinner(string steamID)
        {
            string saveFileName = StatsManager.instance.saveFileCurrent;
            if (!saveData.ContainsKey(saveFileName))
            {
                saveData[saveFileName] = new SaveData();
            }
            saveData[saveFileName].WalkieWinnerSteamID = steamID;
            SaveToFile();
            Debug.Log($"[DifficultySaveManager] Saved WalkieWinner for {saveFileName}: {steamID}");
        }

        public static string LoadDifficulty(string saveFileName)
        {
            if (saveData.TryGetValue(saveFileName, out SaveData data))
            {
                return data.Difficulty;
            }
            return "Normal"; // Valeur par défaut
        }

        public static string LoadWalkieWinner(string saveFileName)
        {
            if (saveData.TryGetValue(saveFileName, out SaveData data))
            {
                return data.WalkieWinnerSteamID;
            }
            return null; // Aucun gagnant
        }

        private static void SaveToFile()
        {
            File.WriteAllText(savePath, JsonConvert.SerializeObject(saveData, Newtonsoft.Json.Formatting.Indented));
        }

        private static void Load()
        {
            if (File.Exists(savePath))
            {
                var json = File.ReadAllText(savePath);
                saveData = JsonConvert.DeserializeObject<Dictionary<string, SaveData>>(json) ?? new();
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
                Debug.Log($"[DifficultySaveContext] Selected save: {currentSave}");

                DifficultySaveContext.SetCurrent(currentSave);

                string loadedDifficulty = DifficultySaveManager.LoadDifficulty(currentSave);
                Debug.Log($"[Difficulty] Difficulty for save: {loadedDifficulty}");
                DifficultyManager.CurrentDifficulty = (DifficultyManager.DifficultyLevel)Enum.Parse(typeof(DifficultyManager.DifficultyLevel), loadedDifficulty);

                // Charger le gagnant du walkie
                string walkieWinner = DifficultySaveManager.LoadWalkieWinner(currentSave);
                Debug.Log($"[Difficulty] Walkie winner for save: {walkieWinner ?? "None"}");
            }
        }

    }
}
