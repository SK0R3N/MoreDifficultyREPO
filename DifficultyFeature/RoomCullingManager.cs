using HarmonyLib;
using Photon.Pun;
using SingularityGroup.HotReload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DifficultyFeature
{
    internal class RoomCullingManager : MonoBehaviour
    {
        private GameObject playerGameObject;
        private List<GameObject> sceneObjects = new List<GameObject>();
        private float cullingDistance = 50f; // Distance max pour garder un objet actif
        private float activationDistance = 40f; // Distance pour réactiver
        private float updateInterval = 1f; // Intervalle augmenté pour moins de charge
        private HashSet<string> excludedTags = new HashSet<string> { "Player", "MainCamera", "UI" };
        private int objectsPerFrame = 50; // Limite d'objets traités par frame

        // Initialisation
        public void Initialize(GameObject player)
        {
            playerGameObject = player;
            Debug.Log("[RoomCullingManager] Initialized with Player.");

            FindAllSceneObjects();
            StartCoroutine(CullObjectsCoroutine());
        }

        private void FindAllSceneObjects()
        {
            var renderers = FindObjectsOfType<Renderer>();
            var colliders = FindObjectsOfType<Collider>();
            HashSet<GameObject> uniqueObjects = new HashSet<GameObject>();

            foreach (var renderer in renderers)
            {
                GameObject go = renderer.gameObject;
                if (!excludedTags.Contains(go.tag) && go.GetComponent<PhotonView>() == null)
                {
                    uniqueObjects.Add(go);
                }
            }
            foreach (var collider in colliders)
            {
                GameObject go = collider.gameObject;
                if (!excludedTags.Contains(go.tag) && go.GetComponent<PhotonView>() == null)
                {
                    uniqueObjects.Add(go);
                }
            }

            sceneObjects.AddRange(uniqueObjects);
            Debug.Log($"[RoomCullingManager] Found {sceneObjects.Count} objects to cull.");
        }

        private IEnumerator CullObjectsCoroutine()
        {
            while (true)
            {
                if (playerGameObject == null) yield return null;

                Vector3 playerPosition = PlayerAvatar.instance.transform.position;
                int processedCount = 0;

                foreach (GameObject obj in sceneObjects)
                {
                    if (obj == null) continue;

                    float distance = Vector3.Distance(playerPosition, obj.transform.position);

                    if (distance > cullingDistance && obj.activeSelf)
                    {
                        obj.SetActive(false);
                        Debug.Log($"[RoomCullingManager] Disabled object {obj.name} at {obj.transform.position}");
                    }
                    else if (distance < activationDistance && !obj.activeSelf)
                    {
                        obj.SetActive(true);
                        Debug.Log($"[RoomCullingManager] Enabled object {obj.name} at {obj.transform.position}");
                    }

                    processedCount++;
                    if (processedCount >= objectsPerFrame)
                    {
                        processedCount = 0;
                        yield return null; // Étaler sur plusieurs frames
                    }
                }

                yield return new WaitForSeconds(updateInterval);
            }
        }
    }
}
