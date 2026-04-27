using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SensitivityControl — slider UI untuk sensitivity mouse.
///
/// Taruh di folder Assets/_Scripts/Settings/ (tanpa asmdef).
///
/// Simpan ke settings.json saat slider berhenti digeser (debounce 0.5 detik)
/// — lebih reliable dari OnPointerUp yang bergantung pada EventSystem forwarding.
/// </summary>
[DefaultExecutionOrder(0)]
public class SensitivityControl : MonoBehaviour
{
    [SerializeField] private Slider         slider;
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("Delay setelah slider berhenti digeser sebelum disimpan ke disk (detik).")]
    [SerializeField] private float saveDebounce = 0.5f;

    private float _saveTimer = -1f;

    private void Awake()
    {
        if (slider == null)
        {
            Debug.LogError("[SensitivityControl] Slider belum di-assign!", this);
            return;
        }

        // SettingsSaveManager sudah Load di Awake (order -50)
        float saved = SettingsSaveManager.GetSavedSensitivity();
        slider.SetValueWithoutNotify(saved);

        if (playerMovement != null)
            playerMovement.SetSensitivity(saved);

        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnDestroy()
    {
        // Simpan segera saat panel ditutup kalau ada perubahan pending
        if (_saveTimer >= 0f)
            SaveNow();

        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void Update()
    {
        if (_saveTimer < 0f) return;

        _saveTimer -= Time.unscaledDeltaTime;
        if (_saveTimer <= 0f)
            SaveNow();
    }

    private void OnSliderChanged(float value)
    {
        // Live preview
        if (playerMovement != null)
            playerMovement.SetSensitivity(value);

        // Reset debounce timer
        _saveTimer = saveDebounce;
    }

    private void SaveNow()
    {
        _saveTimer = -1f;
        SettingsSaveManager.SaveSensitivity(slider.value);
        Debug.Log($"[SensitivityControl] Sensitivity disimpan: {slider.value}");
    }
}