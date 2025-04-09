using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MyMOD;
using Photon.Pun;
using System;
using UnityEngine;

namespace DifficultyFeature
{
    [BepInPlugin("SK0R3N.DifficultyFeature", "DifficultyFeature", "1.0")]
    public class DifficultyFeature : BaseUnityPlugin
    {
        internal static DifficultyFeature Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger => Instance._logger;
        private ManualLogSource _logger => base.Logger;
        internal Harmony? Harmony { get; set; }

        public static int DifficultyLevel { get; set; } = 1;

        private void Awake()
        {
            Instance = this;

            // Prevent the plugin from being deleted
            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;
            //var go = new GameObject("DifficultyNetworkManager");
            //go.AddComponent<DifficultyNetworkManager>();

            //DifficultyNetwork.Init();

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

            if (lobbyPage != null && PhotonNetwork.InRoom)
            {
                var existingLabel = GameObject.FindObjectOfType<DifficultyLabelUI>();

                // Vérifie si l'objet existe encore mais n'est plus rattaché au HUD Canvas (ex: scene reload)
                bool needsRecreate = existingLabel == null || existingLabel.label == null || existingLabel.label.transform.parent == null ;

                if (needsRecreate)
                {
                    if (existingLabel != null)
                    {
                        GameObject.Destroy(existingLabel.gameObject); // Clean l'ancien label si besoin
                    }

                    var go = new GameObject("DifficultyLabelUI");
                    var view = go.AddComponent<PhotonView>();
                    go.AddComponent<DifficultyLabelUI>();
                    GameObject.DontDestroyOnLoad(go);
                    Debug.Log("[DifficultyLabelUI] Nouveau label instancié.");
                }
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                LogAllGameObjectsInScene();
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