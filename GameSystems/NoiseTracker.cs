using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public class NoiseTracker : MonoBehaviour
{
    public static NoiseTracker Instance { get; private set; }

    [Header("Noise Settings")]
    [SerializeField] private float maxNoise           = 100f;
    [SerializeField] private float noiseSwitchCamera  = 15f;
    [SerializeField] private float noiseToggleFuse    = 25f;
    [SerializeField] private float noiseDecayRate     = 10f;
    [Tooltip("Detik player harus diam sebelum noise mulai turun (default: 1.5)")]
    [SerializeField] private float decayDelay         = 1.5f;

    [Header("Thresholds")]
    [Tooltip("Threshold mulai ngintip (0-100)")]
    [SerializeField] private float peekThreshold      = 40f;
    [Tooltip("Threshold jumpscare (0-100)")]
    [SerializeField] private float jumpscareThreshold = 80f;

    [Header("Rhythm Game")]
    [Tooltip("Nama scene rhythm game — noise tidak decay di scene ini")]
    [SerializeField] private string rhythmSceneName   = "Restorasi";

    [Header("Dampener")]
    [SerializeField] private DampenerState dampenerState;

    // Public properties
    public float CurrentNoise          => _currentNoise;
    public float NoisePercent          => _currentNoise / maxNoise;
    public float PeekThresholdPct      => peekThreshold / maxNoise;
    public float JumpscareThresholdPct => jumpscareThreshold / maxNoise;
    public bool  IsDecaying            => _isDecaying;
    public float IdleTimer             => _idleTimer;
    public bool  IsDampenerOn          => dampenerState != null && dampenerState.IsOn;

    // Fase 3 — Inject SanitySystem, ganti SanitySystem.Instance di AddNoise.
    // Instance dipertahankan sebagai shim sampai semua caller dimigrasi.
    [Inject] private SanitySystem _sanitySystem;

    // Private state
    private float _currentNoise;
    private float _idleTimer;
    private bool  _isDecaying;
    private bool  _isRhythmScene;   // cache — tidak cek SceneManager tiap frame

    private void Awake()
    {
        // Fase 3 — Instance dipertahankan sebagai compatibility shim selama migrasi.
        // Setelah semua caller diganti [Inject], hapus blok singleton ini.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // DontDestroyOnLoad diurus ProjectLifetimeScope setelah Fase 3 selesai.
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded   += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded   -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Saat scene ritme di-load, tandai sebagai rhythm scene
        if (scene.name == rhythmSceneName)
        {
            _isRhythmScene = true;
            return; // jangan restore noise di scene rhythm — tidak relevan
        }

        // BUG FIX #3 — Restore noise dari save file saat scene game di-load.
        // Ini mencegah exploit di mana player Save/Load untuk reset noise ke 0.
        RestoreNoiseFromSave();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // Saat scene ritme di-unload, cek active scene sekarang
        if (scene.name == rhythmSceneName)
            _isRhythmScene = SceneManager.GetActiveScene().name == rhythmSceneName;
    }

    private void Start()
    {
        // BUG FIX #3 — Muat noise dari save agar tidak bisa di-reset via Save/Load exploit.
        if (SaveFile.Exists())
        {
            _currentNoise = SaveFile.Data.currentNoise;
            Debug.Log($"[NoiseTracker] Noise di-restore dari save: {_currentNoise}");
        }

        if (dampenerState != null && dampenerState.PendingPenalty > 0f)
        {
            float penalty = dampenerState.PendingPenalty;
            dampenerState.ConsumePenalty();
            AddNoiseRaw(penalty);
            Debug.Log("[NoiseTracker] Dampener penalty: +" + penalty);
        }
    }

    private void Update()
    {
        if (dampenerState != null && dampenerState.IsOn && dampenerState.IsExpired())
        {
            dampenerState.TurnOff();
        }

        // Pakai cache — tidak cek SceneManager tiap frame
        if (_isRhythmScene) return;

        if (_currentNoise <= 0f)
        {
            _idleTimer  = 0f;
            _isDecaying = false;
            return;
        }

        _idleTimer += Time.deltaTime;

        if (_idleTimer >= decayDelay)
        {
            _isDecaying = true;
            float decayMultiplier = dampenerState != null && dampenerState.IsOn
                ? dampenerState.decayMultiplier : 1f;
            _currentNoise -= noiseDecayRate * decayMultiplier * Time.deltaTime;
            _currentNoise  = Mathf.Max(0f, _currentNoise);
        }
        else
        {
            _isDecaying = false;
        }
    }

    public void AddNoise(float amount)
    {
        float dampMult   = dampenerState != null && dampenerState.IsOn ? 0.5f : 1f;
        // Fase 3 — pakai _sanitySystem inject, fallback ke Instance selama migrasi
        float sanityMult = _sanitySystem != null ? _sanitySystem.NoiseMultiplier
                         : SanitySystem.Instance != null ? SanitySystem.Instance.NoiseMultiplier : 1f;
        float final      = amount * dampMult * sanityMult;
        AddNoiseRaw(final);
    }

    private void AddNoiseRaw(float amount)
    {
        _currentNoise = Mathf.Min(_currentNoise + amount, maxNoise);
        _idleTimer    = 0f;
        _isDecaying   = false;
    }

    public void AddNoiseSwitchCamera() => AddNoise(noiseSwitchCamera);
    public void AddNoiseToggleFuse()   => AddNoise(noiseToggleFuse);

    /// BUG FIX #3 — Dipanggil oleh CheckpointTrigger / ScenePortal SEBELUM GameSave.Save().
    /// Menyalin _currentNoise ke GameState.SavedNoise agar GameSave bisa membacanya
    /// tanpa circular assembly reference (GameSave ada di Core, NoiseTracker di GameSystems).
    public void PushNoiseToSave()
    {
        GameState.SavedNoise = _currentNoise;
        Debug.Log($"[NoiseTracker] Noise di-push ke GameState: {_currentNoise:F1}");
    }

    /// BUG FIX #3 — Dipanggil saat scene di-load untuk restore noise dari save file.
    /// Mencegah exploit: load save saat noise tinggi tidak lagi me-reset noise ke 0.
    private void RestoreNoiseFromSave()
    {
        float saved = SaveFile.Data.currentNoise;
        if (saved > 0f)
        {
            _currentNoise = saved;
            _idleTimer    = 0f;
            _isDecaying   = false;
            Debug.Log($"[NoiseTracker] Noise di-restore dari save: {_currentNoise:F1}");
        }
    }

    public void SetGameOver() => ResetNoise();

    public void ResetNoise()
    {
        _currentNoise = 0f;
        _idleTimer    = 0f;
        _isDecaying   = false;
    }
}