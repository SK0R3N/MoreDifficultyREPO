using BepInEx;
using BepInEx.Logging;
using ExitGames.Client.Photon;
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
        private HashSet<string> seenObjects = new();
        private bool subscribed = false;


        public static int DifficultyLevel { get; set; } = 1;

        private void Start()
        {
            if (!subscribed && PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
                subscribed = true;
                Debug.Log("[WalkiePlugin] Event listener registered from Start()");
            }
        }

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
            //SlotEventManager.RegisterEvent(new SurviveHorror());
            //SlotEventManager.RegisterEvent(new BetterWalkieTakkie());
            //SlotEventManager.RegisterEvent(new AlarmEvent());
            //SlotEventManager.RegisterEvent(new MarioStarEvent());
            //SlotEventManager.RegisterEvent(new ExtractionPointHaulModifier());
            //SlotEventManager.RegisterEvent(new RevivePlayerEvent());
            SlotEventManager.RegisterEvent(new ExplosiveDeathEvent());
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


            PlayerAvatar playerAvatar = PlayerAvatar.instance;
            if (playerAvatar.mapToolController.Active && Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (BetterWalkieTakkie.instance != null)
                {
                    //DisableAllMapGeometry();
                    BetterWalkieTakkie.instance.ToggleWalkie(true);
                }

            }
            if (playerAvatar.mapToolController.Active && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (BetterWalkieTakkie.instance != null)
                {
                    BetterWalkieTakkie.instance.ToggleWalkie(false);
                    //ActivateAllMapGeometry();
                }
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                SlotAssetLoader.ShowSlotMachineUI();
            }
        }

        private void OnDestroy()
        {
            if (subscribed && PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
                Debug.Log("[WalkiePlugin] Event listener unregistered from OnDestroy()");
            }
        }

        //Plugin Methode
        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case 103: // Volume voix
                    if (photonEvent.CustomData is object[] data1 && data1.Length == 2)
                    {
                        int viewID = (int)data1[0];
                        float volume = (float)data1[1];

                        if (WalkieRegistry.ActiveWalkieUsers.Contains(viewID))
                        {
                            PhotonView view = PhotonView.Find(viewID);
                            if (view != null && view.TryGetComponent(out WalkieReceiver receiver))
                            {
                                receiver.SetVolume(volume * 2f);
                            }
                        }
                    }
                    break;

                case 104: // Sync état ON/OFF
                    if (photonEvent.CustomData is object[] data2 && data2.Length == 2)
                    {
                        int viewID = (int)data2[0];
                        bool enabled = (bool)data2[1];

                        if (enabled)
                            WalkieRegistry.ActiveWalkieUsers.Add(viewID);
                        else
                            WalkieRegistry.ActiveWalkieUsers.Remove(viewID);

                        Debug.Log($"[WalkieNet] Walkie state updated: ViewID {viewID} -> {(enabled ? "ON" : "OFF")}");
                    }
                    break;

                case 1:
                        Debug.Log($"[AlarmEventHandler] Event received with code: {photonEvent.Code}");

                        if (photonEvent.Code != 1) return;

                        object[] data = (object[])photonEvent.CustomData;
                        if (data == null || data.Length < 2)
                        {
                            Debug.LogError("[AlarmEventHandler] Invalid event data received.");
                            return;
                        }

                        int viewId = (int)data[0];
                        float duration = (float)data[1];

                        PhotonView photonView = PhotonView.Find(viewId);
                        if (photonView == null)
                        {
                            Debug.LogError($"[AlarmEventHandler] ViewID {viewId} not found.");
                            return;
                        }

                        GameObject target = photonView.gameObject;
                        PlayerAvatar avatar = target.GetComponent<PlayerAvatar>();
                        if (avatar == null)
                        {
                            Debug.LogError("[AlarmEventHandler] No PlayerAvatar on target object.");
                            return;
                        }

                        Debug.Log($"[AlarmEventHandler] Event processed for ViewID {viewId}, Duration: {duration}");
                        AlarmEvent.AlarmEffectController.Trigger(avatar, duration);
                        break;
                case 2:
                    object[] data3 = (object[])photonEvent.CustomData;

                    if (data3 == null || data3.Length < 1)
                    {
                        Debug.LogError("[AlarmEventHandler] Invalid event data received.");
                        return;
                    }

                    int viewIdMario = (int)data3[0];
                    PhotonView photonViewMario = PhotonView.Find(viewIdMario);
                    if (photonViewMario == null)
                    {
                        Debug.LogError($"[AlarmEventHandler] ViewID {viewIdMario} not found.");
                        return;
                    }

                    GameObject targetMario = photonViewMario.gameObject;
                    PlayerAvatar avatarMario = targetMario.GetComponent<PlayerAvatar>();

                    if (avatarMario == null)
                    {
                        Debug.LogError("[AlarmEventHandler] No PlayerAvatar on target object.");
                        return;
                    }

                    // Démarrer la coroutine sur le GameObject du joueur
                    Debug.Log("[AlarmEventHandler] Starting RPC_PlayMarioStarSound coroutine.");
                    avatarMario.StartCoroutine(MarioStarEvent.MarioStarPower.RPC_PlayMarioStarSound(avatarMario));
                    break;


            }
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

        public static void DisableAllMapGeometry()
        {
            if (Map.Instance == null) return;

            // Liste de tous les objets à désactiver
            var map = Map.Instance;

            map.FloorObject1x1?.SetActive(false);
            map.FloorObject1x1Diagonal?.SetActive(false);
            map.FloorObject1x1Curve?.SetActive(false);
            map.FloorObject1x1CurveInverted?.SetActive(false);

            map.FloorObject1x05?.SetActive(false);
            map.FloorObject1x05Diagonal?.SetActive(false);
            map.FloorObject1x05Curve?.SetActive(false);
            map.FloorObject1x05CurveInverted?.SetActive(false);

            map.FloorObject1x025?.SetActive(false);
            map.FloorObject1x025Diagonal?.SetActive(false);

            map.RoomVolume?.SetActive(false);
            map.RoomVolumeOutline?.SetActive(false);

            map.FloorTruck?.SetActive(false);
            map.WallTruck?.SetActive(false);

            map.FloorUsed?.SetActive(false);
            map.WallUsed?.SetActive(false);

            map.FloorInactive?.SetActive(false);
            map.WallInactive?.SetActive(false);

            map.Wall1x1Object?.SetActive(false);
            map.Wall1x1DiagonalObject?.SetActive(false);
            map.Wall1x1CurveObject?.SetActive(false);

            map.Wall1x05Object?.SetActive(false);
            map.Wall1x05DiagonalObject?.SetActive(false);
            map.Wall1x05CurveObject?.SetActive(false);

            map.Wall1x025Object?.SetActive(false);
            map.Wall1x025DiagonalObject?.SetActive(false);

            map.Door1x1Object?.SetActive(false);
            map.Door1x05Object?.SetActive(false);
            map.Door1x1DiagonalObject?.SetActive(false);
            map.Door1x05DiagonalObject?.SetActive(false);
            map.Door1x2Object?.SetActive(false);
            map.Door1x1WizardObject?.SetActive(false);
            map.Door1x1ArcticObject?.SetActive(false);

            map.DoorBlockedObject?.SetActive(false);
            map.DoorBlockedWizardObject?.SetActive(false);
            map.DoorBlockedArcticObject?.SetActive(false);
            map.DoorDiagonalObject?.SetActive(false);

            map.StairsObject?.SetActive(false);


            map.EnemyObject?.SetActive(false);
            map.CustomObject?.SetActive(false);
            map.ValuableObject?.SetActive(false);

            foreach (var i in map.MapModules)
            {
                try
                {
                    i.gameObject.SetActive(false);
                }
                catch { }
            }

            foreach (var i in map.Layers)
            {
                try
                {
                    i.gameObject.SetActive(false);
                }
                catch { }
            }
        }

        public static void ActivateAllMapGeometry()
        {
            if (Map.Instance == null) return;

            // Liste de tous les objets à désactiver
            var map = Map.Instance;

            map.EnemyObject?.SetActive(true);
            map.CustomObject?.SetActive(true);
            map.ValuableObject?.SetActive(true);

            map.FloorObject1x1?.SetActive(true);
            map.FloorObject1x1Diagonal?.SetActive(true);
            map.FloorObject1x1Curve?.SetActive(true);
            map.FloorObject1x1CurveInverted?.SetActive(true);

            map.FloorObject1x05?.SetActive(true);
            map.FloorObject1x05Diagonal?.SetActive(true);
            map.FloorObject1x05Curve?.SetActive(true);
            map.FloorObject1x05CurveInverted?.SetActive(true);

            map.FloorObject1x025?.SetActive(true);
            map.FloorObject1x025Diagonal?.SetActive(true);

            map.RoomVolume?.SetActive(true);
            map.RoomVolumeOutline?.SetActive(true);

            map.FloorTruck?.SetActive(true);
            map.WallTruck?.SetActive(true);

            map.FloorUsed?.SetActive(true);
            map.WallUsed?.SetActive(true);

            map.FloorInactive?.SetActive(true);
            map.WallInactive?.SetActive(true);

            map.Wall1x1Object?.SetActive(true);
            map.Wall1x1DiagonalObject?.SetActive(true);
            map.Wall1x1CurveObject?.SetActive(true);

            map.Wall1x05Object?.SetActive(true);
            map.Wall1x05DiagonalObject?.SetActive(true);
            map.Wall1x05CurveObject?.SetActive(true);

            map.Wall1x025Object?.SetActive(true);
            map.Wall1x025DiagonalObject?.SetActive(true);

            map.Door1x1Object?.SetActive(true);
            map.Door1x05Object?.SetActive(true);
            map.Door1x1DiagonalObject?.SetActive(true);
            map.Door1x05DiagonalObject?.SetActive(true);
            map.Door1x2Object?.SetActive(true);
            map.Door1x1WizardObject?.SetActive(true);
            map.Door1x1ArcticObject?.SetActive(true);

            map.DoorBlockedObject?.SetActive(true);
            map.DoorBlockedWizardObject?.SetActive(true);
            map.DoorBlockedArcticObject?.SetActive(true);
            map.DoorDiagonalObject?.SetActive(true);

            map.StairsObject?.SetActive(true);


            foreach (var i in map.MapModules)
            {
                try
                {
                    i.gameObject.SetActive(true);
                }
                catch { }
            }

            foreach (var i in map.Layers)
            {
                try
                {
                    i.gameObject.SetActive(true);
                }
                catch { }
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
    }
}