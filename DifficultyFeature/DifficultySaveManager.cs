using BepInEx;
using DifficultyFeature;
using HarmonyLib;
using Mono.Cecil.Cil;
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
using DifficultyFeature.DifficultyUpdate;
using static DifficultyFeature.DifficultyUpdate.DifficultyManager;

namespace DifficultyFeature
{
    [System.Serializable]
    public class CustomDifficultySettings
    {
        public int ExtractionMultiplier;
        public int ExtractionMaxMultiplier;
        public int PourcentageRoom1;
        public int PourcentageRoom2;
        public int PourcentageRoom3;
        public int EnemyMultiplier;
        public float ShopMultiplier;
        public int ValuableMultiplier;
        public int EnemyLifeMultiplier;
        public bool SlotsActive;
        public bool LevelScaler;

        public CustomDifficultySettings()
        {
            ExtractionMultiplier = DifficultyManager.ExtractionMultiplier;
            ExtractionMaxMultiplier = DifficultyManager.ExtractionMaxMultiplier;
            PourcentageRoom1 = DifficultyManager.PourcentageRoom1;
            PourcentageRoom2 = DifficultyManager.PourcentageRoom2;
            PourcentageRoom3 = DifficultyManager.PourcentageRoom3;
            EnemyMultiplier = DifficultyManager.EnemyMultiplier;
            ShopMultiplier = DifficultyManager.ShopMultiplier;
            ValuableMultiplier = DifficultyManager.ValuableMultiplier;
            EnemyLifeMultiplier = DifficultyManager.MultiplierEnemyLife;
            LevelScaler = DifficultyManager.LevelScaler;
            SlotsActive = DifficultyManager.SlotsActive;
        }
    }

    [System.Serializable]
    public class DifficultyData
    {
        public string DifficultyName;
        public CustomDifficultySettings CustomSettings;
        public HashSet<string> WalkieWinnerSteamIDs { get; set; } = new HashSet<string>();
        public float ProgressBar { get; set; }
    }

    public static class DifficultySaveManager
    {
        private static string savePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "DifficultySaves.json");
        private static Dictionary<string, DifficultyData> difficultyData = new();

        static DifficultySaveManager()
        {
            Load();
        }

        public static void SaveDifficulty(string difficultyName)
        {
            string saveFileName = StatsManager.instance.saveFileCurrent;
            var directory = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            if(!difficultyData.ContainsKey(saveFileName))
            {
                difficultyData[saveFileName] = new DifficultyData();
            }

            difficultyData[saveFileName] = new DifficultyData
            {
                DifficultyName = difficultyName,
                CustomSettings = new CustomDifficultySettings(),
                WalkieWinnerSteamIDs = difficultyData[saveFileName].WalkieWinnerSteamIDs,
                ProgressBar = difficultyData[saveFileName].ProgressBar,
            };

            File.WriteAllText(savePath, JsonConvert.SerializeObject(difficultyData, Newtonsoft.Json.Formatting.Indented));
            Debug.Log($"[DifficultySaveManager] Difficulté sauvegardée pour {saveFileName}: {difficultyName}");
        }

        public static void AddWalkieWinner(string steamID)
        {
            string saveFileName = StatsManager.instance.saveFileCurrent;
            if (!difficultyData.ContainsKey(saveFileName))
            {
                difficultyData[saveFileName] = new DifficultyData();
            }
            difficultyData[saveFileName].WalkieWinnerSteamIDs.Add(steamID);
            SaveToFile();
            Debug.Log($"[DifficultySaveManager] Added WalkieWinner for {saveFileName}: {steamID}");
        }

        public static void SaveProgressBar(float progress)
        {
            string saveFileName = StatsManager.instance.saveFileCurrent;
            if (!difficultyData.ContainsKey(saveFileName))
            {
                difficultyData[saveFileName] = new DifficultyData();
            }
            difficultyData[saveFileName].ProgressBar = progress;
            SaveToFile();
        }

        public static float LoadProgressBar()
        {
            try
            {
                if (difficultyData.TryGetValue(StatsManager.instance.saveFileCurrent, out DifficultyData data))
                {
                    if(data.ProgressBar >= 260)
                    return data.ProgressBar;

                    SaveProgressBar(260);
                    return 260f;
                }
            } catch {
                SaveProgressBar(260);
                return 260f;
            }

            return 260f;
        }

        public static string LoadDifficulty(string saveFileName)
        {
            Debug.Log($"[DifficultySaveManager] Chargement de la difficulté pour {saveFileName}");
            if (difficultyData.TryGetValue(saveFileName, out DifficultyData data) && data != null)
            {
                // Appliquer les paramètres à DifficultyManager
                DifficultyManager.ExtractionMultiplier = data.CustomSettings.ExtractionMultiplier;
                DifficultyManager.ExtractionMaxMultiplier = data.CustomSettings.ExtractionMaxMultiplier;
                DifficultyManager.PourcentageRoom1 = data.CustomSettings.PourcentageRoom1;
                DifficultyManager.PourcentageRoom2 = data.CustomSettings.PourcentageRoom2;
                DifficultyManager.PourcentageRoom3 = data.CustomSettings.PourcentageRoom3;
                DifficultyManager.EnemyMultiplier = data.CustomSettings.EnemyMultiplier;
                DifficultyManager.ShopMultiplier = data.CustomSettings.ShopMultiplier;
                DifficultyManager.ValuableMultiplier = data.CustomSettings.ValuableMultiplier;
                DifficultyManager.MultiplierEnemyLife = data.CustomSettings.EnemyLifeMultiplier;
                DifficultyManager.CurrentDifficulty = Enum.Parse<DifficultyLevel>(data.DifficultyName);
                DifficultyManager.LevelScaler = data.CustomSettings.LevelScaler;
                DifficultyManager.SlotsActive = data.CustomSettings.SlotsActive;
                return data.DifficultyName;
            }

            // Si aucune donnée, utiliser Normal par défaut
            Debug.Log($"[DifficultySaveManager] Aucune difficulté trouvée pour {saveFileName}, utilisation de Normal par défaut.");
            SaveDifficulty("Normal");
            return "Normal";
        }

        public static HashSet<string> LoadWalkieWinners(string saveFileName)
        {
            if (difficultyData.TryGetValue(saveFileName, out DifficultyData data))
            {
                return data.WalkieWinnerSteamIDs;
            }
            return new HashSet<string>(); // Aucun gagnant
        }

        private static void SaveToFile()
        {
            File.WriteAllText(savePath, JsonConvert.SerializeObject(difficultyData, Newtonsoft.Json.Formatting.Indented));
        }

        private static void Load()
        {
            if (!File.Exists(savePath))
            {
                difficultyData = new Dictionary<string, DifficultyData>();
                return;
            }

            string json = File.ReadAllText(savePath);
            try
            {
                // Essayer de charger comme nouveau format
                difficultyData = JsonConvert.DeserializeObject<Dictionary<string, DifficultyData>>(json) ?? new();
                Debug.Log("[DifficultySaveManager] Nouveau format chargé.");
            }
            catch
            {
                try
                {
                    // Essayer de charger comme ancien format
                    var oldData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (oldData != null)
                    {
                        Debug.Log("[DifficultySaveManager] Ancien format détecté, conversion en cours...");
                        difficultyData = new Dictionary<string, DifficultyData>();
                        foreach (var entry in oldData)
                        {
                            difficultyData[entry.Key] = new DifficultyData
                            {
                                DifficultyName = entry.Value,
                                CustomSettings = new CustomDifficultySettings()
                            };
                        }
                        // Sauvegarder dans le nouveau format
                        File.WriteAllText(savePath, JsonConvert.SerializeObject(difficultyData, Newtonsoft.Json.Formatting.Indented));
                        Debug.Log("[DifficultySaveManager] Conversion de l'ancien format terminée et sauvegardée.");
                    }
                    else
                    {
                        difficultyData = new Dictionary<string, DifficultyData>();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DifficultySaveManager] Échec de la lecture du fichier de sauvegarde : {ex.Message}");
                    difficultyData = new Dictionary<string, DifficultyData>();
                }
            }
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

            // Charger la difficulté pour la sauvegarde sélectionnée
            DifficultySaveManager.LoadDifficulty(currentSave);

            // Mettre à jour l'UI
            DifficultyLabelUI.SetDifficulty(DifficultyManager.CurrentDifficulty.ToString());
        }
    }
}