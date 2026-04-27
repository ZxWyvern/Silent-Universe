using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PENTING: Taruh script ini di folder Assets/_Scripts/Settings/ (tanpa asmdef)
/// bukan di GameSystems/ — agar bisa akses SettingsSaveManager.
/// Keybinding — UI rebind satu InputAction.
///
/// Karena SettingsSaveManager pakai [DefaultExecutionOrder(-50)], binding
/// override sudah ter-apply ke inputActionAsset saat Awake() dipanggil di sini,
/// sehingga RefreshUI() langsung menampilkan binding yang benar.
/// </summary>
[DefaultExecutionOrder(0)]
public class Keybinding : MonoBehaviour
{
    [Header("Action")]
    [Tooltip("Nama action di action map Player, contoh: Move, Sprint, Interact")]
    [SerializeField] private string           actionName;
    [SerializeField] private InputActionAsset inputActionAsset;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI  bindLabel;    // Teks tombol saat ini
    [SerializeField] private TextMeshProUGUI  actionLabel;  // Teks nama action
    [SerializeField] private GameObject       listeningUI;  // Panel "Tekan tombol baru..."
    [SerializeField] private TextMeshProUGUI  listeningLabel;

    private InputAction                                       _action;
    private InputActionRebindingExtensions.RebindingOperation _rebindOp;

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        if (inputActionAsset == null)
        {
            Debug.LogError($"[Keybinding] InputActionAsset belum di-assign!", this);
            return;
        }

        _action = inputActionAsset.FindAction("Player/" + actionName);

        if (_action == null)
        {
            Debug.LogError($"[Keybinding] Action 'Player/{actionName}' tidak ditemukan.", this);
            return;
        }

        if (actionLabel != null) actionLabel.text = actionName;
        if (listeningUI != null) listeningUI.SetActive(false);

        RefreshUI();
    }

    private void OnDestroy()
    {
        _rebindOp?.Cancel();
        _rebindOp?.Dispose();
    }

    // ── Public API ────────────────────────────────────────────────

    public void StartRebinding()
    {
        if (_action == null) return;

        if (listeningUI    != null) listeningUI.SetActive(true);
        if (listeningLabel != null) listeningLabel.text = "Tekan tombol baru...";

        // Nonaktifkan seluruh action map agar input tidak terpicu saat rebinding
        inputActionAsset.FindActionMap("Player")?.Disable();

        _rebindOp = _action.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op => FinishRebind(op, cancelled: false))
            .OnCancel(op   => FinishRebind(op, cancelled: true))
            .Start();
    }

    public void ResetToDefault()
    {
        if (_action == null) return;

        // Hapus override hanya untuk action ini
        for (int i = 0; i < _action.bindings.Count; i++)
            _action.RemoveBindingOverride(i);

        // Simpan ulang seluruh asset setelah reset
        string json = inputActionAsset.SaveBindingOverridesAsJson();
        SettingsSaveManager.SaveKeyBindings(json);

        RefreshUI();
        Debug.Log($"[Keybinding] '{actionName}' direset ke default.");
    }

    // ── Private ───────────────────────────────────────────────────

    private void FinishRebind(InputActionRebindingExtensions.RebindingOperation op, bool cancelled)
    {
        op.Dispose();
        _rebindOp = null;

        inputActionAsset.FindActionMap("Player")?.Enable();

        if (listeningUI != null) listeningUI.SetActive(false);

        if (!cancelled)
        {
            // Simpan SEMUA binding overrides asset ke settings.json
            string json = inputActionAsset.SaveBindingOverridesAsJson();
            SettingsSaveManager.SaveKeyBindings(json);
            Debug.Log($"[Keybinding] Rebind '{actionName}' selesai — disimpan.");
        }
        else
        {
            Debug.Log($"[Keybinding] Rebind '{actionName}' dibatalkan.");
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (bindLabel == null || _action == null) return;

        // Tampilkan binding aktif untuk Keyboard&Mouse control scheme
        bindLabel.text = _action.GetBindingDisplayString(
            InputBinding.MaskByGroup("Keyboard&Mouse"));
    }
}