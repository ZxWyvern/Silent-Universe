using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

/// <summary>
/// PlayerBatteryInventory — komponen player, menyimpan stack baterai.
///
/// Fase 4 — Migrasi VContainer:
///   - Hapus Instance singleton dan singleton Awake pattern
///   - Hapus RegisterPersistCallback / UnregisterPersistCallback
///   - Implement IPersistable
///   - FlashlightController akan di-inject instance ini (tidak lagi .Instance)
/// </summary>
public class PlayerBatteryInventory : MonoBehaviour, IPersistable
{
    public static PlayerBatteryInventory Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Maksimum baterai yang bisa dibawa")]
    [SerializeField] private int maxBatteries = 5;

    [Header("Battery Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA BatteryItem asset di sini agar bisa di-restore dari save.")]
    [SerializeField] private List<BatteryItem> allBatteryAssets = new();

    [Header("Events")]
    public UnityEvent<int>    onBatteryCountChanged;
    public UnityEvent<string> onBatteryAdded;
    public UnityEvent         onBatteryUsed;
    public UnityEvent         onInventoryFull;
    public UnityEvent         onInventoryEmpty;

    private readonly List<BatteryItem> _batteries = new();

    public int  Count   => _batteries.Count;
    public bool IsFull  => _batteries.Count >= maxBatteries;
    public bool IsEmpty => _batteries.Count == 0;

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
        var names = new List<string>();
        foreach (var b in _batteries)
            if (b != null) names.Add(b.itemName);
        SaveFile.Data.batteryInventory = string.Join("|", names);
        // Tidak ForceWrite di sini — GameSaveService yang ForceWrite.
    }

    // ── Public API ──

    public void AddBattery(BatteryItem item)
    {
        if (IsFull) { onInventoryFull.Invoke(); return; }
        _batteries.Add(item);
        onBatteryAdded.Invoke(item != null ? item.itemName : "Baterai");
        onBatteryCountChanged.Invoke(_batteries.Count);
        Persist();
        SaveFile.ForceWrite();
    }

    /// Pakai 1 baterai — kembalikan rechargeAmount, -1 jika kosong.
    public float UseBattery()
    {
        if (IsEmpty) return -1f;

        var item = _batteries[_batteries.Count - 1];
        _batteries.RemoveAt(_batteries.Count - 1);
        onBatteryUsed.Invoke();
        onBatteryCountChanged.Invoke(_batteries.Count);

        if (IsEmpty) onInventoryEmpty.Invoke();

        Persist();
        SaveFile.ForceWrite();

        return item != null ? item.rechargeAmount : 60f;
    }

    // ── Load ──

    private void LoadFromSave()
    {
        string raw = SaveFile.Data.batteryInventory ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        foreach (var name in raw.Split('|'))
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (_batteries.Count >= maxBatteries) break;
            var asset = allBatteryAssets.Find(b => b != null && b.itemName == name);
            if (asset == null)
            {
                Debug.LogWarning($"[BatteryInventory] Battery '{name}' ada di save tapi asset tidak ditemukan di allBatteryAssets.");
                continue;
            }
            _batteries.Add(asset);
            onBatteryAdded.Invoke(asset.itemName);
            onBatteryCountChanged.Invoke(_batteries.Count);
            Debug.Log($"[BatteryInventory] Battery di-restore dari save: {asset.itemName}");
        }
    }
}