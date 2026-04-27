using TMPro;
using UnityEngine;

/// <summary>
/// TonemappingDropdown — hubungkan TMP_Dropdown ke TonemappingController.
/// Taruh di folder Settings/ bersama script ini lainnya.
/// </summary>
public class TonemappingDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private void Start()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (dropdown == null)
        {
            Debug.LogError("[TonemappingDropdown] TMP_Dropdown tidak ditemukan!");
            return;
        }

        int savedIndex = SettingsSaveManager.Instance != null
            ? SettingsSaveManager.GetSavedTonemapping()
            : SettingsData.DefaultTonemapping;

        dropdown.SetValueWithoutNotify(savedIndex);
        dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDestroy()
    {
        if (dropdown != null)
            dropdown.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(int index)
    {
        if (SettingsSaveManager.Instance == null)
        {
            Debug.LogWarning("[TonemappingDropdown] SettingsSaveManager tidak ditemukan.");
            return;
        }
        SettingsSaveManager.SaveTonemapping(index);
    }
}