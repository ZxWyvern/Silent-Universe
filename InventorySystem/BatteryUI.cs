using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BatteryUI — HUD senter.
///
/// Tambahan UI yang dibutuhkan:
///   - OverheatBar   [Slider] — progress overheat saat tahan F
///   - OverheatPanel [GameObject] — muncul saat overheat (cooldown)
///   - RechargePanel [GameObject] — muncul saat mengisi battery
///   - RechargeBar   [Slider] — progress mengisi battery
/// </summary>
public class BatteryUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject batteryPanel;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Battery Bar — Segments")]
    [Tooltip("Drag semua Image segment dari kecil ke besar (bawah ke atas)")]
    [SerializeField] private Image[]  segments;
    [SerializeField] private TMP_Text batteryPctText;
    [SerializeField] private Color    colorFull     = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color    colorLow      = new Color(1f, 0.4f, 0.1f);
    [SerializeField] private Color    colorDepleted = Color.red;
    [SerializeField] private Color    colorEmpty    = new Color(0.15f, 0.15f, 0.15f, 0.4f);

    [Header("Stock")]
    [SerializeField] private TMP_Text   stockCountText;
    [SerializeField] private GameObject stockRow;

    [Header("Overheat")]
    [Tooltip("Slider progress overheat saat tahan F — isi saat F ditahan")]
    [SerializeField] private Slider     overheatBar;
    [Tooltip("Panel yang muncul saat senter sedang cooldown overheat")]
    [SerializeField] private GameObject overheatPanel;
    [Tooltip("Teks cooldown (opsional)")]
    [SerializeField] private TMP_Text   overheatText;

    [Header("Broken")]
    [Tooltip("Panel yang muncul saat senter rusak akibat spam")]
    [SerializeField] private GameObject brokenPanel;
    [Tooltip("Countdown timer teks (opsional)")]
    [SerializeField] private TMPro.TMP_Text brokenTimerText;

    [Header("Recharge")]
    [Tooltip("Panel yang muncul saat mengisi battery")]
    [SerializeField] private GameObject rechargePanel;
    [Tooltip("Slider progress mengisi battery")]
    [SerializeField] private Slider     rechargeBar;
    [Tooltip("Prompt recharge saat battery habis dan punya battery di inventory")]
    [SerializeField] private GameObject rechargePrompt;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration  = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float autoHideDelay   = 3f;

    private Coroutine _fadeRoutine;
    private Coroutine _autoHideRoutine;
    private bool      _flashlightOwned;

    private void Awake()
    {
        if (panelCanvasGroup == null && batteryPanel != null)
            panelCanvasGroup = batteryPanel.GetComponent<CanvasGroup>()
                            ?? batteryPanel.AddComponent<CanvasGroup>();

        HideImmediate();
        SetActive(overheatPanel,  false);
        SetActive(rechargePanel,  false);
        SetActive(rechargePrompt, false);
        SetActive(brokenPanel,    false);
        if (overheatBar  != null) overheatBar.value  = 0f;
        if (rechargeBar  != null) rechargeBar.value  = 0f;
    }

    private void Start()
    {
        var fl = FlashlightController.Instance;
        if (fl != null)
        {
            fl.onFlashlightOn.AddListener(OnFlashlightOn);
            fl.onFlashlightOff.AddListener(OnFlashlightOff);
            fl.onBatteryChanged.AddListener(OnBatteryChanged);
            fl.onBatteryDepleted.AddListener(OnBatteryDepleted);
            fl.onOverheatStart.AddListener(OnOverheatStart);
            fl.onOverheatEnd.AddListener(OnOverheatEnd);
            fl.onOverheatProgress.AddListener(OnOverheatProgress);
            fl.onRechargeProgress.AddListener(OnRechargeProgress);
            fl.onRechargeComplete.AddListener(OnRechargeComplete);
            fl.onBrokenStart.AddListener(OnBrokenStart);
            fl.onBrokenEnd.AddListener(OnBrokenEnd);
        }

        var equip = PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
            equip.onFlashlightUnequipped.AddListener(OnFlashlightUnequipped);
        }

        var inv = PlayerBatteryInventory.Instance;
        if (inv != null)
            inv.onBatteryCountChanged.AddListener(OnBatteryCountChanged);

        UpdateStockUI(inv != null ? inv.Count : 0);
        SetBarValue(1f);
    }

    // ──────────────────────────────────────────
    // Flashlight Callbacks
    // ──────────────────────────────────────────

    private void OnFlashlightEquipped(FlashlightItem fl)
    {
        _flashlightOwned = true;
        ShowPanel();
        SetBarValue(1f);
    }

    private void OnFlashlightUnequipped()
    {
        _flashlightOwned = false;
        SetActive(rechargePrompt, false);
        SetActive(overheatPanel,  false);
        SetActive(rechargePanel,  false);
        StartAutoHide();
    }

    private void OnFlashlightOn()
    {
        ShowPanel();
        CancelAutoHide();
        SetActive(rechargePrompt, false);
    }

    private void OnFlashlightOff()
    {
        UpdateRechargePrompt();
        StartAutoHide();
    }

    private void OnBatteryChanged(float percent)
    {
        SetBarValue(percent);
        ShowPanel();
    }

    private void OnBatteryDepleted()
    {
        SetBarValue(0f);
        UpdateRechargePrompt();
        ShowPanel();
        StartAutoHide();
    }

    private void OnBatteryCountChanged(int count)
    {
        UpdateStockUI(count);
        UpdateRechargePrompt();
        if (count > 0 && _flashlightOwned) ShowPanel();
    }

    // ──────────────────────────────────────────
    // Overheat Callbacks
    // ──────────────────────────────────────────

    private void OnOverheatProgress(float t)
    {
        if (overheatBar != null) overheatBar.value = t;
    }

    private void OnOverheatStart()
    {
        SetActive(overheatPanel, true);
        if (overheatText != null) overheatText.text = "OVERHEAT!";
        SetActive(rechargePrompt, false);
        ShowPanel();
        CancelAutoHide();
    }

    private void OnOverheatEnd()
    {
        SetActive(overheatPanel, false);
        if (overheatBar != null) overheatBar.value = 0f;
        UpdateRechargePrompt();
        StartAutoHide();
    }

    // ──────────────────────────────────────────
    // Broken Callbacks
    // ──────────────────────────────────────────

    private System.Collections.IEnumerator BrokenCountdown(float duration)
    {
        float remaining = duration;
        while (remaining > 0f)
        {
            if (brokenTimerText != null)
                brokenTimerText.text = $"RUSAK ({Mathf.CeilToInt(remaining)}s)";
            remaining -= UnityEngine.Time.deltaTime;
            yield return null;
        }
        if (brokenTimerText != null) brokenTimerText.text = "";
    }

    private Coroutine _brokenCountdownRoutine;

    private void OnBrokenStart(float duration)
    {
        SetActive(brokenPanel, true);
        SetActive(rechargePrompt, false);
        SetActive(overheatPanel, false);
        ShowPanel();
        CancelAutoHide();
        if (_brokenCountdownRoutine != null) StopCoroutine(_brokenCountdownRoutine);
        _brokenCountdownRoutine = StartCoroutine(BrokenCountdown(duration));
    }

    private void OnBrokenEnd()
    {
        SetActive(brokenPanel, false);
        if (brokenTimerText != null) brokenTimerText.text = "";
        StartAutoHide();
    }

    // ──────────────────────────────────────────
    // Recharge Callbacks
    // ──────────────────────────────────────────

    private void OnRechargeProgress(float t)
    {
        SetActive(rechargePanel, t > 0f);
        if (rechargeBar != null) rechargeBar.value = t;
        if (t > 0f) ShowPanel();
    }

    private void OnRechargeComplete()
    {
        SetActive(rechargePanel, false);
        if (rechargeBar != null) rechargeBar.value = 0f;
        SetActive(rechargePrompt, false);
        ShowPanel();
    }

    // ──────────────────────────────────────────
    // UI Helpers
    // ──────────────────────────────────────────

    private float _displayPercent = 1f;

    private void SetBarValue(float percent)
    {
        _displayPercent = percent;

        if (batteryPctText != null)
            batteryPctText.text = Mathf.RoundToInt(percent * 100f) + "%";

        if (segments == null || segments.Length == 0) return;

        Color activeColor;
        if (percent <= 0f)        activeColor = colorDepleted;
        else if (percent <= 0.2f) activeColor = colorLow;
        else                      activeColor = Color.Lerp(colorLow, colorFull, (percent - 0.2f) / 0.8f);

        float filledCount = percent * segments.Length;

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            float fill = Mathf.Clamp01(filledCount - i);
            segments[i].color = fill > 0.01f ? activeColor : colorEmpty;
        }
    }

    private void UpdateStockUI(int count)
    {
        if (stockCountText != null) stockCountText.text = $"× {count}";
        if (stockRow != null)       stockRow.SetActive(count > 0);
    }

    private void UpdateRechargePrompt()
    {
        if (rechargePrompt == null) return;
        var fl  = FlashlightController.Instance;
        var inv = PlayerBatteryInventory.Instance;

        bool show = _flashlightOwned
                 && fl  != null && !fl.IsOn && !fl.IsOverheated && !fl.IsRecharging
                 && inv != null && !inv.IsEmpty
                 && (fl.BatteryRemaining <= 0);

        rechargePrompt.SetActive(show);
    }

    private void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    // ──────────────────────────────────────────
    // Fade
    // ──────────────────────────────────────────

    private void ShowPanel()
    {
        if (batteryPanel == null) return;
        CancelAutoHide();
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeIn());
    }

    private void HideImmediate()
    {
        if (batteryPanel == null) return;
        batteryPanel.SetActive(false);
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
    }

    private void StartAutoHide()
    {
        if (autoHideDelay <= 0f) return;
        CancelAutoHide();
        _autoHideRoutine = StartCoroutine(AutoHideRoutine());
    }

    private void CancelAutoHide()
    {
        if (_autoHideRoutine != null) { StopCoroutine(_autoHideRoutine); _autoHideRoutine = null; }
    }

    private IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSeconds(autoHideDelay);
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeIn()
    {
        batteryPanel.SetActive(true);
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            if (panelCanvasGroup != null)
                panelCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
        float start   = panelCanvasGroup != null ? panelCanvasGroup.alpha : 1f;
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            if (panelCanvasGroup != null)
                panelCanvasGroup.alpha = Mathf.Lerp(start, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        batteryPanel.SetActive(false);
    }
}