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

    private CharacterController _cc;
    private InputAction         _action;
    private bool                _wantsToStand;

    // FIX spawn: simpan center asli dari Inspector sebelum apapun diubah
    private Vector3 _originalCenter;

    public bool  IsCrouching      { get; private set; }
    public bool  IsCeilingBlocked { get; private set; }
    public float SpeedMultiplier  => IsCrouching ? crouchSpeedMultiplier : 1f;
    public float CrouchProgress   { get; private set; }

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        // Catat center dari Inspector SEBELUM kita sentuh apapun
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
        IsCrouching   = true;
        _wantsToStand = false;
    }

    private void OnCrouchCanceled(InputAction.CallbackContext ctx)
    {
        _wantsToStand = true;
    }

    private void TickCrouch()
    {
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

        LerpControllerHeight(targetHeight);

        if (cameraTarget != null)
            LerpCameraHeight(targetCamY);

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

    private void LerpControllerHeight(float targetHeight)
    {
        float t         = heightLerpSpeed * Time.deltaTime;
        float newHeight = Mathf.Lerp(_cc.height, targetHeight, t);
        float newCenterY = Mathf.Lerp(_cc.center.y, newHeight * 0.5f, t);

        _cc.height = newHeight;
        _cc.center = new Vector3(_originalCenter.x, newCenterY, _originalCenter.z);
    }

    private void LerpCameraHeight(float targetY)
    {
        Vector3 pos = cameraTarget.localPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, cameraLerpSpeed * Time.deltaTime);
        cameraTarget.localPosition = pos;
    }

    private void SnapControllerHeight(float height)
    {
        _cc.height = height;
        // FIX: pertahankan x dan z dari Inspector, hanya y yang disesuaikan
        _cc.center = new Vector3(_originalCenter.x, height * 0.5f, _originalCenter.z);
    }

    private void SnapCameraHeight(float y)
    {
        Vector3 pos = cameraTarget.localPosition;
        pos.y = y;
        cameraTarget.localPosition = pos;
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