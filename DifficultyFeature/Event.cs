using BepInEx;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Extensions;
using REPOLib.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using static DifficultyFeature.Event;

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

                Debug.Log("[AlarmEvent] Triggering AlarmEffectController...");
                AlarmEffectController.Trigger(playerAvatar, duration: 5f);
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
                Debug.Log("[AlarmEffect] Creating AlarmEffectController on player...");
                var controller = avatar.gameObject.AddComponent<AlarmEffectController>();
                controller.duration = duration;

                string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "alarm");
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Debug.LogError("[AlarmEffect] Failed to load AssetBundle!");
                    return;
                }

                controller.alarmClip = bundle.LoadAsset<AudioClip>("alarm_event");
                if (controller.alarmClip == null)
                {
                    Debug.LogError("[AlarmEffect] AudioClip 'alarm_event' not found in bundle!");
                }
                else
                {
                    Debug.Log("[AlarmEffect] AudioClip loaded successfully.");
                }
            }

            private void Start()
            {
                Debug.Log("[AlarmEffect] Starting AlarmEffectController...");

                if (!TrySetup()) return;

                timer = duration;
                running = true;

                Debug.Log("[AlarmEffect] Setting up AudioSource and playing sound...");
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = alarmClip;
                audioSource.loop = true;
                audioSource.spatialBlend = 1f;
                audioSource.volume = 0.3f;
                audioSource.maxDistance = 30f;
                audioSource.Play();

                Debug.Log("[AlarmEffect] Alerting enemies via SetInvestigate...");
                EnemyDirector.instance.SetInvestigate(transform.position, 5f);
            }

            private bool TrySetup()
            {
                Debug.Log("[AlarmEffect] Searching for eye and head transforms...");
                PlayerAvatar avatar = PlayerController.instance.playerAvatarScript;
                if (avatar == null)
                {
                    Debug.LogError("[AlarmEffect] PlayerAvatar component not found.");
                    return false;
                }

                var eyeL = avatar.playerAvatarVisuals.transform.Find("mesh_eye_l");
                var eyeR = avatar.playerAvatarVisuals.transform.Find("mesh_eye_r");
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

                Debug.LogWarning($"[AlarmEffect] {eyeL}");
                Debug.LogWarning($"[AlarmEffect] {eyeR}");
                Debug.LogWarning($"[AlarmEffect] {head}");

                if (eyeL == null || eyeR == null || head == null)
                {
                    Debug.LogWarning("[AlarmEffect] One or more key transforms not found (eyeL, eyeR, head).");
                    Destroy(this);
                    return false;
                }

                Debug.Log("[AlarmEffect] Eye and head transforms found successfully.");
                eyeMatL = eyeL.GetComponent<Renderer>().material;
                eyeMatR = eyeR.GetComponent<Renderer>().material;
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
                    Debug.Log("[AlarmEffect] Timer finished, stopping effect.");
                    StopEffect();
                }
            }

            private void StopEffect()
            {
                running = false;
                Debug.Log("[AlarmEffect] Resetting visuals and stopping audio.");

                eyeMatL?.SetFloat("_ColorOverlayAmount", 0f);
                eyeMatR?.SetFloat("_ColorOverlayAmount", 0f);
                if (head) head.localRotation = Quaternion.identity;

                if (audioSource != null)
                {
                    audioSource.Stop();
                }

                Debug.Log("[AlarmEffect] AlarmEffectController destroyed.");
                Destroy(this);
            }
        }

    }



}
