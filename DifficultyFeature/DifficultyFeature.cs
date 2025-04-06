using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MyMOD;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

        public static int DifficultyLevel { get; set; } = 1;

        private void Awake()
        {
            Instance = this;

            // Prevent the plugin from being deleted
            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;
            SlotAssetLoader.LoadSlotAsset();
            //SlotEventManager.RegisterEvent(new EnemyRainEvent());
            //SlotEventManager.RegisterEvent(new AlarmEvent());
            //SlotEventManager.RegisterEvent(new VideoMapEvent());
            SlotEventManager.RegisterEvent(new NoMinimap());
            string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "video");
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            request1 = bundle.LoadAssetAsync<VideoClip>("Clash");
            request2 = bundle.LoadAssetAsync<VideoClip>("Undertale");
            request3 = bundle.LoadAssetAsync<VideoClip>("minecraft");

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
                var all = GameObject.FindObjectsOfType<GameObject>();
                foreach (var obj in all)
                {
                    if (obj.scene.IsValid() && !seenObjects.Contains(obj.name))
                    {
                        if (obj.name.Contains("enemy", StringComparison.OrdinalIgnoreCase) || obj.name.Contains("head", StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[SpawnLogger] Spawned: {obj.name} | Active: {obj.activeInHierarchy}");
                        }
                        seenObjects.Add(obj.name);
                    }
                }
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