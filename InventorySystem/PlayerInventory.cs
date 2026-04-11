using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

/// <summary>
/// PlayerInventory — komponen player, menyimpan key inventory.
///
/// Fase 4 — Migrasi VContainer:
///   - Hapus Instance singleton dan singleton Awake pattern
///   - Hapus RegisterPersistCallback / UnregisterPersistCallback
///   - Implement IPersistable — GameSaveService inject dan panggil Persist() sebelum write
///   - GameSave.RegisterPlayer() tetap ada — GameSave masih butuh ref player untuk spawn point
///   - SceneLifetimeScope register instance ini sebagai komponen DAN sebagai IPersistable
/// </summary>
public class PlayerInventory : MonoBehaviour, IPersistable
{
    // Compatibility shim — hapus setelah semua caller di-inject.
    // Saat ini: tidak ada caller eksternal yang masih pakai Instance setelah Fase 4.
    public static PlayerInventory Instance { get; private set; }

    [Header("Events")]
    public UnityEvent<string> onKeyAdded;
    public UnityEvent<string> onKeyRemoved;

    [Header("Key Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA KeyItem asset di sini agar bisa di-restore dari save.")]
    [SerializeField] private List<KeyItem> allKeyAssets = new();

    private readonly List<KeyItem> _keys = new();

    private void Awake()
    {
        // Fase 4: Singleton Awake dihapus. SceneLifetimeScope mengurus instance ini.
        // Instance di-set sebagai shim untuk backward compatibility sementara.
        Instance = this;

        // RegisterPlayer tetap dipanggil — GameSave butuh ref Player untuk spawn point.
        // Ini bukan singleton pattern — hanya registrasi referensi ke GameSave static.
        GameSave.RegisterPlayer(gameObject);

        // RegisterPersistCallback DIHAPUS — diganti IPersistable.
        // GameSaveService menerima List<IPersistable> via inject dan panggil Persist()
        // sebelum SaveFile.ForceWrite(). Tidak ada lagi delegate callback.
    }

    private void Start()
    {
        LoadFromSave();
    }

    // IPersistable.Persist() — dipanggil GameSaveService sebelum ForceWrite()
    public void Persist()
    {
        var names = new List<string>();
        foreach (var key in _keys)
            if (key != null) names.Add(key.keyName);
        SaveFile.Data.inventoryKeys = string.Join("|", names);
        // MarkDirty, bukan ForceWrite — SaveFileAutoFlush flush di LateUpdate,
        // atau GameSaveService.Save() yang ForceWrite setelah semua Persist() selesai.
        SaveFile.MarkDirty();
    }

    // ── Public API ──

    public void AddKey(KeyItem key)
    {
        if (key == null) return;
        if (_keys.Contains(key)) return;
        _keys.Add(key);
        onKeyAdded.Invoke(key.keyName);
        Debug.Log($"[Inventory] Key ditambahkan: {key.keyName}");
        Persist();
    }

    public void RemoveKey(KeyItem key)
    {
        if (key == null) return;
        _keys.Remove(key);
        onKeyRemoved.Invoke(key.keyName);
        Persist();
    }

    public bool HasKey(KeyItem key)              => key != null && _keys.Contains(key);
    public KeyItem[] GetAllKeys()                => _keys.ToArray();
    public KeyItem GetLatestKey()                => _keys.Count > 0 ? _keys[_keys.Count - 1] : null;
    public IReadOnlyList<KeyItem> GetAllKeyAssets() => allKeyAssets;

    // ── Load ──

    private void LoadFromSave()
    {
        var savedNames = GameSave.GetSavedKeyNames();
        if (savedNames.Count == 0) return;

        foreach (var name in savedNames)
        {
            var asset = allKeyAssets.Find(k => k != null && k.keyName == name);
            if (asset == null)
            {
                Debug.LogWarning($"[Inventory] Key '{name}' ada di save tapi asset tidak ditemukan di allKeyAssets.");
                continue;
            }
            if (!_keys.Contains(asset))
            {
                _keys.Add(asset);
                onKeyAdded.Invoke(asset.keyName);
                Debug.Log($"[Inventory] Key di-restore dari save: {asset.keyName}");
            }
        }
    }
}