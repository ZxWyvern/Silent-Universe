using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveFileAutoFlush : MonoBehaviour
{
    private static SaveFileAutoFlush _instance;
    private static bool _preloaded; // true = SaveFile.Read() sudah dipanggil sebelum LoadScene

    /// Dipanggil oleh GameSave.Load() sebelum LoadScene agar OnSceneLoaded skip Read().
    public static void MarkPreloaded() => _preloaded = true;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        // DontDestroyOnLoad hanya bekerja pada root GameObject.
        // Jika script ini dipasang pada child object, detach dulu dari parent.
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;

        if (_preloaded)
        {
            // Data sudah di-read sebelum LoadScene — skip disk I/O di frame ini.
            _preloaded = false;
            Debug.Log($"[SaveFileAutoFlush] Preloaded — skip Read() untuk scene: {scene.name}");
            return;
        }

        SaveFile.Read();
        Debug.Log($"[SaveFileAutoFlush] SaveFile di-reload untuk scene: {scene.name}");
    }

    private void LateUpdate()
    {
        SaveFile.FlushPending();
    }

    private void OnApplicationQuit()
    {
        SaveFile.ForceWrite();
    }
}