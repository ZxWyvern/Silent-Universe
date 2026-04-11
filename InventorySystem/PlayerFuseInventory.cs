using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

/// <summary>
/// PlayerFuseInventory — komponen player, menyimpan fuse inventory.
///
/// Fase 4 — Migrasi VContainer:
///   - Hapus Instance singleton dan singleton Awake pattern
///   - Hapus RegisterPersistCallback / UnregisterPersistCallback
///   - Implement IPersistable
///   - ItemDropper dan ItemInventoryUI akan di-inject instance ini
/// </summary>
public class PlayerFuseInventory : MonoBehaviour, IPersistable
{
    public static PlayerFuseInventory Instance { get; private set; }

    [Header("Events")]
    public UnityEvent<string> onFuseAdded;
    public UnityEvent<string> onFuseRemoved;

    [Header("Fuse Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA FuseItem asset di sini agar bisa di-restore dari save.")]
    [SerializeField] private List<FuseItem> allFuseAssets = new();

    private readonly List<FuseItem> _fuses = new();

    public bool     HasAnyFuse             => _fuses.Count > 0;
    public bool     HasFuse(FuseItem fuse) => fuse != null && _fuses.Contains(fuse);
    public int      Count                  => _fuses.Count;
    public FuseItem[] GetAll()             => _fuses.ToArray();

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
        foreach (var f in _fuses)
            if (f != null) names.Add(f.itemName);
        SaveFile.Data.fuseInventory = string.Join("|", names);
    }

    // ── Public API ──

    public void AddFuse(FuseItem fuse)
    {
        if (fuse == null) return;
        _fuses.Add(fuse);
        onFuseAdded.Invoke(fuse.itemName);
        Debug.Log($"[FuseInventory] Fuse ditambahkan: {fuse.itemName}");
        Persist();
        SaveFile.ForceWrite();
    }

    public void RemoveFuse(FuseItem fuse)
    {
        if (fuse == null) return;
        _fuses.Remove(fuse);
        onFuseRemoved.Invoke(fuse.itemName);
        Persist();
        SaveFile.ForceWrite();
    }

    public FuseItem TakeFirst()
    {
        if (_fuses.Count == 0) return null;
        var fuse = _fuses[0];
        _fuses.RemoveAt(0);
        onFuseRemoved.Invoke(fuse != null ? fuse.itemName : "");
        Persist();
        SaveFile.ForceWrite();
        return fuse;
    }

    // ── Load ──

    private void LoadFromSave()
    {
        string raw = SaveFile.Data.fuseInventory ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        foreach (var name in raw.Split('|'))
        {
            if (string.IsNullOrEmpty(name)) continue;
            var asset = allFuseAssets.Find(f => f != null && f.itemName == name);
            if (asset == null)
            {
                Debug.LogWarning($"[FuseInventory] Fuse '{name}' ada di save tapi asset tidak ditemukan di allFuseAssets.");
                continue;
            }
            _fuses.Add(asset);
            onFuseAdded.Invoke(asset.itemName);
            Debug.Log($"[FuseInventory] Fuse di-restore dari save: {asset.itemName}");
        }
    }
}