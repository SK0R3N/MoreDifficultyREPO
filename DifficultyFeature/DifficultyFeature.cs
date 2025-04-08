using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MyMOD;
using Photon.Pun;
using REPOLib.Modules;
using REPOLib.Objects;
using Steamworks.Ugc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;
using static DifficultyFeature.Event;

namespace DifficultyFeature
{
    [BepInPlugin("SK0R3N.DifficultyFeature", "DifficultyFeature", "1.0")]
    public class DifficultyFeature : BaseUnityPlugin
    {
        internal static DifficultyFeature Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger => Instance._logger;
        private ManualLogSource _logger => base.Logger;
        internal Harmony? Harmony { get; set; }
        internal static AssetBundleRequest request1 { get; set; }
        internal static AssetBundleRequest request2 { get; set; }
        internal static AssetBundleRequest request3 { get; set; }
        private static CustomPrefabPool _customPool;

        public static int DifficultyLevel { get; set; } = 1;

        private void Awake()
        {
            Instance = this;

            // Prevent the plugin from being deleted
            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;
            SlotAssetLoader.LoadSlotAsset();

            //SlotEventManager.RegisterEvent(new GoldenGunEvent());
            //SlotEventManager.RegisterEvent(new RevealMapEvent());
            //SlotEventManager.RegisterEvent(new RandomTeleportEvent());
            //SlotEventManager.RegisterEvent(new TimeSlowEvent());
            SlotEventManager.RegisterEvent(new SurviveHorror());
            string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "video");
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            request1 = bundle.LoadAssetAsync<VideoClip>("Clash");
            request2 = bundle.LoadAssetAsync<VideoClip>("Undertale");
            request3 = bundle.LoadAssetAsync<VideoClip>("minecraft");

            string bundlePath2 = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "goldengun");
            AssetBundle bundle2 = AssetBundle.LoadFromFile(bundlePath2);

            GameObject goldenGunPrefab = bundle2.LoadAsset<GameObject>("Golden_Gun");
            Item item = bundle2.LoadAsset<Item>("Golden_Gun.asset");

            REPOLib.Modules.Items.RegisterItem(item);

            Patch();

            Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
        }

        private IEnumerator RegisterGoldenGunWhenReady(GameObject goldenGunPrefab)
        {
            while (!(PhotonNetwork.PrefabPool.GetType().Name.Contains("CustomPrefabPool")))
            {
                Debug.Log("[GoldenGun] Waiting for CustomPrefabPool...");
                yield return null;
            }

            // Enregistrement du prefab dans CustomPrefabPool
            var registerMethod = PhotonNetwork.PrefabPool.GetType().GetMethod("RegisterPrefab");
            if (registerMethod != null)
            {
                registerMethod.Invoke(PhotonNetwork.PrefabPool, new object[] { "Items/Golden Gun", goldenGunPrefab });
                Debug.Log("[GoldenGun] Prefab enregistré avec succès !");
            }
            else
            {
                Debug.LogError("[GoldenGun] Méthode RegisterPrefab introuvable !");
            }

            // Enregistrement de l'item dans les systèmes

            Debug.Log("[GoldenGun] Item enregistré avec succès !");
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
        private HashSet<string> seenObjects = new();

        private void Update()
        {
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


            if (Input.GetKeyDown(KeyCode.F5))
            {
                ListHUDObjects();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                foreach (GameObject go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go.activeInHierarchy)
                    {
                        Debug.Log($"[Debug] Active GameObject: {go.name} | Parent: {go.transform.GetComponentFastPath}");
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {

                SlotAssetLoader.ShowSlotMachineUI();
            }
        }

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
    }
}