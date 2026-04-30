using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ScreenFader — singleton DontDestroyOnLoad yang membuat Canvas fade-to-black sendiri via code.
/// Tidak perlu prefab — cukup attach script ini ke GameObject kosong di scene pertama,
/// atau panggil ScreenFader.Instance dari mana saja (auto-create).
///
/// Usage:
///   ScreenFader.FadeOutThenLoad("SceneName");          // fade out → load → fade in
///   ScreenFader.FadeOutThenLoad("SceneName", onDone);  // + callback setelah fade in selesai
/// </summary>
public class ScreenFader : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────

    [Tooltip("Durasi fade out ke hitam (detik)")]
    [SerializeField] private float fadeOutDuration = 0.4f;

    [Tooltip("Durasi fade in dari hitam (detik)")]
    [SerializeField] private float fadeInDuration  = 0.5f;

    // ── Singleton ─────────────────────────────────────────────────

    private static ScreenFader _instance;

    public static ScreenFader Instance
    {
        get
        {
            if (_instance != null) return _instance;

            // Auto-create jika belum ada di scene
            var go = new GameObject("[ScreenFader]");
            _instance = go.AddComponent<ScreenFader>();
            return _instance;
        }
    }

    // ── Runtime ───────────────────────────────────────────────────

    private Canvas    _canvas;
    private Image     _overlay;
    private bool      _busy;

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        BuildOverlay();
    }

    private void BuildOverlay()
    {
        // Canvas
        var canvasGo = new GameObject("[ScreenFader_Canvas]");
        canvasGo.transform.SetParent(transform);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999; // selalu di atas semua UI lain

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Overlay Image — hitam penuh, mulai transparan
        var imgGo = new GameObject("[ScreenFader_Overlay]");
        imgGo.transform.SetParent(canvasGo.transform, false);

        _overlay            = imgGo.AddComponent<Image>();
        _overlay.color      = new Color(0f, 0f, 0f, 0f);
        _overlay.raycastTarget = false; // tidak blok UI saat transparan

        var rect = imgGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _canvas.gameObject.SetActive(true);
    }

    // ── Public API ────────────────────────────────────────────────

    /// Fade out ke hitam → load scene by name → fade in.
    public static void FadeOutThenLoad(string sceneName, Action onFadeInComplete = null)
    {
        Instance.StartFade(sceneName, -1, onFadeInComplete);
    }

    /// Fade out ke hitam → load scene by build index → fade in.
    public static void FadeOutThenLoad(int sceneIndex, Action onFadeInComplete = null)
    {
        Instance.StartFade(null, sceneIndex, onFadeInComplete);
    }

    // ── Internal ──────────────────────────────────────────────────

    private void StartFade(string sceneName, int sceneIndex, Action onFadeInComplete)
    {
        if (_busy)
        {
            Debug.LogWarning("[ScreenFader] Fade sedang berjalan, request diabaikan.");
            return;
        }
        StartCoroutine(FadeSequence(sceneName, sceneIndex, onFadeInComplete));
    }

    private IEnumerator FadeSequence(string sceneName, int sceneIndex, Action onFadeInComplete)
    {
        _busy = true;
        _overlay.raycastTarget = true; // blok input selama fade berlangsung

        // ── 1. Fade OUT (transparan → hitam) ──────────────────────
        yield return Fade(0f, 1f, fadeOutDuration);

        // ── 2. Load scene (tersembunyi di balik hitam) ─────────────
        if (sceneIndex >= 0)
            SceneManager.LoadScene(sceneIndex);
        else
            SceneManager.LoadScene(sceneName);

        // Tunggu 1 frame agar scene selesai di-initialize
        yield return null;

        // ── 3. Fade IN (hitam → transparan) ───────────────────────
        yield return Fade(1f, 0f, fadeInDuration);

        _busy = false;
        _overlay.raycastTarget = false; // UI di bawah bisa diklik lagi
        onFadeInComplete?.Invoke();
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = _overlay.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // pakai unscaled agar bekerja saat timeScale=0
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            _overlay.color = c;
            yield return null;
        }

        c.a = to;
        _overlay.color = c;
    }
}