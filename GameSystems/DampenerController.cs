using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using TMPro;

public class DampenerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DampenerState dampenerState;

    [Header("UI")]
    [SerializeField] private TMP_Text   statusText;
    [SerializeField] private GameObject turnOnButton;
    [SerializeField] private GameObject turnOffButton;

    [Header("Scene")]
    [SerializeField] private string cctvSceneName = "SampleScene";

    [Header("Events")]
    public UnityEvent onTurnOn;
    public UnityEvent onTurnOff;

    private void Start()
    {
        if (dampenerState == null)
        {
            Debug.LogError("[Dampener] DampenerState belum diassign!");
            return;
        }

        // Disable AudioListener duplikat saat Additive
        var allAL = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        if (allAL.Length > 1)
        {
            foreach (var al in allAL)
            {
                if (al.gameObject.scene == gameObject.scene)
                {
                    al.enabled = false;
                    break;
                }
            }
        }

        UpdateUI();
    }

private void Update()
    {
        if (dampenerState == null || !dampenerState.IsOn) return;

        if (statusText != null && dampenerState.activeDuration > 0f)
        {
            // Ambil waktu aktif berdasarkan Unix Timestamp (sesuai update di DampenerState)
            long turnOnUnix = SaveFile.Data.dampenerTurnOnUnix;
            long nowUnix    = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            // Hitung sisa waktu
            float remaining = dampenerState.activeDuration - (nowUnix - turnOnUnix);
            remaining       = Mathf.Max(0f, remaining);
            
            statusText.text  = "DAMPENER: ON (" + Mathf.CeilToInt(remaining) + "s)";
            statusText.color = new Color(0.2f, 0.9f, 0.3f);
            if (remaining <= 0f) UpdateUI();
        }
    }

    public void TurnOn()
    {
        if (dampenerState == null) return;
        dampenerState.TurnOn();
        onTurnOn.Invoke();
        UpdateUI();
    }

    public void TurnOff()
    {
        if (dampenerState == null) return;
        dampenerState.TurnOff();
        onTurnOff.Invoke();
        UpdateUI();
    }

    public void GoBack()
    {
        if (string.IsNullOrEmpty(cctvSceneName)) return;

        Scene cctvScene = SceneManager.GetSceneByName(cctvSceneName);
        if (cctvScene.IsValid() && cctvScene.isLoaded)
        {
            SceneManager.SetActiveScene(cctvScene);

            // FindObjectsByType diperlukan di sini karena MonitorInteractable
            // ada di scene CCTV yang berbeda — tidak bisa cross-scene reference.
            // Aman karena hanya dipanggil sekali saat tombol Back ditekan.
            MonitorInteractable monitor = null;
            var allMonitors = FindObjectsByType<MonitorInteractable>(FindObjectsSortMode.None);
            foreach (var m in allMonitors)
            {
                if (m.gameObject.scene == cctvScene) { monitor = m; break; }
            }

            if (monitor != null)
                monitor.SetPaused(false);

            // Pastikan SanitySystem tahu CCTV masih aktif
            SanitySystem.Instance?.SetCCTVActive(true);

            // Unload scene Dampener
            SceneManager.UnloadSceneAsync(gameObject.scene);

            // Cursor kembali ke mode CCTV
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible   = true;
        }
        else
        {
            SceneManager.LoadScene(cctvSceneName);
        }
    }

    private void UpdateUI()
    {
        bool isOn = dampenerState.IsOn;
        if (statusText != null)
        {
            statusText.text  = isOn ? "DAMPENER: ON" : "DAMPENER: OFF";
            statusText.color = isOn
                ? new Color(0.2f, 0.9f, 0.3f)
                : new Color(0.9f, 0.3f, 0.2f);
        }
        if (turnOnButton  != null) turnOnButton.SetActive(!isOn);
        if (turnOffButton != null) turnOffButton.SetActive(isOn);
    }
}