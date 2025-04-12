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



namespace DifficultyFeature
{
    internal class Event
    {
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

                float modifier = UnityEngine.Random.value < 0.5f ? 1.5f : 0.5f;
                int newHaulGoal = Mathf.RoundToInt(point.haulGoal * modifier);

                Debug.Log($"[ExtractionPointHaulModifier] Modifying haulGoal from {point.haulGoal} to {newHaulGoal} ({(modifier == 1.5f ? "+50%" : "-50%")})");

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
            public string EventName => "Time Slow";
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


            public void Execute()
            {
                instance = this;
                //ToggleWalkie(true);
            }

            public void ToggleWalkie(bool enabled)
            {
                PlayerAvatar player = PlayerAvatar.instance;
                Transform display = player.mapToolController.transform.Find("Controller/Visuals/Hide/Main Spring Target/Main Spring/Base Offset/Bob/Stick/stick/Main Unit/Display Spring Target/Display Spring/Counter/display_1x1");

                // Rechercher dynamiquement si besoin
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
                    // Sauvegarde le matériel de base
                    WalkieReceiver.walkieEnabled = true;
                    if (originalDisplayMaterial == null)
                        originalDisplayMaterial = renderer.material;

                    // Créer RenderTexture
                    waveformRenderTexture = new RenderTexture(512, 512, 16);

                    // Créer caméra
                    waveformCameraGO = new GameObject("WaveformCamera");
                    var cam = waveformCameraGO.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 3f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    cam.cullingMask = LayerMask.GetMask("TopLayerOnly");
                    cam.targetTexture = waveformRenderTexture;
                    waveformCameraGO.transform.position = new Vector3(0, 5f, 0);
                    waveformCameraGO.transform.rotation = Quaternion.Euler(90f, 0, 0);

                    // Créer waveform
                    waveformVisualizerGO = new GameObject("WaveformVisualizer");
                    waveformVisualizerGO.layer = LayerMask.NameToLayer("TopLayerOnly");
                    var lr = waveformVisualizerGO.AddComponent<LineRenderer>();
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = Color.green;
                    lr.endColor = Color.red;
                    lr.widthMultiplier = 0.05f;
                    lr.useWorldSpace = false;
                    waveformVisualizerGO.transform.position = new Vector3(-512 * 0.005f, 0, -256 * 0.005f);
                    waveformVisualizerGO.transform.rotation = Quaternion.identity;

                    var vis = waveformVisualizerGO.AddComponent<WaveformVisualizer>();
                    vis.voiceChat = player.voiceChat;

                    // Charger asset bundle et matériel
                    string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "walky");
                    if(walkyBundle == null)
                    walkyBundle = AssetBundle.LoadFromFile(bundlePath);

                    if (walkyBundle == null)
                    {
                        Debug.LogError("[WalkieToggle] AssetBundle introuvable.");
                        return;
                    }

                    if(waveformMat == null)
                    waveformMat = walkyBundle.LoadAsset<Material>("WaveformDisplayMat");

                    if (waveformMat == null)
                    {
                        Debug.LogError("[WalkieToggle] WaveformDisplayMat introuvable dans l'AssetBundle.");
                        return;
                    }

                    waveformMaterialInstance = new Material(waveformMat);
                    waveformMaterialInstance.mainTexture = waveformRenderTexture;

                    renderer.material = waveformMaterialInstance;

                    // Ajout du WalkieReceiver si manquant
                    if (!player.GetComponent<WalkieReceiver>())
                        player.gameObject.AddComponent<WalkieReceiver>();
                }
                else
                {
                    // Restauration matériel original
                    WalkieReceiver.walkieEnabled = false;
                    Debug.Log(originalDisplayMaterial);
                    if (originalDisplayMaterial != null)
                        renderer.material = originalDisplayMaterial;

                    Debug.Log(waveformCameraGO);
                    Debug.Log(waveformVisualizerGO);
                    Debug.Log(waveformRenderTexture);
                    // Suppression objets
                    if (waveformCameraGO != null) GameObject.Destroy(waveformCameraGO);
                    if (waveformVisualizerGO != null) GameObject.Destroy(waveformVisualizerGO);
                    if (waveformRenderTexture != null) waveformRenderTexture.Release();

                    waveformCameraGO = null;
                    waveformVisualizerGO = null;
                    waveformRenderTexture = null;
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
                        lineRenderer.SetPosition(i, new Vector3(x, 0, z));
                    }
                }
            }
        } //Finis ?

        public class SurviveHorror : ISlotEvent
        {
            public string EventName => "SurviveHorrror";
            public string IconName => "icon_timeslow";
            public string Asset => "TestAsset";

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null) return;

                int count = 15;
                float radius = 10f;
                Vector3 center = player.transform.position;
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

                    if (!enemyName.ToLower().Contains("gnome"))
                    {
                        if (!EnemyDirector.instance.TryGetEnemyThatContainsName(enemyName, out EnemySetup enemySetup))
                        {
                            Debug.LogWarning($"[EnemyRainEvent] Enemy not found: {enemyName}");
                            continue;
                        }

                        float angle = i * Mathf.PI * 2f / count;
                        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                        Vector3 spawnPos = center + offset + Vector3.up;

                        List<EnemyParent> enemy = Enemies.SpawnEnemy(enemySetup, spawnPos, Quaternion.identity, spawnDespawned: false);
                        spawnedEnemies.Add(enemy.First());

                        Debug.Log($"[EnemyRainEvent] Spawned {enemy.First().name} at {spawnPos}");
                    }
                    else
                    {
                        i--;
                    }
                }

                // Lance la suppression après 30 secondes
                Debug.Log(spawnedEnemies.ToList());
                foreach (var enemy in spawnedEnemies)
                {
                    Debug.Log($"[EnemyRainEvent] Destroyed {enemy.name}");
                }
                PlayerController.instance.StartCoroutine(DestroyEnemiesAfterDelay(spawnedEnemies, 30f));
            }

            private IEnumerator DestroyEnemiesAfterDelay(List<EnemyParent> enemies, float delay)
            {
                yield return new WaitForSeconds(delay);
                

                foreach (var enemy in enemies)
                {
                    if (enemy != null)
                    {
                       PhotonNetwork.Destroy(enemy.GameObject());
                       Debug.Log($"[EnemyRainEvent] Destroyed {enemy.name}");

                    }
                }
            }
        } //Non Finis

        public class TimeSlowEvent : ISlotEvent
        {
            public string EventName => "Time Slow";
            public string IconName => "icon_timeslow";
            public string Asset => "TestAsset";

            public void Execute()
            {
                var player = PlayerController.instance;
                if (player == null) return;

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
                if (modules.Length  == 0)
                {
                    Debug.LogError("[RandomTP] Aucun module trouvé !");
                    return;
                }

                // On prend une salle aléatoire
                var randomModule = modules[UnityEngine.Random.Range(0, modules.Length)];

                // On essaie de trouver un point de positionnement safe dans la salle
                var targetPosition = randomModule.transform.position + Vector3.up * 1.5f;

                // Appliquer le TP
                player.transform.position = targetPosition;
                Debug.Log($"[RandomTP] Joueur téléporté dans {randomModule.name}");
            }
        } //Finis (Manque vérification tp dans un trou) //Bug tp juste une fois

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

                    if (eyeR == null || eyeL == null )
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
        } //Probablement a retirer

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
            }
        }//Fonctionne Bien
    }

}



