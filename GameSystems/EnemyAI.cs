using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// EnemyAI — controller state machine enemy. Tidak punya mesh sendiri.
///
/// Setiap EnemyPeekPoint punya prefab sendiri yang di-spawn saat peek.
/// EnemyAI hanya mengatur kapan dan di mana enemy muncul.
///
/// State Machine:
///   IDLE    → cek noise tiap interval → roll chance peek
///   PEEKING → spawn prefab di peek point → fade in → tunggu → fade out → destroy
///   LEAVING → cleanup prefab → kembali IDLE
///   JUMPING → jumpscare → game over
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public enum EnemyState { Idle, Peeking, Leaving, Jumping }

    [Header("Peek Points")]
    [Tooltip("Titik-titik enemy bisa muncul — tiap point punya prefab sendiri")]
    [SerializeField] private EnemyPeekPoint[] peekPoints;

    [Header("Jumpscare UI")]
    [SerializeField] private Image jumpscareOverlay;
    [SerializeField] private float jumpscareDuration = 1.5f;

    [Header("Timing")]
    [Tooltip("Chance maksimal peek per roll (0-1)")]
    [SerializeField] private float peekMaxChance     = 0.3f;
    [Tooltip("Delay sebelum muncul setelah roll berhasil (detik)")]
    [SerializeField] private float peekDelay         = 1.5f;
    [Tooltip("Durasi enemy terlihat sebelum cek noise lagi (detik)")]
    [SerializeField] private float peekDuration      = 3f;
    [Tooltip("Kecepatan fade in/out")]
    [SerializeField] private float fadeSpeed         = 2f;
    [Tooltip("Interval cek noise saat IDLE (detik)")]
    [SerializeField] private float idleCheckInterval = 0.5f;

    [Header("Grace Period")]
    [Tooltip("Detik immunity dari jumpscare saat scene baru load")]
    [SerializeField] private float gracePeriod = 5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   peekSound;
    [SerializeField] private AudioClip   jumpscareSound;

    [Header("Events")]
    public UnityEvent onStartPeeking;
    public UnityEvent onStopPeeking;
    public UnityEvent onJumpscare;

    // ── Inject — fallback ke .Instance selama migrasi ──
    [Inject] private NoiseTracker _noiseTracker;
    [Inject] private SanitySystem _sanitySystem;

    // ── Runtime ──
    private EnemyState _state         = EnemyState.Idle;
    private int        _lastPeekIndex = -1;
    private bool       _graceActive;

    // Prefab yang sedang di-spawn untuk peek saat ini
    private GameObject  _spawnedPeekObject;
    private Material[]  _spawnedMaterials;

    public EnemyState CurrentState => _state;

    // ──────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────

    private void Start()
    {
        // BUG FIX — EnemyAI tidak boleh mengelola GameOverManager.
        // Reset + RegisterMainScene seharusnya dipanggil oleh MainMenuHandler saat
        // New Game / Load Game, bukan oleh EnemyAI yang bisa ada lebih dari satu di scene.
        // Hanya register scene name jika belum terdaftar (pertama kali masuk main scene).
        if (!GameOverManager.IsMainSceneRegistered)
            GameOverManager.RegisterMainScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        if (jumpscareOverlay != null) jumpscareOverlay.gameObject.SetActive(false);

        // BUG FIX — Tunggu NoiseTracker & SanitySystem siap sebelum memulai state machine.
        // Tanpa ini, jika EnemyAI.Start() dipanggil sebelum NoiseTracker.Awake() selesai
        // (order tidak dijamin Unity), _noiseTracker dan .Instance keduanya null →
        // IdleRoutine selalu continue → enemy tidak pernah muncul.
        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        // Tunggu sampai dependency tersedia (max ~2 detik agar tidak hang selamanya)
        float waited = 0f;
        while (waited < 2f)
        {
            var noise  = _noiseTracker ?? NoiseTracker.Instance;
            var sanity = _sanitySystem ?? SanitySystem.Instance;
            if (noise != null && sanity != null) break;
            waited += Time.deltaTime;
            yield return null;
        }

        if ((_noiseTracker ?? NoiseTracker.Instance) == null)
            Debug.LogError("[EnemyAI] NoiseTracker tidak ditemukan setelah 2 detik! " +
                           "Pastikan NoiseTracker di-assign di ProjectLifetimeScope atau ada di scene.");

        StartCoroutine(GraceRoutine());
        StartCoroutine(StateMachine());
    }

    private IEnumerator GraceRoutine()
    {
        _graceActive = true;
        Debug.Log($"[EnemyAI] Grace period {gracePeriod}s");
        yield return new WaitForSeconds(gracePeriod);
        _graceActive = false;
        Debug.Log("[EnemyAI] Grace period selesai");
    }

    // ──────────────────────────────────────────
    // State Machine
    // ──────────────────────────────────────────

    private IEnumerator StateMachine()
    {
        while (true)
        {
            switch (_state)
            {
                case EnemyState.Idle:    yield return IdleRoutine();      break;
                case EnemyState.Peeking: yield return PeekingRoutine();   break;
                case EnemyState.Leaving: yield return LeavingRoutine();   break;
                case EnemyState.Jumping: yield return JumpscareRoutine(); break;
            }
        }
    }

    // ── IDLE ──
    private IEnumerator IdleRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(idleCheckInterval);

            var noise = _noiseTracker ?? NoiseTracker.Instance;
            if (noise == null) continue;

            if (!_graceActive && noise.NoisePercent >= noise.JumpscareThresholdPct)
            {
                _state = EnemyState.Jumping;
                yield break;
            }

            if (_graceActive) continue;

            float noiseRatio = noise.NoisePercent / Mathf.Max(noise.PeekThresholdPct, 0.01f);
            var   sanity     = _sanitySystem ?? SanitySystem.Instance;
            float sanityMult = sanity != null ? sanity.PeekChanceMultiplier : 1f;
            float peekChance = Mathf.Clamp01(
                Mathf.Lerp(0f, peekMaxChance, Mathf.Clamp01(noiseRatio)) * sanityMult);

            float roll = Random.Range(0f, 1f);
// #if UNITY_EDITOR
//             Debug.Log($"[EnemyAI] Roll: {roll:F2} | Chance: {peekChance:F2}");
// #endif

            if (roll < peekChance)
            {
                yield return new WaitForSeconds(peekDelay);

                var n2 = _noiseTracker ?? NoiseTracker.Instance;
                _state = (n2 != null && n2.NoisePercent >= n2.JumpscareThresholdPct)
                    ? EnemyState.Jumping
                    : EnemyState.Peeking;

                yield break;
            }
        }
    }

    // ── PEEKING ──
    private IEnumerator PeekingRoutine()
    {
        var point = GetNextPeekPoint();
        if (point == null || point.enemyPrefab == null)
        {
            Debug.LogWarning("[EnemyAI] Peek point atau prefab null — skip ke Idle.");
            _state = EnemyState.Idle;
            yield break;
        }

        // Spawn prefab di posisi & rotasi peek point
        _spawnedPeekObject = Instantiate(point.enemyPrefab, point.transform.position, point.transform.rotation);

        // Cache semua material dari Renderer[] di prefab untuk fade
        var renderers = _spawnedPeekObject.GetComponentsInChildren<Renderer>();
        var matList   = new System.Collections.Generic.List<Material>();
        foreach (var r in renderers)
            matList.AddRange(r.materials);
        _spawnedMaterials = matList.ToArray();

        // Mulai dari alpha 0
        SetSpawnedAlpha(0f);

        // Fade in
        yield return FadeSpawned(1f);

        // Sound — pakai override per point jika ada
        AudioClip clip = point.overrideSound != null ? point.overrideSound : peekSound;
        PlaySound(clip);
        onStartPeeking.Invoke();
        Debug.Log($"[EnemyAI] Peek di: {point.name} | Prefab: {point.enemyPrefab.name}");

        // Tunggu sambil cek noise
        float elapsed = 0f;
        while (elapsed < peekDuration)
        {
            elapsed += Time.deltaTime;

            var noise = _noiseTracker ?? NoiseTracker.Instance;
            if (noise != null && noise.NoisePercent >= noise.JumpscareThresholdPct)
            {
                _state = EnemyState.Jumping;
                yield break;
            }
            if (noise != null && noise.NoisePercent < noise.PeekThresholdPct)
            {
                _state = EnemyState.Leaving;
                yield break;
            }

            yield return null;
        }

        // Peek duration habis
        var n = _noiseTracker ?? NoiseTracker.Instance;
        _state = (n != null && n.NoisePercent >= n.JumpscareThresholdPct)
            ? EnemyState.Jumping
            : EnemyState.Leaving;
    }

    // ── LEAVING ──
    private IEnumerator LeavingRoutine()
    {
        // Fade out prefab yang sedang spawn
        yield return FadeSpawned(0f);

        // Destroy prefab setelah fade out
        if (_spawnedPeekObject != null)
        {
            // FIX — Destroy material instances yang di-buat oleh r.materials sebelum
            // Destroy GameObject. Renderer.materials membuat instanced Material baru
            // per renderer — Destroy(gameObject) tidak otomatis destroy Material instances
            // tersebut. Tanpa ini setiap peek cycle membocorkan N Material ke GPU memory
            // hingga scene unload.
            DestroySpawnedMaterials();
            Destroy(_spawnedPeekObject);
            _spawnedPeekObject = null;
            _spawnedMaterials  = null;
        }

        onStopPeeking.Invoke();
        _state = EnemyState.Idle;
        Debug.Log("[EnemyAI] Enemy pergi.");
    }

    // ── JUMPSCARE ──
    private IEnumerator JumpscareRoutine()
    {
        if (GameOverManager.IsGameOver) yield break;

        // Fade in prefab yang sedang aktif (kalau ada) dengan cepat
        if (_spawnedPeekObject != null)
            yield return FadeSpawned(1f, fadeSpeed * 3f);

        PlaySound(jumpscareSound);
        onJumpscare.Invoke();
        Debug.Log("[EnemyAI] JUMPSCARE!");

        if (jumpscareOverlay != null)
        {
            jumpscareOverlay.gameObject.SetActive(true);
            float e = 0f;
            while (e < 0.3f)
            {
                e += Time.deltaTime;
                SetImageAlpha(jumpscareOverlay, Mathf.Clamp01(e / 0.3f));
                yield return null;
            }
        }

        yield return new WaitForSeconds(jumpscareDuration);

        if (GameOverManager.IsGameOver) yield break;

        (_noiseTracker ?? NoiseTracker.Instance)?.SetGameOver();
        (_sanitySystem ?? SanitySystem.Instance)?.ForceReset();

        GameOverManager.TriggerGameOver();
    }

    // ──────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────

    private EnemyPeekPoint GetNextPeekPoint()
    {
        if (peekPoints == null || peekPoints.Length == 0) return null;
        if (peekPoints.Length == 1) return peekPoints[0];

        int index    = _lastPeekIndex;
        int attempts = 0;
        do
        {
            index = Random.Range(0, peekPoints.Length);
            attempts++;
        }
        while (index == _lastPeekIndex && attempts < peekPoints.Length * 2);

        _lastPeekIndex = index;
        return peekPoints[index];
    }

    // Fade material alpha di spawned object
    private IEnumerator FadeSpawned(float target, float speed = -1f)
    {
        if (_spawnedMaterials == null) yield break;

        float s       = speed > 0f ? speed : fadeSpeed;
        float current = GetSpawnedAlpha();

        // BUG FIX — Ganti Mathf.Approximately dengan threshold berbasis step.
        // Mathf.Approximately memakai epsilon fixed (1e-5) yang terlalu kecil
        // untuk kasus deltaTime besar, menyebabkan loop extra iteration.
        // MoveTowards sudah menjamin konvergensi; cukup cek apakah sudah mencapai target.
        while (current != target)
        {
            current = Mathf.MoveTowards(current, target, s * Time.deltaTime);
            SetSpawnedAlpha(current);
            yield return null;
        }
        SetSpawnedAlpha(target);
    }

    private void SetSpawnedAlpha(float alpha)
    {
        if (_spawnedMaterials == null) return;
        foreach (var mat in _spawnedMaterials)
        {
            if (mat == null) continue;
            Color c = mat.color;
            mat.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    private float GetSpawnedAlpha()
    {
        if (_spawnedMaterials == null || _spawnedMaterials.Length == 0) return 0f;
        return _spawnedMaterials[0] != null ? _spawnedMaterials[0].color.a : 0f;
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        var c = img.color;
        img.color = new Color(c.r, c.g, c.b, alpha);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Destroy semua Material instances yang dibuat oleh r.materials di PeekingRoutine.
    /// Harus dipanggil SEBELUM Destroy(_spawnedPeekObject).
    /// </summary>
    private void DestroySpawnedMaterials()
    {
        if (_spawnedMaterials == null) return;
        foreach (var mat in _spawnedMaterials)
        {
            if (mat != null) Destroy(mat);
        }
    }

    public void Disable()
    {
        StopAllCoroutines();

        if (_spawnedPeekObject != null)
        {
            // FIX — Sama seperti LeavingRoutine: destroy material instances dulu
            // sebelum Destroy GameObject agar tidak ada GPU memory leak.
            DestroySpawnedMaterials();
            Destroy(_spawnedPeekObject);
            _spawnedPeekObject = null;
            _spawnedMaterials  = null;
        }

        if (jumpscareOverlay != null) jumpscareOverlay.gameObject.SetActive(false);
        _state = EnemyState.Idle;
        this.enabled = false;

        Debug.Log($"[EnemyAI] {gameObject.name} disabled.");
    }

    /// <summary>
    /// Re-aktifkan EnemyAI setelah kembali dari rhythm scene.
    /// Dipanggil oleh RhythmGameReturn.ReturnToCCTV() saat win (bukan game over).
    ///
    /// Disable() menghentikan semua coroutine dan set enabled=false.
    /// Tanpa Enable() dipanggil setelah kembali ke CCTV scene, StateMachine
    /// tidak pernah restart → IdleRoutine tidak jalan → jumpscare tidak bisa
    /// terpicu meski noise 100, sampai scene di-reload.
    /// </summary>
    public void Enable()
    {
        if (this.enabled) return; // sudah aktif, tidak perlu restart

        this.enabled = true;
        _state       = EnemyState.Idle;

        // Restart state machine dengan grace period baru agar tidak langsung
        // jumpscare saat player baru kembali dari rhythm scene.
        StartCoroutine(GraceRoutine());
        StartCoroutine(StateMachine());

        Debug.Log($"[EnemyAI] {gameObject.name} re-enabled setelah rhythm scene.");
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (peekPoints == null) return;
        Gizmos.color = Color.red;
        foreach (var p in peekPoints)
        {
            if (p == null) continue;
            Gizmos.DrawLine(transform.position, p.transform.position);
            if (p.enemyPrefab != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                Gizmos.DrawSphere(p.transform.position, 0.25f);
                Gizmos.color = Color.red;
            }
        }
    }
#endif
}