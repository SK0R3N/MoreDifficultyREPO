using BepInEx;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice;
using REPOLib.Extensions;
using REPOLib.Modules;
using SingularityGroup.HotReload;
using Steamworks;
using Steamworks.Ugc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
using static MonoMod.Cil.RuntimeILReferenceBag.FastDelegateInvokers;

namespace DifficultyFeature
{
    internal class Event
    {
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
                    gunInstance2 = PhotonNetwork.Instantiate("items/Golden_Gun", player.transform.position + player.transform.forward, Quaternion.identity);
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

                int count = 15;
                float radius = 10f;
                Vector3 center = player.transform.position;

                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    Vector3 spawnPos = center + offset;


                    spawnPos.y += 2f;

                    Debug.Log($"[EnemyRainEvent] Spawning enemy #{i + 1} at {spawnPos}");
                    Enemies.SpawnEnemy(enemySetup, spawnPos, Quaternion.identity, spawnDespawned: false);
                }
            }
        }

        public class MarioStarEvent : ISlotEvent
        {
            public string EventName => "MarioStar";
            public string IconName => "icon_enemy_rain";

            public string Asset => "TestAsset";
            public AssetBundle AssetBundle { get; set; }

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

                AudioClip starClip = AssetBundle.LoadAsset<AudioClip>("videoplayback");
                if (starClip == null)
                {
                    Debug.LogError("[AlarmEffectController] Failed to load Star.");
                    return;
                }

                PlayerAvatar avatar = PlayerAvatar.instance;
                avatar.StartCoroutine(ApplyMarioStarEffect(avatar, starClip));
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
                    PhotonView photonView = avatar.GetComponent<PhotonView>();
                    photonView.RPC("RPC_PlayMarioStarSound", RpcTarget.Others, avatar.transform.position);
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
            }

            [PunRPC]
            public void RPC_PlayMarioStarSound(Vector3 position, PhotonMessageInfo info)
            {
                // Joué pour les autres joueurs
                string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "Mario");
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null) return;

                AudioClip starClip = bundle.LoadAsset<AudioClip>("Star");
                if (starClip == null) return;

                GameObject obj = new GameObject("StarSoundRemote");
                obj.transform.position = position;

                AudioSource audio = obj.AddComponent<AudioSource>();
                audio.clip = starClip;
                audio.spatialBlend = 1f; // 3D spatial
                audio.maxDistance = 50f;
                audio.Play();

                UnityEngine.Object.Destroy(obj, starClip.length + 1f);
            }

           
        }

        public class MarioStarPower : MonoBehaviour
        {
            public float duration = 5f;
            private Collider triggerCollider;

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
                        enemy.Health.Hurt(999, Vector3.zero);
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

        public class NoMinimap : ISlotEvent
        {
            public string EventName => "NoMinimap";
            public string IconName => "icon_enemy_rain";

            public string Asset => "TestAsset";

            public void Execute()
            {
                MapLockController.LockForSeconds(60f); 
            }
        }

        public static class MapLockController
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

        public class VideoMapEvent : ISlotEvent
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
        }

        public class AlarmEvent : ISlotEvent
        {
            public string EventName => "Alarm";
            public string IconName => "icon_enemy_rain";
            public string Asset => "TestAsset";

            public void Execute()
            {
                PlayerAvatar playerAvatar = PlayerController.instance.playerAvatarScript;
                if (playerAvatar == null)
                {
                    Debug.LogWarning("[AlarmEvent] PlayerAvatar is null.");
                    return;
                }

                Debug.Log("[AlarmEvent] Triggering alarm via RPC.");

                PhotonView photonView = playerAvatar.GetComponent<PhotonView>();
                if (photonView == null)
                {
                    Debug.LogError("[AlarmEvent] No PhotonView on PlayerAvatar.");
                    return;
                }

                photonView.RPC("TriggerAlarmRPC", RpcTarget.All, photonView.ViewID, 5f);
            }
        }

        public class AlarmEffectController : MonoBehaviour
        {
            public float duration = 3f;
            public AudioClip alarmClip;

            private AudioSource audioSource;
            private Material eyeMatL, eyeMatR;
            private Transform head;
            private float timer;
            private bool running;

            public static void Trigger(PlayerAvatar avatar, float duration)
            {
                Debug.Log("[AlarmEffectController] Trigger called.");

                var controller = avatar.gameObject.AddComponent<AlarmEffectController>();
                controller.duration = duration;

                string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "alarm");
                Debug.Log($"[AlarmEffectController] Loading asset bundle from: {bundlePath}");

                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Debug.LogError("[AlarmEffectController] Failed to load AssetBundle.");
                    return;
                }

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
                audioSource.volume = 0.3f;
                audioSource.maxDistance = 30f;
                audioSource.Play();

                Debug.Log("[AlarmEffectController] Alarm sound started.");

                EnemyDirector.instance.SetInvestigate(transform.position, 5f);
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
                    Debug.LogWarning("[EyeFlashEffect] One or both eyes not found.");
                    return false
;
                }

                eyeMatL = eyeL.GetComponent<Renderer>().material;
                eyeMatR = eyeR.GetComponent<Renderer>().material;
                Debug.Log("[AlarmEffectController] Eyes and head initialized.");


                return true;
            }

            private void Update()
            {
                if (!running) return;

                float intensity = Mathf.PingPong(Time.time * 5f, 1f);
                eyeMatL.SetColor("_ColorOverlay", Color.red);
                eyeMatR.SetColor("_ColorOverlay", Color.red);
                eyeMatL.SetFloat("_ColorOverlayAmount", intensity);
                eyeMatR.SetFloat("_ColorOverlayAmount", intensity);

                if (head)
                {
                    head.localRotation = Quaternion.Euler(
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
                if (head) head.localRotation = Quaternion.identity;

                audioSource?.Stop();
                Debug.Log("[AlarmEffectController] Alarm effect stopped. Cleaning up.");
                Destroy(this);
            }

            [PunRPC]
            public static void TriggerAlarmRPC(int viewId, float duration)
            {
                if (!PhotonView.Find(viewId))
                {
                    Debug.LogError($"[AlarmEffectController] ViewID {viewId} not found.");
                    return;
                }

                GameObject target = PhotonView.Find(viewId).gameObject;
                PlayerAvatar avatar = target.GetComponent<PlayerAvatar>();

                if (avatar == null)
                {
                    Debug.LogError("[AlarmEffectController] No PlayerAvatar on target object.");
                    return;
                }

                Debug.Log($"[AlarmEffectController] RPC triggered for player {viewId}");
                Trigger(avatar, duration);
            }
        }


    }



}
