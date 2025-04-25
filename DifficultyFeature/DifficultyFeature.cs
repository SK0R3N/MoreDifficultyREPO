using BepInEx;
using BepInEx.Logging;
using DifficultyFeature.DifficultyUpdate;
using DifficultyFeature.DifficultyUpdate.GenerationRework;
using DifficultyFeature.SlotsChaos;
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
        private RoomCullingManager cullingManager;
        private bool subscribed = false;
        private const byte EVENT_CHECK_WALKIE_WINNER = 6;
        private bool voteUI = true;
        public DateTime startVote;




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
            gameObject.transform.parent = null;
            gameObject.hideFlags = HideFlags.HideAndDontSave;
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
            //SlotEventManager.RegisterEvent(new ExplosiveDeathEvent());
            SlotEventManager.RegisterEvent(new TinyPlayerEvent());

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

            if (PlayerAvatarDeathPatch.voteStart && startVote > startVote.AddMinutes(1))
            {
                if(VoteSlotsUI.vote.Count == 0)
                {
                    foreach (var item in GameDirector.instance.PlayerList)
                    {
                        VoteSlotsUI.vote.Add(item.playerName);
                    }
                }

                VoteSlotsUI.ExecuteVote();
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

            if (Input.GetKeyDown(KeyCode.F5))
            {
                SlotAssetLoader.ShowSlotMachineUI();
            }

            PlayerAvatar playerAvatar = PlayerAvatar.instance;
            if (playerAvatar.mapToolController.Active && Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (BetterWalkieTakkie.instance != null)
                {
                    //DisableAllMapGeometry();
                    BetterWalkieTakkie.instance.ToggleWalkie(true);
                }
                else
                {
                    // BetterWalkieTakkie est null, demander à l'host de vérifier
                    RequestWalkieWinnerCheck();
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

            //if (Input.GetKeyDown(KeyCode.F7))
            //{
            //    //SlotAssetLoader.ShowSlotMachineUI();
            //    SlotsChaos.ProgressBar.LoadProgressAsset();
            //}
        }

        private static void WaitForLevelGenerator()
        {
            Debug.LogError("WaitForLevelGenerator");
            while (LevelGenerator.Instance == null)
            {
                Debug.LogError("LevelGen Non trouver");
                return;
            }
            GameObject managerObject = new GameObject("TileActivationManager");
            managerObject.AddComponent<TileActivationManager>();
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
                case 105: // Gagnant du walkie
                    BetterWalkieTakkie.HandleWinnerEvent(photonEvent);
                    break;
                case EVENT_CHECK_WALKIE_WINNER: // Vérification du gagnant
                    if (photonEvent.CustomData is object[] data6 && data6.Length == 2)
                    {
                        int viewID = (int)data6[0];
                        string steamID = (string)data6[1];

                        if (PhotonNetwork.IsMasterClient)
                        {
                            string saveFileName = DifficultySaveContext.CurrentSaveFileName;
                            HashSet<string> winners = DifficultySaveManager.LoadWalkieWinners(saveFileName);

                            if (winners.Contains(steamID))
                            {
                                object[] content = new object[] { steamID };
                                var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                                PhotonNetwork.RaiseEvent(7, content, options, SendOptions.SendReliable);
                            }
                            else
                            {
                                Debug.Log($"[DifficultyFeature] SteamID={steamID} n'est pas dans la liste des gagnants.");
                            }
                        }
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
                case 3:
                    object[] data4 = (object[])photonEvent.CustomData;

                    if (data4 == null || data4.Length < 1)
                    {
                        Debug.LogError("[AlarmEventHandler] Invalid event data received.");
                        return;
                    }

                    int viewIdTiny = (int)data4[0];
                    PhotonView photonViewTiny = PhotonView.Find(viewIdTiny);
                    if (photonViewTiny == null)
                    {
                        Debug.LogError($"[AlarmEventHandler] ViewID {viewIdTiny} not found.");
                        return;
                    }

                    GameObject targetTiny = photonViewTiny.gameObject;
                    PlayerAvatar avatarTiny = targetTiny.GetComponent<PlayerAvatar>();

                    if (avatarTiny == null)
                    {
                        Debug.LogError("[AlarmEventHandler] No PlayerAvatar on target object.");
                        return;
                    }

                    // Démarrer la coroutine sur le GameObject du joueur
                    Debug.Log("[AlarmEventHandler] Starting RPC_PlayMarioStarSound coroutine.");
                    avatarTiny.StartCoroutine(TinyPlayerEvent.TinyPlayerManager.ApplyTinyEffectRPC(avatarTiny));
                    break;
                case 4:
                    object[] data5 = (object[])photonEvent.CustomData;

                    if (data5 == null || data5.Length < 1)
                    {
                        Debug.LogError("[AlarmEventHandler] Invalid event data received.");
                        return;
                    }

                    int viewIdTinyRevert = (int)data5[0];
                    PhotonView photonViewTinyRevert = PhotonView.Find(viewIdTinyRevert);
                    if (photonViewTinyRevert == null)
                    {
                        Debug.LogError($"[AlarmEventHandler] ViewID {viewIdTinyRevert} not found.");
                        return;
                    }

                    GameObject targetTinyRevert = photonViewTinyRevert.gameObject;
                    PlayerAvatar avatarTinyRevert = targetTinyRevert.GetComponent<PlayerAvatar>();

                    if (avatarTinyRevert == null)
                    {
                        Debug.LogError("[AlarmEventHandler] No PlayerAvatar on target object.");
                        return;
                    }

                    Debug.Log("[AlarmEventHandler] Starting RPC_PlayMarioStarSound coroutine.");
                    avatarTinyRevert.StartCoroutine(TinyPlayerEvent.TinyPlayerManager.RevertTinyEffectRPC(avatarTinyRevert));
                    break;
                case 5: // Gagnant du walkie
                    BetterWalkieTakkie.HandleWinnerEvent(photonEvent);
                    break;
                case 7: // Gagnant du walkie
                    if (photonEvent.CustomData is object[] dataWalkie && dataWalkie.Length == 1)
                    {
                        string steamId = (string)dataWalkie[0];
                        ExecuteWalkieEvent(steamId);
                    }

                    break;
                case 8:
                    Debug.Log(photonEvent.CustomData);

                    if (photonEvent.CustomData is object[] dataVote && dataVote.Length == 2)
                    {
                        int viewIDVote = (int)dataVote[0];
                        string Vote = (string)dataVote[1];

                        PlayerAvatar avatarVote = PlayerAvatar.instance;
                        if (avatarVote == null)
                        {
                            Debug.Log($"[DifficultyFeature] avatarVote failed.");
                            return;
                        }
                        if (VoteSlotsUI.PlayerVote.Contains(avatarVote))
                        {
                            Debug.Log($"[DifficultyFeature] AlreadyVoted.");
                            return;
                        }

                        VoteSlotsUI.PlayerVote.Add(avatarVote);
                        VoteSlotsUI.vote.Add(Vote);
                        if (VoteSlotsUI.PlayerVote.Count >= VoteSlotsUI.voteCountMax)
                            VoteSlotsUI.ExecuteVote();
                    }
                    break;
                case 9:
                    object[] dataVoteExecute = (object[])photonEvent.CustomData;
                    if (dataVoteExecute == null || dataVoteExecute.Length < 1)
                    {
                        return;
                    }
                    string[] VoteExecute = (string[])dataVoteExecute[0];
                    winner(VoteExecute);
                break;
                case 10:
                    object[] dataPoints = (object[])photonEvent.CustomData;
                    int points = (int)dataPoints[0];
                    Debug.Log($"[ProgressBar] Event Raised progress increment event with {points} points.");
                    DifficultySaveManager.SaveProgressBar((int)SlotsChaos.ProgressBar.progress + points);
                    CoroutineRunner.instance.StartCoroutine(SlotsChaos.ProgressBar.UpdateProgress((int)SlotsChaos.ProgressBar.progress + points));

                    object[] contentPoints = new object[] { SlotsChaos.ProgressBar.progress };
                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                    PhotonNetwork.RaiseEvent(11, contentPoints, raiseEventOptions, SendOptions.SendReliable);
                    break;
                case 11:
                    if(!PhotonNetwork.IsMasterClient)
                    {
                        object[] dataPointsUpdate = (object[])photonEvent.CustomData;
                        int pointsUpdate = (int)dataPointsUpdate[0];
                        SlotsChaos.ProgressBar.UpdateProgress(pointsUpdate);
                    }
                    break;
                case 12:
                    object[] dataProgress = (object[])photonEvent.CustomData;
                    SlotsChaos.ProgressBar.progress = (float)dataProgress[0];
                    break;
            }
        }

        [HarmonyPatch(typeof(LevelGenerator))]
        internal static class PunManagerPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("GenerateDone")]
            private static void Start_Postfix(PunManager __instance)
            {
                if(SlotsChaos.ProgressBar.currentInstance != null)
                {
                    SlotsChaos.ProgressBar.progress = 0;
                }
                
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
                            default:
                                break;
                        }
                    }


                    object[] contentPoints = new object[] { DifficultySaveManager.LoadProgressBar() };
                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                    PhotonNetwork.RaiseEvent(12, contentPoints, raiseEventOptions, SendOptions.SendReliable);
                }

                if(DifficultyManager.SlotsActive)
                SlotsChaos.ProgressBar.LoadProgressAsset();;
            }
        } 

        private void winner(string[] vote)
        {
            CoroutineRunner.instance.StartCoroutine(WinnerVote(vote));
        }

        private IEnumerator WinnerVote(string[] vote)
        {
            Debug.Log(vote[0]);
            List<string> Quote = new List<string>();

            if (vote.Count() == 1)
            {
                Quote = new List<string>() { "Death has spoken. It's " + vote[0] + " who has won!", vote[0] + " has earned the chance to play the Slot of Chaos." };
            }
            else if (vote.Count() == 2)
            {
                Quote = new List<string>() { "Death has spoken. It's " + vote[0] + " and " + vote[1] + " who have won!", vote[0] + " and " + vote[1] + " have earned the chance to play the Slot of Chaos." };
            }
            else if (vote.Count() == 3)
            {
                Quote = new List<string>() { "Death has spoken. It's " + vote[0] + ", " + vote[1] + ", and " + vote[2] + " who have won!", vote[0] + ", " + vote[1] + ", and " + vote[2] + " have earned the chance to play the Slot of Chaos." };
            }
            else if (vote.Count() == 4)
            {
                Quote = new List<string>() { "Death has spoken. It's " + vote[0] + ", " + vote[1] + ", " + vote[2] + ", and " + vote[3] + " who have won!", vote[0] + ", " + vote[1] + ", " + vote[2] + ", and " + vote[3] + " have earned the chance to play the Slot of Chaos." };
            }
            else if (vote.Count() >= 5)
            {
                Quote = new List<string>() { "Death has spoken. It's " + vote[0] + ", " + vote[1] + ", " + vote[2] + ", " + vote[3] + ", and others who have won!", vote[0] + ", " + vote[1] + ", " + vote[2] + ", " + vote[3] + ", and others have earned the chance to play the Slot of Chaos." };
            }
            else
            {
                Quote = new List<string>() { "How is that even possible? No one has won the Slot of Chaos..." };
            }

            Debug.Log(Quote[0]);
            bool isSemiBotTalkComplete = false;
            GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(Quote), 0.3f, () => isSemiBotTalkComplete = true);
            yield return new WaitUntil(() => isSemiBotTalkComplete);

            foreach (var vote2 in vote)
            {
                Debug.Log(vote2);
                if (vote2 == PlayerAvatar.instance.playerName)
                {
                    SlotAssetLoader.ShowSlotMachineUI();
                }
            }
        }

        private void ExecuteWalkieEvent(string steamID)
        {
            PlayerAvatar player = PlayerAvatar.instance;
            if (steamID.ToString() != SemiFunc.PlayerGetSteamID(player))
            {
                Debug.LogError($"[DifficultyFeature] PlayerAvatar non trouvé pour ViewID={steamID}.");
                return;
            }
            // Exécuter l'événement
            BetterWalkieTakkie t = new BetterWalkieTakkie();
            t.Execute();
            t.ToggleWalkie(true);
            Debug.Log($"[DifficultyFeature] Événement BetterWalkieTakkie exécuté pour {steamID}.");
        }

        private void RequestWalkieWinnerCheck()
        {
            PlayerAvatar playerAvatar = PlayerAvatar.instance;
            if (playerAvatar == null) return;

            string steamID = SemiFunc.PlayerGetSteamID(playerAvatar);
            if (string.IsNullOrEmpty(steamID))
            {
                Debug.LogError("[DifficultyFeature] Impossible d'obtenir le SteamID du joueur.");
                return;
            }

            PhotonView view = playerAvatar.GetComponent<PhotonView>();
            if (view == null)
            {
                Debug.LogError("[DifficultyFeature] PhotonView introuvable sur PlayerAvatar.");
                return;
            }

            object[] content = new object[] { view.ViewID, steamID };
            var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
            PhotonNetwork.RaiseEvent(EVENT_CHECK_WALKIE_WINNER, content, options, SendOptions.SendReliable);
            Debug.Log($"[DifficultyFeature] Demande envoyée à l'host pour vérifier le gagnant: SteamID={steamID}, ViewID={view.ViewID}");
        }

        private void OnDestroy()
        {
            if (subscribed && PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
                Debug.Log("[WalkiePlugin] Event listener unregistered from OnDestroy()");
            }
        }

        private IEnumerator RegisterGoldenGunWhenReady(GameObject goldenGunPrefab)
        {
            while (!PhotonNetwork.PrefabPool.GetType().Name.Contains("CustomPrefabPool"))
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