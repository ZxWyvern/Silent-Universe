using UnityEngine;

public sealed class HeadbobSystem : MonoBehaviour
{
    [Header("── References ──────────────────────────────────────────")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private FootstepSystem footstepSystem;
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
    
    // Variabel baru untuk melacak offset headbob agar tidak bentrok dengan CrouchSystem
    private Vector3 _currentBobOffset = Vector3.zero;

    private void Start()
    {
        // Auto-resolve jika kosong
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (footstepSystem == null) footstepSystem = GetComponent<FootstepSystem>();
        if (cameraTarget == null && Camera.main != null) cameraTarget = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (cameraTarget == null || footstepSystem == null || playerMovement == null) return;

        // 1. KUNCI PERBAIKAN: Kembalikan posisi kamera ke state "bersih" dari CrouchSystem.
        // Kita kurangi posisi kamera dengan guncangan frame sebelumnya.
        cameraTarget.localPosition -= _currentBobOffset;

        // 2. Tentukan target intensitas guncangan berdasarkan state Locomotion
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
                default: // Walk
                    targetAmpX = walkBobX;
                    targetAmpY = walkBobY;
                    break;
            }
        }

        // Lerp intensitas agar mulus saat mulai jalan / berhenti tiba-tiba
        _currentAmpX = Mathf.Lerp(_currentAmpX, targetAmpX, Time.deltaTime * returnSpeed);
        _currentAmpY = Mathf.Lerp(_currentAmpY, targetAmpY, Time.deltaTime * returnSpeed);

        // Ambil data mentah langsung dari FootstepSystem
        float finalX     = 0f;
        float finalY     = 0f;
        float finalPitch = 0f;
        float finalRoll  = 0f;

        float interval = footstepSystem.CurrentInterval;
        if (interval > 0.01f)
        {
            // Rasio progres dari 0.0 (Kaki diangkat) ke 1.0 (Kaki menapak)
            float stepProgress = footstepSystem.DistanceTravelled / interval;

            // Sumbu Y (Bounce Atas-Bawah)
            float bouncePhase = stepProgress * 2f * Mathf.PI;
            float bounceY     = -Mathf.Cos(bouncePhase);
            finalY            = bounceY * _currentAmpY;

            // Sumbu X (Sway Kanan-Kiri)
            float swayX = Mathf.Sin(stepProgress * Mathf.PI);
            float swayDir = footstepSystem.IsLeftFoot ? -1f : 1f;
            finalX        = swayX * swayDir * _currentAmpX;

            // Rotasi Lensa (Tilt)
            float intensity = _currentAmpY / Mathf.Max(walkBobY, 0.001f);
            finalPitch = -bounceY * pitchMultiplier * intensity;
            finalRoll  = (swayX * swayDir) * rollMultiplier * intensity;
        }

        // 3. TERAPKAN Headbob baru sebagai offset tambahan (Additive) di atas posisi bersih
        _currentBobOffset = new Vector3(finalX, finalY, 0f);
        cameraTarget.localPosition += _currentBobOffset;

        // 4. Terapkan Rotasi secara Additive
        cameraTarget.localRotation *= Quaternion.Euler(finalPitch, 0f, finalRoll);
    }
}