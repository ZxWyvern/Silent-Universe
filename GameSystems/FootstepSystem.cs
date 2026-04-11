using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(CharacterController))]
public class FootstepSystem : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceProfile
    {
        public string surfaceName = "Concrete";
        public AudioClip[] footstepClips;
        [Range(0f, 1f)]   public float volumeScale   = 1f;
        [Range(0f, 0.3f)] public float pitchVariance = 0.1f;
        [Tooltip("Noise ditambahkan ke NoiseTracker per langkah")]
        public float noisePerStep = 3f;
    }

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private AudioSource    footstepSource;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string     reverbParam      = "ReverbWetLevel";
    [SerializeField] private float      reverbBlendSpeed = 2f;

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

    // ── State ────────────────────────────────────────────────────────

    private CharacterController _cc;
    private float _distanceTravelled;
    private bool  _isLeftFoot       = true;
    private float _currentReverbFactor;

    // ── Lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        HandleFootsteps();
        if (audioMixer != null) HandleReverb();
    }

    // ── Footsteps ────────────────────────────────────────────────────

    private void HandleFootsteps()
    {
        bool grounded = _cc.isGrounded ||
                        Physics.Raycast(transform.position, Vector3.down, 0.15f);
        if (!grounded) return;

        Vector3 hVel  = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
        float   speed = hVel.magnitude;

        if (speed < minSpeed)
        {
            // HAPUS baris _distanceTravelled = 0f; di sini!
            // Kita biarkan saja nilainya "pause" di tempat terakhir.
            return;
        }

        float interval = GetCurrentInterval();
        _distanceTravelled += speed * Time.deltaTime;

        if (_distanceTravelled < interval) return;

        _distanceTravelled -= interval;
        PlayFootstep();
    }

    private float GetCurrentInterval()
    {
        if (playerMovement == null) return walkInterval;
        if (playerMovement.IsCrouching) return crouchInterval;
        if (playerMovement.IsSprinting) return sprintInterval;
        return walkInterval;
    }

    private void PlayFootstep()
    {
        SurfaceProfile profile = GetSurfaceProfile();
        if (profile == null || profile.footstepClips == null ||
            profile.footstepClips.Length == 0) return;

        bool isSprinting = playerMovement != null && playerMovement.IsSprinting;
        bool isCrouching = playerMovement != null && playerMovement.IsCrouching;

        float volume = profile.volumeScale * (_isLeftFoot ? 0.85f : 1.0f);
        _isLeftFoot  = !_isLeftFoot;

        if (isCrouching) volume *= crouchVolumeScale;

        float     pitch = 1f + Random.Range(-profile.pitchVariance, profile.pitchVariance);
        AudioClip clip  = profile.footstepClips[Random.Range(0, profile.footstepClips.Length)];

        if (footstepSource != null)
        {
            footstepSource.pitch = pitch;
            footstepSource.PlayOneShot(clip, volume);
        }

        // Tambah noise ke NoiseTracker setiap langkah
        float noise = profile.noisePerStep;
        if (isSprinting) noise *= sprintNoiseMult;
        if (isCrouching) noise *= crouchNoiseMult;
        NoiseTracker.Instance?.AddNoise(noise);
    }

    // ── Surface Detection ────────────────────────────────────────────

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

    // ── Reverb ───────────────────────────────────────────────────────

    private void HandleReverb()
    {
        float target = _cc.velocity.magnitude > 0.1f ? 1f : 0f;
        _currentReverbFactor = Mathf.Lerp(_currentReverbFactor, target,
                                          Time.deltaTime * reverbBlendSpeed);
        float db = Mathf.Log10(Mathf.Max(_currentReverbFactor, 0.0001f)) * 20f;
        audioMixer.SetFloat(reverbParam, db);
    }
    
    public void SetEnabled(bool enabled) => this.enabled = enabled;
    // ── Public Getters untuk Sinkronisasi Headbob ────────────────────
    public float DistanceTravelled => _distanceTravelled;
    public bool  IsLeftFoot        => _isLeftFoot;
    public float CurrentInterval   => GetCurrentInterval();
}