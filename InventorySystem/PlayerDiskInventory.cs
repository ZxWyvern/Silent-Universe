using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PlayerDiskInventory — Singleton, pasang pada Player.
/// Menyimpan disk yang dimiliki player (urutan LIFO untuk drop).
///
/// BUG FIX #2: Tambahkan PersistDisks() agar inventory bisa flush ke SaveFile
/// sebelum WorldFlags.Set() dipanggil oleh DiskBox, mencegah race condition duplikasi disk.
///
/// Fase 4 — Migrasi VContainer:
///   - Implement IPersistable agar bisa di-register ke GameSaveService
///   - Hapus singleton Awake pattern (Instance sebagai shim sementara)
///   - Hapus RegisterPersistCallback / UnregisterPersistCallback
/// </summary>
public class PlayerDiskInventory : MonoBehaviour, IPersistable
{
    public static PlayerDiskInventory Instance { get; private set; }

    [Header("Events")]
    public UnityEvent<string>   onDiskAdded;    // (diskName)
    public UnityEvent<string>   onDiskRemoved;  // (diskName)

    [Header("Disk Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA DiskItem asset di sini agar bisa di-restore dari save.")]
    [SerializeField] private List<DiskItem> allDiskAssets = new();

    private readonly List<DiskItem> _disks = new();

    public bool HasAnyDisk             => _disks.Count > 0;
    public bool HasDisk(DiskItem disk) => disk != null && _disks.Contains(disk);
    public int  Count                  => _disks.Count;

    private void Awake()
    {
        // Fase 4: Singleton Awake dihapus. Instance sebagai shim sementara.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // RegisterPersistCallback DIHAPUS — diganti IPersistable.
        // DiskRegistry tetap diisi di sini karena PlayerDiskInventory selalu ada di Player.
        DiskRegistry.Register(allDiskAssets);
    }

    private void OnDestroy()
    {
        // UnregisterPersistCallback DIHAPUS — tidak ada lagi callback untuk di-unregister.
    }

    private void Start()
    {
        LoadFromSave();
    }

    // IPersistable.Persist() — dipanggil GameSaveService sebelum ForceWrite()
    public void Persist()
    {
        PersistDisks();
    }

    public void AddDisk(DiskItem disk)
    {
        if (disk == null || _disks.Contains(disk)) return;
        _disks.Add(disk);
        onDiskAdded.Invoke(disk.itemName);
        Debug.Log($"[DiskInventory] Disk ditambahkan: {disk.itemName}");
        PersistDisks();
        SaveFile.ForceWrite();
    }

    public void RemoveDisk(DiskItem disk)
    {
        if (disk == null) return;
        _disks.Remove(disk);
        onDiskRemoved.Invoke(disk.itemName);
        PersistDisks();
        SaveFile.ForceWrite();
    }

    public DiskItem GetLatest() => _disks.Count > 0 ? _disks[_disks.Count - 1] : null;
    public DiskItem[] GetAll()  => _disks.ToArray();

    // BUG FIX #2 — Flush daftar disk saat ini ke SaveFile.Data.
    // Dipanggil oleh DiskBox.OnInteract() SEBELUM WorldFlags.Set() agar save konsisten.
    public void PersistDisks()
    {
        var names = new List<string>();
        foreach (var disk in _disks)
            if (disk != null) names.Add(disk.itemName);
        SaveFile.Data.diskInventory = string.Join("|", names);
        Debug.Log($"[DiskInventory] PersistDisks: {SaveFile.Data.diskInventory}");
    }

    private void LoadFromSave()
    {
        string raw = SaveFile.Data.diskInventory ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        foreach (var name in raw.Split('|'))
        {
            if (string.IsNullOrEmpty(name)) continue;
            var asset = allDiskAssets.Find(d => d != null && d.itemName == name);
            if (asset == null)
            {
                Debug.LogWarning($"[DiskInventory] Disk '{name}' ada di save tapi asset tidak ditemukan di allDiskAssets.");
                continue;
            }
            if (!_disks.Contains(asset))
            {
                _disks.Add(asset);
                onDiskAdded.Invoke(asset.itemName);
                Debug.Log($"[DiskInventory] Disk di-restore dari save: {asset.itemName}");
            }
        }
    }
}
