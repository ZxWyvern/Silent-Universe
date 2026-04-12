using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MainMenuHandler — pasang pada GameObject di scene Main Menu.
/// Hubungkan tombol "New Game" dan "Continue" ke fungsi ini via Button OnClick() di Inspector.
///
/// BUG FIX:
///   - NewGame() kini mereset semua static state (SanitySystem, QuestManager)
///     sebelum load scene, sehingga sesi baru tidak mewarisi sanity rendah
///     atau _gameOver = true dari sesi sebelumnya.
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

        // Reset semua static state agar sesi baru bersih.
        SanitySystem.ResetStaticData();

        // BUG FIX — Reset GameOverManager agar _alreadyTriggered tidak stuck true
        // dari sesi sebelumnya. Tanpa ini, game over tidak bisa terpicu di sesi baru.

        // BUG FIX — Reset noise ke 0 saat New Game.
        // NoiseTracker adalah DontDestroyOnLoad — tanpa reset ini noise dari sesi
        // sebelumnya (termasuk saat game over di rhythm scene) terbawa ke sesi baru
        // dan tidak decay karena _isRhythmScene mungkin masih true.
        NoiseTracker.Instance?.ResetNoise();

        // Reset quest progress jika QuestManager ada
        if (QuestManager.Instance != null)
            QuestManager.Instance.ResetAllProgress();

        Debug.Log("[MainMenu] New Game — semua state direset.");
        SceneManager.LoadScene(firstSceneName);
    }

    /// Hubungkan ke tombol "Continue" di Inspector.
    public void ContinueGame()
    {
        if (!GameSave.HasSave())
        {
            Debug.LogWarning("[MainMenu] Tidak ada save data untuk di-continue.");
            return;
        }

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