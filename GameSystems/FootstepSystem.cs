using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(CharacterController))]
public class FootstepSystem : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceProfile
    {
        public string     surfaceName   = "Concrete";
        public AudioClip[] footstepClips;
        [Range(0f, 1f)]   public float volumeScale   = 1f;
        [Range(0f, 0.3f)] public float pitchVariance = 0.1f;
        [Tooltip("Noise ditambahkan ke NoiseTracker per langkah")]
        public float noisePerStep = 3f;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private AudioSource    footstepSource;

    [Header("Surface Profiles")]
    [SerializeField] private SurfaceProfile[] surfaces;
    [SerializeField] private SurfaceProfile   defaultSurface = new SurfaceProfile
    {
        surfaceName   = "Default",
        volumeScale   = 0.8f,
        pitchVariance = 0.1f,
        noisePerStep  = 3f,
    };

    [Header("Timing — Distance Based")]
    [Tooltip("Jarak tempuh (meter) antar langkah saat jalan")]
    [SerializeField] private float walkInterval   = 0.5f;
    [Tooltip("Jarak tempuh antar langkah saat sprint")]
    [SerializeField] private float sprintInterval = 0.35f;
    [Tooltip("Jarak tempuh antar langkah saat crouch")]
    [SerializeField] private float crouchInterval = 0.7f;
    [SerializeField] private float minSpeed       = 0.2f;

    [Header("Sprint Noise")]
    [SerializeField] private float sprintNoiseMult = 1.8f;

    [Header("Crouch Audio")]
    [SerializeField] private float crouchVolumeScale = 0.5f;
    [SerializeField] private float crouchNoiseMult   = 0.4f;

    [Header("Reverb — Environment Detection")]
    [Tooltip("Aktifkan reverb detection. Reverb berubah berdasarkan seberapa 'tertutup' environment.")]
    [SerializeField] private bool  reverbEnabled     = true;
    [Tooltip("Panjang raycast ke setiap arah untuk deteksi dinding/langit-langit.")]
    [SerializeField] private float reverbRayLength   = 6f;
    [Tooltip("Kecepatan transisi reverb. Nilai kecil = transisi lambat (smooth), besar = cepat.")]
    [SerializeField] private float reverbBlendSpeed  = 1.5f;
    [Tooltip("Layer mask untuk raycast environment. Jangan include layer Player atau Enemy.")]
    [SerializeField] private LayerMask reverbRayMask = ~0; // default: everything

    [Tooltip("Jarak minimum ke dinding agar reverb dianggap 'penuh'. " +
             "Kalau semua ray hit dalam jarak ini = reverb 1.0.")]
    [SerializeField] private float reverbCloseRange  = 2f;

    [Tooltip("Interval (detik) antar perhitungan reverb. " +
             "Tidak perlu tiap frame — setiap 0.2-0.5 detik cukup.")]
    [SerializeField] private float reverbCheckInterval = 0.25f;

    // ── State ─────────────────────────────────────────────────────────────────

    private CharacterController _cc;
    private float _distanceTravelled;
    private bool  _isLeftFoot      = true;
    private float _currentReverb;       // nilai yang dikirim ke AudioManager (lerped)
    private float _targetReverb;        // target dari environment detection
    private float _reverbCheckTimer;

    // Directions untuk environment probe (horizontal 4 arah + atas)
    private static readonly Vector3[] ReverbProbeDirections =
    {
        Vector3.forward,
        Vector3.back,
        Vector3.left,
        Vector3.right,
        Vector3.up,
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        // FIX #3 — Pindah register ke Start agar AudioManager sudah selesai Awake.
        // Awake() tidak ada jaminan urutan antar GameObject — kalau FootstepSystem.Awake()
        // jalan sebelum AudioManager.Awake(), AudioServices.Manager masih null
        // dan footstep tidak ter-register tanpa error apapun.
        if (footstepSource != null)
        {
            if (AudioServices.Manager != null)
                AudioServices.Manager.RegisterSource(AudioCategory.Footstep, footstepSource);
            else
                Debug.LogWarning("[FootstepSystem] AudioServices.Manager null saat Start — " +
                                 "pastikan AudioManager ada di scene.");
        }
    }

    private void OnDestroy()
    {
        // Penting: unregister saat scene unload
        if (footstepSource != null)
            AudioServices.Manager?.UnregisterSource(AudioCategory.Footstep, footstepSource);
    }

    private void Update()
    {
        HandleFootsteps();

        if (reverbEnabled)
            HandleReverb();
    }

    // ── Footsteps ─────────────────────────────────────────────────────────────

    private void HandleFootsteps()
    {
        bool grounded = _cc.isGrounded ||
                        Physics.Raycast(transform.position, Vector3.down, 0.15f);
        if (!grounded) return;

        Vector3 hVel  = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
        float   speed = hVel.magnitude;

        if (speed < minSpeed) return;

        float interval = GetCurrentInterval();
        _distanceTravelled += speed * Time.deltaTime;

        if (_distanceTravelled < interval) return;

        _distanceTravelled -= interval;
        PlayFootstep();
    }

    private float GetCurrentInterval()
    {
        if (playerMovement == null)  return walkInterval;
        if (playerMovement.IsCrouching) return crouchInterval;
        if (playerMovement.IsSprinting) return sprintInterval;
        return walkInterval;
    }

    private void PlayFootstep()
    {
        SurfaceProfile profile = GetSurfaceProfile();
        if (profile?.footstepClips == null || profile.footstepClips.Length == 0) return;

        bool isSprinting = playerMovement != null && playerMovement.IsSprinting;
        bool isCrouching = playerMovement != null && playerMovement.IsCrouching;

        float volume    = profile.volumeScale * (_isLeftFoot ? 0.85f : 1.0f);
        _isLeftFoot     = !_isLeftFoot;

        if (isCrouching) volume *= crouchVolumeScale;

        float     pitch = 1f + Random.Range(-profile.pitchVariance, profile.pitchVariance);
        AudioClip clip  = profile.footstepClips[Random.Range(0, profile.footstepClips.Length)];

        if (footstepSource != null)
        {
            footstepSource.pitch = pitch;
            footstepSource.PlayOneShot(clip, volume);
        }

        float noise = profile.noisePerStep;
        if (isSprinting) noise *= sprintNoiseMult;
        if (isCrouching) noise *= crouchNoiseMult;
        NoiseTracker.Instance?.AddNoise(noise);
    }

    // ── Surface Detection ─────────────────────────────────────────────────────

    private SurfaceProfile GetSurfaceProfile()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f,
                            Vector3.down, out RaycastHit hit, 0.5f))
        {
            if (hit.collider.sharedMaterial != null)
            {
                string matName = hit.collider.sharedMaterial.name
                                    .Replace(" (Instance)", "").Trim();
                foreach (var s in surfaces)
                    if (!string.IsNullOrEmpty(s.surfaceName) &&
                        matName.IndexOf(s.surfaceName,
                            System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return s;
            }

            foreach (var s in surfaces)
                if (!string.IsNullOrEmpty(s.surfaceName) &&
                    hit.collider.CompareTag(s.surfaceName))
                    return s;
        }

        return defaultSurface;
    }

    // ── Reverb — Environment-Based ────────────────────────────────────────────

    /// <summary>
    /// Deteksi seberapa "tertutup" environment dengan menembakkan ray ke 5 arah.
    /// Semakin banyak ray yang mengenai dinding dalam jarak dekat,
    /// semakin tinggi reverb target.
    ///
    /// Logika:
    ///   - Tembak 5 ray: depan, belakang, kiri, kanan, atas.
    ///   - Tiap ray yang hit dalam reverbRayLength menghasilkan contribution.
    ///   - Contribution berbanding terbalik dengan jarak: hit dekat = kontribusi penuh.
    ///   - Rata-rata semua contribution = _targetReverb.
    ///
    /// Ini tidak perlu per-frame karena ruangan tidak berubah cepat.
    /// Interval 0.25 detik sudah cukup.
    /// </summary>
    private void HandleReverb()
    {
        // Throttle: hitung ulang target hanya setiap reverbCheckInterval
        _reverbCheckTimer -= Time.deltaTime;
        if (_reverbCheckTimer <= 0f)
        {
            _reverbCheckTimer = reverbCheckInterval;
            _targetReverb     = CalculateEnvironmentReverb();
        }

        // Lerp smooth menuju target
        _currentReverb = Mathf.Lerp(_currentReverb, _targetReverb,
                                     Time.deltaTime * reverbBlendSpeed);

        AudioServices.Manager?.SetReverbSend(_currentReverb);
    }

    private float CalculateEnvironmentReverb()
    {
        Vector3 origin    = transform.position + Vector3.up * 1f; // tinggi dada
        float   totalContrib = 0f;
        int     rayCount  = ReverbProbeDirections.Length;

        foreach (Vector3 dir in ReverbProbeDirections)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit,
                                reverbRayLength, reverbRayMask,
                                QueryTriggerInteraction.Ignore))
            {
                // Contribution = 1.0 di jarak 0, turun linear ke 0 di reverbRayLength
                // Tapi kita beri "boost" kalau sangat dekat (< reverbCloseRange)
                float normalizedDist = hit.distance / reverbRayLength;
                float contrib        = 1f - normalizedDist; // makin dekat, makin tinggi

                // Close-range boost: kalau hit di bawah reverbCloseRange, pastikan contrib >= 0.7
                if (hit.distance <= reverbCloseRange)
                    contrib = Mathf.Max(contrib, 0.7f);

                totalContrib += contrib;
            }
            // Ray yang tidak hit = tidak ada dinding = 0 contribution (outdoor)
        }

        return totalContrib / rayCount; // 0.0 = fully outdoor, ~1.0 = enclosed room
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetEnabled(bool value) => this.enabled = value;

    // Untuk sinkronisasi Headbob
    public float DistanceTravelled => _distanceTravelled;
    public bool  IsLeftFoot        => _isLeftFoot;
    public float CurrentInterval   => GetCurrentInterval();

    // Debug: lihat reverb value saat runtime
    public float CurrentReverbLevel => _currentReverb;
    public float TargetReverbLevel  => _targetReverb;
}