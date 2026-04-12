using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NoiseUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider      noiseBar;
    [SerializeField] private Image       barFill;
    [SerializeField] private TMP_Text    noiseLabel;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Warna")]
    [SerializeField] private Color colorSafe    = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color colorWarning = new Color(1f, 0.8f, 0f);
    [SerializeField] private Color colorDanger  = new Color(1f, 0.3f, 0.1f);

    [Header("Settings")]
    [SerializeField] private float smoothSpeed   = 5f;
    [SerializeField] private bool  autoHide      = true;
    [SerializeField] private float autoHideDelay = 2f;
    [SerializeField] private float fadeSpeed     = 3f;

    private float _displayValue;
    private float _targetValue;
    private float _hideTimer;

    private void Start()
    {
        if (noiseBar != null)
        {
            noiseBar.minValue = 0f;
            noiseBar.maxValue = 1f;
            noiseBar.value    = 0f;
        }
        if (autoHide && panelCanvasGroup != null)
            panelCanvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (NoiseTracker.Instance == null) return;

        _targetValue  = NoiseTracker.Instance.NoisePercent;
        _displayValue = Mathf.Lerp(_displayValue, _targetValue, smoothSpeed * Time.deltaTime);

        if (noiseBar != null)
            noiseBar.value = _displayValue;

        if (barFill != null)
        {
            float peek = NoiseTracker.Instance.PeekThresholdPct;

            Color targetColor = _displayValue >= peek
                ? Color.Lerp(colorWarning, colorDanger,
                    (_displayValue - peek) / Mathf.Max(1f - peek, 0.01f))
                : Color.Lerp(colorSafe, colorWarning,
                    _displayValue / Mathf.Max(peek, 0.01f));

            barFill.color = Color.Lerp(barFill.color, targetColor, smoothSpeed * Time.deltaTime);
        }

        if (noiseLabel != null)
            noiseLabel.text = "NOISE: " + Mathf.RoundToInt(_displayValue * 100f) + "%";

        if (autoHide && panelCanvasGroup != null)
        {
            float newAlpha;
            if (_targetValue > 0f)
            {
                _hideTimer = 0f;
                newAlpha   = Mathf.Lerp(panelCanvasGroup.alpha, 1f, fadeSpeed * Time.deltaTime);
                if (newAlpha > 0.999f) newAlpha = 1f;
            }
            else
            {
                _hideTimer += Time.deltaTime;
                if (_hideTimer >= autoHideDelay)
                {
                    newAlpha = Mathf.Lerp(panelCanvasGroup.alpha, 0f, fadeSpeed * Time.deltaTime);
                    if (newAlpha < 0.001f) newAlpha = 0f;
                }
                else
                {
                    newAlpha = panelCanvasGroup.alpha;
                }
            }
            panelCanvasGroup.alpha = newAlpha;
        }
    }
}