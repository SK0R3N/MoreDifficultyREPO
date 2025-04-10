using BepInEx;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

using static DifficultyFeature.Event;


public class WalkieVoiceDuplicator : MonoBehaviour
{
    private class BufferedVoiceStream
    {
        private AudioSource source;

        public BufferedVoiceStream(GameObject target)
        {
            if (target == null)
            {
                Debug.LogError("[BufferedVoiceStream] Target GameObject is null!");
                return;
            }

            source = target.AddComponent<AudioSource>();
            if (source == null)
            {
                Debug.LogError("[BufferedVoiceStream] Failed to add AudioSource!");
                return;
            }

            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.volume = 0.8f;

            try
            {
                var filter = target.AddComponent<AudioHighPassFilter>();
                filter.cutoffFrequency = 1000f;

                var disto = target.AddComponent<AudioDistortionFilter>();
                disto.distortionLevel = 0.6f;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BufferedVoiceStream] Failed to add audio filters: " + e);
            }
        }

        public void PlayFrame(float[] frame, int sampleRate = 48000)
        {
            if (source == null || frame == null || frame.Length == 0) return;

            var clip = AudioClip.Create("WalkieOneShot", frame.Length, 1, sampleRate, false);
            clip.SetData(frame, 0);
            source.PlayOneShot(clip);
        }
    }

    private VoiceConnection voiceConnection;
    private Dictionary<int, BufferedVoiceStream> duplicators = new();

    void Start()
    {
        voiceConnection = FindObjectOfType<VoiceConnection>();
        if (voiceConnection == null)
        {
            Debug.LogError("[WalkieVoiceDuplicator] VoiceConnection not found in scene.");
            return;
        }

        voiceConnection.RemoteVoiceAdded += OnRemoteVoiceAdded;
        Debug.Log("[WalkieVoiceDuplicator] Listening for remote voices.");
    }

    void OnDestroy()
    {
        if (voiceConnection != null)
        {
            voiceConnection.RemoteVoiceAdded -= OnRemoteVoiceAdded;
        }
    }

    private void OnRemoteVoiceAdded(RemoteVoiceLink link)
    {
        int viewID = link.VoiceInfo.UserData is int id ? id : -1;

        if (viewID == -1)
        {
            Debug.LogWarning("[WalkieVoiceDuplicator] Received voice without valid PhotonView ID.");
            return;
        }

        PhotonView view = PhotonView.Find(viewID);
        if (view == null || !view.TryGetComponent(out WalkieReceiver receiver))
        {
            Debug.LogWarning("[WalkieVoiceDuplicator] No WalkieReceiver found for ViewID " + viewID);
            return;
        }

        if (duplicators.ContainsKey(viewID)) return;

        var stream = new BufferedVoiceStream(view.gameObject);
        duplicators[viewID] = stream;

        link.FloatFrameDecoded += frame =>
        {
            if (!WalkieReceiver.walkieEnabled || !WalkieRegistry.ActiveWalkieUsers.Contains(viewID)) return;
            stream.PlayFrame(frame.Buf); // lecture immédiate
        };

        Debug.Log($"[WalkieVoiceDuplicator] Duplicating voice for ViewID {viewID}");
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
            samples[i] = Random.Range(-0.3f, 0.3f); // bruit blanc
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

        if (!__instance.photonView.IsMine)
        {
            if (__instance.GetComponent<WalkieReceiver>() == null)
            {
                __instance.gameObject.AddComponent<WalkieReceiver>();
                Debug.Log("[WalkiePatch] WalkieReceiver added to remote player");
            }

            if (__instance.GetComponent<WalkieVoiceDuplicator>() == null)
            {
                __instance.gameObject.AddComponent<WalkieVoiceDuplicator>();
                Debug.Log("[WalkiePatch] WalkieVoiceDuplicator added to remote player");
            }
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
    }

    public void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == EVENT_WALKIE)
        {
            object[] data = (object[])photonEvent.CustomData;
            int viewID = (int)data[0];
            float volume = (float)data[1];

            PhotonView view = PhotonView.Find(viewID);
            if (view != null)
            {
                if (view.TryGetComponent<WalkieReceiver>(out var receiver))
                {
                    receiver.SetVolume(volume);
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

        // 🔊 On garde la transmission active, le duplicateur écoute tout
        if (voiceView.RecorderInUse != null)
        {
            voiceView.RecorderInUse.TransmitEnabled = true;
            Debug.Log("[WalkieSender] Recorder transmit enabled.");
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

        // On envoie la puissance du signal (volume) toutes les 0.1s
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