using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MainMenuHandler — pasang pada GameObject di scene Main Menu.
/// Hubungkan tombol "New Game" dan "Continue" ke fungsi ini via Button OnClick() di Inspector.
/// </summary>
public class MainMenuHandler : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Nama scene pertama yang di-load saat New Game")]
    [SerializeField] private string firstSceneName = "Level1";

    // ── Button Callbacks ──────────────────────────────────────────

    /// Hubungkan ke tombol "New Game" di Inspector.
    public void NewGame()
    {
        // Hapus save lama
        GameSave.DeleteSave();

        // Reset semua static state agar sesi baru bersih
        SanitySystem.ResetStaticData();
        NoiseTracker.Instance?.ResetNoise();

        if (QuestManager.Instance != null)
            QuestManager.Instance.ResetAllProgress();

        // FIX #2 — Reset timeScale sebelum load scene.
        // Kalau player pause → exit to menu → new game, timeScale masih 0
        // dari sesi pause sebelumnya → scene baru load tapi semua frozen.
        Time.timeScale = 1f;

        Debug.Log("[MainMenu] New Game — semua state direset.");
        ScreenFader.FadeOutThenLoad(firstSceneName);
    }

    /// Hubungkan ke tombol "Continue" di Inspector.
    public void ContinueGame()
    {
        if (!GameSave.HasSave())
        {
            Debug.LogWarning("[MainMenu] Tidak ada save data untuk di-continue.");
            return;
        }

        // FIX #2 — Reset timeScale sebelum load scene.
        // Kalau player pause → exit to menu → continue, timeScale masih 0
        // dari sesi pause sebelumnya → scene load tapi semua frozen.
        Time.timeScale = 1f;

        GameSave.Load();
    }

    /// Hubungkan ke tombol "Quit" di Inspector.
    public void QuitGame()
    {
        Debug.Log("[MainMenu] Quit.");
        Application.Quit();
    }

    /// Cek apakah ada save data — bisa dipakai untuk enable/disable tombol Continue.
    public bool HasSaveData() => GameSave.HasSave();
}