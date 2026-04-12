using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerMovement — Unity 6000.3.8f1 | New Input System (Manual Subscription)
///
/// Setelah refactor, PlayerMovement hanya mengurus:
///   • Gravity + CharacterController movement
///   • Look (yaw + pitch)
///   • Baca input Move
///   • Kalkulasi speed akhir dengan mengalikan multiplier dari
///     CrouchSystem dan SprintSystem
///
/// Crouch dan Sprint sepenuhnya dikelola oleh system masing-masing.
/// PlayerMovement hanya "konsumen" dari state yang mereka expose.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovement : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  Enums
    // ═══════════════════════════════════════════════════════════════

    public enum LocomotionState { Idle, Walk, Sprint, Crouch }

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Input
    // ═══════════════════════════════════════════════════════════════

    [Header("── Input ──────────────────────────────────────────────")]
    [Tooltip("Drag file .inputactions kamu ke sini")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string actionMove    = "Move";
    [SerializeField] private string actionLook    = "Look";

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Movement
    // ═══════════════════════════════════════════════════════════════

    [Header("── Movement ──────────────────────────────────────────")]
    [Tooltip("Kecepatan dasar saat berjalan. Sprint dan Crouch mengalikan nilai ini.")]
    [SerializeField] private float walkSpeed = 4f;

    [Header("── Gravity ───────────────────────────────────────────")]
    [SerializeField] private float gravity          = -19.62f;
    [SerializeField] private float groundedVelocity = -2f;

    [Header("── Look ──────────────────────────────────────────────")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float maxPitchUp       = 80f;
    [SerializeField] private float maxPitchDown     = 80f;

    [Header("── Camera ─────────────────────────────────────────────")]
    [SerializeField] private Transform cameraTarget;

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Dependencies
    // ═══════════════════════════════════════════════════════════════

    [Header("── Dependencies ────────────────────────────────────────")]
    [Tooltip("Auto-resolved dari GameObject jika kosong")]
    [SerializeField] private CrouchSystem crouchSystem;
    [SerializeField] private SprintSystem sprintSystem;

    // ═══════════════════════════════════════════════════════════════
    //  Private — Input
    // ═══════════════════════════════════════════════════════════════

    private InputAction _actionMove;
    private InputAction _actionLook;

    // ═══════════════════════════════════════════════════════════════
    //  Private — Runtime
    // ═══════════════════════════════════════════════════════════════

    private CharacterController _cc;
    private Vector2             _inputMove;
    private Vector2             _inputLook;
    private Vector3             _verticalVelocity;
    private float               _cameraPitch;
    private bool                _moveLocked;
    private bool                _lookLocked;

    // ═══════════════════════════════════════════════════════════════
    //  Public Read-Only Properties
    // ═══════════════════════════════════════════════════════════════

    public LocomotionState State       { get; private set; }
    public bool            IsMoving    => _inputMove.sqrMagnitude > 0.01f;
    public bool            IsCrouching => crouchSystem != null && crouchSystem.IsCrouching;
    public bool            IsSprinting => sprintSystem != null && sprintSystem.IsSprinting && IsMoving;

    /// <summary>
    /// Kecepatan aktual = walkSpeed × sprint multiplier × crouch multiplier.
    /// Jika tidak ada system yang ter-assign, fallback ke walkSpeed murni.
    /// </summary>
    public float CurrentSpeed
    {
        get
        {
            float speed = walkSpeed;
            if (sprintSystem != null) speed *= sprintSystem.SpeedMultiplier;
            if (crouchSystem != null) speed *= crouchSystem.SpeedMultiplier;
            return speed;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Lock/unlock semua input. Digunakan cutscene, dialogue, UI, dsb.
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        _moveLocked = !enabled;
        _lookLocked = !enabled;
        GameState.IsInputLocked = !enabled;

        if (!enabled)
        {
            _inputMove = Vector2.zero;
            _inputLook = Vector2.zero;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        // Auto-resolve dependencies dari GameObject yang sama
        if (crouchSystem == null) crouchSystem = GetComponent<CrouchSystem>();
        if (sprintSystem == null) sprintSystem = GetComponent<SprintSystem>();

        if (cameraTarget == null && Camera.main != null)
            cameraTarget = Camera.main.transform;

        LockCursor();

        ResolveActions();
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }
    private void OnEnable()
    {
        inputActions?.FindActionMap(actionMapName)?.Enable();
    }

    private void OnDisable()
    {
        inputActions?.FindActionMap(actionMapName)?.Disable();
    }

    private void Update()
    {
        ReadAxesThisFrame();
        TickGravity();

        if (!_moveLocked)
        {
            TickLocomotionState();
            TickMovement();
        }

        if (!_lookLocked)
            TickLook();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Input Setup
    // ═══════════════════════════════════════════════════════════════

    private void ResolveActions()
    {
        if (inputActions == null)
        {
            Debug.LogError("[PlayerMovement] InputActionAsset belum di-assign!", this);
            return;
        }

        InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogError($"[PlayerMovement] Action Map '{actionMapName}' tidak ditemukan.", this);
            return;
        }

        _actionMove = map.FindAction(actionMove, throwIfNotFound: false);
        _actionLook = map.FindAction(actionLook, throwIfNotFound: false);

        if (_actionMove == null) Debug.LogError($"[PlayerMovement] Action '{actionMove}' tidak ditemukan.", this);
        if (_actionLook == null) Debug.LogError($"[PlayerMovement] Action '{actionLook}' tidak ditemukan.", this);
    }

    // Move & Look dibaca polling — lebih reliable untuk Vector2 axis dan Mouse Delta
    private void ReadAxesThisFrame()
    {
        _inputMove = _actionMove?.ReadValue<Vector2>() ?? Vector2.zero;
        _inputLook = _actionLook?.ReadValue<Vector2>() ?? Vector2.zero;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Tick Methods
    // ═══════════════════════════════════════════════════════════════

    private void TickGravity()
    {
        if (_cc.isGrounded && _verticalVelocity.y < 0f)
            _verticalVelocity.y = groundedVelocity;

        _verticalVelocity.y += gravity * Time.deltaTime;
        _cc.Move(_verticalVelocity * Time.deltaTime);
    }

    /// <summary>
    /// Update LocomotionState berdasarkan state dari CrouchSystem & SprintSystem.
    /// Priority: Crouch > Sprint > Walk > Idle.
    /// </summary>
    private void TickLocomotionState()
    {
        if (IsCrouching)               { State = LocomotionState.Crouch; return; }
        if (!IsMoving)                 { State = LocomotionState.Idle;   return; }
        if (IsSprinting)               { State = LocomotionState.Sprint; return; }
        State = LocomotionState.Walk;
    }
    
    private void TickMovement()
    {
        if (!IsMoving) return;

        Vector3 direction = transform.right   * _inputMove.x
                          + transform.forward * _inputMove.y;

        _cc.Move(direction * (CurrentSpeed * Time.deltaTime));
    }

private void TickLook()
    {
        // 1. Kalkulasi input mouse HANYA jika ada pergerakan
        if (_inputLook != Vector2.zero)
        {
            transform.Rotate(Vector3.up, _inputLook.x * mouseSensitivity);
            _cameraPitch -= _inputLook.y * mouseSensitivity;
            _cameraPitch  = Mathf.Clamp(_cameraPitch, -maxPitchUp, maxPitchDown);
        }

        // 2. WAJIB ditaruh di luar 'if' agar selalu tereksekusi setiap frame!
        // Ini memberikan "pondasi" rotasi yang stabil untuk ditumpangi oleh HeadbobSystem.
        if (cameraTarget != null)
            cameraTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }
}