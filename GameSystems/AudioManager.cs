using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour, IAudioManager
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer masterMixer;

    [Header("Mixer Groups")]
    [SerializeField] private AudioMixerGroup groupFootstep;
    [SerializeField] private AudioMixerGroup groupFlashlight;
    [SerializeField] private AudioMixerGroup groupItem;
    [SerializeField] private AudioMixerGroup groupEnemy;
    [SerializeField] private AudioMixerGroup groupNPC;
    [SerializeField] private AudioMixerGroup groupGenerator;
    [SerializeField] private AudioMixerGroup groupMusic;
    [SerializeField] private AudioMixerGroup groupAmbience;

    [Header("Mixer Volume Parameter Names")]
    [SerializeField] private string paramMaster     = "VolumeMaster";
    [SerializeField] private string paramSFX        = "VolumeSFX";
    [SerializeField] private string paramFootstep   = "VolumeFootstep";
    [SerializeField] private string paramFlashlight = "VolumeFlashlight";
    [SerializeField] private string paramItem       = "VolumeItem";
    [SerializeField] private string paramEnemy      = "VolumeEnemy";
    [SerializeField] private string paramNPC        = "VolumeNPC";
    [SerializeField] private string paramGenerator  = "VolumeGenerator";
    [SerializeField] private string paramMusic      = "VolumeMusic";
    [SerializeField] private string paramAmbience   = "VolumeAmbience";

    [Header("Reverb Parameter Names")]
    [Tooltip("Exposed parameter: Send level di SFX Footstep → Reverb Bus")]
    [SerializeField] private string paramReverbSend         = "FootstepReverbSend";
    [Tooltip("Exposed parameter: Send level di SFX Flashlight → Reverb Bus")]
    [SerializeField] private string paramFlashlightReverbSend = "FlashlightReverbSend";

    // ── Private State ─────────────────────────────────────────────────────────

    private readonly Dictionary<AudioCategory, List<AudioSource>> _registry
        = new Dictionary<AudioCategory, List<AudioSource>>();

    private readonly Dictionary<AudioCategory, AudioMixerGroup> _groupMap
        = new Dictionary<AudioCategory, AudioMixerGroup>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildGroupMap();
        AudioServices.Register(this);
    }

    private void OnDestroy()
    {
        AudioServices.Unregister(this);
    }

    private void BuildGroupMap()
    {
        _groupMap[AudioCategory.Footstep]   = groupFootstep;
        _groupMap[AudioCategory.Flashlight] = groupFlashlight;
        _groupMap[AudioCategory.Item]       = groupItem;
        _groupMap[AudioCategory.Enemy]      = groupEnemy;
        _groupMap[AudioCategory.NPC]        = groupNPC;
        _groupMap[AudioCategory.Generator]  = groupGenerator;
        _groupMap[AudioCategory.Music]      = groupMusic;
        _groupMap[AudioCategory.Ambience]   = groupAmbience;
    }

    // ── Registration API ──────────────────────────────────────────────────────

    public void RegisterSource(AudioCategory category, AudioSource source)
    {
        if (source == null) return;
        if (!_registry.TryGetValue(category, out var list))
        {
            list = new List<AudioSource>();
            _registry[category] = list;
        }
        if (!list.Contains(source)) list.Add(source);
        if (_groupMap.TryGetValue(category, out var group) && group != null)
            source.outputAudioMixerGroup = group;
    }

    public void UnregisterSource(AudioCategory category, AudioSource source)
    {
        if (_registry.TryGetValue(category, out var list)) list.Remove(source);
    }

    public AudioSource GetSource(AudioCategory category)
    {
        if (_registry.TryGetValue(category, out var list) && list.Count > 0) return list[0];
        return null;
    }

    // ── Volume API ────────────────────────────────────────────────────────────

    public void SetVolumeMaster(float v)     => SetVolume(paramMaster,     v);
    public void SetVolumeSFX(float v)        => SetVolume(paramSFX,        v);
    public void SetVolumeFootstep(float v)   => SetVolume(paramFootstep,   v);
    public void SetVolumeFlashlight(float v) => SetVolume(paramFlashlight, v);
    public void SetVolumeItem(float v)       => SetVolume(paramItem,       v);
    public void SetVolumeEnemy(float v)      => SetVolume(paramEnemy,      v);
    public void SetVolumeNPC(float v)        => SetVolume(paramNPC,        v);
    public void SetVolumeGenerator(float v)  => SetVolume(paramGenerator,  v);
    public void SetVolumeMusic(float v)      => SetVolume(paramMusic,      v);
    public void SetVolumeAmbience(float v)   => SetVolume(paramAmbience,   v);

    public float GetVolumeMaster()     => GetVolume(paramMaster);
    public float GetVolumeSFX()        => GetVolume(paramSFX);
    public float GetVolumeFootstep()   => GetVolume(paramFootstep);
    public float GetVolumeFlashlight() => GetVolume(paramFlashlight);
    public float GetVolumeItem()       => GetVolume(paramItem);
    public float GetVolumeEnemy()      => GetVolume(paramEnemy);
    public float GetVolumeNPC()        => GetVolume(paramNPC);
    public float GetVolumeGenerator()  => GetVolume(paramGenerator);
    public float GetVolumeMusic()      => GetVolume(paramMusic);
    public float GetVolumeAmbience()   => GetVolume(paramAmbience);

    // ── Reverb API ────────────────────────────────────────────────────────────

    /// <summary>Footstep reverb — dikontrol dynamic oleh FootstepSystem.</summary>
    public void SetReverbSend(float normalizedValue)
    {
        if (masterMixer == null) return;
        masterMixer.SetFloat(paramReverbSend, NormalizedToSendDb(normalizedValue));
    }

    public float GetReverbSend()
    {
        if (masterMixer == null) return 0f;
        if (!masterMixer.GetFloat(paramReverbSend, out float db)) return 0f;
        return Mathf.InverseLerp(-80f, 0f, db);
    }

    /// <summary>
    /// Flashlight reverb — dikontrol oleh FlashlightSoundFeedback.
    /// Sama persis konversinya dengan SetReverbSend.
    /// value: 0.0 = dry, 1.0 = full send ke Reverb Bus.
    /// </summary>
    public void SetFlashlightReverb(float normalizedValue)
    {
        if (masterMixer == null) return;
        masterMixer.SetFloat(paramFlashlightReverbSend, NormalizedToSendDb(normalizedValue));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Konversi 0-1 linear ke dB range -80..0 untuk Send level.</summary>
    private static float NormalizedToSendDb(float normalizedValue)
    {
        float clamped = Mathf.Clamp01(normalizedValue);
        return clamped <= 0f ? -80f : Mathf.Lerp(-80f, 0f, clamped);
    }

    private void SetVolume(string param, float value)
    {
        if (masterMixer == null) return;
        masterMixer.SetFloat(param, Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    private float GetVolume(string param)
    {
        if (masterMixer == null) return 1f;
        if (!masterMixer.GetFloat(param, out float db)) return 1f;
        return Mathf.Pow(10f, db / 20f);
    }
}