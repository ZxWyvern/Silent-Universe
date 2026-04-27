using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class CrouchSystem : MonoBehaviour
{
    [Header("── Input ──────────────────────────────────────────────")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string actionCrouch  = "Crouch";

    [Header("── Controller Height ──────────────────────────────────")]
    [SerializeField] private float heightStand     = 2f;
    [SerializeField] private float heightCrouch    = 1f;
    [SerializeField] private float heightLerpSpeed = 12f;

    [Header("── Camera ──────────────────────────────────────────────")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float cameraHeightStand  = 1.7f;
    [SerializeField] private float cameraHeightCrouch = 0.7f;
    [SerializeField] private float cameraLerpSpeed    = 14f;

    [Header("── Ceiling Check ───────────────────────────────────────")]
    [SerializeField] private LayerMask ceilingMask = ~0;
    [SerializeField] [Range(0.5f, 1f)] private float ceilingRadiusFactor = 0.85f;

    [Header("── Speed ───────────────────────────────────────────────")]
    [SerializeField] [Range(0.1f, 1f)] private float crouchSpeedMultiplier = 0.55f;

    [Header("── Input Delay ─────────────────────────────────────────")]
    [Tooltip("Delay sebelum crouch aktif setelah tombol ditekan (detik). 0 = instan.")]
    [SerializeField] [Range(0f, 0.5f)] private float crouchInputDelay = 0.15f;

    private CharacterController _cc;
    private InputAction         _action;
    private bool                _wantsToStand;
    private Vector3             _originalCenter;
    private float               _crouchDelayTimer;
    private bool                _pendingCrouch;

    // FIX MANTUL — CrouchSystem tidak langsung set localPosition.y kamera.
    // Sebaliknya, expose target Y ini ke HeadBobSystem agar HeadBob yang
    // apply posisi kamera secara terpusat di LateUpdate, tidak ada dua sistem
    // yang saling override localPosition di frame yang sama.
    public float CameraTargetY { get; private set; }

    // Smooth damp state — lebih smooth dan framerate-independent vs Lerp
    private float _cameraYVelocity;
    private float _heightVelocity;

    public bool  IsCrouching      { get; private set; }
    public bool  IsCeilingBlocked { get; private set; }
    public float SpeedMultiplier  => IsCrouching ? crouchSpeedMultiplier : 1f;
    public float CrouchProgress   { get; private set; }

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _originalCenter = _cc.center;

        if (cameraTarget == null && Camera.main != null)
            cameraTarget = Camera.main.transform;

        _action = inputActions?
            .FindActionMap(actionMapName, throwIfNotFound: false)?
            .FindAction(actionCrouch, throwIfNotFound: false);

        if (_action == null)
            Debug.LogError($"[CrouchSystem] Action '{actionCrouch}' tidak ditemukan di map '{actionMapName}'.", this);
    }

    private void Start()
    {
        SnapControllerHeight(heightStand);
        CameraTargetY = cameraHeightStand;

        if (cameraTarget != null)
            SnapCameraHeight(cameraHeightStand);
    }

    private void OnEnable()
    {
        if (_action == null) return;
        _action.started  += OnCrouchStarted;
        _action.canceled += OnCrouchCanceled;
        inputActions?.FindActionMap(actionMapName)?.Enable();
    }

    private void OnDisable()
    {
        if (_action == null) return;
        _action.started  -= OnCrouchStarted;
        _action.canceled -= OnCrouchCanceled;
    }

    private void Update() => TickCrouch();

    private void OnCrouchStarted(InputAction.CallbackContext ctx)
    {
        _wantsToStand     = false;
        _pendingCrouch    = true;
        _crouchDelayTimer = crouchInputDelay;
    }

    private void OnCrouchCanceled(InputAction.CallbackContext ctx)
    {
        // Kalau tombol dilepas sebelum delay habis, batalkan pending crouch
        _pendingCrouch = false;
        _wantsToStand  = true;
    }

    private void TickCrouch()
    {
        // Hitung countdown delay crouch
        if (_pendingCrouch)
        {
            _crouchDelayTimer -= Time.deltaTime;
            if (_crouchDelayTimer <= 0f)
            {
                IsCrouching    = true;
                _pendingCrouch = false;
            }
        }

        if (_wantsToStand)
        {
            IsCeilingBlocked = CheckCeiling();
            if (!IsCeilingBlocked)
            {
                IsCrouching   = false;
                _wantsToStand = false;
            }
        }
        else
        {
            IsCeilingBlocked = false;
        }

        float targetHeight = IsCrouching ? heightCrouch : heightStand;
        float targetCamY   = IsCrouching ? cameraHeightCrouch : cameraHeightStand;

        SmoothDampControllerHeight(targetHeight);

        // FIX MANTUL — Update CameraTargetY via SmoothDamp (framerate-independent).
        // HeadBobSystem yang apply posisi ini ke localPosition di LateUpdate,
        // bukan CrouchSystem langsung — menghindari double-write di frame yang sama.
        CameraTargetY = Mathf.SmoothDamp(
            CameraTargetY,
            targetCamY,
            ref _cameraYVelocity,
            1f / cameraLerpSpeed   // smoothTime = kebalikan speed
        );

        float range = heightStand - heightCrouch;
        CrouchProgress = range > 0f
            ? 1f - Mathf.Clamp01((_cc.height - heightCrouch) / range)
            : (IsCrouching ? 1f : 0f);
    }

    private bool CheckCeiling()
    {
        Vector3 origin = transform.position + Vector3.up * (heightStand - _cc.radius);
        return Physics.CheckSphere(
            origin,
            _cc.radius * ceilingRadiusFactor,
            ceilingMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void SmoothDampControllerHeight(float targetHeight)
    {
        // FIX SMOOTH — SmoothDamp lebih smooth dan framerate-independent vs Lerp
        float newHeight = Mathf.SmoothDamp(
            _cc.height,
            targetHeight,
            ref _heightVelocity,
            1f / heightLerpSpeed
        );

        float newCenterY = newHeight * 0.5f;
        _cc.height = newHeight;
        _cc.center = new Vector3(_originalCenter.x, newCenterY, _originalCenter.z);
    }

    private void SnapControllerHeight(float height)
    {
        _cc.height = height;
        _cc.center = new Vector3(_originalCenter.x, height * 0.5f, _originalCenter.z);
    }

    private void SnapCameraHeight(float y)
    {
        Vector3 pos = cameraTarget.localPosition;
        pos.y = y;
        cameraTarget.localPosition = pos;
        CameraTargetY      = y;
        _cameraYVelocity   = 0f;
    }

    public void ForceCrouch()
    {
        IsCrouching   = true;
        _wantsToStand = false;
    }

    public void ForceStand()
    {
        _wantsToStand = true;
    }
}