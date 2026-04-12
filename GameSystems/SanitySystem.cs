using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>
/// SanitySystem — Singleton, pasang di scene CCTV.
/// 
/// Sanity hanya aktif saat CCTV mode.
/// - Fuse 2 aktif → sanity turun
/// - Fuse 1 aktif → sanity naik
/// - Sanity rendah → noise multiplier naik + enemy lebih agresif
/// - Sanity 0 → onSanityDepleted (mechanic TBD)
///
/// Setup:
///   1. Pasang script ini di scene CCTV
///   2. FuseSwitcher Fuse1.onActivated → SanitySystem.OnFuse1Activated()
///   3. FuseSwitcher Fuse2.onActivated → SanitySystem.OnFuse2Activated()
///   4. MonitorInteractable.onCCTVEntered → SanitySystem.SetCCTVActive(true)
///   5. MonitorInteractable.onCCTVExited  → SanitySystem.SetCCTVActive(false)
/// </summary>
public class SanitySystem : MonoBehaviour
{
    public static SanitySystem Instance { get; private set; }

    [Header("Sanity Settings")]
    [SerializeField] private float maxSanity          = 100f;
    [Tooltip("Sanity turun per detik saat Fuse 2 aktif")]
    [SerializeField] private float sanityDecayRate    = 5f;
    [Tooltip("Sanity naik per detik saat Fuse 1 aktif")]
    [SerializeField] private float sanityRecoverRate  = 3f;

    [Header("Noise Multiplier")]
    [Tooltip("Noise multiplier saat sanity 100% (normal)")]
    [SerializeField] private float noiseMultiplierMin = 1f;
    [Tooltip("Noise multiplier saat sanity 0% (paling parah)")]
    [SerializeField] private float noiseMultiplierMax = 2.5f;

    [Header("Enemy Aggressiveness")]
    [Tooltip("Peek chance multiplier saat sanity 0%")]
    [SerializeField] private float peekChanceMultiplierMax = 3f;

    [Header("Rhythm Game")]
    [Tooltip("Nama scene rhythm game — sanity baru turun setelah note pertama")]
    [SerializeField] private string rhythmSceneName = "Restorasi";

    [Header("Events")]
    public UnityEvent         onSanityChanged;   // saat sanity berubah
    public UnityEvent         onSanityDepleted;  // saat sanity = 0
    public UnityEvent<float>  onSanityUpdated;   // (0-1) setiap frame

    // ── Static — persist cross-scene ──
    private static float s_savedSanity    = 100f;
    private static bool  s_hasSavedSanity = false;

    // ── Public Properties ──
    public float SanityPercent        => _currentSanity / maxSanity;
    public bool  IsCCTVActive         => _isCCTVActive;
    public bool  IsFuse2Active        => _isFuse2Active;
    public float NoiseMultiplier      => Mathf.Lerp(noiseMultiplierMax, noiseMultiplierMin, SanityPercent);
    public float PeekChanceMultiplier => Mathf.Lerp(peekChanceMultiplierMax, 1f, SanityPercent);

    // ── State ──
    private float _currentSanity;
    private bool  _isCCTVActive;
    private bool  _isFuse2Active;
    private bool  _isRhythmScene;      // cache scene name
    private float _lastInvokedSanity;  // hanya invoke event saat nilai berubah

    private void Awake()
    {
        // Fase 3 — Instance dipertahankan sebagai compatibility shim selama migrasi.
        // Setelah semua caller diganti [Inject], hapus blok singleton ini.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // DontDestroyOnLoad diurus ProjectLifetimeScope setelah Fase 3 selesai.
        // Sementara ini tetap di sini agar behaviour tidak berubah saat testing.
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        _currentSanity = s_hasSavedSanity ? s_savedSanity : maxSanity;
        s_hasSavedSanity = false;
        _lastInvokedSanity = _currentSanity;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Hanya update state saat Single load (pindah scene beneran)
        // Additive load (scene UI/Dampener) TIDAK boleh reset _rhythmStarted
        // karena bisa membekukan pengurangan Sanity secara permanen
        if (mode == LoadSceneMode.Single)
        {
            _isRhythmScene   = scene.name == rhythmSceneName;
            s_savedSanity    = _currentSanity;
            s_hasSavedSanity = true;
            _rhythmStarted   = false;

            // BUG FIX #5 — JANGAN reset _isCCTVActive di sini.
            // CCTVAutoRestore memanggil monitor.ForceEnterCCTV() pada frame berikutnya
            // (via StartCoroutine(RestoreNextFrame())), yang akan set _isCCTVActive = true.
            // Jika di-reset di sini, ada race condition: OnSceneLoaded berjalan setelah
            // CCTVAutoRestore.Start(), sehingga flag yang baru di-set langsung ter-override.
            // _isCCTVActive hanya boleh di-reset oleh: ForceReset(), SetCCTVActive(false),
            // _isCCTVActive = false; // <-- DIHAPUS

        }
    }

    private bool _rhythmStarted;

    private void Update()
    {

        if (!_isCCTVActive) return;

        // Pakai cache — tidak cek SceneManager tiap frame
        if (_isRhythmScene && !_rhythmStarted) return;

        float prev = _currentSanity;

        if (_isFuse2Active)
        {
            _currentSanity -= sanityDecayRate * Time.deltaTime;
            _currentSanity  = Mathf.Max(0f, _currentSanity);

            if (_currentSanity <= 0f)
            {
                _currentSanity = 0f;
                onSanityDepleted.Invoke();
                Debug.Log("[Sanity] Sanity habis — mechanic baru belum diimplementasi.");
                return;
            }
        }
        else if (_currentSanity < maxSanity)
        {
            _currentSanity += sanityRecoverRate * Time.deltaTime;
            _currentSanity  = Mathf.Min(maxSanity, _currentSanity);
        }

        // Invoke event hanya jika sanity berubah lebih dari 0.5% — tidak tiap frame
        if (Mathf.Abs(_currentSanity - _lastInvokedSanity) >= maxSanity * 0.005f)
        {
            _lastInvokedSanity = _currentSanity;
            onSanityUpdated.Invoke(SanityPercent);
            onSanityChanged.Invoke();
        }
    }


    /// Dipanggil Tile.OnHit() saat note pertama ditekan
    public void NotifyFirstHit()
    {
        _rhythmStarted = true;
    }

    // ──────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────

    public void SetCCTVActive(bool active)
    {
        _isCCTVActive  = active;
        _rhythmStarted = false;
    }

    public void OnFuse1Activated()
    {
        _isFuse2Active = false;
    }

    public void OnFuse2Activated()
    {
        _isFuse2Active = true;
    }

    /// Dipanggil saat tombol "New Game" ditekan — reset semua static state
    /// agar sesi baru tidak mewarisi sanity rendah dari sesi sebelumnya.
    /// Fase 3: Method ini tetap static — dipanggil MainMenuHandler tanpa inject.
    public static void ResetStaticData()
    {
        s_savedSanity    = 100f;
        s_hasSavedSanity = false;
        if (Instance != null)
        {
            Instance._currentSanity = Instance.maxSanity;
            Instance._isCCTVActive  = false;
            Instance._isFuse2Active = false;
            Instance._rhythmStarted = false;
        }
        Debug.Log("[Sanity] Static data direset untuk New Game.");
    }

    public void ResetSanity()
    {
        _currentSanity   = maxSanity;
        s_savedSanity    = maxSanity;
        s_hasSavedSanity = false;
    }

    /// Reset semua state — dipanggil saat perlu reset penuh
    public void ForceReset()
    {
        _isCCTVActive    = false;
        _isFuse2Active   = false;
        _currentSanity   = maxSanity;
        s_savedSanity    = maxSanity;
        s_hasSavedSanity = false;
    }


}