using BepInEx;
using BepInEx.Logging;
using DifficultyFeature.DifficultyUpdate;
using DifficultyFeature.DifficultyUpdate.GenerationRework;
using ExitGames.Client.Photon;
using HarmonyLib;
using MenuLib;
using Photon.Pun;
using Photon.Realtime;
using REPOLib.Modules;
using REPOLib.Objects;
using Steamworks;
using Steamworks.Ugc;
using SingularityGroup.HotReload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace DifficultyFeature
{
    [BepInPlugin("SK0R3N.DifficultyFeature", "DifficultyFeature", "1.0")]
    public class DifficultyFeature : BaseUnityPlugin 
    {
        internal static DifficultyFeature Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger => Instance._logger;
        private ManualLogSource _logger => base.Logger;
        internal Harmony? Harmony { get; set; }
        private bool subscribed = false;




        public static int DifficultyLevel { get; set; } = 1;

        private void Awake()
        {
            Instance = this;

            // Prevent the plugin from being deleted
            gameObject.transform.parent = null;
            gameObject.hideFlags = HideFlags.HideAndDontSave;

            string bundlePath2 = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "goldengun");
            AssetBundle bundle2 = AssetBundle.LoadFromFile(bundlePath2);

            GameObject goldenGunPrefab = bundle2.LoadAsset<GameObject>("Golden_Gun");
            Item item = bundle2.LoadAsset<Item>("Golden_Gun.asset");

            Items.RegisterItem(item);

            var harmony = new Harmony("SK0R3N.DifficultyFeature");
            harmony.PatchAll();

            Patch();

            Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
        }

        internal void Patch()
        {
            Harmony ??= new Harmony(Info.Metadata.GUID);
            Harmony.PatchAll();
        }

        internal void Unpatch()
        {
            Harmony?.UnpatchSelf();
        }

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("DifficultyFeature.ViewIDLogger");
        private PhotonView[] lastPhotonViews = new PhotonView[0];

        private void Update()
        {

            if (RunManager.instance.levelCurrent.name.ToLower().Contains("vaultline"))
            {
                PhotonView[] allViews = PhotonNetwork.PhotonViews;
                foreach (PhotonView view in allViews)
                {
                    if (view.gameObject.name.ToLower().Contains("door") || view.gameObject.name.ToLower().Contains("hinge"))
                    {
                        PhotonNetwork.Destroy(view.gameObject);
                        Debug.Log("PhotonDestroy");
                    }
                }
            }

            GameObject lobbyPage = GameObject.Find("Menu Page Lobby(Clone)");
            if (lobbyPage != null && PhotonNetwork.IsMasterClient)
            {
                Transform existingButton = lobbyPage.transform.Find("DifficultyButton");
                if (existingButton == null)
                {
                    DifficultyMenu.CreateDifficultyButton();
                    Debug.Log("[DifficultyUI] Difficulty button added to lobby.");
                }
            }

            if (lobbyPage != null && PhotonNetwork.InRoom)
            {
                var existingLabel = FindObjectOfType<DifficultyLabelUI>();

                // Vérifie si l'objet existe encore mais n'est plus rattaché au HUD Canvas (ex: scene reload)
                bool needsRecreate = existingLabel == null || existingLabel.label == null || existingLabel.label.transform.parent == null;

                if (needsRecreate)
                {
                    if (existingLabel != null)
                    {
                        Destroy(existingLabel.gameObject); // Clean l'ancien label si besoin
                    }

                    var go = new GameObject("DifficultyLabelUI");
                    var view = go.AddComponent<PhotonView>();
                    go.AddComponent<DifficultyLabelUI>();
                    DontDestroyOnLoad(go);
                    Debug.Log("[DifficultyLabelUI] Nouveau label instancié.");
                }
            }

            //if (Input.GetKeyDown(KeyCode.F7))
            //{
            //    //SlotAssetLoader.ShowSlotMachineUI();
            //    SlotsChaos.ProgressBar.LoadProgressAsset();
            //}
        }


        public string GetComponentInfo(GameObject go)
        {
            if (go == null) return "None";
            var components = go.GetComponents<MonoBehaviour>();
            if (components.Length == 0) return "No MonoBehaviours";

            System.Text.StringBuilder sb = new();
            foreach (var comp in components)
            {
                if (comp != null && !(comp is PhotonView))
                {
                    sb.Append(comp.GetType().Name).Append(", ");
                }
            }
            return sb.Length > 0 ? sb.ToString(0, sb.Length - 2) : "None";
        }


        [HarmonyPatch(typeof(LevelGenerator))]
        internal static class PunManagerPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("GenerateDone")]
            private static void Start_Postfix(PunManager __instance)
            {
                
                //WaitForLevelGenerator();
                if (PhotonNetwork.IsMasterClient)
                {
                    foreach (var item in EnemyDirector.instance.enemiesSpawned)
                    {
                        switch (DifficultyManager.CurrentDifficulty)
                        {
                            case DifficultyManager.DifficultyLevel.Custom:
                                item.Enemy.Health.healthCurrent = item.Enemy.Health.healthCurrent * DifficultyManager.MultiplierEnemyLife;
                                item.Enemy.Health.health = item.Enemy.Health.health * DifficultyManager.MultiplierEnemyLife;   
                                break;
                            case DifficultyManager.DifficultyLevel.Normal:
                                item.Enemy.Health.healthCurrent = item.Enemy.Health.healthCurrent * 100;
                                item.Enemy.Health.health = item.Enemy.Health.health * 100;
                                break;
                            case DifficultyManager.DifficultyLevel.Hard:
                                item.Enemy.Health.healthCurrent = item.Enemy.Health.healthCurrent * 1;
                                item.Enemy.Health.health = item.Enemy.Health.health * 1;
                                break;
                            case DifficultyManager.DifficultyLevel.Hardcore:
                                item.Enemy.Health.healthCurrent = item.Enemy.Health.healthCurrent * 2;
                                item.Enemy.Health.health = item.Enemy.Health.health * 2;
                                break;
                            case DifficultyManager.DifficultyLevel.Nightmare:
                                item.Enemy.Health.healthCurrent = item.Enemy.Health.healthCurrent * 2;
                                item.Enemy.Health.health = item.Enemy.Health.health * 2;
                                break;
                            case DifficultyManager.DifficultyLevel.IsThatEvenPossible:
                                item.Enemy.Health.healthCurrent = item.Enemy.Health.healthCurrent * 3;
                                item.Enemy.Health.health = item.Enemy.Health.health * 3;
                                break;
                            case DifficultyManager.DifficultyLevel.CrazyMonster:
                                item.Enemy.Health.healthCurrent = item.Enemy.Health.healthCurrent * 100;
                                item.Enemy.Health.health = item.Enemy.Health.health * 100;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        } 



        //Commande DEVS

        public static void ListHUDObjects()
        {
            GameObject hudCanvas = GameObject.Find("HUD Canvas");
            if (hudCanvas == null)
            {
                Debug.LogError("[HUD Explorer] HUD Canvas introuvable !");
                return;
            }

            Debug.Log("[HUD Explorer] --- Objets enfants du HUD Canvas ---");
            ListChildrenRecursive(hudCanvas.transform, 0);
        }

        private static void ListChildrenRecursive(Transform parent, int indent)
        {
            string indentStr = new string(' ', indent * 2);
            Debug.Log($"{indentStr}- {parent.name}");

            foreach (Transform child in parent)
            {
                ListChildrenRecursive(child, indent + 1);
            }
        }

        private void LogAllGameObjectsInScene()
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.ToLower().Contains("lobby"))
                {
                    Debug.Log($"[Debug Find] Found: {obj.name} | ActiveInHierarchy: {obj.activeInHierarchy} | Path: {GetGameObjectPath(obj)}");
                }
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            Transform current = obj.transform;
            while (current.parent != null)
            {
                current = current.parent;
                path = "/" + current.name + path;
            }
            return path;
        }


        // Trouver tous les PhotonView actifs
        //var photonViews = FindObjectsOfType<PhotonView>();
        //if (photonViews.Length != lastPhotonViews.Length)
        //{
        //    // Loguer les nouveaux ViewID
        //    foreach (var view in photonViews)
        //    {
        //        if (view == null || view.ViewID == 0) continue;

        //        bool isNew = !System.Array.Exists(lastPhotonViews, v => v != null && v.ViewID == view.ViewID);
        //        if (isNew)
        //        {
        //            GameObject go = view.gameObject;
        //            string objectName = go != null ? go.name : "Unknown";
        //            string prefabName = view.name ?? "None";
        //            string owner = view.Owner != null ? $"Player {view.Owner.ActorNumber} ({view.Owner.NickName})" : "Room (Scene)";
        //            string componentInfo = GetComponentInfo(go);

        //            Log.LogInfo($"[ViewIDLogger] Assigned ViewID: {view.ViewID} | Object: {objectName} | Prefab: {prefabName} | Owner: {owner} | Components: {componentInfo}");
        //        }
        //    }

        //    // Mettre à jour la liste des ViewID connus
        //    lastPhotonViews = photonViews;

        //    // Loguer le nombre total d'objets réseau
        //    Log.LogInfo($"[ViewIDMonitor] Current networked objects: {PhotonNetwork.ViewCount}");
        //}

        //    // Créer un GameObject pour RoomCullingManager
        //    GameObject cullingManagerObject = new GameObject("RoomCullingManager");
        //    DontDestroyOnLoad(cullingManagerObject); // Persister entre les scènes
        //    RoomCullingManager cullingManager = cullingManagerObject.AddComponent<RoomCullingManager>();

        //    // Trouver le joueur
        //    GameObject player = GameObject.FindGameObjectWithTag("Player"); // À adapter selon R.E.P.O.

        //    if (player == null)
        //    {
        //        Debug.LogError("[DifficultyFeaturePlugin] Failed to find Player.");
        //        return;
        //    }

        //    // Initialiser RoomCullingManager
        //    cullingManager.Initialize(player);
        //}
    }
}