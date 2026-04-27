using UnityEngine;

public sealed class HeadbobSystem : MonoBehaviour
{
    [Header("── References ──────────────────────────────────────────")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private FootstepSystem footstepSystem;
    [SerializeField] private CrouchSystem   crouchSystem;
    [SerializeField] private Transform      cameraTarget;

    [Header("── Positional Amplitude (Jarak Guncangan) ──────────────")]
    [SerializeField] private float walkBobY   = 0.05f;
    [SerializeField] private float walkBobX   = 0.025f;
    [SerializeField] private float sprintBobY = 0.1f;
    [SerializeField] private float sprintBobX = 0.05f;
    [SerializeField] private float crouchBobY = 0.025f;
    [SerializeField] private float crouchBobX = 0.01f;

    [Header("── Rotational Amplitude (Kemiringan Lensa) ───────────")]
    [Tooltip("Membuat kamera sedikit menunduk saat kaki menginjak tanah")]
    [SerializeField] private float pitchMultiplier = 1.5f;
    [Tooltip("Membuat kamera miring ke kiri/kanan saat memindahkan berat badan")]
    [SerializeField] private float rollMultiplier  = 1.0f;

    [Header("── Smoothing ───────────────────────────────────────────")]
    [Tooltip("Seberapa cepat kamera kembali ke tengah saat player berhenti")]
    [SerializeField] private float returnSpeed = 8f;

    private float   _currentAmpX;
    private float   _currentAmpY;
    private Vector3 _currentBobOffset = Vector3.zero;

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Start()
    {
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (footstepSystem == null) footstepSystem = GetComponent<FootstepSystem>();
        if (crouchSystem   == null) crouchSystem   = GetComponent<CrouchSystem>();
        if (cameraTarget   == null && Camera.main != null) cameraTarget = Camera.main.transform;
    }

    public void SetEnabled(bool value)
    {
        if (!value && cameraTarget != null)
        {
            cameraTarget.localPosition -= _currentBobOffset;
            _currentBobOffset           = Vector3.zero;
            _currentAmpX                = 0f;
            _currentAmpY                = 0f;
        }
        enabled = value;
    }

    private void LateUpdate()
    {
        if (cameraTarget == null || footstepSystem == null || playerMovement == null) return;

        // FIX MANTUL — Step 1: kembalikan posisi ke base Y dari CrouchSystem,
        // bukan dari localPosition yang mungkin sudah termodifikasi bob frame sebelumnya.
        // Ini mencegah double-write: CrouchSystem set Y, HeadBob subtract offset lama
        // dari Y yang sudah bergeser → posisi drift tiap frame → mantul-mantul.
        float baseY = crouchSystem != null ? crouchSystem.CameraTargetY : cameraTarget.localPosition.y;

        Vector3 cleanPos = cameraTarget.localPosition;
        cleanPos.y = baseY;
        // Hapus bob offset lama dari X (Y sudah di-replace dengan baseY)
        cleanPos.x -= _currentBobOffset.x;
        cleanPos.z -= _currentBobOffset.z;
        cameraTarget.localPosition = cleanPos;

        // FIX #7 — Early return saat player diam dan amplitude sudah mendekati 0
        if (!playerMovement.IsMoving && _currentAmpX < 0.0001f && _currentAmpY < 0.0001f)
        {
            _currentBobOffset = Vector3.zero;
            return;
        }

        // Step 2: tentukan target amplitudo berdasarkan locomotion state
        float targetAmpX = 0f;
        float targetAmpY = 0f;

        if (playerMovement.IsMoving)
        {
            switch (playerMovement.State)
            {
                case PlayerMovement.LocomotionState.Sprint:
                    targetAmpX = sprintBobX;
                    targetAmpY = sprintBobY;
                    break;
                case PlayerMovement.LocomotionState.Crouch:
                    targetAmpX = crouchBobX;
                    targetAmpY = crouchBobY;
                    break;
                default:
                    targetAmpX = walkBobX;
                    targetAmpY = walkBobY;
                    break;
            }
        }

        _currentAmpX = Mathf.Lerp(_currentAmpX, targetAmpX, Time.deltaTime * returnSpeed);
        _currentAmpY = Mathf.Lerp(_currentAmpY, targetAmpY, Time.deltaTime * returnSpeed);

        float finalX     = 0f;
        float finalY     = 0f;
        float finalPitch = 0f;
        float finalRoll  = 0f;

        float interval = footstepSystem.CurrentInterval;
        if (interval > 0.01f)
        {
            float stepProgress = footstepSystem.DistanceTravelled / interval;

            float bouncePhase = stepProgress * 2f * Mathf.PI;
            float bounceY     = -Mathf.Cos(bouncePhase);
            finalY            = bounceY * _currentAmpY;

            float swayX  = Mathf.Sin(stepProgress * Mathf.PI);
            float swayDir = footstepSystem.IsLeftFoot ? -1f : 1f;
            finalX        = swayX * swayDir * _currentAmpX;

            float intensity = _currentAmpY / Mathf.Max(walkBobY, 0.001f);
            finalPitch = -bounceY * pitchMultiplier * intensity;
            finalRoll  = (swayX * swayDir) * rollMultiplier * intensity;
        }

        // Step 3: terapkan bob offset baru di atas base position
        _currentBobOffset = new Vector3(finalX, finalY, 0f);
        cameraTarget.localPosition += _currentBobOffset;

        // Step 4: terapkan rotasi additive
        cameraTarget.localRotation *= Quaternion.Euler(finalPitch, 0f, finalRoll);
    }
}