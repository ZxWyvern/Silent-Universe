using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PlayerEquipment — komponen player, menyimpan item yang sedang dipegang.
///
/// Fase 4 — Migrasi VContainer:
///   - Hapus Instance singleton dan singleton Awake pattern
///   - Hapus RegisterPersistCallback / UnregisterPersistCallback
///   - Implement IPersistable
///   - FlashlightController dan ItemDropper akan di-inject instance ini
/// </summary>
public class PlayerEquipment : MonoBehaviour, IPersistable
{
    public static PlayerEquipment Instance { get; private set; }

    [Header("Axe Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA AxeItem asset di sini.")]
    [SerializeField] private System.Collections.Generic.List<AxeItem> allAxeAssets = new();

    [Header("Flashlight Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA FlashlightItem asset di sini.")]
    [SerializeField] private System.Collections.Generic.List<FlashlightItem> allFlashlightAssets = new();

    [Header("Events — Axe")]
    public UnityEvent<AxeItem>        onAxeEquipped;
    public UnityEvent                 onAxeUnequipped;

    [Header("Events — Flashlight")]
    public UnityEvent<FlashlightItem> onFlashlightEquipped;
    public UnityEvent                 onFlashlightUnequipped;

    private AxeItem        _equippedAxe;
    private FlashlightItem _equippedFlashlight;

    public bool           HasAxe             => _equippedAxe != null;
    public AxeItem        EquippedAxe        => _equippedAxe;
    public bool           HasFlashlight      => _equippedFlashlight != null;
    public FlashlightItem EquippedFlashlight => _equippedFlashlight;

    private void Awake()
    {
        // Fase 4: Singleton Awake dihapus. Instance sebagai shim sementara.
        Instance = this;
        // RegisterPersistCallback DIHAPUS — diganti IPersistable.
    }

    private void Start()
    {
        LoadFromSave();
    }

    // IPersistable.Persist()
    public void Persist()
    {
        var d = SaveFile.Data;
        d.equippedAxe        = _equippedAxe != null;
        d.equippedFlashlight = _equippedFlashlight != null ? _equippedFlashlight.itemName : "";
        SaveFile.ForceWrite();
    }

    // ── Axe ──

    public void EquipAxe(AxeItem axe)
    {
        _equippedAxe = axe;
        onAxeEquipped.Invoke(axe);
        Persist();
        Debug.Log($"[Equipment] Equipped axe: {axe?.itemName}");
    }

    public void UnequipAxe()
    {
        _equippedAxe = null;
        onAxeUnequipped.Invoke();
        Persist();
    }

    // ── Flashlight ──

    public void EquipFlashlight(FlashlightItem flashlight)
    {
        _equippedFlashlight = flashlight;
        onFlashlightEquipped.Invoke(flashlight);
        Persist();
        Debug.Log($"[Equipment] Equipped flashlight: {flashlight?.itemName}");
    }

    public void UnequipFlashlight()
    {
        _equippedFlashlight = null;
        onFlashlightUnequipped.Invoke();
        Persist();
    }

    // ── Load ──

    private void LoadFromSave()
    {
        var d = SaveFile.Data;

        if (d.equippedAxe && _equippedAxe == null)
        {
            var axeAsset = allAxeAssets.Count > 0 ? allAxeAssets[0] : null;
            if (axeAsset != null)
            {
                _equippedAxe = axeAsset;
                onAxeEquipped.Invoke(_equippedAxe);
                Debug.Log($"[Equipment] Axe di-restore dari save: {_equippedAxe.itemName}");
            }
            else
            {
                Debug.LogWarning("[Equipment] equippedAxe=true di save tapi allAxeAssets kosong!");
            }
        }

        if (!string.IsNullOrEmpty(d.equippedFlashlight) && _equippedFlashlight == null)
        {
            var asset = allFlashlightAssets.Find(f => f != null && f.itemName == d.equippedFlashlight);
            if (asset != null)
            {
                _equippedFlashlight = asset;
                onFlashlightEquipped.Invoke(_equippedFlashlight);
                Debug.Log($"[Equipment] Flashlight di-restore dari save: {_equippedFlashlight.itemName}");
            }
            else
            {
                Debug.LogWarning($"[Equipment] Flashlight '{d.equippedFlashlight}' tidak ditemukan di allFlashlightAssets!");
            }
        }
    }
}