using UnityEngine;
using VContainer;

/// <summary>
/// FlashlightSoundFeedback — pasang pada Player GameObject.
/// soundOn diputar loop selama flashlight menyala (hold), stop saat release.
/// Suara lain diputar sekali sebagai one-shot.
/// </summary>
public class FlashlightSoundFeedback : MonoBehaviour
{
    [Header("Audio Source — Loop (untuk suara On)")]
    [SerializeField] private AudioSource loopSource;

    [Header("Audio Source — OneShot (untuk suara lain)")]
    [SerializeField] private AudioSource oneShotSource;

    [Header("Sounds")]
    [Tooltip("Suara buzz/hum selama flashlight menyala — diloop")]
    [SerializeField] private AudioClip soundOn;
    [SerializeField] private AudioClip soundOff;
    [SerializeField] private AudioClip soundBatteryDepleted;
    [SerializeField] private AudioClip soundOverheatStart;
    [SerializeField] private AudioClip soundOverheatEnd;
    [SerializeField] private AudioClip soundRechargeComplete;
    [SerializeField] private AudioClip soundBrokenStart;
    [SerializeField] private AudioClip soundBrokenEnd;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float loopVolume    = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float oneShotVolume = 0.8f;

    [Inject] private FlashlightController _flashlight;

    private FlashlightController _fl;

    private void Awake()
    {
        // Auto-resolve: buat dua AudioSource jika belum diassign
        var sources = GetComponents<AudioSource>();

        if (loopSource == null)
            loopSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        if (oneShotSource == null)
            oneShotSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

        loopSource.playOnAwake    = false;
        loopSource.loop           = true;
        oneShotSource.playOnAwake = false;
        oneShotSource.loop        = false;
    }

    private void Start()
    {
        _fl = _flashlight ?? FlashlightController.Instance;
        if (_fl == null) return;

        _fl.onFlashlightOn.AddListener(OnOn);
        _fl.onFlashlightOff.AddListener(OnOff);
        _fl.onBatteryDepleted.AddListener(OnBatteryDepleted);
        _fl.onOverheatStart.AddListener(OnOverheatStart);
        _fl.onOverheatEnd.AddListener(OnOverheatEnd);
        _fl.onRechargeComplete.AddListener(OnRechargeComplete);
        _fl.onBrokenStart.AddListener(OnBrokenStart);
        _fl.onBrokenEnd.AddListener(OnBrokenEnd);
    }

    private void OnDestroy()
    {
        if (_fl == null) return;

        _fl.onFlashlightOn.RemoveListener(OnOn);
        _fl.onFlashlightOff.RemoveListener(OnOff);
        _fl.onBatteryDepleted.RemoveListener(OnBatteryDepleted);
        _fl.onOverheatStart.RemoveListener(OnOverheatStart);
        _fl.onOverheatEnd.RemoveListener(OnOverheatEnd);
        _fl.onRechargeComplete.RemoveListener(OnRechargeComplete);
        _fl.onBrokenStart.RemoveListener(OnBrokenStart);
        _fl.onBrokenEnd.RemoveListener(OnBrokenEnd);

        StopLoop();
    }

    // ── Handlers ──

    private void OnOn()
    {
        if (soundOn == null) return;
        loopSource.clip   = soundOn;
        loopSource.volume = loopVolume;
        loopSource.Play();
    }

    private void OnOff()
    {
        StopLoop();
        PlayOneShot(soundOff);
    }

    private void OnBatteryDepleted()  => PlayOneShot(soundBatteryDepleted);
    private void OnOverheatStart()    { StopLoop(); PlayOneShot(soundOverheatStart); }
    private void OnOverheatEnd()      => PlayOneShot(soundOverheatEnd);
    private void OnRechargeComplete() => PlayOneShot(soundRechargeComplete);
    private void OnBrokenStart(float _) { StopLoop(); PlayOneShot(soundBrokenStart); }
    private void OnBrokenEnd()        => PlayOneShot(soundBrokenEnd);

    // ── Helpers ──

    private void StopLoop()
    {
        if (loopSource.isPlaying)
            loopSource.Stop();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || oneShotSource == null) return;
        oneShotSource.PlayOneShot(clip, oneShotVolume);
    }
}