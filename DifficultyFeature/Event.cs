using BepInEx;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Extensions;
using REPOLib.Modules;
using SingularityGroup.HotReload;
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
using UnityEngine.Video;
using static DifficultyFeature.Event;
using static MonoMod.Cil.RuntimeILReferenceBag.FastDelegateInvokers;

namespace DifficultyFeature
{
    internal class Event
    {
        public class EnemyRainEvent : ISlotEvent
        {
            public string EventName => "EnemyRain";
            public string IconName => "icon_enemy_rain";

            public string Asset => "TestAsset";

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null) return;

                Vector3 spawnPos = player.transform.position + player.transform.forward * 5f;

                Debug.Log("[SlotEvent] Spawning enemy in front of player.");

                string name = "Headman";

                if (!EnemyDirector.instance.TryGetEnemyThatContainsName(name, out EnemySetup enemySetup))
                {
                    Debug.Log("[SlotEvent] EnemyNotfound");
                    return;
                }

                Enemies.SpawnEnemy(enemySetup, spawnPos, Quaternion.identity, spawnDespawned: false);
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
