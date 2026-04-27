using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SettingsManager — muat dan terapkan semua setting dari settings.json saat scene load.
///
/// PENTING: Taruh script ini di folder Assets/_Scripts/Settings/ (tanpa asmdef)
/// bukan di GameSystems/ — agar bisa akses SettingsSaveManager yang sudah dipindah
/// ke Settings/.
///
/// Pasang di GameObject yang sama dengan SettingsSaveManager, atau di scene gameplay.
/// Jika SettingsSaveManager sudah ada dan DontDestroyOnLoad, maka LoadAll() sudah
/// dipanggil otomatis dari Awake — script ini hanya backup jika SettingsSaveManager
/// belum di-setup sebagai DontDestroyOnLoad.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [SerializeField] private InputActionAsset _actions;
    [SerializeField] private PlayerMovement   _playerMovement;

    private void Awake()
    {
        // Jika SettingsSaveManager sudah ada (DontDestroyOnLoad), daftarkan player baru
        if (SettingsSaveManager.Instance != null && _playerMovement != null)
        {
            SettingsSaveManager.Instance.RegisterPlayerMovement(_playerMovement);
            return;
        }

        // Fallback: muat langsung jika SettingsSaveManager belum ada
        LoadAll();
    }

    public void LoadAll()
    {
        SettingsSave.Read();

        // Apply keybinds
        string binds = SettingsSave.Data.keyBindings;
        if (!string.IsNullOrEmpty(binds) && _actions != null)
            _actions.LoadBindingOverridesFromJson(binds);

        // Apply sensitivity
        if (_playerMovement != null)
        {
            float sens = SettingsSaveManager.GetSavedSensitivity();
            _playerMovement.SetSensitivity(sens);
        }
    }
}