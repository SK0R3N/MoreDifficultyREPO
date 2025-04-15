using BepInEx;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using POpusCodec.Enums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

using static DifficultyFeature.Event;


public class WalkieVoiceDuplicator : MonoBehaviour
{
    private class BufferedVoiceStream
    {
        private AudioSource source;
        private Queue<float[]> frameQueue = new Queue<float[]>();
        private AudioClip currentClip;
        private bool isPlaying = false;
        private const int MAX_QUEUE_SIZE = 10;
        private const int SAMPLE_RATE = 48000;

        public BufferedVoiceStream(GameObject target)
        {
            source = target.AddComponent<AudioSource>();
            source.spatialBlend = 0f; // Audio 2D
            source.volume = 0.8f;
            source.playOnAwake = false;
            source.loop = false;

            currentClip = AudioClip.Create("VoiceStream", SAMPLE_RATE / 5, 1, SAMPLE_RATE, false); // 200ms buffer
            source.clip = currentClip;

            try
            {
                if (!source.gameObject.GetComponent<AudioHighPassFilter>())
                {
                    var filter = source.gameObject.AddComponent<AudioHighPassFilter>();
                    filter.cutoffFrequency = 1000f;
                }
                if (!source.gameObject.GetComponent<AudioDistortionFilter>())
                {
                    var disto = source.gameObject.AddComponent<AudioDistortionFilter>();
                    disto.distortionLevel = 0.6f;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BufferedVoiceStream] Failed to add audio filters: {ex.Message}");
            }
        }

        public void PlayFrame(float[] frame)
        {
            if (frame == null || frame.Length == 0)
            {
                Debug.LogWarning("[BufferedVoiceStream] Received empty or null frame, skipping");
                return;
            }

            if (frameQueue.Count < MAX_QUEUE_SIZE)
            {
                frameQueue.Enqueue(frame);
            }
            else
            {
                Debug.LogWarning("[BufferedVoiceStream] Frame queue full, dropping frame");
                return;
            }

            if (!isPlaying)
            {
                PlayNextFrame();
            }
        }

        private void PlayNextFrame()
        {
            if (frameQueue.Count == 0)
            {
                isPlaying = false;
                source.Stop();
                return;
            }

            isPlaying = true;
            float[] frame = frameQueue.Dequeue();

            try
            {
                if (currentClip.samples < frame.Length)
                {
                    currentClip = AudioClip.Create("VoiceStream", frame.Length, 1, SAMPLE_RATE, false);
                    source.clip = currentClip;
                }
                currentClip.SetData(frame, 0);

                source.Play();
                float duration = (float)frame.Length / SAMPLE_RATE;
                source.gameObject.GetComponent<MonoBehaviour>().StartCoroutine(WaitAndPlayNext(duration));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BufferedVoiceStream] Error playing frame: {ex.Message}\n{ex.StackTrace}");
                isPlaying = false;
                source.Stop();
            }
        }

        private IEnumerator WaitAndPlayNext(float duration)
        {
            yield return new WaitForSeconds(duration * 0.9f); // Légère avance pour éviter les gaps
            PlayNextFrame();
        }
    }

    private VoiceConnection voiceConnection;
    private Dictionary<int, BufferedVoiceStream> duplicators = new();

    void Awake()
    {
        Debug.Log($"[WalkieVoiceDuplicator] Awake() called. Attached to: {gameObject.name}, PhotonView.IsMine: {(GetComponent<PhotonView>()?.IsMine ?? false)}");
    }

    void OnEnable()
    {
        Debug.Log("[WalkieVoiceDuplicator] OnEnable() called.");
    }

    void Start()
    {
        voiceConnection = FindObjectOfType<VoiceConnection>();
        if (voiceConnection == null)
        {
            Debug.LogError("[WalkieVoiceDuplicator] VoiceConnection not found in scene.");
            return;
        }

        Debug.Log($"[WalkieVoiceDuplicator] VoiceConnection found. ClientState: {voiceConnection.ClientState}, Connected: {voiceConnection.Client.IsConnected}");
        voiceConnection.RemoteVoiceAdded += OnRemoteVoiceAdded;
        Debug.Log("[WalkieVoiceDuplicator] Listening for remote voices (hooked RemoteVoiceAdded).");
    }

    void Update()
    {
        CleanupDisconnectedPlayers();
    }

    void OnDestroy()
    {
        if (voiceConnection != null)
        {
            voiceConnection.RemoteVoiceAdded -= OnRemoteVoiceAdded;
        }
        duplicators.Clear();
        Debug.Log("[WalkieVoiceDuplicator] Destroyed and cleared duplicators");
    }

    private void OnRemoteVoiceAdded(RemoteVoiceLink link)
    {
        Debug.Log($"[WalkieVoiceDuplicator] OnRemoteVoiceAdded called for ActorNumber: {link.PlayerId}, VoiceInfo: {link.VoiceInfo}");

        int viewID = link.VoiceInfo.UserData is int id ? id : -1;
        if (viewID == -1)
        {
            Debug.LogWarning($"[WalkieVoiceDuplicator] Invalid ViewID from UserData: {link.VoiceInfo.UserData}");
            return;
        }

        PhotonView view = PhotonView.Find(viewID);
        if (view == null)
        {
            Debug.LogWarning($"[WalkieVoiceDuplicator] PhotonView not found for ViewID {viewID}");
            return;
        }

        if (!view.TryGetComponent(out WalkieReceiver receiver))
        {
            Debug.LogWarning($"[WalkieVoiceDuplicator] No WalkieReceiver found on ViewID {viewID}");
            return;
        }

        if (duplicators.ContainsKey(viewID))
        {
            Debug.Log($"[WalkieVoiceDuplicator] Cleaning up existing duplicator for ViewID {viewID}");
            duplicators.Remove(viewID);
        }

        var stream = new BufferedVoiceStream(view.gameObject);
        duplicators[viewID] = stream;
        Action<FrameOut<float>> frameHandler = frame =>
        {
            try
            {
                if (view.IsMine)
                {
                    Debug.Log($"[WalkieVoiceDuplicator] Ignoring local player frame for ViewID {viewID}");
                    return;
                }

                if (frame == null || frame.Buf == null || frame.Buf.Length == 0)
                {
                    Debug.LogWarning($"[WalkieVoiceDuplicator] Invalid frame received from {viewID}");
                    return;
                }

                Debug.Log($"[WalkieVoiceDuplicator] Frame received from {viewID}, length: {frame.Buf.Length}, WalkieEnabled: {WalkieReceiver.walkieEnabled}, ActiveWalkieUsers: {string.Join(",", WalkieRegistry.ActiveWalkieUsers)}");

                if (!WalkieReceiver.walkieEnabled)
                {
                    Debug.Log("[WalkieVoiceDuplicator] Walkie not enabled, skipping frame");
                    return;
                }
                if (!WalkieRegistry.ActiveWalkieUsers.Contains(viewID))
                {
                    Debug.Log($"[WalkieVoiceDuplicator] ViewID {viewID} not in ActiveWalkieUsers, skipping frame");
                    return;
                }

                if (!duplicators.ContainsKey(viewID))
                {
                    Debug.LogWarning($"[WalkieVoiceDuplicator] Duplicator for {viewID} no longer exists, skipping frame");
                    return;
                }

                Debug.Log($"[WalkieVoiceDuplicator] Playing frame from {viewID}, length: {frame.Buf.Length}");
                stream.PlayFrame(frame.Buf);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WalkieVoiceDuplicator] Error processing frame for ViewID {viewID}: {ex.Message}\n{ex.StackTrace}");
            }
        };

        link.FloatFrameDecoded += frameHandler;
        view.gameObject.AddComponent<RemoteVoiceHandler>().Initialize(link, frameHandler);

        Debug.Log($"[WalkieVoiceDuplicator] Duplicator set up for ViewID {viewID}");
    }

    private void CleanupDisconnectedPlayers()
    {
        List<int> toRemove = new List<int>();
        foreach (var kvp in duplicators)
        {
            int viewID = kvp.Key;
            PhotonView pv = PhotonView.Find(viewID);
            if (pv == null || (!pv.IsMine && pv.Owner == null))
            {
                toRemove.Add(viewID);
            }
        }

        foreach (int viewID in toRemove)
        {
            Debug.Log($"[WalkieVoiceDuplicator] Cleaning up duplicator for disconnected ViewID {viewID}");
            duplicators.Remove(viewID);
        }
    }
}

public class RemoteVoiceHandler : MonoBehaviour
{
    private RemoteVoiceLink link;
    private Action<FrameOut<float>> handler;

    public void Initialize(RemoteVoiceLink link, Action<FrameOut<float>> handler)
    {
        this.link = link;
        this.handler = handler;
    }

    void OnDestroy()
    {
        if (link != null && handler != null)
        {
            link.FloatFrameDecoded -= handler;
            Debug.Log("[RemoteVoiceHandler] Removed FloatFrameDecoded handler");
        }
    }
}

[HarmonyPatch(typeof(GameManager), "Awake")]
public static class DEBUG_ForceWalkie
{
    public static void Postfix()
    {
        if (GameObject.Find("WalkieDEBUG") == null)
        {
            GameObject go = new GameObject("WalkieDEBUG");
            go.AddComponent<WalkieVoiceDuplicator>();
            GameObject.DontDestroyOnLoad(go);
            Debug.Log("[DEBUG] WalkieVoiceDuplicator forcé manuellement.");
        }
        else
        {
            Debug.Log("[DEBUG] WalkieVoiceDuplicator déjà présent, ignoré.");
        }
    }
}


public class WalkieReceiver : MonoBehaviour
{
    internal static WalkieReceiver instance;
    private AudioSource walkieAudioSource;
    internal static float targetVolume = 0.05f;
    internal static AssetBundle AssetBundle;
    internal static AudioClip walkieLoopClip;
    internal static bool walkieEnabled = false;

    void Start()
    {
        instance = this;
        Debug.Log("[WalkieReceiver] Start()");

        // Loop walkie background
        walkieAudioSource = gameObject.AddComponent<AudioSource>();
        walkieAudioSource.loop = true;
        walkieAudioSource.spatialBlend = 0f;
        walkieAudioSource.playOnAwake = false;

        string bundlePath = Path.Combine(Paths.PluginPath, "SK0R3N-DifficultyFeature", "assets", "WalkySound");
        if (AssetBundle == null)
            AssetBundle = AssetBundle.LoadFromFile(bundlePath);

        if (walkieLoopClip == null)
            walkieLoopClip = AssetBundle.LoadAsset<AudioClip>("WalkieLoop");

        walkieAudioSource.clip = walkieLoopClip;

        if (walkieAudioSource.clip == null)
        {
            Debug.LogError("[WalkieReceiver] WalkieLoop audio clip not found!");
        }
        else
        {
            walkieAudioSource.Play();
            Debug.Log("[WalkieReceiver] Walkie loop started.");
        }

        var filter = walkieAudioSource.gameObject.AddComponent<AudioHighPassFilter>();
        filter.cutoffFrequency = 1000f;

        var disto = walkieAudioSource.gameObject.AddComponent<AudioDistortionFilter>();
        disto.distortionLevel = 0.6f;
    }

    void Update()
    {
        float target = walkieEnabled ? targetVolume : 0f;
        walkieAudioSource.volume = Mathf.Lerp(walkieAudioSource.volume, target, Time.deltaTime * 5f);
    }

    public void SetVolume(float volume)
    {
        if (!walkieEnabled) return;

        // Cette méthode est désormais utilisée uniquement pour gérer le background loop volume
        float boosted = Mathf.Clamp01(volume) * 0.2f;
        targetVolume = boosted;
    }

    public float GetCurrentVolume()
    {
        return targetVolume;
    }
}

public static class MicroLoop
{
    public static AudioClip GetStaticBuzz()
    {
        int sampleRate = 44100;
        int length = sampleRate / 10; // 0.1s
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            samples[i] = UnityEngine.Random.Range(-0.3f, 0.3f); // bruit blanc
        }

        AudioClip clip = AudioClip.Create("StaticBuzz", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}


[HarmonyPatch(typeof(PlayerVoiceChat), "Start")]
public static class Patch_PlayerVoiceChat_Start
{
    public static void Postfix(PlayerVoiceChat __instance)
    {
        Debug.Log("[WalkiePatch] Postfix Start called");

        if (__instance.GetComponent<WalkieReceiver>() == null)
        {
            __instance.gameObject.AddComponent<WalkieReceiver>();
            Debug.Log("[WalkiePatch] WalkieReceiver added");
        }

        if (__instance.GetComponent<WalkieVoiceDuplicator>() == null)
        {
            __instance.gameObject.AddComponent<WalkieVoiceDuplicator>();
            Debug.Log("[WalkiePatch] WalkieVoiceDuplicator added");
        }

        if (__instance.photonView != null && __instance.photonView.IsMine)
        {
            if (__instance.GetComponent<WalkieSender>() == null)
            {
                __instance.gameObject.AddComponent<WalkieSender>();
                Debug.Log("[WalkiePatch] WalkieSender added to local player");
            }
        }
    }
}

public class WalkieNetManager : MonoBehaviour, IOnEventCallback
{
    private const byte EVENT_WALKIE = 103;

    public void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
        Debug.Log("[WalkieNetManager] Enabled and listening to events");
    }

    public void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        Debug.Log("[WalkieNetManager] Disabled and stopped listening to events");
    }

    public void OnEvent(EventData photonEvent)
    {
        Debug.Log($"[WalkieNetManager] Event received: {photonEvent.Code}");

        if (photonEvent.Code == EVENT_WALKIE)
        {
            object[] data = (object[])photonEvent.CustomData;
            int viewID = (int)data[0];
            float volume = (float)data[1];

            Debug.Log($"[WalkieNetManager] Volume update from {viewID}: {volume}");

            PhotonView view = PhotonView.Find(viewID);
            if (view != null)
            {
                if (view.TryGetComponent<WalkieReceiver>(out var receiver))
                {
                    receiver.SetVolume(volume);
                    Debug.Log("[WalkieNetManager] Volume applied to receiver");
                }
                else
                {
                    Debug.LogWarning($"[WalkieNetManager] WalkieReceiver not found on ViewID {viewID}");
                }
            }
            else
            {
                Debug.LogWarning($"[WalkieNetManager] PhotonView not found for ViewID {viewID}");
            }
        }
        else if (photonEvent.Code == 104)
        {
            object[] data = (object[])photonEvent.CustomData;
            int viewID = (int)data[0];
            bool enabled = (bool)data[1];

            Debug.Log($"[WalkieNetManager] Walkie toggle update: ViewID {viewID} -> {enabled}, Current ActiveWalkieUsers: {string.Join(",", WalkieRegistry.ActiveWalkieUsers)}");

            if (enabled)
                WalkieRegistry.ActiveWalkieUsers.Add(viewID);
            else
                WalkieRegistry.ActiveWalkieUsers.Remove(viewID);

            Debug.Log($"[WalkieNetManager] Walkie state updated for {viewID}: {enabled}, New ActiveWalkieUsers: {string.Join(",", WalkieRegistry.ActiveWalkieUsers)}");
        }
    }
}


public class WalkieSender : MonoBehaviour
{
    internal static WalkieSender instance;
    private const byte EVENT_WALKIE = 103;
    private float lastSendTime = 0f;
    private PlayerVoiceChat voiceChat;
    private PhotonView photonView;
    internal static PhotonVoiceView voiceView;

    void Start()
    {
        instance = this;
        Debug.Log("[WalkieSender] Start()");

        voiceChat = GetComponent<PlayerVoiceChat>();
        photonView = GetComponent<PhotonView>();
        voiceView = GetComponent<PhotonVoiceView>();

        if (voiceChat == null || photonView == null || voiceView == null)
        {
            Debug.LogError("[WalkieSender] Missing required components.");
            enabled = false;
            return;
        }

        if (voiceView.RecorderInUse != null)
        {
            voiceView.RecorderInUse.TransmitEnabled = true;
            voiceView.RecorderInUse.VoiceDetection = false; // Désactiver la détection vocale
                                                            // Tenter d'utiliser FrameDuration
            voiceView.RecorderInUse.FrameDuration = Photon.Voice.OpusCodec.FrameDuration.Frame60ms; // 60ms
            Debug.Log($"[WalkieSender] Recorder config: TransmitEnabled={voiceView.RecorderInUse.TransmitEnabled}, VoiceDetection={voiceView.RecorderInUse.VoiceDetection}, FrameDuration={voiceView.RecorderInUse.FrameDuration}");
        }
    }



        void Update()
    {
        if (!photonView.IsMine) return;

        bool shouldEnableWalkie = BetterWalkieTakkie.Toggle && PlayerAvatar.instance.mapToolController.Active;
        if (WalkieReceiver.walkieEnabled != shouldEnableWalkie)
        {
            WalkieReceiver.walkieEnabled = shouldEnableWalkie;
            SendWalkieState(shouldEnableWalkie);
        }

        if (voiceView.RecorderInUse != null && voiceView.RecorderInUse.IsCurrentlyTransmitting)
        {
            //Debug.Log($"[WalkieSender] Recording and transmitting voice, Loudness: {voiceChat.clipLoudness}");
            // Limiter la taille des buffers si nécessaire
            if (voiceChat.clipSampleData.Length > 10000) // Ajuste cette limite selon tes besoins
            {
                Debug.LogWarning("[WalkieSender] Clip sample data too large, resetting");
                voiceChat.clipSampleData = new float[voiceChat.sampleDataLength];
            }
        }

        if (WalkieReceiver.walkieEnabled && Time.time - lastSendTime > 0.1f)
        {
            lastSendTime = Time.time;
            float loudness = voiceChat.clipLoudness;

            object[] content = new object[] { photonView.ViewID, loudness };
            var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(EVENT_WALKIE, content, options, SendOptions.SendUnreliable);
        }
    }

    public void SendWalkieState(bool enabled)
    {
        object[] content = new object[] { photonView.ViewID, enabled };
        PhotonNetwork.RaiseEvent(104, content, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    }
}

public static class WalkieRegistry
{
    public static HashSet<int> ActiveWalkieUsers = new HashSet<int>();
}