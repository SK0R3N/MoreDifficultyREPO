using BepInEx;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using REPOLib.Extensions;
using REPOLib.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

namespace DifficultyFeature.DifficultyUpdate
{
    public class ExplosiveDeathEvent : MonoBehaviour
    {
        public const byte ExplosiveDeathEventCode = 100; // Changé à 200 pour éviter tout conflit

        [SerializeField]
        private GameObject explosionPrefab;
        private static bool isExplosiveDeathActive = false;
        private static float effectDuration = 180f;
        private static float checkInterval = 0.1f;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log("[ExplosiveDeathEvent] Awake called. Initializing NetworkEventHandler.");
        }

        public void Execute()
        {
            CoroutineRunner.instance.StartCoroutine(ExecuteCoroutine());
        }

        private IEnumerator ExecuteCoroutine()
        {
            if (isExplosiveDeathActive)
            {
                Debug.LogWarning("[ExplosiveDeathEvent] Explosive death event is already active. Ignoring.");
                yield break;
            }

            if (EnemyDirector.instance == null)
            {
                Debug.LogWarning("[ExplosiveDeathEvent] EnemyDirector.instance is null. Cannot execute.");
                yield break;
            }

            Debug.Log($"[ExplosiveDeathEvent] Starting explosive death event for {effectDuration} seconds.");
            isExplosiveDeathActive = true;
            GameObject managerObj = new GameObject("ExplosiveDeathManager");
            var manager = managerObj.AddComponent<ExplosiveDeathManager>();
            manager.Initialize(this, effectDuration);
        }

        public static void CreateExplosion(UnityEngine.Vector3 position, string sourceName)
        {
            try
            {
                GameObject explosionPrefab = Resources.Load<GameObject>("Effects/Part Prefab Explosion");
                if (explosionPrefab == null)
                {
                    Debug.LogError($"[ExplosiveDeathEvent] Failed to load explosion prefab for {sourceName}. Skipping explosion.");
                    return;
                }

                GameObject explosionObj = Instantiate(explosionPrefab, position, UnityEngine.Quaternion.identity);
                Debug.Log($"[ExplosiveDeathEvent] Instantiated explosion for {sourceName} at {position}: {explosionObj.name}");

                ParticleScriptExplosion explosionScript = explosionObj.GetComponent<ParticleScriptExplosion>();
                if (explosionScript != null)
                {
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
                        Destroy(particleEffect.gameObject, 2f);
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

                Destroy(explosionObj, 2f);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ExplosiveDeathEvent] Error creating explosion for {sourceName}: {ex.Message}");
            }
        }

        private class ExplosiveDeathManager : MonoBehaviour
        {
            private ExplosiveDeathEvent parentEvent;
            private HashSet<EnemyParent> deadEnemies = new HashSet<EnemyParent>();
            private HashSet<UnityEngine.Vector3> explosionPositions = new HashSet<UnityEngine.Vector3>();

            public void Initialize(ExplosiveDeathEvent parent, float duration)
            {
                parentEvent = parent;
                DontDestroyOnLoad(gameObject);
                Debug.Log("[ExplosiveDeathEvent] Manager initialized.");

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
                            continue;
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
                    if (!PhotonNetwork.IsConnectedAndReady)
                    {
                        Debug.LogWarning("[ExplosiveDeathEvent] Photon is not connected. Cannot raise event.");
                        return;
                    }

                    object[] eventData = new object[] { position, enemyName };
                    Debug.Log($"[ExplosiveDeathEvent] Preparing to raise event. Position: {position}, EnemyName: {enemyName}");
                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                    bool success = PhotonNetwork.RaiseEvent(ExplosiveDeathEventCode, eventData, raiseEventOptions, SendOptions.SendReliable);
                    Debug.Log($"[ExplosiveDeathEvent] Raised event {ExplosiveDeathEventCode} for {enemyName} at {position}. Success: {success}");
                }
                else
                {
                    CreateExplosion(position, enemyName);
                }
            }
        }
    }
}