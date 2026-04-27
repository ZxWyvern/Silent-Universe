using UnityEngine;
using UnityEngine.InputSystem;

public class PauseHandler : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName   = "Player";
    [SerializeField] private string pauseActionName = "Pause";

    [Header("Player References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private HeadbobSystem  headbobSystem;
    [SerializeField] private FootstepSystem footstepSystem;

    private InputAction _pauseAction;

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        if (pausePanel     == null) Debug.LogError("[PauseHandler] Pause Panel belum di-assign!",    this);
        if (settingsPanel  == null) Debug.LogError("[PauseHandler] Settings Panel belum di-assign!", this);
        if (playerMovement == null) Debug.LogError("[PauseHandler] PlayerMovement belum di-assign!", this);

        if (pausePanel    != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // FIX #6 — Build pause action SETELAH SettingsSaveManager load binding overrides
        // (order -50), sehingga effectivePath sudah ter-override kalau player rebind ESC.
        BuildPauseAction();
    }

    private void Start()
    {
        LockCursor();
    }

    private void OnEnable()
    {
        _pauseAction.performed += OnPausePerformed;
        _pauseAction.Enable();
    }

    private void OnDisable()
    {
        _pauseAction.performed -= OnPausePerformed;
        _pauseAction.Disable();
    }

    private void OnDestroy()
    {
        _pauseAction.Dispose();
    }

    // ── Input ─────────────────────────────────────────────────────

    private void BuildPauseAction()
    {
        // FIX #6 — Baca binding dari InputActionAsset (sudah ter-apply override
        // oleh SettingsSaveManager di Awake order -50), sehingga kalau player
        // rebind tombol pause, PauseHandler ikut pakai binding yang baru.
        string binding = "<Keyboard>/escape";
        if (inputActions != null)
        {
            InputAction asset = inputActions.FindAction(actionMapName + "/" + pauseActionName);
            if (asset != null && asset.bindings.Count > 0)
            {
                string path = asset.bindings[0].effectivePath;
                if (!string.IsNullOrEmpty(path)) binding = path;
            }
        }
        _pauseAction = new InputAction("Pause", InputActionType.Button, binding);
    }

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        // FIX #1 — Jangan pause saat player di dalam CCTV mode.
        // MonitorInteractable mengurus cursor dan state-nya sendiri.
        if (GameState.IsCCTVActive) return;

        bool settingsOpen = settingsPanel != null && settingsPanel.activeSelf;
        bool pauseOpen    = pausePanel    != null && pausePanel.activeSelf;

        if (settingsOpen)
            CloseSettingsBackToPause();
        else if (!pauseOpen)
            OpenPause();
        // ESC saat Pause terbuka → tidak melakukan apa-apa, harus pencet Resume
    }

    // ── State Transitions ─────────────────────────────────────────

    private void OpenPause()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel    != null) pausePanel.SetActive(true);

        Time.timeScale = 0f;
        if (playerMovement != null) playerMovement.SetInputEnabled(false);
        if (headbobSystem  != null) headbobSystem.SetEnabled(false);
        if (footstepSystem != null) footstepSystem.SetEnabled(false);

        UnlockCursor();
        Debug.Log("[PauseHandler] PAUSED");
    }

    /// Dipanggil tombol Resume di Pause Panel
    public void ResumeGame()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel    != null) pausePanel.SetActive(false);

        Time.timeScale = 1f;
        if (playerMovement != null) playerMovement.SetInputEnabled(true);
        if (headbobSystem  != null) headbobSystem.SetEnabled(true);
        if (footstepSystem != null) footstepSystem.SetEnabled(true);

        LockCursor();
        Debug.Log("[PauseHandler] RESUMED");
    }

    /// Dipanggil tombol Settings di Pause Panel
    public void OpenSettings()
    {
        if (pausePanel    != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    /// Dipanggil tombol Back di Settings Panel
    public void OnSettingsBack() => CloseSettingsBackToPause();

    private void CloseSettingsBackToPause()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel    != null) pausePanel.SetActive(true);
    }

    // ── Cursor ────────────────────────────────────────────────────

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}