using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// TonemappingController — apply dan simpan tonemapping mode dari Settings ke Global Volume.
///
/// Menggabungkan TonemappingController lama dengan logika persistence dari SettingsSave.
/// Script ini di folder Settings/ (tanpa asmdef) sehingga bebas akses URP.
///
/// Setup:
///   1. Pasang di GameObject yang sama dengan SettingsSaveManager
///   2. Assign Global Volume (opsional — auto-find jika kosong)
///   3. TMP_Dropdown tonemapping → TonemappingDropdown.cs → SaveTonemapping()
/// </summary>
public class TonemappingController : MonoBehaviour
{
    public static TonemappingController Instance { get; private set; }

    [SerializeField] private Volume globalVolume;

    // Dropdown index → TonemappingMode
    // Urutan: ACES(0), Neutral(1), None(2)
    private static readonly TonemappingMode[] IndexToMode = new[]
    {
        TonemappingMode.ACES,
        TonemappingMode.Neutral,
        TonemappingMode.None,
    };

    private UnityEngine.Rendering.Universal.Tonemapping _tonemapping;

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Start dipanggil setelah SettingsSaveManager.Awake() (order -50)
        // sehingga SettingsSave.Data sudah ter-load saat Apply() dipanggil
        ResolveVolume();
        Apply();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset cache agar Volume di scene baru di-find ulang
        globalVolume  = null;
        _tonemapping  = null;
        ResolveVolume();
        Apply();
    }

    // ── Public API ────────────────────────────────────────────────

    /// Dipanggil SettingsSaveManager.SaveTonemapping() saat dropdown berubah
    public void Apply()
    {
        if (_tonemapping == null) ResolveVolume();
        if (_tonemapping == null)
        {
            Debug.LogWarning("[TonemappingController] Tonemapping component tidak ditemukan.");
            return;
        }

        int index = Mathf.Clamp(SettingsSave.Data.tonemappingMode, 0, IndexToMode.Length - 1);
        _tonemapping.mode.overrideState = true;
        _tonemapping.mode.value         = IndexToMode[index];

        Debug.Log($"[TonemappingController] Tonemapping diterapkan: {IndexToMode[index]}");
    }

    // ── Private ───────────────────────────────────────────────────

    private void ResolveVolume()
    {
        if (globalVolume == null)
            globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume == null || globalVolume.profile == null) return;

        globalVolume.profile.TryGet<UnityEngine.Rendering.Universal.Tonemapping>(out _tonemapping);
    }
}