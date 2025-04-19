using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MyMOD;
using Photon.Pun;
using SingularityGroup.HotReload;
using System;
using System.Linq;
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
        private RoomCullingManager cullingManager;

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
                var existingLabel = GameObject.FindObjectOfType<DifficultyLabelUI>();

                // Vérifie si l'objet existe encore mais n'est plus rattaché au HUD Canvas (ex: scene reload)
                bool needsRecreate = existingLabel == null || existingLabel.label == null || existingLabel.label.transform.parent == null;

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
                Logger.LogInfo("[ListViewIDsCommand] Listing all assigned ViewIDs...");

                // Get all PhotonViews in the scene
                PhotonView[] photonViews2 = PhotonNetwork.PhotonViews;

                if (photonViews2 == null || photonViews2.Length == 0)
                {
                    Logger.LogWarning("[ListViewIDsCommand] No PhotonViews found in the scene.");
                    return;
                }

                int viewCount = 0;
                foreach (PhotonView view in photonViews2)
                {
                    if (view != null && view.gameObject != null)
                    {
                        string objectName = view.gameObject.name ?? "Unknown";
                        string prefabName = view.name ?? "Unknown";
                        string owner = view.Owner != null ? view.Owner.NickName : "Room (Scene)";
                        string components = string.Join(", ", view.gameObject.GetComponents<Component>().Select(c => c.GetType().Name));

                        Logger.LogInfo($"[ListViewIDsCommand] ViewID: {view.ViewID} | Object: {objectName} | Prefab: {prefabName} | Owner: {owner} | Components: {components}");
                        viewCount++;
                    }
                }


                Logger.LogInfo($"[ListViewIDsCommand] Total ViewIDs listed: {viewCount} | Current networked objects: {photonViews2.Length}");
            }
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