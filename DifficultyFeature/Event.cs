using Audial.Utils;
using BepInEx;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice;
using POpusCodec.Enums;
using REPOLib.Extensions;
using REPOLib.Modules;
using SingularityGroup.HotReload;
using Steamworks;
using Steamworks.Ugc;
using System;
using System.Collections;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.Video;
using static DifficultyFeature.Event;



namespace DifficultyFeature
{
    internal class Event
    {
        //Non Event
        public class GenerateText
        {
            public static IEnumerable SemiBotTalk(string message, Color possessColor, float typingspeed = 1f)
            {
                ChatManager.instance.PossessChatScheduleStart(10);
                ChatManager.instance.PossessChat(ChatManager.PossessChatID.LovePotion, message, typingspeed, possessColor);
                ChatManager.instance.PossessChatScheduleEnd();

                yield return new WaitForSeconds(5F);
            }

            public static IEnumerable SemiBotTalk(string message, float typingspeed = 1f)
            {
                ChatManager.instance.PossessChatScheduleStart(10);
                Color possessColor = new Color(1f, 0.3f, 0.6f, 1f);
                ChatManager.instance.PossessChat(ChatManager.PossessChatID.LovePotion, message, typingspeed, possessColor);
                ChatManager.instance.PossessChatScheduleEnd();

                yield return new WaitForSeconds(5F);
            }

            public static string GenerateAffectionateSentence(List<string> listsentence)
            {
                string text = listsentence[UnityEngine.Random.Range(0, listsentence.Count)]; ;
                return text;

            }
        }


        //Event

        public class TinyPlayerEvent : MonoBehaviour, ISlotEvent
        {
            public string EventName => "TinyPlayer";
            public string IconName => "icon_tiny_player";
            public string Asset => "TinyPlayerAsset";

            private static bool isActive = false;
            private static float duration = 60f; // Durée de l'effet : 60 secondes
            private static float scaleFactor = 0.2f; // Taille réduite
            private static float jumpMultiplier = 3f; // Sauts 3x plus hauts
            private static float voicePitch = 1.5f; // Voix plus aiguë
            private static float cameraOffsetY = -1.2f; // Décalage caméra pour joueurs minuscules
            private static float checkInterval = 0.1f;


            private void Awake()
            {
                DontDestroyOnLoad(gameObject);
                Debug.Log("[TinyPlayerEvent] Initialized.");
            }

            public void Execute()
            {


                if (isActive)
                {
                    Debug.Log("[TinyPlayerEvent] Already active.");
                    return;
                }

                if (GameDirector.instance == null || GameDirector.instance.PlayerList == null)
                {
                    Debug.LogError("[TinyPlayerEvent] GameDirector or PlayerList is null.");
                    return;
                }

                List<string> TinyEvent = new List<string> { "I got a little mushroom, does it make me stronger? Nope, but I’m taking it anyway.",
                    "They told me to think big. I saw a mushroom… error 0_0.",
                    "I thought it was a power-up… now the doors are too big and my dignity’s too small.",
                    "Congratulations! You’ve unlocked Mini Mode. Zero benefits. Enjoy.",
                    "Power-up? Nope, just a mushroom that made me shrink. My confidence too, by the way.",
                    "I tried to jump on the mushroom, but it jumped me instead. Game over." };

                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(TinyEvent));

                isActive = true;
                GameObject managerObj = new GameObject("TinyPlayerManager");
                var manager = managerObj.AddComponent<TinyPlayerManager>();
                manager.Initialize(this, duration);
                Debug.Log($"[TinyPlayerEvent] Started for {duration} seconds.");
            }

            public class TinyPlayerManager : MonoBehaviour
            {
                private TinyPlayerEvent parentEvent;
                public static PhotonView photonView;
                private static AudioClip tinySound;
                public static AssetBundle AssetBundle { get; set; }

                public void Initialize(TinyPlayerEvent parent, float duration)
                {
                    parentEvent = parent;
                    DontDestroyOnLoad(gameObject);

                    if (SemiFunc.IsMultiplayer())
                    {
                        photonView = gameObject.AddComponent<PhotonView>();
                        photonView.ViewID = PhotonNetwork.AllocateViewID(true);
                        Debug.Log($"[TinyPlayerEvent] PhotonView ID: {photonView.ViewID}");
                    }

                    // Appliquer l'effet à tous les joueurs
                    ApplyTinyEffectToAll(PlayerAvatar.instance);
                }

                private void ApplyTinyEffectToAll(PlayerAvatar avatar)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (photonView != null && photonView.IsMine)
                        {
                            if (PhotonNetwork.InRoom)
                            {
                                PhotonView photonView = avatar.GetComponent<PhotonView>();
                                object[] eventData = new object[] { photonView.ViewID };
                                RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                                PhotonNetwork.RaiseEvent(3, eventData, raiseEventOptions, SendOptions.SendReliable);
                            }
                        }
                        else
                        {
                            ApplyTinyEffectRPC(avatar);
                        }
                    }
                }
                [PunRPC]
                public static IEnumerator ApplyTinyEffectRPC(PlayerAvatar avatar)
                {

                    string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "tiny");
                    if (AssetBundle == null)
                    {
                        AssetBundle = AssetBundle.LoadFromFile(bundlePath);
                    }

                    if (tinySound == null)
                        tinySound = AssetBundle.LoadAsset<AudioClip>("tinymushroom");

                    if (avatar == null || avatar.isDisabled || avatar.deadSet)
                    {
                        Debug.Log($"[TinyPlayerEvent] Skipping player {avatar?.playerName} (null, disabled, or dead).");
                        yield return null;
                    }

                    // Réduire la taille

                    // Jouer le son
                    if (tinySound != null)
                    {
                        AudioSource localAudio = avatar.gameObject.AddComponent<AudioSource>();
                        localAudio.clip = tinySound;
                        localAudio.spatialBlend = 1f;
                        localAudio.loop = false;
                        localAudio.Play();
                        Debug.Log($"[TinyPlayerEvent] Played 'tinymushroom' for {avatar.playerName}");
                    }

                    // Modifier la voix
                    if (avatar.voiceChat != null)
                    {
                        avatar.voiceChat.OverridePitch(1.5f, 1f, 2f, duration);
                        Debug.Log($"[TinyPlayerEvent] Set voice pitch to {voicePitch} for {avatar.playerName}");
                    }

                    // Augmenter les sauts (stocké dans une variable temporaire)
                    avatar.gameObject.AddComponent<TinyJumpModifier>().Initialize(jumpMultiplier);

                    avatar.playerAvatarVisuals.transform.localScale = UnityEngine.Vector3.one * scaleFactor;
                    // Ajuster la camér
                    Debug.Log($"[TinyPlayerEvent] Adjusted camera for {avatar.playerName} to {avatar.mapToolController.VisualTransform.position}");

                    if (avatar.isLocal)
                    {
                        avatar.playerTransform.localScale = UnityEngine.Vector3.one * scaleFactor;
                        avatar.localCamera.transform.position += UnityEngine.Vector3.up * cameraOffsetY;
                        avatar.localCamera.transform.localScale = UnityEngine.Vector3.one * scaleFactor;
                        avatar.playerAvatarCollision.transform.localScale = UnityEngine.Vector3.one * scaleFactor;
                        var visualsTransform = avatar.mapToolController.VisualTransform;
                        PlayerAvatarUpdatePatch.IsTinyPlayerActive = true;
                        foreach (Transform t in avatar.mapToolController.transform.parent.parent)
                        {
                            Debug.Log(t.name);

                            try
                            {
                                Debug.Log($"[TinyPlayerEvent] Adjusted camera for {avatar.playerName} to {t.transform.position}");
                                if (t.name.ToLower() == "map tool")
                                    t.transform.position += UnityEngine.Vector3.up * cameraOffsetY;
                                Debug.Log($"[TinyPlayerEvent] Adjusted camera for {avatar.playerName} to {t.transform.position}");
                            }
                            catch
                            {
                                Debug.Log(t.name);
                            }

                        }

                    }
                    yield return null;
                }

                public static string RevertTinyEffectRPC(PlayerAvatar player)
                {
                    Debug.Log("I am dead 5");
                    if (player == null)
                    {
                        return null;
                    }
                    Debug.Log("I am dead 6");
                    // Restaurer la taille
                    player.playerTransform.localScale = UnityEngine.Vector3.one;
                    if (player.playerAvatarVisuals != null)
                    {
                        player.playerAvatarVisuals.transform.localScale = UnityEngine.Vector3.one;
                    }

                    // Restaurer la caméra
                    if (player.isLocal)
                    {
                        player.localCamera.transform.position -= UnityEngine.Vector3.up * cameraOffsetY;
                        player.playerAvatarCollision.transform.localScale = UnityEngine.Vector3.one * scaleFactor;

                        var visualsTransform = player.mapToolController.VisualTransform;
                        player.localCamera.transform.position -= UnityEngine.Vector3.up * cameraOffsetY;
                        foreach (Transform t in player.mapToolController.transform.parent.parent)
                        {
                            UnityEngine.Debug.Log(t.name);

                            try
                            {
                                if (t.name.ToLower() == "map tool")
                                    t.transform.position -= UnityEngine.Vector3.up * cameraOffsetY;

                            }
                            catch
                            {
                                UnityEngine.Debug.Log(t.name);
                            }

                        }
                        Debug.Log($"[TinyPlayerEvent] Restored camera for {player.playerName}");
                    }

                    // Restaurer la voix
                    if (player.voiceChat != null)
                    {
                        player.voiceChat.OverridePitchCancel();
                        Debug.Log($"[TinyPlayerEvent] Restored voice for {player.playerName}");
                    }

                    // Supprimer le modificateur de saut
                    TinyJumpModifier modifier = player.GetComponent<TinyJumpModifier>();
                    if (modifier != null)
                    {
                        Destroy(modifier);
                    }
                    return null;
                }
            }

            // Composant temporaire pour gérer les sauts
            private class TinyJumpModifier : MonoBehaviour
            {
                private float jumpMultiplier;

                public void Initialize(float multiplier)
                {
                    jumpMultiplier = multiplier;
                }

                private void OnEnable()
                {
                    PlayerAvatar player = GetComponent<PlayerAvatar>();
                    if (player != null)
                    {
                        Debug.Log($"[TinyPlayerEvent] Jump modifier added for {player.playerName}");
                    }
                }

                // Appelé via reflection ou patching dans Jump, si nécessaire
                public float GetJumpMultiplier()
                {
                    return jumpMultiplier;
                }
            }

            [HarmonyPatch(typeof(PlayerAvatar), "Update")]
            public class PlayerAvatarUpdatePatch
            {
                private static readonly float cameraCrouchFixOffset = -1.2f; // Offset pour rehausser la caméra
                private static readonly float scaleFactor = 0.6f; // Doit correspondre à TinyPlayerEvent.scaleFactor

                public static bool IsTinyPlayerActive = false;

                [HarmonyPostfix]
                public static void Postfix(PlayerAvatar __instance)
                {
                    if (!__instance.isLocal && !IsTinyPlayerActive)
                    {
                        Debug.Log("Nop marche pas");
                        return; // Ne s'applique qu'au joueur local pendant l'événement tiny
                    }

                    if (__instance.isCrouching)
                    {
                        //Debug.Log("Je crouch");
                        UnityEngine.Vector3 currentPos = __instance.transform.position;
                        float minY = 0.2f;
                        //Debug.Log("[Ce qu'on veut]" + minY);
                        //Debug.Log("[Ce qu'il est]" + __instance.localCamera.transform.position.y);
                        if (__instance.localCamera.transform.position.y < currentPos.y)
                        {
                            //Debug.Log("cam trop basse");
                            __instance.localCamera.transform.position = new UnityEngine.Vector3(currentPos.x, __instance.transform.position.y + 0.1f, currentPos.z);
                            Debug.Log($"[TinyPlayerEventPatch] Caméra ajustée pour joueur accroupi tiny {__instance.playerName} : Y={minY}");
                        }
                    }
                    else if (__instance.isDisabled)
                    {
                        IsTinyPlayerActive = false;

                        Debug.Log("I am dead");
                        if (SemiFunc.IsMultiplayer())
                        {
                            if (TinyPlayerManager.photonView != null && TinyPlayerManager.photonView.IsMine)
                            {
                                Debug.Log("I am dead 2");
                                if (PhotonNetwork.InRoom)
                                {
                                    Debug.Log("I am dead 3");
                                    PhotonView photonView = __instance.GetComponent<PhotonView>();
                                    object[] eventData = new object[] { photonView.ViewID };
                                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                                    PhotonNetwork.RaiseEvent(4, eventData, raiseEventOptions, SendOptions.SendReliable);
                                }
                            }
                            else
                            {
                                TinyPlayerManager.RevertTinyEffectRPC(__instance);
                            }
                        }
                        else if (__instance.isGrounded)
                        {
                            //Debug.Log("[cam] y :" + __instance.localCamera.transform.position.y);
                            //Debug.Log("[position] y :" + (__instance.transform.position.y + 0.4));
                            if (__instance.localCamera.transform.position.y > (__instance.transform.position.y + 0.4) || __instance.transform.position.y < 0.2)
                            {
                                UnityEngine.Vector3 currentPos = __instance.transform.position;
                                //Debug.Log("cam trop basse");
                                __instance.localCamera.transform.position = new UnityEngine.Vector3(currentPos.x, __instance.transform.position.y + 0.2f, currentPos.z);
                            }
                        }


                    }
                }
            }
        } //Bug actuelle , clip le sol quand on se fait écraser

        public class ExplosiveDeathEvent : MonoBehaviour, ISlotEvent
        {
            public string EventName => "ExplosiveDeath";
            public string IconName => "icon_explosive_death";
            public string Asset => "ExplosiveDeathAsset";

            [SerializeField]
            private GameObject explosionPrefab; // Prefab d'explosion assigné dans l'Inspector
            private static bool isExplosiveDeathActive = false;
            private static float effectDuration = 180f; // Durée de l'effet (3 minutes)
            private static float checkInterval = 0.1f; // Intervalle de vérification des ennemis

            private void Awake()
            {
                DontDestroyOnLoad(gameObject);
                Debug.Log("[ExplosiveDeathEvent] Initialized and set to DontDestroyOnLoad.");
            }

            public void Execute()
            {
                if (isExplosiveDeathActive)
                {
                    Debug.LogWarning("[ExplosiveDeathEvent] Explosive death event is already active. Ignoring.");
                    return;
                }

                if (EnemyDirector.instance == null)
                {
                    Debug.LogWarning("[ExplosiveDeathEvent] EnemyDirector.instance is null. Cannot execute.");
                    return;
                }
                List<string> ExplosiveDeath = new List<string>
                {
                    "All mobs are creeper? AAAAAW MAN!",
                    "No need for the Big Nuke mod, the enemies are handling it now.",
                    "Achievement unlocked: Survived a kamikaze duck. Reward: more explosions.",
                    "New game rule: Don’t touch anything unless you love explosions.",
                    "Mobs go boom? My new hobby is sprinting and screaming."
                };

                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(ExplosiveDeath));

                Debug.Log($"[ExplosiveDeathEvent] Starting explosive death event for {effectDuration} seconds.");
                isExplosiveDeathActive = true;
                GameObject managerObj = new GameObject("ExplosiveDeathManager");
                var manager = managerObj.AddComponent<ExplosiveDeathManager>();
                manager.Initialize(this, effectDuration);
            }

            private class ExplosiveDeathManager : MonoBehaviour
            {
                private ExplosiveDeathEvent parentEvent;
                private PhotonView photonView;
                private HashSet<EnemyParent> deadEnemies = new HashSet<EnemyParent>(); // Suivre les ennemis déjà morts
                private HashSet<UnityEngine.Vector3> explosionPositions = new HashSet<UnityEngine.Vector3>(); // Éviter les doublons d'explosions

                public void Initialize(ExplosiveDeathEvent parent, float duration)
                {
                    parentEvent = parent;
                    DontDestroyOnLoad(gameObject);
                    Debug.Log("[ExplosiveDeathEvent] Manager initialized.");

                    if (SemiFunc.IsMultiplayer())
                    {
                        photonView = gameObject.AddComponent<PhotonView>();
                        photonView.ViewID = PhotonNetwork.AllocateViewID(true);
                        Debug.Log($"[ExplosiveDeathEvent] Assigned PhotonView ID: {photonView.ViewID}");
                    }

                    StartCoroutine(MonitorEnemies(duration));
                }

                private IEnumerator MonitorEnemies(float duration)
                {
                    Debug.Log("[ExplosiveDeathEvent] Monitoring enemies started.");

                    float elapsed = 0f;
                    while (elapsed < duration && isExplosiveDeathActive)
                    {
                        if (EnemyDirector.instance == null || EnemyDirector.instance.enemiesSpawned == null)
                        {
                            Debug.LogWarning("[ExplosiveDeathEvent] EnemyDirector or enemiesSpawned is null. Waiting...");
                            yield return new WaitForSeconds(checkInterval);
                            elapsed += checkInterval;
                            continue;
                        }

                        foreach (EnemyParent enemyParent in EnemyDirector.instance.enemiesSpawned)
                        {
                            if (enemyParent == null || enemyParent.Enemy == null || enemyParent.Enemy.gameObject == null)
                            {
                                continue;
                            }

                            if (deadEnemies.Contains(enemyParent))
                            {
                                continue; // Déjà traité
                            }

                            if (enemyParent.Enemy.HasHealth && enemyParent.Enemy.Health != null && enemyParent.Enemy.Health.healthCurrent <= 0)
                            {
                                deadEnemies.Add(enemyParent);
                                TriggerExplosion(enemyParent);
                            }
                        }

                        yield return new WaitForSeconds(checkInterval);
                        elapsed += checkInterval;
                    }

                    isExplosiveDeathActive = false;
                    deadEnemies.Clear();
                    explosionPositions.Clear();
                    Debug.Log("[ExplosiveDeathEvent] Monitoring ended. Cleaning up.");
                    Destroy(gameObject);
                }

                private void TriggerExplosion(EnemyParent enemyParent)
                {
                    string enemyName = enemyParent.Enemy.gameObject.name;
                    UnityEngine.Vector3 position = enemyParent.Enemy.CenterTransform != null
                        ? enemyParent.Enemy.CenterTransform.position
                        : enemyParent.Enemy.transform.position;

                    // Arrondir la position pour éviter les micro-variations
                    UnityEngine.Vector3 roundedPosition = new UnityEngine.Vector3(
                        Mathf.Round(position.x * 1000f) / 1000f,
                        Mathf.Round(position.y * 1000f) / 1000f,
                        Mathf.Round(position.z * 1000f) / 1000f);

                    if (explosionPositions.Contains(roundedPosition))
                    {
                        Debug.Log($"[ExplosiveDeathEvent] Explosion already triggered at {roundedPosition} for {enemyName}. Skipping.");
                        return;
                    }
                    explosionPositions.Add(roundedPosition);

                    Debug.Log($"[ExplosiveDeathEvent] Triggering explosion for {enemyName} at {position}");

                    if (SemiFunc.IsMultiplayer())
                    {
                        if (photonView != null && photonView.IsMine)
                        {
                            photonView.RPC("ExplodeAtPosition", RpcTarget.All, position);
                        }
                        else
                        {
                            Debug.LogWarning($"[ExplosiveDeathEvent] PhotonView is null or not owned for {enemyName}.");
                        }
                    }
                    else
                    {
                        CreateExplosion(position, enemyName);
                    }
                }

                private void CreateExplosion(UnityEngine.Vector3 position, string sourceName)
                {
                    try
                    {
                        if (parentEvent.explosionPrefab == null)
                        {
                            Debug.LogWarning($"[ExplosiveDeathEvent] Explosion prefab is null for {sourceName}. Attempting fallback.");
                            GameObject fallbackPrefab = Resources.Load<GameObject>("Effects/Part Prefab Explosion");
                            if (fallbackPrefab == null)
                            {
                                Debug.LogError($"[ExplosiveDeathEvent] Failed to load fallback prefab for {sourceName}. Skipping explosion.");
                                return;
                            }
                            parentEvent.explosionPrefab = fallbackPrefab;
                        }

                        GameObject explosionObj = Instantiate(parentEvent.explosionPrefab, position, UnityEngine.Quaternion.identity);
                        Debug.Log($"[ExplosiveDeathEvent] Instantiated explosion for {sourceName} at {position}: {explosionObj.name}");

                        ParticleScriptExplosion explosionScript = explosionObj.GetComponent<ParticleScriptExplosion>();
                        if (explosionScript != null)
                        {
                            // S'assurer que l'explosionPreset est assigné
                            if (explosionScript.explosionPreset == null)
                            {
                                ExplosionPreset preset = Resources.Load<ExplosionPreset>("Explosions/ExplosionPreset");
                                if (preset == null)
                                {
                                    preset = ScriptableObject.CreateInstance<ExplosionPreset>();
                                    preset.explosionForceMultiplier = 1f;
                                    preset.explosionColors = new Gradient();
                                    preset.smokeColors = new Gradient();
                                    preset.lightColor = new Gradient();
                                    Debug.Log($"[ExplosiveDeathEvent] Created temporary ExplosionPreset for {sourceName}");
                                }
                                explosionScript.explosionPreset = preset;
                            }

                            ParticlePrefabExplosion particleEffect = explosionScript.Spawn(position, 1f, 10, 25, 2f, false, false, 1f);
                            if (particleEffect != null)
                            {
                                Debug.Log($"[ExplosiveDeathEvent] Spawned particle effect for {sourceName}: {particleEffect.gameObject.name}");
                                Destroy(particleEffect.gameObject, 2f); // Détruire l'effet après 2 secondes
                            }
                            else
                            {
                                Debug.LogWarning($"[ExplosiveDeathEvent] Failed to spawn particle effect for {sourceName}.");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[ExplosiveDeathEvent] No ParticleScriptExplosion on prefab for {sourceName}.");
                        }

                        Destroy(explosionObj, 2f); // Détruire l'objet principal après 2 secondes
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[ExplosiveDeathEvent] Error creating explosion for {sourceName}: {ex.Message}");
                    }
                }

                [PunRPC]
                private void ExplodeAtPosition(UnityEngine.Vector3 position)
                {
                    UnityEngine.Vector3 roundedPosition = new UnityEngine.Vector3(
                        Mathf.Round(position.x * 1000f) / 1000f,
                        Mathf.Round(position.y * 1000f) / 1000f,
                        Mathf.Round(position.z * 1000f) / 1000f);

                    //if (explosionPositions.Contains(roundedPosition))
                    //{
                    //    Debug.Log($"[ExplosiveDeathEvent] RPC explosion already processed at {roundedPosition}. Skipping.");
                    //    return;
                    //}
                    explosionPositions.Add(roundedPosition);

                    Debug.Log($"[ExplosiveDeathEvent] RPC explosion triggered at {position}");
                    CreateExplosion(position, "RPC Explosion");
                }
            }

        }//Finis (test a faire)

        public class RevivePlayerEvent : ISlotEvent
        {
            public string EventName => "Revive";

            public string IconName => "Revive";

            public string Asset => "Revive";

            public void Execute()
            {

                if (!SemiFunc.IsMasterClientOrSingleplayer())
                {
                    Debug.Log("[RandomRevivePotion] Not master client or singleplayer. Skipping revive logic.");
                    return;
                }
                List<PlayerDeathHead> deadHeads = FindAllPlayerDeathHeads();

                if (deadHeads.Count > 0)
                {
                    List<string> RevivePlayer = new List<string>
                    {
                        "Respawn activated! Now you're alive… and broke.",
                        "You awaken, revived by an unseen force, ready to loot again.",
                        "Revived by an unknown force, you’re thrust back into action."
                    };

                    GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(RevivePlayer));

                    // Sélectionner une tête aléatoire
                    PlayerDeathHead selectedHead = deadHeads[UnityEngine.Random.Range(0, deadHeads.Count)];
                    Debug.Log($"[RandomRevivePotion] Reviving player associated with head: {selectedHead.gameObject.name}");
                    selectedHead.FlashEyeRPC(true);
                    selectedHead.Revive();
                }
                else
                {
                    // Aucune tête trouvée : exécuter la logique alternative
                    List<string> RevivePlayer = new List<string>
                    {
                        "I want to revive someone , but nobody dies. Maybe kill myself will resolve the problem :O",
                        "Nobody’s dying? Damn, for once I wanted my friends to bite the dust…",
                    };
                    GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(RevivePlayer));
                    Debug.Log("[RandomRevivePotion] No PlayerDeathHead found on the map.");
                }
            }

            private List<PlayerDeathHead> FindAllPlayerDeathHeads()
            {
                List<PlayerDeathHead> deadHeads = new List<PlayerDeathHead>();

                // Vérifier chaque joueur dans GameDirector.PlayerList
                if (GameDirector.instance != null && GameDirector.instance.PlayerList != null)
                {
                    foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
                    {
                        if (player == null) continue;

                        // Vérifier si le joueur a un PlayerDeathHead actif
                        PlayerDeathHead deathHead = player.playerDeathHead;
                        if (deathHead != null && deathHead.gameObject.activeInHierarchy)
                        {
                            deadHeads.Add(deathHead);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[RevivePlayerEvent] GameDirector.instance or PlayerList is null.");
                }

                return deadHeads;
            }

            // Exemple de méthode pour la logique alternative (à remplacer par ton code)
            private void AlternativeLogic()
            {
                Debug.Log("[RandomRevivePotion] Alternative logic triggered.");
            }
        } //Finis Marche bien

        public class ExtractionPointHaulModifier : MonoBehaviourPunCallbacks, ISlotEvent
        {
            private bool hasModifiedHaulGoal = false;
            private ExtractionPoint currentExtractionPoint = null;

            public string EventName => "Extraction";
            public string IconName => "Extraction";
            public string Asset => "TestAsset"; // Remplacé throw par une valeur par défaut
            public bool oneTimeOnly = true;

            public void Execute()
            {
                List<string> ExtractionPointHaulModifier = new List<string>
                {
                    "I'm calling Taxman, hope he will make your objective easier.",
                    "Taxman, I'm broke, please put a lower tax."
                };

                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(ExtractionPointHaulModifier));

                // Créer un GameObject si le composant n'est pas attaché
                if (this == null || !TryGetComponent(out MonoBehaviour _))
                {
                    Debug.LogWarning("[ExtractionPointHaulModifier] Component not attached. Creating new GameObject.");
                    GameObject modifierObj = new GameObject("ExtractionPointHaulModifier");
                    ExtractionPointHaulModifier modifier = modifierObj.AddComponent<ExtractionPointHaulModifier>();
                    modifier.Execute(); // Appeler Execute sur la nouvelle instance
                    return;
                }

                // Vérifier RoundDirector
                if (RoundDirector.instance == null)
                {
                    Debug.LogWarning("[ExtractionPointHaulModifier] RoundDirector.instance is null. Waiting for initialization.");
                    StartCoroutine(WaitForRoundDirector());
                    return;
                }

                // Démarrer la vérification immédiatement
                StartCheck();
            }

            private void StartCheck()
            {
                oneTimeOnly = true;
                StartCoroutine(CheckExtractionPoints());
            }

            private IEnumerator WaitForRoundDirector()
            {
                while (RoundDirector.instance == null)
                {
                    yield return new WaitForSeconds(0.1f);
                }
                StartCheck();
            }

            private IEnumerator CheckExtractionPoints()
            {
                while (oneTimeOnly)
                {
                    if (RoundDirector.instance == null || RoundDirector.instance.extractionPointList == null)
                    {
                        Debug.LogWarning("[ExtractionPointHaulModifier] RoundDirector or extractionPointList is null. Waiting...");
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }
                    foreach (GameObject extractionPointObj in RoundDirector.instance.extractionPointList)
                    {
                        if (extractionPointObj == null) continue;

                        ExtractionPoint point = extractionPointObj.GetComponent<ExtractionPoint>();
                        if (point == null) continue;

                        if (point.currentState == ExtractionPoint.State.Active && !hasModifiedHaulGoal)
                        {
                            currentExtractionPoint = point;
                            ModifyHaulGoal(point);
                            hasModifiedHaulGoal = true;
                            yield break;
                        }
                    }
                    yield return new WaitForSeconds(0.5f);
                }
            }

            private void ModifyHaulGoal(ExtractionPoint point)
            {
                if (point == null || point.haulGoal <= 0)
                {
                    Debug.LogWarning("[ExtractionPointHaulModifier] Invalid haulGoal or ExtractionPoint.");
                    return;
                }

                float modifier = UnityEngine.Random.Range(0.5f, 1.5f);
                int newHaulGoal = Mathf.RoundToInt(point.haulGoal * modifier);

                float percentage = (modifier - 1f) * 100f;
                string sign = percentage >= 0 ? "+" : "";
                Debug.Log($"[ExtractionPointHaulModifier] Modifying haulGoal from {point.haulGoal} to {newHaulGoal} ({sign}{percentage:F0}%)");

                List<string> ExtractionPointHaulModifier = new List<string>
                {
                    $"We got ({sign}{percentage:F0}%)",
                };

                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(ExtractionPointHaulModifier));

                oneTimeOnly = false;
                if (SemiFunc.IsMultiplayer())
                {
                    if (SemiFunc.IsMasterClient())
                    {
                        point.photonView.RPC("HaulGoalSetRPC", RpcTarget.All, newHaulGoal);
                    }
                }
                else
                {
                    point.haulGoal = newHaulGoal;
                    RoundDirector.instance.extractionHaulGoal = newHaulGoal;
                    point.haulGoalFetched = true;
                }
            }

            private void OnEnable()
            {
                if (RoundDirector.instance != null && RoundDirector.instance.extractionPointActive && hasModifiedHaulGoal)
                {
                    ExtractionPoint newActivePoint = RoundDirector.instance.extractionPointCurrent;
                    if (newActivePoint != null && newActivePoint != currentExtractionPoint)
                    {
                        Debug.Log("[ExtractionPointHaulModifier] New extraction point detected. Resetting modifier.");
                        hasModifiedHaulGoal = false;
                        currentExtractionPoint = null;
                        StartCoroutine(CheckExtractionPoints());
                    }
                }
            }

            private void OnDisable()
            {
                StopAllCoroutines();
            }
        } // Finis marche bien (check a faire pour le multi)

        public class BetterWalkieTakkie : ISlotEvent
        {
            internal static BetterWalkieTakkie instance { get; private set; } = null;
            public string EventName => "BetterWalkieTakkie";
            public string IconName => "icon_timeslow";
            public string Asset => "TestAsset";

            internal static GameObject waveformCameraGO;
            internal static GameObject waveformVisualizerGO;
            internal static Material originalDisplayMaterial;
            internal static Material waveformMaterialInstance;
            internal static RenderTexture waveformRenderTexture;
            internal static AssetBundle walkyBundle;
            internal static Material waveformMat;
            internal static bool Toggle;
            private static HashSet<string> currentWinnerSteamIDs = new HashSet<string>(); // Ensemble de gagnants
            private const byte EVENT_WALKIE_WINNER = 5; // Code pour l'événement Photon

            public void Execute()
            {
                instance = this;
                // Initialiser ou charger les gagnants au démarrage
                List<string> WalkiTalki = new List<string>
                {
                    "New upgrade acquired. I can use the arrow to activate the walkie in the map module.",
                    "I can now talk to my friend, if I use the arrow in the map module. Hope I'm not the only one.",
                    "YOU HEARD ME? I HAVE A WALKIE NOW IF I USE THE ARROW IN THE MAP MODULE? YOU HEARD ME????"
                };

                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(WalkiTalki));

                if (PhotonNetwork.IsMasterClient)
                {
                    string saveFileName = DifficultySaveContext.CurrentSaveFileName;
                    currentWinnerSteamIDs = DifficultySaveManager.LoadWalkieWinners(saveFileName);
                    SyncWinnersToClients(currentWinnerSteamIDs);
                }
            }

            public void ToggleWalkie(bool enabled)
            {
                PlayerAvatar player = PlayerAvatar.instance;
                Transform display = player.mapToolController.transform.Find("Controller/Visuals/Hide/Main Spring Target/Main Spring/Base Offset/Bob/Stick/stick/Main Unit/Display Spring Target/Display Spring/Counter/display_1x1");

                foreach (Transform t in player.mapToolController.VisualTransform.GetComponentsInChildren<Transform>(true))
                    if (t.name == "display_1x1")
                        display = t;

                if (!display.TryGetComponent(out MeshRenderer renderer))
                {
                    Debug.LogError("[WalkieToggle] Pas de MeshRenderer sur display_1x1.");
                    return;
                }

                Toggle = enabled;
                if (enabled)
                {
                    // Ajouter le joueur comme gagnant
                    string playerSteamID = SemiFunc.PlayerGetSteamID(player);
                    AddWinner(playerSteamID);

                    WalkieReceiver.walkieEnabled = true;
                    if (originalDisplayMaterial == null)
                        originalDisplayMaterial = renderer.material;

                    waveformRenderTexture = new RenderTexture(512, 512, 16);

                    waveformCameraGO = new GameObject("WaveformCamera");
                    var cam = waveformCameraGO.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 3f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    cam.cullingMask = LayerMask.GetMask("TopLayerOnly");
                    cam.targetTexture = waveformRenderTexture;
                    waveformCameraGO.transform.position = new UnityEngine.Vector3(0, 5f, 0);
                    waveformCameraGO.transform.rotation = UnityEngine.Quaternion.Euler(90f, 0, 0);

                    waveformVisualizerGO = new GameObject("WaveformVisualizer");
                    waveformVisualizerGO.layer = LayerMask.NameToLayer("TopLayerOnly");
                    var lr = waveformVisualizerGO.AddComponent<LineRenderer>();
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = Color.green;
                    lr.endColor = Color.red;
                    lr.widthMultiplier = 0.05f;
                    lr.useWorldSpace = false;
                    waveformVisualizerGO.transform.position = new UnityEngine.Vector3(-512 * 0.005f, 0, -256 * 0.005f);
                    waveformVisualizerGO.transform.rotation = UnityEngine.Quaternion.identity;

                    var vis = waveformVisualizerGO.AddComponent<WaveformVisualizer>();
                    vis.voiceChat = player.voiceChat;

                    string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "walky");
                    if (walkyBundle == null)
                        walkyBundle = AssetBundle.LoadFromFile(bundlePath);

                    if (walkyBundle == null)
                    {
                        Debug.LogError("[WalkieToggle] AssetBundle introuvable.");
                        return;
                    }

                    if (waveformMat == null)
                        waveformMat = walkyBundle.LoadAsset<Material>("WaveformDisplayMat");

                    if (waveformMat == null)
                    {
                        Debug.LogError("[WalkieToggle] WaveformDisplayMat introuvable dans l'AssetBundle.");
                        return;
                    }

                    waveformMaterialInstance = new Material(waveformMat);
                    waveformMaterialInstance.mainTexture = waveformRenderTexture;

                    renderer.material = waveformMaterialInstance;

                    if (!player.GetComponent<WalkieReceiver>())
                        player.gameObject.AddComponent<WalkieReceiver>();
                }
                else
                {
                    WalkieReceiver.walkieEnabled = false;
                    if (originalDisplayMaterial != null)
                        renderer.material = originalDisplayMaterial;

                    if (waveformCameraGO != null) GameObject.Destroy(waveformCameraGO);
                    if (waveformVisualizerGO != null) GameObject.Destroy(waveformVisualizerGO);
                    if (waveformRenderTexture != null) waveformRenderTexture.Release();

                    waveformCameraGO = null;
                    waveformVisualizerGO = null;
                    waveformRenderTexture = null;
                }
            }

            private void AddWinner(string steamID)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    currentWinnerSteamIDs.Add(steamID);
                    string saveFileName = DifficultySaveContext.CurrentSaveFileName;
                    DifficultySaveManager.AddWalkieWinner(steamID);
                    SyncWinnersToClients(currentWinnerSteamIDs);
                    Debug.Log($"[BetterWalkieTakkie] Host added winner: {steamID}");
                }
                else
                {
                    // Les clients envoient une demande à l'host
                    PhotonView view = PlayerAvatar.instance.GetComponent<PhotonView>();
                    if (view != null)
                    {
                        object[] content = new object[] { view.ViewID, steamID };
                        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
                        PhotonNetwork.RaiseEvent(EVENT_WALKIE_WINNER, content, options, SendOptions.SendReliable);
                    }
                }
            }

            // Synchroniser les gagnants à tous les clients
            private void SyncWinnersToClients(HashSet<string> steamIDs)
            {
                if (!PhotonNetwork.IsMasterClient) return;

                object[] content = new object[] { steamIDs.ToArray() };
                var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                PhotonNetwork.RaiseEvent(EVENT_WALKIE_WINNER, content, options, SendOptions.SendReliable);
                Debug.Log($"[BetterWalkieTakkie] Synced winners to all clients: {string.Join(", ", steamIDs)}");
            }

            // Recevoir les événements réseau
            public static void HandleWinnerEvent(EventData photonEvent)
            {
                if (photonEvent.Code != EVENT_WALKIE_WINNER) return;

                object[] data = (object[])photonEvent.CustomData;
                if (PhotonNetwork.IsMasterClient && data.Length == 2)
                {
                    // L'host reçoit une demande de client
                    int viewID = (int)data[0];
                    string steamID = (string)data[1];
                    instance.AddWinner(steamID); // Ajoute et synchronise
                }
                else if (data.Length == 1 && data[0] is string[] steamIDs)
                {
                    // Tous les clients reçoivent la synchro des gagnants
                    currentWinnerSteamIDs = new HashSet<string>(steamIDs);
                    Debug.Log($"[BetterWalkieTakkie] Received winners: {string.Join(", ", currentWinnerSteamIDs)}");
                }
            }

            [RequireComponent(typeof(LineRenderer))]
            public class WaveformVisualizer : MonoBehaviour
            {
                public AudioSource audioSource;
                public int sampleSize = 512;
                public float amplitude = 10f;
                private float[] samples;
                private LineRenderer lineRenderer;
                public PlayerVoiceChat voiceChat;

                void Start()
                {
                    samples = new float[sampleSize];
                    lineRenderer = GetComponent<LineRenderer>();
                    lineRenderer.positionCount = sampleSize;
                }

                void Update()
                {
                    float loudness = WalkieReceiver.instance != null ? WalkieReceiver.instance.GetCurrentVolume() : 0.01f;

                    for (int i = 0; i < sampleSize; i++)
                    {
                        float x = i * 0.01f;
                        float z = Mathf.Sin(i * 0.2f + Time.time * 4f) * loudness * amplitude;
                        lineRenderer.SetPosition(i, new UnityEngine.Vector3(x, 0, z));
                    }
                }
            }

        } //Finis ?

        public class SurviveHorror : ISlotEvent
        {
            public string EventName => "SurviveHorrror";
            public string IconName => "icon_timeslow";
            public string Asset => "TestAsset";

            private ExtractionPoint currentExtractionPoint = null;
            private bool oneTimeOnly = false;
            private bool hasModifiedHaulGoal = false;

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null) return;

                List<string> SurvivreHorror = new List<string>
                {
                    "Congrats! I’ve unlocked Apocalypse Mode. I’ll need luck I think.",
                    "The horror will begin, I need to hide fast.",
                    "I have to hide, RIP my friend if they come."
                };
                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(SurvivreHorror));

                int count = 15;
                float radius = 10f;
                UnityEngine.Vector3 center = player.transform.position;
                List<EnemyParent> spawnedEnemies = new();

                for (int i = 0; i < count; i++)
                {
                    string enemyName = "";
                    int difficulty = UnityEngine.Random.Range(0, 3); // 0 → Easy, 1 → Med, 2 → Hard
                    int t = 0;

                    switch (difficulty)
                    {
                        case 0:
                            t = EnemyDirector.instance.enemiesDifficulty1.Count;
                            enemyName = EnemyDirector.instance.enemiesDifficulty1[UnityEngine.Random.Range(0, t)].name;
                            break;
                        case 1:
                            t = EnemyDirector.instance.enemiesDifficulty2.Count;
                            enemyName = EnemyDirector.instance.enemiesDifficulty2[UnityEngine.Random.Range(0, t)].name;
                            break;
                        case 2:
                            t = EnemyDirector.instance.enemiesDifficulty3.Count;
                            enemyName = EnemyDirector.instance.enemiesDifficulty3[UnityEngine.Random.Range(0, t)].name;
                            break;
                    }

                    if (!enemyName.ToLower().Contains("gnome") && !enemyName.ToLower().Contains("bang"))
                    {
                        if (!EnemyDirector.instance.TryGetEnemyThatContainsName(enemyName, out EnemySetup enemySetup))
                        {
                            Debug.LogWarning($"[EnemyRainEvent] Enemy not found: {enemyName}");
                            continue;
                        }
                        if (!enemySetup.name.ToLower().Contains("group"))
                        {
                            float angle = i * Mathf.PI * 2f / count;
                            UnityEngine.Vector3 offset = new UnityEngine.Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                            UnityEngine.Vector3 spawnPos = center + offset + UnityEngine.Vector3.up;
                            Debug.Log(enemySetup.name);
                            EnemyParent enemy = Enemies.SpawnEnemy(enemySetup, spawnPos, UnityEngine.Quaternion.identity, spawnDespawned: false).First();
                            spawnedEnemies.Add(enemy);

                            Debug.Log($"[EnemyRainEvent] Spawned {enemy.name} at {spawnPos}");
                        }
                    }
                    else
                    {
                        i--;
                    }
                }

                // Lance la suppression après 30 secondes
                Debug.Log(spawnedEnemies.ToList());
                PlayerController.instance.StartCoroutine(DestroyEnemiesAfterDelay(spawnedEnemies, 30f));
            }

            private IEnumerator DestroyEnemiesAfterDelay(List<EnemyParent> enemies, float delay)
            {
                yield return new WaitForSeconds(delay);

                foreach (var enemy in enemies)
                {
                    Debug.Log(enemy.name);
                    if (enemy != null)
                    {
                        enemy.Despawn();
                        Debug.Log($"[EnemyRainEvent] Destroyed {enemy.name}");

                    }
                }

                Debug.Log(PlayerAvatar.instance.isDisabled);
                if (!PlayerAvatar.instance.isDisabled)
                {
                    Debug.Log("test");
                    oneTimeOnly = true;
                    PlayerController.instance.StartCoroutine(CheckExtractionPoints());
                }
            }

            private IEnumerator CheckExtractionPoints()
            {
                while (oneTimeOnly)
                {
                    if (RoundDirector.instance == null || RoundDirector.instance.extractionPointList == null)
                    {
                        Debug.LogWarning("[ExtractionPointHaulModifier] RoundDirector or extractionPointList is null. Waiting...");
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }
                    foreach (GameObject extractionPointObj in RoundDirector.instance.extractionPointList)
                    {
                        if (extractionPointObj == null) continue;

                        ExtractionPoint point = extractionPointObj.GetComponent<ExtractionPoint>();
                        if (point == null) continue;

                        if (point.currentState == ExtractionPoint.State.Active && !hasModifiedHaulGoal)
                        {
                            currentExtractionPoint = point;
                            ModifyHaulGoal(point);
                            hasModifiedHaulGoal = true;
                            yield break;
                        }
                    }
                    yield return new WaitForSeconds(0.5f);
                }
            }

            private void ModifyHaulGoal(ExtractionPoint point)
            {
                if (point == null || point.haulGoal <= 0)
                {
                    Debug.LogWarning("[ExtractionPointHaulModifier] Invalid haulGoal or ExtractionPoint.");
                    return;
                }

                float modifier = UnityEngine.Random.Range(0.01f, 0.99f);
                int newHaulGoal = Mathf.RoundToInt(point.haulGoal * modifier);

                float percentage = (modifier - 1f) * 100f;
                string sign = percentage >= 0 ? "+" : "";
                Debug.Log($"[ExtractionPointHaulModifier] Modifying haulGoal from {point.haulGoal} to {newHaulGoal} ({sign}{percentage:F0}%)");

                oneTimeOnly = false;
                if (SemiFunc.IsMultiplayer())
                {
                    if (SemiFunc.IsMasterClient())
                    {
                        point.photonView.RPC("HaulGoalSetRPC", RpcTarget.All, newHaulGoal);
                    }
                }
                else
                {
                    point.haulGoal = newHaulGoal;
                    RoundDirector.instance.extractionHaulGoal = newHaulGoal;
                    point.haulGoalFetched = true;
                }
            }
        } //Finis ?

        public class TimeSlowEvent : ISlotEvent
        {
            public string EventName => "Time Slow";
            public string IconName => "icon_timeslow";
            public string Asset => "TestAsset";

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null) return;

                List<string> TimeSlowEvent = new List<string>
                {
                    "Have to slow down for a minute.",
                    "Slow-mo vibes, but the berserk enemies didn’t get the memo."
                };
                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(TimeSlowEvent));

                PhotonView view = player.GetComponent<PhotonView>();

                if (player.GetComponent<TimeSlowEffectController>() == null)
                {
                    player.gameObject.AddComponent<TimeSlowEffectController>();
                }

                if (view != null && view.IsMine)
                {
                    view.RPC("ApplyTimeSlowEffect", RpcTarget.All, view.ViewID);
                }
            }

            public class TimeSlowEffectController : MonoBehaviourPun
            {
                private float duration = 30f;
                private bool isActive = false;

                public void Trigger()
                {
                    if (isActive) return;
                    isActive = true;

                    if (photonView.IsMine)
                    {
                        ApplySlow();
                        StartCoroutine(RemoveEffectAfterTime());
                    }
                }

                [PunRPC]
                public void ApplyTimeSlowEffect(int viewID)
                {
                    PhotonView view = PhotonView.Find(viewID);
                    if (view == null) return;

                    var controller = view.GetComponent<TimeSlowEffectController>();
                    if (controller == null)
                        controller = view.gameObject.AddComponent<TimeSlowEffectController>();

                    controller.Trigger();
                }

                private void ApplySlow()
                {
                    var player = PlayerController.instance;
                    Debug.Log(player.name);
                    if (player == null) return;

                    PlayerAvatar instance = PlayerAvatar.instance;
                    if ((bool)instance.voiceChat)
                    {
                        instance.voiceChat.OverridePitch(0.65f, 1f, 2f);
                    }
                    instance.OverridePupilSize(3f, 4, 1f, 1f, 5f, 0.5f, 30f);
                    PlayerController.instance.OverrideSpeed(0.5f, 30f);
                    PlayerController.instance.OverrideLookSpeed(0.5f, 2f, 1f, 30f);
                    PlayerController.instance.OverrideAnimationSpeed(0.2f, 1f, 2f, 30f);
                    PlayerController.instance.OverrideTimeScale(0.1f, 30f);
                    CameraZoom.Instance.OverrideZoomSet(50f, 30f, 0.5f, 1f, base.gameObject, 0);
                    PostProcessing.Instance.SaturationOverride(50f, 0.1f, 0.5f, 30f, base.gameObject);
                }

                private void ResetToDefault()
                {
                    var player = PlayerController.instance;
                    if (player == null) return;


                    player.OverrideSpeed(1f);                // Vitesse normale
                    player.OverrideLookSpeed(1f, 1f, 1f);     // Rotation standard
                    player.OverrideAnimationSpeed(1f, 1f, 1f); // Animations
                    player.OverrideTimeScale(1f);
                    // Temps normal
                }

                private IEnumerator RemoveEffectAfterTime()
                {
                    yield return new WaitForSeconds(duration);
                    ResetToDefault();
                    isActive = false;
                    Destroy(this);
                }
            }

        } //Finis (Ajout peut-être bien)

        public class RandomTeleportEvent : ISlotEvent
        {
            public string EventName => "Random TP";
            public string IconName => "icon_random_tp";
            public string Asset => "TestAsset";

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null)
                {
                    Debug.LogError("[RandomTP] Joueur introuvable !");
                    return;
                }

                // Liste des modules existants
                var modules = GameObject.FindObjectsOfType<Module>();
                if (modules.Length == 0)
                {
                    Debug.LogError("[RandomTP] Aucun module trouvé !");
                    return;
                }

                List<string> RandomTP = new List<string>
                {
                    "Reality bends, teleporting to a random destination.",
                    "The fabric of the game shifts, placing you somewhere new.",
                    "A mysterious force relocates you to an unpredictable spot."
                };
                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(RandomTP));

                // On prend une salle aléatoire
                var randomModule = modules[UnityEngine.Random.Range(0, modules.Length)];

                // On essaie de trouver un point de positionnement safe dans la salle
                var targetPosition = randomModule.transform.position + UnityEngine.Vector3.up * 1.5f;

                // Appliquer le TP
                player.transform.position = targetPosition;
                Debug.Log($"[RandomTP] Joueur téléporté dans {randomModule.name}");
            }
        } //Finis (Manque vérification tp dans un trou) //Bug Ne tp plus

        public class GoldenGunEvent : ISlotEvent
        {
            public string EventName => "EnemyRain";
            public string IconName => "icon_enemy_rain";

            public string Asset => "TestAsset";

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null) return;
                object[] instantiationData = new object[] { true }; // true = GoldenGun
                GameObject gunInstance2 = new GameObject();
                try
                {
                    List<string> GoldenGun = new List<string>
                    {
                        "Golden Gun acquired! Time to shine… until the Taxman taxes it.",
                        "Shiny gun, zero skills. The Taxman’s laughing at my shots.",
                        "Got the Golden Gun! My confidence? Still in the shop."
                    };

                    GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(GoldenGun));

                    gunInstance2 = PhotonNetwork.Instantiate("items/Golden_Gun", player.transform.position + player.transform.forward, UnityEngine.Quaternion.identity);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                }

                if (gunInstance2 == null)
                {
                    Debug.LogError("[GoldenGun] PhotonNetwork.Instantiate a retourné NULL !");
                }
                else
                {
                    Debug.Log("[GoldenGun] Gun instancié, name: " + gunInstance2.name);
                    foreach (var view in gunInstance2.GetComponentsInChildren<PhotonView>())
                        Debug.Log($"[GoldenGun] PhotonView: {view.name}, ViewID: {view.ViewID}, IsMine: {view.IsMine}");
                }
            }

            [HarmonyPatch(typeof(ItemGunBullet), nameof(ItemGunBullet.ActivateAll))]
            public class GoldenGunBulletPatch
            {
                static void Postfix(ItemGunBullet __instance)
                {
                    if (__instance.hurtCollider == null)
                        return;

                    if (GoldenGunLinker.GunQueue.Count > 0)
                    {
                        var gun = GoldenGunLinker.GunQueue.Dequeue();

                        if (gun != null && gun.name.ToLower().Contains("golden"))
                        {
                            __instance.hurtCollider.enemyDamage = 999;
                            Debug.Log("[GoldenGun] Dégâts boostés à 999 !");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[GoldenGun] Aucun gun lié trouvé dans la file !");
                    }
                }
            }

            public static class GoldenGunLinker
            {
                public static Queue<ItemGun> GunQueue = new Queue<ItemGun>();
            }

            [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootBulletRPC))]
            public class GoldenGun_ShootBulletPatch
            {
                static void Prefix(ItemGun __instance)
                {
                    GoldenGunLinker.GunQueue.Enqueue(__instance);
                }
            }

        } //Finis Marche Bien

        public class RevealMapEvent : ISlotEvent
        {
            public string EventName => "Reveal Map";
            public string IconName => "reveal_icon";
            public string Asset => "TestAsset";

            public void Execute()
            {
                Debug.Log("[RevealMapEvent] Début du reveal");

                int revealed = 0;
                var volumes = GameObject.FindObjectsOfType<RoomVolume>();
                Debug.Log($"[RevealMapEvent] {volumes.Length} RoomVolume trouvés.");

                List<string> RevealMap = new List<string>
                {
                    "All terrain is exposed, the map’s boundaries now clear."
                };
                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(RevealMap));

                foreach (var room in volumes)
                {

                    room.SetExplored();
                }

                foreach (var valuable in GameObject.FindObjectsOfType<ValuableObject>())
                {
                    Map.Instance.AddValuable(valuable);
                }

                Debug.Log($"[RevealMapEvent] Carte révélée : {revealed} objets affichés.");
            }

        } //Finis Marche Bien

        public class EnemyDuckEvent : ISlotEvent
        {
            public string EventName => "EnemyRain";
            public string IconName => "icon_enemy_rain";

            public string Asset => "TestAsset";

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null) return;

                string enemyName = "Duck";

                if (!EnemyDirector.instance.TryGetEnemyThatContainsName(enemyName, out EnemySetup enemySetup))
                {
                    Debug.Log("[EnemyRainEvent] Enemy not found: " + enemyName);
                    return;
                }

                List<string> Duck = new List<string>
                    {
                        "Duck army incoming! My cart’s quacking for mercy.",
                        "Feathers, quacks, and pain. This is my life now, thanks.",
                        "Quackpocalypse unleashed! The shop’s out of duck repellent."
                    };
                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(Duck));

                int count = 15;
                float radius = 10f;
                UnityEngine.Vector3 center = player.transform.position;

                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    UnityEngine.Vector3 offset = new UnityEngine.Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    UnityEngine.Vector3 spawnPos = center + offset;


                    spawnPos.y += 2f;

                    Debug.Log($"[EnemyRainEvent] Spawning enemy #{i + 1} at {spawnPos}");
                    Enemies.SpawnEnemy(enemySetup, spawnPos, UnityEngine.Quaternion.identity, spawnDespawned: false);
                }
            }
        } //Finis Marche Bien

        public class MarioStarEvent : MonoBehaviour, ISlotEvent
        {
            public string EventName => "MarioStar";
            public string IconName => "icon_enemy_rain";

            public string Asset => "TestAsset";
            public static AssetBundle AssetBundle { get; set; }
            public static AudioClip starClip { get; set; }

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null) return;

                string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "Mario");
                Debug.Log($"[AlarmEffectController] Loading asset bundle from: {bundlePath}");

                if (AssetBundle == null)
                {
                    AssetBundle = AssetBundle.LoadFromFile(bundlePath);
                }

                if (starClip == null)
                    starClip = AssetBundle.LoadAsset<AudioClip>("videoplayback");

                if (starClip == null)
                {
                    Debug.LogError("[AlarmEffectController] Failed to load Star.");
                    return;
                }

                PlayerAvatar avatar = PlayerAvatar.instance;
                // S'assurer que MarioStarEvent est attaché au joueur
                var marioStarEvent = avatar.gameObject.GetComponent<MarioStarEvent>();
                if (marioStarEvent == null)
                {
                    marioStarEvent = avatar.gameObject.AddComponent<MarioStarEvent>();
                }

                List<string> Mario = new List<string>
                {
                    "The Mario Star surges through you, granting invincibility for 20 seconds.",
                    "The star’s light shields you, granting 20 seconds of perfect safety.",
                    "For 20 seconds, no enemy can touch me—pure invulnerability."
                };
                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(Mario));

                // Lancer la coroutine sur le composant attaché
                avatar.StartCoroutine(marioStarEvent.ApplyMarioStarEffect(avatar, starClip));
            }

            private IEnumerator ApplyMarioStarEffect(PlayerAvatar avatar, AudioClip clip)
            {
                float duration = clip.length;
                Debug.Log($"[MarioStarEvent] Applying star power for {duration} seconds.");

                // 1. HP invincible

                PlayerController t = PlayerController.instance;
                float originalMaxHealth = avatar.playerHealth.maxHealth;
                float originalHealth = avatar.playerHealth.health;
                float originalspeed = t.EnergySprintDrain;
                t.EnergySprintDrain = 0;
                avatar.playerHealth.maxHealth = 999;
                avatar.playerHealth.health = 999;

                Debug.Log("[MarioStarEvent] Creating rainbow overlay...");

                Material rainbowMat = AssetBundle.LoadAsset<Material>("MarioStar");
                if (rainbowMat == null)
                {
                    Debug.LogError("[MarioStarEvent] Material MarioStar introuvable !");
                    yield break;
                }
                else
                {
                    Debug.Log("[MarioStarEvent] Material chargé avec succès.");
                }
                GameObject overlayPrefab = AssetBundle.LoadAsset<GameObject>("RainbowOverlay");
                GameObject overlay = GameObject.Instantiate(overlayPrefab);
                HUDCanvas h = HUDCanvas.instance;

                overlay.transform.SetParent(h.transform, false); // si tu veux le mettre dans HUD Canvas
                overlay.SetActive(true);
                Debug.Log("[MarioStarEvent] RainbowOverlay activé.");

                Debug.Log("[MarioStarEvent] Overlay UI créé et affiché.");

                // 2. Audio local (dans les oreilles du joueur)
                AudioSource localAudio = avatar.gameObject.AddComponent<AudioSource>();
                localAudio.clip = clip;
                localAudio.spatialBlend = 0f; // son 2D dans les oreilles
                localAudio.loop = false;
                localAudio.Play();

                // 3. Audio global (réplication réseau)
                if (PhotonNetwork.InRoom)
                {
                    Debug.Log("Envoie Photon");
                    PhotonView photonView = avatar.GetComponent<PhotonView>();
                    object[] eventData = new object[] { photonView.ViewID };
                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                    PhotonNetwork.RaiseEvent(2, eventData, raiseEventOptions, SendOptions.SendReliable);
                }

                // 4. Activer l'effet de "kill au contact"
                var marioEffect = avatar.gameObject.AddComponent<MarioStarPower>();
                marioEffect.duration = duration;

                yield return new WaitForSeconds(duration);

                // 5. Fin de l’effet
                if (overlay != null)
                {
                    overlay.SetActive(false);
                    Debug.Log("[MarioStarEvent] RainbowOverlay désactivé.");
                }
                t.EnergySprintDrain = originalspeed;
                avatar.playerHealth.health = (int)originalHealth;
                avatar.playerHealth.maxHealth = (int)originalMaxHealth;
                UnityEngine.Object.Destroy(localAudio);
                UnityEngine.Object.Destroy(marioEffect);
                Debug.Log("[MarioStarEvent] Star effect ended.");

                // Détruire le composant MarioStarEvent
                UnityEngine.Object.Destroy(this);
            }

            private void Update()
            {
                if (!MarioStarPower.running) return;

                float hue = Mathf.Repeat(Time.time * 0.5f, 1f);
                Color rainbowColor = Color.HSVToRGB(hue, 1f, 1f);

                float intensity = Mathf.PingPong(Time.time * 5f, 1f);

                MarioStarPower.eyeMatL.SetColor("_ColorOverlay", rainbowColor);
                MarioStarPower.eyeMatR.SetColor("_ColorOverlay", rainbowColor);
                MarioStarPower.eyeMatL.SetFloat("_ColorOverlayAmount", intensity);
                MarioStarPower.eyeMatR.SetFloat("_ColorOverlayAmount", intensity);
            }

            public class MarioStarPower : MonoBehaviour
            {
                public float duration = 5f;
                private Collider triggerCollider;
                public static bool running = false;
                public static Material eyeMatL;
                public static Material eyeMatR;

                private void Start()
                {
                    Debug.Log("[MarioStarPower] Init...");

                    // Ajoute un SphereCollider si aucun
                    triggerCollider = GetComponent<Collider>();
                    if (triggerCollider == null)
                    {
                        Debug.Log("[MarioStarPower] Aucun collider trouvé. Ajout d’un SphereCollider.");
                        triggerCollider = gameObject.AddComponent<SphereCollider>();
                        ((SphereCollider)triggerCollider).radius = 1.5f;
                    }
                    else
                    {
                        Debug.Log($"[MarioStarPower] Collider déjà présent: {triggerCollider.GetType().Name}");
                    }
                    GameObject overlay = GameObject.Find("RainbowOverlay");
                    foreach (Transform t in GameObject.FindObjectsOfType<Transform>(true))
                    {
                        if (t.name == "RainbowOverlay")
                            overlay = t.gameObject;
                    }

                    triggerCollider.isTrigger = true;

                    // Vérifie/ajoute un Rigidbody (obligatoire pour que OnTriggerEnter fonctionne)
                    if (GetComponent<Rigidbody>() == null)
                    {
                        Debug.Log("[MarioStarPower] Aucun Rigidbody trouvé. Ajout d’un Rigidbody kinematic.");
                        var rb = gameObject.AddComponent<Rigidbody>();
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    }
                    else
                    {
                        Debug.Log("[MarioStarPower] Rigidbody déjà présent.");
                    }
                }

                public static IEnumerator RPC_PlayMarioStarSound(PlayerAvatar position)
                {
                    Debug.Log("[RPC_PlayMarioStarSound] Début de la méthode.");

                    string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "Mario");
                    Debug.Log($"[RPC_PlayMarioStarSound] Chemin de l'AssetBundle : {bundlePath}");

                    if (AssetBundle == null)
                    {
                        Debug.Log("[RPC_PlayMarioStarSound] Chargement de l'AssetBundle...");
                        AssetBundle = AssetBundle.LoadFromFile(bundlePath);
                    }

                    if (AssetBundle == null)
                    {
                        Debug.LogError("[RPC_PlayMarioStarSound] Échec du chargement de l'AssetBundle.");
                        yield break;
                    }

                    if (starClip == null)
                    {
                        Debug.Log("[RPC_PlayMarioStarSound] Chargement de l'AudioClip 'videoplayback'...");
                        starClip = AssetBundle.LoadAsset<AudioClip>("videoplayback");
                    }

                    if (starClip == null)
                    {
                        Debug.LogError("[RPC_PlayMarioStarSound] Échec du chargement de l'AudioClip.");
                        yield break;
                    }

                    var marioStarEvent = position.gameObject.GetComponent<MarioStarEvent>();
                    if (marioStarEvent == null)
                    {
                        marioStarEvent = position.gameObject.AddComponent<MarioStarEvent>();
                    }

                    var eyeL = position.playerAvatarVisuals.transform.Find("ANIM EYE LEFT/mesh_eye_l");
                    var eyeR = position.playerAvatarVisuals.transform.Find("ANIM EYE RIGHT/mesh_eye_r");

                    foreach (Transform t in position.playerAvatarVisuals.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "mesh_eye_r")
                            eyeR = t;
                        if (t.name == "mesh_eye_l")
                            eyeL = t;
                    }

                    if (eyeR == null || eyeL == null)
                    {
                        Debug.LogWarning("[AlarmEffectController] One or both eyes not found.");
                        yield break;
                    }

                    eyeMatL = eyeL.GetComponent<Renderer>().material;
                    eyeMatR = eyeR.GetComponent<Renderer>().material;

                    Debug.Log("[RPC_PlayMarioStarSound] Configuration de l'AudioSource...");
                    AudioSource audio = position.gameObject.AddComponent<AudioSource>();
                    audio.clip = starClip;
                    audio.spatialBlend = 1f; // 3D spatial
                    audio.maxDistance = 50f;
                    audio.Play();

                    running = true;
                    Debug.Log($"[RPC_PlayMarioStarSound] Lecture de l'audio pendant {starClip.length} secondes.");
                    yield return new WaitForSeconds(starClip.length);
                    running = false;


                    eyeMatL?.SetFloat("_ColorOverlayAmount", 0f);
                    eyeMatR?.SetFloat("_ColorOverlayAmount", 0f);
                    Debug.Log("[RPC_PlayMarioStarSound] Destruction de l'AudioSource.");
                    UnityEngine.Object.Destroy(audio);
                }

                private Enemy FindEnemyRoot(Transform current)
                {
                    while (current != null)
                    {
                        // Si on tombe directement sur un Enemy, parfait
                        Enemy direct = current.GetComponent<Enemy>();
                        if (direct != null)
                            return direct;

                        // Sinon on check s'il y a un EnemyParent, et on va chercher .Enemy
                        EnemyParent parent = current.GetComponent<EnemyParent>();
                        if (parent != null && parent.Enemy != null)
                        {
                            Debug.Log($"[MarioStarPower] Enemy trouvé via EnemyParent → {parent.Enemy.name}");
                            return parent.Enemy;
                        }

                        current = current.parent;
                    }

                    return null;
                }

                private void OnTriggerEnter(Collider other)
                {

                    Enemy enemy = FindEnemyRoot(other.transform);
                    if (enemy != null)
                    {
                        Debug.Log($"[MarioStarPower] {enemy.name} est un ennemi. Tentative de dégâts...");
                        if (enemy.HasHealth && enemy.Health != null && !enemy.Health.dead)
                        {
                            Debug.Log($"[MarioStarPower] Ennemi {enemy.name} valide. Application des 999 dégâts.");
                            enemy.Health.Hurt(999, UnityEngine.Vector3.zero);
                        }
                        else
                        {
                            Debug.LogWarning($"[MarioStarPower] {enemy.name} n’a pas de composant santé valide.");
                        }
                    }
                    else
                    {
                        Debug.Log($"[MarioStarPower] {other.name} n’est pas un ennemi (Enemy introuvable dans la hiérarchie).");
                    }
                }
            }

        } //Finis 

        public class NoMinimap : ISlotEvent
        {
            public string EventName => "NoMinimap";
            public string IconName => "icon_enemy_rain";

            public string Asset => "TestAsset";

            public void Execute()
            {
                MapLockController.LockForSeconds(60f);
            }

            private static class MapLockController
            {
                private static Coroutine lockRoutine;
                private static PlayerAvatar player;
                private static InputKey originalKey = InputKey.Map;

                public static void LockForSeconds(float seconds)
                {
                    player = PlayerAvatar.instance;
                    if (player == null)
                    {
                        Debug.LogError("[MapLockController] PlayerAvatar is null.");
                        return;
                    }

                    if (lockRoutine != null)
                    {
                        player.StopCoroutine(lockRoutine);
                    }

                    List<string> NoMinimap = new List<string>
                    {
                        "Minimap disabled. Guess I’ll follow the trail of my own tears."
                    };

                    GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(NoMinimap));

                    lockRoutine = player.StartCoroutine(LockRoutine(seconds));
                }

                private static IEnumerator LockRoutine(float seconds)
                {
                    Debug.Log("[MapLockController] Map disabled.");
                    var action = InputManager.instance.GetAction(InputKey.Map);
                    string bindingPath = action.bindings[0].overridePath;

                    Debug.Log($"[MapLockController]  {bindingPath} ");
                    while (seconds > 0f)
                    {
                        if (player == null) yield break;

                        // Force la map à rester fermée

                        InputManager.instance.Rebind(InputKey.Map, "<Keyboard>/pause");
                        seconds -= Time.deltaTime;
                        yield return null;
                    }
                    InputManager.instance.Rebind(InputKey.Map, bindingPath);

                    Debug.Log("[MapLockController] Map re-enabled.");
                }
            }
        } //Finis Marche Bien

        /*public class VideoMapEvent : ISlotEvent
        {
            public string EventName => "VideoMap";
            public string IconName => "icon_video";
            public string Asset => "video";

            public async void Execute()
            {
                PlayerAvatar map = PlayerAvatar.instance;

                if (map == null)
                {
                    Debug.LogError("[VideoMapEvent] PlayerAvatar is null.");
                    return;
                }

                Transform display = map.mapToolController.transform.Find("Controller/Visuals/Hide/Main Spring Target/Main Spring/Base Offset/Bob/Stick/stick/Main Unit/Display Spring Target/Display Spring/Counter/display_1x1");
                foreach (Transform t in map.mapToolController.VisualTransform.GetComponentsInChildren<Transform>(true))
                {
                    Debug.LogError($"{t.name}");
                    if (t.name == "display_1x1")
                        display = t;
                }

                var meshRenderer = display.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    Debug.LogError("[VideoMapEvent] MeshRenderer not found on display_1x1.");
                    return;
                }

                System.Random rand = new System.Random();

                AssetBundleRequest request = DifficultyFeature.request1;

                int i = rand.Next(3);
                Debug.LogError($"[VideoMapEvent] {i}");
                switch (i)
                {
                    case 0:
                        {
                            request = DifficultyFeature.request1;
                            break;
                        }
                    case 1:
                        {
                            request = DifficultyFeature.request2;
                            break;
                        }
                    case 2:
                        {
                            request = DifficultyFeature.request1;
                            break;
                        }
                    default:
                        Debug.LogError($"[VideoMapEvent] {i}");
                        break;
                }


                request.completed += (asyncOp) =>
                {
                    VideoClip videoClip = request.asset as VideoClip;
                    if (videoClip == null)
                    {
                        Debug.LogError("[VideoMapEvent] Video clip not found in bundle.");
                        return;
                    }

                    InitVideoPlayer(videoClip, display, map);
                };

            }

            public async void InitVideoPlayer(VideoClip videoClip, Transform display, PlayerAvatar map)
            {
                RenderTexture renderTexture = new RenderTexture(256, 256, 0);
                Debug.LogError("[VideoMapEvent] Where lag 1");
                renderTexture.wrapMode = TextureWrapMode.Clamp;
                renderTexture.filterMode = FilterMode.Point;
                renderTexture.anisoLevel = 0;
                renderTexture.useMipMap = false;
                map.mapToolController.Active = true;

                GameObject videoObject = new GameObject("MinimapVideoPlayer");
                VideoPlayer player = videoObject.AddComponent<VideoPlayer>();

                player.clip = videoClip;
                player.isLooping = false;
                player.playOnAwake = false;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.targetTexture = renderTexture;
                player.audioOutputMode = VideoAudioOutputMode.AudioSource;
                player.SetDirectAudioVolume(0, 0f);


                player.prepareCompleted += (_) =>
                {
                    Debug.Log("[VideoMapEvent] Video prepared, applying to minimap...");
                    player.Play();
                    map.StartCoroutine(UpdateAudioWithMapToggle(player, map));
                    if (display != null && display.TryGetComponent(out MeshRenderer renderer))
                    {
                        renderer.material.mainTexture = renderTexture;
                    }
                };

                Debug.Log("[VideoMapEvent] Video successfully applied to minimap screen.");
            }
            private float volume = 0f;

            private IEnumerator UpdateAudioWithMapToggle(VideoPlayer audioSource, PlayerAvatar map)
            {
                while (audioSource != null && map != null)
                {
                    bool isMapOpen = map.mapToolController.Active;
                    Debug.LogError($"[VideoMapEvent] {isMapOpen}");
                    volume = isMapOpen ? 1f : volume - 0.1f;
                    audioSource.SetDirectAudioVolume(0, volume);
                    yield return new WaitForSeconds(0.1f); // vérifie toutes les 100 ms
                }
            }
        } */ //Probablement a retirer

        public class AlarmEvent : ISlotEvent
        {
            public string EventName => "Alarm";
            public string IconName => "icon_enemy_rain";
            public string Asset => "TestAsset";

            // Code d'événement personnalisé pour Photon
            private const byte ALARM_EVENT_CODE = 1;

            public void Execute()
            {
                PlayerAvatar playerAvatar = PlayerController.instance.playerAvatarScript;
                if (playerAvatar == null)
                {
                    Debug.LogWarning("[AlarmEvent] PlayerAvatar is null.");
                    return;
                }

                Debug.Log("[AlarmEvent] Triggering alarm via RaiseEvent.");

                PhotonView photonView = playerAvatar.GetComponent<PhotonView>();
                if (photonView == null)
                {
                    Debug.LogError("[AlarmEvent] No PhotonView on PlayerAvatar.");
                    return;
                }
                List<string> Alarm = new List<string>
                {
                    "ERROR, MY SYSTEM IS BROKEN.",
                    "I don’t have error, but I need some attention, ALARM ON.",
                    "Time to break your ear, watch out."
                };
                GenerateText.SemiBotTalk(GenerateText.GenerateAffectionateSentence(Alarm));

                // Préparer les données de l'événement
                object[] eventData = new object[] { photonView.ViewID, 15f }; // viewID et duration

                // Envoyer l'événement à tous les joueurs
                RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                PhotonNetwork.RaiseEvent(ALARM_EVENT_CODE, eventData, raiseEventOptions, SendOptions.SendReliable);
                AlarmEffectController.Trigger(playerAvatar, 15f);

                Debug.Log($"[AlarmEvent] RaiseEvent sent with ViewID: {photonView.ViewID}, Duration: 5f");
            }

            // Le reste de la classe AlarmEffectController reste inchangé pour l'instant
            public class AlarmEffectController : MonoBehaviour
            {
                public float duration = 3f;
                public AudioClip alarmClip;

                private AudioSource audioSource;
                private Material eyeMatL, eyeMatR;
                private Transform head;
                private float timer;
                private bool running;
                public static AssetBundle bundle;

                public static void Trigger(PlayerAvatar avatar, float duration)
                {
                    Debug.Log("[AlarmEffectController] Trigger called.");

                    var controller = avatar.gameObject.AddComponent<AlarmEffectController>();
                    controller.duration = duration;

                    string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "alarm");
                    Debug.Log($"[AlarmEffectController] Loading asset bundle from: {bundlePath}");

                    if (bundle == null)
                    {
                        bundle = AssetBundle.LoadFromFile(bundlePath);
                    }

                    if (bundle == null)
                    {
                        Debug.LogError("[AlarmEffectController] Failed to load AssetBundle.");
                        return;
                    }

                    if (controller.alarmClip == null)
                        controller.alarmClip = bundle.LoadAsset<AudioClip>("alarm_event");

                    if (controller.alarmClip == null)
                    {
                        Debug.LogError("[AlarmEffectController] Alarm clip not found in bundle.");
                    }
                    else
                    {
                        Debug.Log("[AlarmEffectController] Alarm clip loaded successfully.");
                    }
                }

                private void Start()
                {
                    Debug.Log("[AlarmEffectController] Start called.");

                    if (!TrySetup()) return;

                    timer = duration;
                    running = true;

                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.clip = alarmClip;
                    audioSource.loop = true;
                    audioSource.spatialBlend = 1f;
                    audioSource.volume = 1f;
                    audioSource.maxDistance = 30f;
                    audioSource.Play();

                    Debug.Log("[AlarmEffectController] Alarm sound started.");
                    Debug.Log("[AlarmEffectController] Enemies alerted by alarm.");
                }

                private bool TrySetup()
                {
                    PlayerAvatar avatar = GetComponent<PlayerAvatar>();
                    if (avatar == null)
                    {
                        Debug.LogError("[AlarmEffectController] PlayerAvatar component not found.");
                        return false;
                    }

                    Debug.Log("[AlarmEffectController] Locating eyes and head transforms.");

                    var eyeL = avatar.playerAvatarVisuals.transform.Find("ANIM EYE LEFT/mesh_eye_l");
                    var eyeR = avatar.playerAvatarVisuals.transform.Find("ANIM EYE RIGHT/mesh_eye_r");
                    head = avatar.playerAvatarVisuals.transform.Find("ANIM HEAD TOP");

                    foreach (Transform t in avatar.playerAvatarVisuals.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "mesh_eye_r")
                            eyeR = t;
                        if (t.name == "mesh_eye_l")
                            eyeL = t;
                        if (t.name == "ANIM HEAD TOP")
                            head = t;
                    }

                    if (eyeR == null || eyeL == null || head == null)
                    {
                        Debug.LogWarning("[AlarmEffectController] One or both eyes not found.");
                        return false;
                    }

                    eyeMatL = eyeL.GetComponent<Renderer>().material;
                    eyeMatR = eyeR.GetComponent<Renderer>().material;
                    Debug.Log("[AlarmEffectController] Eyes and head initialized.");

                    return true;
                }

                private void Update()
                {
                    if (!running) return;

                    EnemyDirector.instance.SetInvestigate(transform.position, 5f);
                    float intensity = Mathf.PingPong(Time.time * 5f, 1f);
                    eyeMatL.SetColor("_ColorOverlay", Color.red);
                    eyeMatR.SetColor("_ColorOverlay", Color.red);
                    eyeMatL.SetFloat("_ColorOverlayAmount", intensity);
                    eyeMatR.SetFloat("_ColorOverlayAmount", intensity);

                    if (head)
                    {
                        head.localRotation = UnityEngine.Quaternion.Euler(
                            Mathf.Sin(Time.time * 20f) * 2f,
                            Mathf.Sin(Time.time * 15f) * 2f,
                            0f
                        );
                    }

                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        Debug.Log("[AlarmEffectController] Alarm duration ended. Stopping effect.");
                        StopEffect();
                    }
                }

                private void StopEffect()
                {
                    running = false;

                    eyeMatL?.SetFloat("_ColorOverlayAmount", 0f);
                    eyeMatR?.SetFloat("_ColorOverlayAmount", 0f);
                    if (head) head.localRotation = UnityEngine.Quaternion.identity;

                    audioSource?.Stop();
                    Debug.Log("[AlarmEffectController] Alarm effect stopped. Cleaning up.");
                    Destroy(this);
                }
            }
        }//Fonctionne Bien
    }

}



