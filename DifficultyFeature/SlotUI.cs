using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DifficultyFeature
{
    public class SlotMachineLoader : MonoBehaviour
    {
        public static GameObject slotMachinePrefab;

        public static void Load(AssetBundle bundle)
        {
            slotMachinePrefab = bundle.LoadAsset<GameObject>("SlotMachineUI");
            Debug.Log("[SlotMachine] Prefab loaded");
        }
    }

    public static class SlotAssetLoader
    {
        private static AssetBundle slotBundle;
        private static GameObject slotPrefab;
        public static GameObject currentInstance;

        private const string BundleName = "slotui";
        private const string PrefabName = "SlotMachineUI";

        public static void LoadSlotAsset()
        {
            string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", BundleName);

            if (!File.Exists(bundlePath))
            {
                Debug.LogError($"[SlotAssetLoader] AssetBundle not found at: {bundlePath}");
                return;
            }

            slotBundle = AssetBundle.LoadFromFile(bundlePath);
            if (slotBundle == null)
            {
                Debug.LogError("[SlotAssetLoader] Failed to load AssetBundle.");
                return;
            }

            slotPrefab = slotBundle.LoadAsset<GameObject>(PrefabName);
            if (slotPrefab == null)
            {
                Debug.LogError($"[SlotAssetLoader] Prefab '{PrefabName}' not found in AssetBundle.");
            }
            else
            {
                Debug.Log("[SlotAssetLoader] SlotMachineUI prefab loaded successfully.");
            }


        }
        public static void ShowSlotMachineUI()
        {
            if (slotPrefab == null)
            {
                Debug.LogWarning("[SlotAssetLoader] Prefab not loaded, call LoadSlotAsset() first.");
                return;
            }

            if (currentInstance != null)
            {
                GameObject.Destroy(currentInstance);
            }

            GameObject healthUI = GameObject.Find("Health");
            if (healthUI == null)
            {
                Debug.LogError("[SlotAssetLoader] Could not find Health UI.");
                return;
            }

            Transform hudCanvas = healthUI.transform.parent?.parent?.parent;
            if (hudCanvas == null || hudCanvas.GetComponent<Canvas>() == null)
            {
                Debug.LogError("[SlotAssetLoader] HUD Canvas not found.");
                return;
            }


            Debug.Log("[SlotAssetLoader] SlotMachineUI added to HUD Canvas.");

            currentInstance = GameObject.Instantiate(slotPrefab);
           // currentInstance.transform.localScale = Vector3.zero;
            currentInstance.name = "SlotMachineUI_Instance";
            currentInstance.transform.SetParent(hudCanvas, false);
            Debug.Log($"[SlotAssetLoader] Slot parented under: {hudCanvas.name}");
            var slotScript = currentInstance.AddComponent<SlotMachineUI>();


            // Ajoute ce log :
            foreach (Transform child in currentInstance.transform)
            {
                Debug.Log($"[SlotAssetLoader] Child of SlotMachineUI: {child.name}, active: {child.gameObject.activeSelf}");
            }

            CoroutineHelper.Instance.StartCoroutine(DelayedTrigger(slotScript));

            

        }

        private static IEnumerator DelayedTrigger(SlotMachineUI slotScript)
        {
            yield return null;
            slotScript.TriggerSlotAnimation(UnityEngine.Random.Range(0, 3));
        }

        public static void HideSlotMachineUI()
        {
            if (currentInstance != null)
            {
                GameObject.Destroy(currentInstance);
                currentInstance = null;
                Debug.Log("[SlotAssetLoader] SlotMachineUI destroyed.");
            }
        }
    }

    public class CoroutineHelper : MonoBehaviour
    {
        private static CoroutineHelper _instance;

        public static CoroutineHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject helperObj = new GameObject("CoroutineHelper");
                    _instance = helperObj.AddComponent<CoroutineHelper>();
                    DontDestroyOnLoad(helperObj);
                }
                return _instance;
            }
        }
    }

}
