using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MenuManger — UI controller untuk Pause Menu dan Settings Panel.
///
/// FIX: Logika open/close settings kini di-delegate ke PauseHandler agar state
/// cursor, Time.timeScale, dan panel visibility dikelola di satu tempat.
/// MenuManger hanya bertanggung jawab atas navigasi antar panel via button.
///
/// Cara pasang di Inspector:
///   • PauseHandler di-assign ke field _pauseHandler
///   • ATAU PauseHandler di-cari otomatis dari scene saat Awake()
/// </summary>
public class MenuManger : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Scene to Load")]
    [SerializeField] private int SceneToLoad;

    // Referensi PauseHandler untuk delegate state management
    private PauseHandler _pauseHandler;

    private void Awake()
    {
        // Cari PauseHandler otomatis jika tidak di-assign
        _pauseHandler = FindFirstObjectByType<PauseHandler>();
        if (_pauseHandler == null)
            Debug.LogWarning("[MenuManger] PauseHandler tidak ditemukan di scene!");
    }

    // ── Button Callbacks ──────────────────────────────────────────

    /// Hubungkan ke tombol "Settings" di Pause Panel
    public void OpenSettings()
    {
        if (_pauseHandler != null)
        {
            _pauseHandler.OpenSettings();
        }
        else
        {
            // Fallback jika PauseHandler tidak ada
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (pausePanel    != null) pausePanel.SetActive(false);
        }
    }

    /// Hubungkan ke tombol "Back" di Settings Panel
    public void CloseSettings()
    {
        if (_pauseHandler != null)
        {
            _pauseHandler.OnSettingsBack();
        }
        else
        {
            // Fallback
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pausePanel    != null) pausePanel.SetActive(true);
        }
    }

    /// Hubungkan ke tombol "Resume" di Pause Panel
    public void ResumeGame()
    {
        if (_pauseHandler != null)
            _pauseHandler.ResumeGame();
        else
        {
            // Fallback
            Time.timeScale   = 1f;
            Cursor.visible   = false;
            Cursor.lockState = CursorLockMode.Locked;
            if (pausePanel    != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }
    }

    /// Hubungkan ke tombol "Exit to Menu" di Pause Panel
    public void ExitToMenu()
    {
        GameSave.Save();

        Time.timeScale   = 1f;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
        ScreenFader.FadeOutThenLoad(SceneToLoad);
    }
}