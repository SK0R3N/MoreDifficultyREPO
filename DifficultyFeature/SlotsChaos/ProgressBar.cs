using BepInEx;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using static b;

namespace DifficultyFeature.SlotsChaos
{
    public static class ProgressBar
    {
        private static AssetBundle slotBundle;
        private static GameObject slotPrefab;
        private static GameObject progressPrefab;
        public static GameObject currentInstance;
        public static UnityEngine.UI.Image currentInstanceProgress; 
        private static UnityEngine.UI.Image progressBarFill; // Le remplissage (ProgressBar)
        public static double progress = 260;
        private const double PROGRESSMAX = 1000;

        public static void LoadProgressAsset()
        {
            string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "Progress");

            if (!File.Exists(bundlePath))
            {
                Debug.LogError($"[SlotAssetLoader] AssetBundle not found at: {bundlePath}");
                return;
            }
            if (slotBundle == null)
                slotBundle = AssetBundle.LoadFromFile(bundlePath);

            if (slotBundle == null)
            {
                Debug.LogError("[SlotAssetLoader] Failed to load AssetBundle.");
                return;
            }
            if (slotPrefab == null)
                slotPrefab = slotBundle.LoadAsset<GameObject>("ProgressBarSlot");
            if (progressPrefab == null)
                progressPrefab = slotBundle.LoadAsset<GameObject>("ProgressBar");

            if (slotPrefab == null)
            {
                Debug.LogError($"[SlotAssetLoader] Prefab 'ProgressBarSlot' not found in AssetBundle.");
                return;
            }

            Debug.Log("[SlotAssetLoader] ProgressBarSlot prefab loaded successfully.");
            ShowSlotProgressUI();
        }

        public static void ShowSlotProgressUI()
        {
            if (slotPrefab == null)
            {
                Debug.LogWarning("[SlotAssetLoader] Prefab not loaded, call LoadSlotAsset() first.");
                return;
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

            Debug.Log("[SlotAssetLoader] ProgressBarSlot added to HUD Canvas.");

            // Instancier le prefab ProgressBarSlot (contient l'icône et la barre)
            currentInstance = UnityEngine.Object.Instantiate(slotPrefab);
            currentInstance.name = "ProgressBarSlot_Instance";
            currentInstance.transform.SetParent(hudCanvas, false);

            currentInstanceProgress = UnityEngine.Object.Instantiate(progressPrefab).GetComponent<UnityEngine.UI.Image>();
            currentInstanceProgress.name = "ProgressBar_Instance";
            currentInstanceProgress.transform.SetParent(hudCanvas, false);

            // Masquer les instances par défaut
            currentInstance.SetActive(false);
            currentInstanceProgress.gameObject.SetActive(false);

            // Initialiser la progression
            progress = 260;
            double progressNormalized = progress / PROGRESSMAX;
            currentInstanceProgress.fillAmount = Mathf.Clamp01((float)progressNormalized);

            Debug.Log($"[SlotAssetLoader] Slot parented under: {hudCanvas.name}");

            foreach (Transform child in currentInstance.transform)
            {
                Debug.Log($"[SlotAssetLoader] Child of ProgressBarSlot: {child.name}, active: {child.gameObject.activeSelf}");
            }
        }

        // Méthode pour mettre à jour la progression
        private static IEnumerator AnimatePopUp(GameObject target, float duration, float overshootScale = 1.2f)
        {
            if (target == null) yield break;

            Debug.Log($"[ProgressBar] Starting PopUp animation for {target.name}");
            target.SetActive(true); // Rendre visible

            Transform targetTransform = target.transform;
            // Stocker l'échelle initiale du prefab ou une échelle par défaut
            Vector3 originalScale = targetTransform.localScale == Vector3.zero ? Vector3.one : targetTransform.localScale;
            Vector3 targetScale = originalScale * overshootScale;

            // S'assurer que l'échelle de départ est correcte
            targetTransform.localScale = originalScale;

            float elapsed = 0f;

            // Pop-up : agrandir
            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.5f);
                targetTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }

            // Revenir à la taille normale
            elapsed = 0f;
            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.5f);
                targetTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
                yield return null;
            }

            targetTransform.localScale = originalScale; // S'assurer que la taille est exacte
            Debug.Log($"[ProgressBar] PopUp animation completed for {target.name}");
        }

        // Méthode pour animer la disparition
        private static IEnumerator AnimatePopOut(GameObject target, float duration)
        {
            if (target == null) yield break;

            Debug.Log($"[ProgressBar] Starting PopOut animation for {target.name}");
            Transform targetTransform = target.transform;
            Vector3 originalScale = targetTransform.localScale;
            Vector3 targetScale = Vector3.zero;

            float elapsed = 0f;

            // Réduire à zéro
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                targetTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }

            targetTransform.localScale = Vector3.zero;
            Debug.Log($"[ProgressBar] PopOut animation completed for {target.name}");
        }

        // Méthode pour mettre à jour la progression avec animations
        public static IEnumerator UpdateProgress(int newProgress)
        {
            if (currentInstanceProgress != null && currentInstance != null)
            {
                Debug.Log($"[ProgressBar] Updating progress from {progress} to {newProgress}");

                // Lancer les animations de pop-up
                CoroutineRunner.instance.StartCoroutine(AnimatePopUp(currentInstance, 0.4f));
                CoroutineRunner.instance.StartCoroutine(AnimatePopUp(currentInstanceProgress.gameObject, 0.4f));

                // Attendre un court instant pour que l'animation pop-up commence
                yield return new WaitForSeconds(0.2f);

                // Animation de la barre de progression
                for (double i = 0; i < Math.Abs(newProgress - progress); i += 1)
                {
                    double pourcentage = (newProgress - progress > 0 ? (newProgress - i) / newProgress : (progress - i) / progress);
                    yield return new WaitForSeconds(0.04f * (float)pourcentage);

                    if(progress + i == PROGRESSMAX)
                    {
                        yield return new WaitForSeconds(0.5f);
                        CoroutineRunner.instance.StartCoroutine(AnimatePopOut(currentInstance, 0.3f));
                        CoroutineRunner.instance.StartCoroutine(AnimatePopOut(currentInstanceProgress.gameObject, 0.3f));

                        yield return new WaitForSeconds(0.5f);
                        SlotAssetLoader.ShowSlotMachineUI();
                        progress = 260;
                        double progressNormalized2 = progress / PROGRESSMAX;
                        currentInstanceProgress.fillAmount = Mathf.Clamp01((float)progressNormalized2);
                        break;
                    }

                    double progressNormalized = (progress + (newProgress > progress ? i : -i)) / PROGRESSMAX;
                    Debug.Log($"[ProgressBar] Progress normalized: {progressNormalized}");
                    currentInstanceProgress.fillAmount = Mathf.Clamp01((float)progressNormalized);
                }

                progress = newProgress;
                Debug.Log($"[ProgressBar] Progress updated to {progress}");


                    yield return new WaitForSeconds(0.5f); 
                    CoroutineRunner.instance.StartCoroutine(AnimatePopOut(currentInstance, 0.3f));
                    CoroutineRunner.instance.StartCoroutine(AnimatePopOut(currentInstanceProgress.gameObject, 0.3f));
            }
            else
            {
                Debug.LogError("[ProgressBar] UpdateProgress failed: currentInstance or currentInstanceProgress is null");
            }
        }

        public static void PlusProgress()
        {
            if (currentInstanceProgress != null)
            {
               /* UpdateProgress(progress + 10)*/;

            }
        }
        public static void MinusProgress()
        {
            if (currentInstanceProgress != null)
            {
                /*UpdateProgress(progress - 10)*/;

            }
        }

        public static void RaiseProgressIncrementEvent(int points)
        {
            if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
            {
                object[] content = new object[] { points };
                RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
                PhotonNetwork.RaiseEvent(10, content, raiseEventOptions, SendOptions.SendReliable);
                Debug.Log($"[ProgressBar] Raised progress increment event with {points} points.");
            }
            else
            {
                // Fallback for single-player or offline mode
                UpdateProgress(points);
                Debug.Log($"[ProgressBar] Offline mode: Added {points} progress points directly.");
            }
        }
    }

    [HarmonyPatch]
    public static class Patches
    {
        // Patch for EnemyDirector to detect enemy death
        public static bool onetime = true;
        public static bool onetimeMob = true;
        public static bool onetimeDeath = true;

        [HarmonyPatch(typeof(EnemyDirector), "AddEnemyValuable")]
        [HarmonyPostfix]
        public static void EnemyParent_Despawn_Postfix(EnemyParent __instance)
        {
            if (onetimeMob)
            {
                onetimeMob = false;
                CoroutineRunner.instance.StartCoroutine(Wait());

                int points = 1000;
                ProgressBar.RaiseProgressIncrementEvent(points);
                Debug.Log($"[Patches] Enemy died, raising event to add {points} progress points.");
            }

        }

        [HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance)
        {

            if (onetimeDeath)
            {
                onetimeDeath = false;
                CoroutineRunner.instance.StartCoroutine(Wait());

                int points = 75;
                ProgressBar.RaiseProgressIncrementEvent(points);
                Debug.Log($"[Patches] Enemy died, raising event to add {points} progress points.");
            }
        }

        // Patch for ExtractionPoint to detect completion
        [HarmonyPatch(typeof(ExtractionPoint), "StateSet")]
        [HarmonyPostfix]
        public static void ExtractionPoint_StateSet_Postfix(ExtractionPoint __instance, ExtractionPoint.State newState)
        {
            // Check if the extraction point is moving to the Complete state
            if (newState == ExtractionPoint.State.Complete && onetime)
            {
                onetime = false;
                CoroutineRunner.instance.StartCoroutine(Wait());

                int points = 200; 
                ProgressBar.RaiseProgressIncrementEvent(points);
                Debug.Log($"[Patches] Extraction point completed, raising event to add {points} progress points.");
            }
        }

        public static IEnumerator Wait()
        {
            yield return new WaitForSeconds(0.1f);
            onetime = true;
            onetimeMob = true;
            onetimeDeath = true;
        }
    }

    // Classe helper pour ajouter un MonoBehaviour à un GameObject statique
    public class MonoBehaviourHelper : MonoBehaviour
    {
    }

    // Coroutine pour augmenter la progression (exemple)
    //private static IEnumerator IncreaseProgressOverTime()
    //{
    //    progress = 0; // Réinitialiser la progression
    //    while (progress < PROGRESSMAX)
    //    {
    //        progress += 10; // Augmenter de 10 à chaque étape (ajuste selon tes besoins)
    //        UpdateProgress();
    //        yield return new WaitForSeconds(0.1f); // Attendre 0.1 seconde entre chaque étape
    //    }
    //}
}
