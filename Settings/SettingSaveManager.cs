using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// SettingsSaveManager — singleton DontDestroyOnLoad untuk settings.json.
///
/// PENTING: Taruh script ini di folder Assets/_Scripts/Settings/ (tanpa asmdef)
/// bukan di GameSystems/ — agar bisa diakses oleh TonemappingController dan
/// script Settings lain yang butuh URP tanpa circular asmdef dependency.
///
/// EXECUTION ORDER: Set ke -50 di Project Settings → Script Execution Order
/// agar Awake() selesai sebelum Keybinding dan SensitivityControl Awake/Start.
/// </summary>
[DefaultExecutionOrder(-50)]
public class SettingsSaveManager : MonoBehaviour
{
    public static SettingsSaveManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private PlayerMovement   playerMovement;

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SettingsSave.Read();
        ApplyKeyBindings();
        ApplySensitivity();
    }

    // ── Public API ────────────────────────────────────────────────

    public static void SaveSensitivity(float value)
    {
        SettingsSave.Data.sensitivity = value;
        SettingsSave.Write();
    }

    public static void SaveKeyBindings(string json)
    {
        SettingsSave.Data.keyBindings = json;
        SettingsSave.Write();
    }

    public static void SaveTonemapping(int dropdownIndex)
    {
        SettingsSave.Data.tonemappingMode = dropdownIndex;
        SettingsSave.Write();
        TonemappingController.Instance?.Apply();
    }

    public static float GetSavedSensitivity()
    {
        float s = SettingsSave.Data.sensitivity;
        return s >= 0f ? s : SettingsData.DefaultSensitivity;
    }

    public static int GetSavedTonemapping()
    {
        return SettingsSave.Data.tonemappingMode;
    }

    public void RegisterPlayerMovement(PlayerMovement pm)
    {
        playerMovement = pm;
        ApplySensitivity();
    }

    // ── DEV ───────────────────────────────────────────────────────

    [ContextMenu("[DEV] Delete Settings")]
    public void DEV_DeleteSettings()
    {
        SettingsSave.Delete();
        if (inputActions != null) inputActions.RemoveAllBindingOverrides();
        ApplySensitivity();
        TonemappingController.Instance?.Apply();
        Debug.Log("[SettingsSaveManager] [DEV] settings.json dihapus — semua setting direset.");
    }

    // ── Private ───────────────────────────────────────────────────

    private void ApplyKeyBindings()
    {
        string binds = SettingsSave.Data.keyBindings;
        if (!string.IsNullOrEmpty(binds) && inputActions != null)
        {
            inputActions.LoadBindingOverridesFromJson(binds);
            Debug.Log("[SettingsSaveManager] KeyBindings diterapkan.");
        }
    }

    private void ApplySensitivity()
    {
        if (playerMovement == null) return;
        playerMovement.SetSensitivity(GetSavedSensitivity());
    }
}