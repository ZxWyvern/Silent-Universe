using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveFile
{
    private static readonly string FilePath =
        System.IO.Path.Combine(Application.persistentDataPath, "save.json");
    private static readonly string BackupPath =
        System.IO.Path.Combine(Application.persistentDataPath, "save.json.bak");
    private static readonly string TempPath =
        System.IO.Path.Combine(Application.persistentDataPath, "save.json.tmp");
    private static readonly string CorruptPath =
        System.IO.Path.Combine(Application.persistentDataPath, "save.json.corrupt");

    private static SaveData _data;

    private static bool  _dirty = false;
    private static float _lastWriteTime = -999f;
    private const  float WriteIntervalSeconds = 0.1f;

    // Save versioning — naikkan saat ada breaking change di SaveData
    private const int CurrentSaveVersion = 2;

    public static SaveData Data
    {
        get
        {
            if (_data == null) Read();
            return _data;
        }
    }

    public static void Read()
    {
        // Fase 1.1 — Bersihkan .tmp sisa crash sebelumnya
        if (File.Exists(TempPath)) { try { File.Delete(TempPath); } catch { } }

        // Fase 1.2 — Coba baca file utama, fallback ke backup valid
        if (File.Exists(FilePath) && TryRead(FilePath, out _data))
        {
            Debug.Log($"[SaveFile] Loaded from {FilePath}");
        }
        else if (File.Exists(BackupPath) && TryRead(BackupPath, out _data))
        {
            Debug.LogWarning("[SaveFile] save.json corrupt/hilang — recovered dari backup.");
        }
        else
        {
            _data = new SaveData();
        }

        // Fase 1.3 — Migrasi save version jika perlu
        if (_data.saveVersion < CurrentSaveVersion)
            MigrateSave(_data);

        _dirty = false;
    }

    private static bool TryRead(string path, out SaveData data)
    {
        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveFile] Gagal baca {path}: {e.Message}");
            // Simpan file corrupt untuk diagnosis
            try { File.Copy(path, CorruptPath, overwrite: true); } catch { }
            data = null;
            return false;
        }
    }

    private static void MigrateSave(SaveData d)
    {
        // v1 → v2: dampenerTurnOnTime (float, Time.time) → dampenerTurnOnUnix (long, Unix epoch)
        // Time.time dari sesi lama tidak bisa dikonversi ke Unix yang akurat, jadi jika
        // dampener masih aktif saat migrasi, paksa TurnOff agar tidak stuck selamanya.
        if (d.saveVersion < 2)
        {
            if (d.dampenerOn && d.dampenerTurnOnUnix == 0L)
            {
                Debug.LogWarning("[SaveFile] Migrasi v1→v2: dampener aktif dari save lama — reset state (tidak bisa konversi Time.time ke Unix).");
                d.dampenerOn             = false;
                d.dampenerTurnOnUnix     = 0L;
                d.dampenerPendingPenalty = 0f;
            }
        }

        Debug.Log($"[SaveFile] Migrasi save dari v{d.saveVersion} ke v{CurrentSaveVersion}.");
        d.saveVersion = CurrentSaveVersion;
        ForceWrite();
    }

    public static void MarkDirty() => _dirty = true;

    public static void Write()
    {
        _dirty = true;
        float now = Time.realtimeSinceStartup;
        if (now - _lastWriteTime < WriteIntervalSeconds) return;
        FlushNow();
    }

    public static void ForceWrite() => FlushNow();

    public static void FlushPending()
    {
        if (!_dirty) return;
        float now = Time.realtimeSinceStartup;
        if (now - _lastWriteTime < WriteIntervalSeconds) return;
        FlushNow();
    }

    private static void FlushNow()
    {
        if (_data == null) return;
        try
        {
            string json = JsonUtility.ToJson(Data, prettyPrint: true);

            // Fase 1.1 — Atomic write: tulis ke .tmp dulu, backup valid, baru rename.
            // Jika crash saat WriteAllText(.tmp), file utama tidak tersentuh.
            // Jika crash saat Move, .tmp ada tapi file utama masih valid.
            File.WriteAllText(TempPath, json);

            // Backup versi valid sebelum overwrite
            if (File.Exists(FilePath))
                File.Copy(FilePath, BackupPath, overwrite: true);

            // Rename atomic — OS menjamin ini tidak bisa "setengah jalan"
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(TempPath, FilePath);

            _lastWriteTime = Time.realtimeSinceStartup;
            _dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveFile] Gagal tulis save.json: {e.Message}");
        }
    }

    public static void Delete()
    {
        _data  = new SaveData();
        _dirty = false;

        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
            Debug.Log("[SaveFile] save.json dihapus.");
        }

        // BUG FIX — Hapus backup juga saat Delete() agar Read() tidak
        // restore dari backup setelah New Game / Reset All Progress.
        if (File.Exists(BackupPath))
        {
            File.Delete(BackupPath);
            Debug.Log("[SaveFile] save.json.bak dihapus.");
        }

        if (File.Exists(TempPath))
        {
            try { File.Delete(TempPath); } catch { }
        }
    }

    public static bool Exists() => File.Exists(FilePath);
}

[Serializable]
public class SaveData
{
    // Fase 1.3 — Versi save untuk deteksi breaking changes.
    // Naikkan CurrentSaveVersion di SaveFile dan tambah logika di MigrateSave() saat ada perubahan.
    public int saveVersion = 1;

    public bool   hasSave       = false;
    public string sceneName     = "";
    public float  playerX       = 0f;
    public float  playerY       = 0f;
    public float  playerZ       = 0f;
    public string inventoryKeys = "";
    public string diskInventory = "";

    public bool   cctvActive    = false;
    public string cctvScene     = "";
    public string cctvCamera    = "";
    public bool   cctvDiskIn    = false;

    public bool  dampenerOn             = false;
    // FIX — Ganti float dampenerTurnOnTime (Time.time) dengan long Unix timestamp.
    // Time.time reset ke 0 setiap launch; Unix timestamp persisten lintas sesi.
    // Field lama dampenerTurnOnTime dipertahankan untuk migrasi save v1→v2.
    [System.Obsolete("Gunakan dampenerTurnOnUnix (long). Field ini hanya untuk migrasi save lama.")]
    public float dampenerTurnOnTime     = 0f;
    public long  dampenerTurnOnUnix     = 0L;
    public float dampenerPendingPenalty = 0f;

    public string questActiveId   = "";
    public int    questActiveStep = 0;
    public string questCompleted  = "";

    public float currentNoise = 0f;

    // World flags bool: "Key=1|Key2=0"
    public string worldFlags = "";

    // BUG FIX — World flags string: "Key=NamaDisk|Key2=NamaLain"
    // Dipisah dari worldFlags agar tidak ada konflik parsing
    public string worldFlagsString = "";

    public string droppedItems = "";

    // ── Settings (sensitivity + keybinds) ────────────────────────
    // Disimpan di sini agar tersinkron dengan save system JSON,
    // bukan tersebar di PlayerPrefs yang bisa hilang saat clear cache.
    // Nilai -1 pada sensitivity = belum pernah disimpan → pakai default.
    public float  sensitivity  = -1f;  // -1 = pakai default (0.15f)
    public string keyBindings  = "";   // JSON dari InputActionAsset.SaveBindingOverridesAsJson()

    // Flashlight state — di-persist via FlashlightController.PersistFlashlight()
    public float flashlightBattery           = -1f;  // -1 = belum pernah save, pakai default
    public bool  flashlightOverheat          = false;
    public float flashlightOverheatRemaining = 0f;
    public bool  flashlightBroken            = false;
    public float flashlightBrokenRemaining   = 0f;

    // Fuse & Battery inventory — di-persist via PlayerFuseInventory / PlayerBatteryInventory
    public string fuseInventory    = "";
    public string batteryInventory = "";

    // Equipment — di-persist via PlayerEquipment
    public bool   equippedAxe         = false;
    public string equippedFlashlight  = "";  // itemName, kosong = tidak equipped
}


/// <summary>
/// WorldFlags — baca/tulis bool flags ke SaveData.worldFlags.
/// Format: "Key1=1|Key2=0"
/// JANGAN gunakan karakter '=' atau '|' dalam nama key.
/// </summary>
public static class WorldFlags
{
    // FIX — Cache hasil Parse() agar tidak alokasi Dictionary baru setiap Get/Set/Has.
    // Sebelumnya setiap operasi memanggil Parse() yang Split + alokasi Dictionary baru.
    // DiskRepairHandler.Update() memanggil WorldFlags.Get() setiap frame → ratusan
    // alokasi per detik → GC pressure signifikan.
    //
    // Invalidasi cache hanya terjadi saat raw string berubah (Set/Remove).
    private static Dictionary<string, bool> _boolCache;
    private static string                   _boolCacheRaw;

    private static Dictionary<string, string> _stringCache;
    private static string                     _stringCacheRaw;
    public static bool Get(string key)
    {
        // FIX — Gunakan GetBoolFlags() (cached) bukan Parse() (alokasi baru tiap panggil)
        return GetBoolFlags().TryGetValue(key, out bool val) && val;
    }

    public static void Set(string key, bool value)
    {
        if (key.Contains('=') || key.Contains('|'))
        {
#if UNITY_EDITOR
            throw new ArgumentException($"[WorldFlags] Invalid key: '{key}' — tidak boleh mengandung '=' atau '|'.");
#else
            Debug.LogError($"[WorldFlags] INVALID KEY '{key}': tidak boleh mengandung '=' atau '|'.");
            return;
#endif
        }
        var flags = GetBoolFlags();
        flags[key] = value;
        SaveFile.Data.worldFlags = Serialize(flags);
        InvalidateBoolCache(); // raw string berubah — invalidasi cache
        SaveFile.ForceWrite();
    }

    /// <summary>
    /// Set flag ke SaveData.worldFlags tanpa langsung menulis ke disk.
    /// Gunakan ini saat ingin mengubah beberapa flag sekaligus, lalu panggil
    /// SaveFile.ForceWrite() satu kali setelah semua flag di-set.
    /// Ini menghindari N kali ForceWrite (N kali disk I/O) untuk N flag.
    ///
    /// Contoh pemakaian:
    ///   WorldFlags.SetNoWrite("Door_X_o", true);
    ///   WorldFlags.SetNoWrite("Door_X_k", false);
    ///   SaveFile.ForceWrite();  // satu kali saja
    /// </summary>
    public static void SetNoWrite(string key, bool value)
    {
        if (key.Contains('=') || key.Contains('|'))
        {
#if UNITY_EDITOR
            throw new ArgumentException($"[WorldFlags] Invalid key: '{key}' — tidak boleh mengandung '=' atau '|'.");
#else
            Debug.LogError($"[WorldFlags] INVALID KEY '{key}': tidak boleh mengandung '=' atau '|'.");
            return;
#endif
        }
        var flags = GetBoolFlags();
        flags[key] = value;
        SaveFile.Data.worldFlags = Serialize(flags);
        InvalidateBoolCache();
        // Sengaja TIDAK memanggil ForceWrite() — caller yang bertanggung jawab.
    }

    public static void Remove(string key)
    {
        var flags = GetBoolFlags();
        flags.Remove(key);
        SaveFile.Data.worldFlags = Serialize(flags);
        InvalidateBoolCache();
        SaveFile.ForceWrite();
    }

    public static bool Has(string key) => GetBoolFlags().ContainsKey(key);

    // ── String flags ─────────────────────────────────────────────
    // Digunakan oleh DiskBox untuk menyimpan nama disk yang diinsert.
    // Disimpan di worldFlagsString terpisah agar tidak bentrok dengan bool flags.

    public static string GetString(string key)
    {
        // FIX — Gunakan GetStringFlags() (cached)
        return GetStringFlags().TryGetValue(key, out string val) ? val : "";
    }

    public static void SetString(string key, string value)
    {
        if (key.Contains('=') || key.Contains('|'))
        {
#if UNITY_EDITOR
            throw new ArgumentException($"[WorldFlags] Invalid key: '{key}' — tidak boleh mengandung '=' atau '|'.");
#else
            Debug.LogError($"[WorldFlags] INVALID KEY '{key}': tidak boleh mengandung '=' atau '|'.");
            return;
#endif
        }
        // Nilai juga tidak boleh mengandung separator
        string safeValue = value.Replace("|", "_").Replace("=", "_");
        var flags = GetStringFlags();
        flags[key] = safeValue;
        SaveFile.Data.worldFlagsString = SerializeString(flags);
        InvalidateStringCache();
        // Sengaja tidak Write() di sini — caller (Set) yang akan trigger Write()
    }

    public static void RemoveString(string key)
    {
        var flags = GetStringFlags();
        flags.Remove(key);
        SaveFile.Data.worldFlagsString = SerializeString(flags);
        InvalidateStringCache();
        SaveFile.ForceWrite();
    }

    // ── Private ──────────────────────────────────────────────────

    private static Dictionary<string, bool> GetBoolFlags()
    {
        string raw = SaveFile.Data.worldFlags ?? "";
        if (_boolCache != null && _boolCacheRaw == raw) return _boolCache;

        // Cache miss — parse ulang dan simpan
        _boolCacheRaw = raw;
        _boolCache    = Parse(raw);
        return _boolCache;
    }

    private static void InvalidateBoolCache() => _boolCache = null;

    private static Dictionary<string, string> GetStringFlags()
    {
        string raw = SaveFile.Data.worldFlagsString ?? "";
        if (_stringCache != null && _stringCacheRaw == raw) return _stringCache;

        _stringCacheRaw = raw;
        _stringCache    = ParseString(raw);
        return _stringCache;
    }

    private static void InvalidateStringCache() => _stringCache = null;

    private static Dictionary<string, bool> Parse(string raw)
    {
        var result = new Dictionary<string, bool>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var entry in raw.Split('|'))
        {
            if (string.IsNullOrEmpty(entry)) continue;
            var parts = entry.Split(new char[] { '=' }, 2);
            if (parts.Length == 2)
                result[parts[0]] = parts[1] == "1";
        }
        return result;
    }

    private static string Serialize(Dictionary<string, bool> flags)
    {
        var parts = new List<string>();
        foreach (var kv in flags)
            parts.Add($"{kv.Key}={(kv.Value ? 1 : 0)}");
        return string.Join("|", parts);
    }

    // Parameterized — dipanggil hanya oleh GetStringFlags() saat cache miss
    private static Dictionary<string, string> ParseString(string raw)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var entry in raw.Split('|'))
        {
            if (string.IsNullOrEmpty(entry)) continue;
            var parts = entry.Split(new char[] { '=' }, 2);
            if (parts.Length == 2)
                result[parts[0]] = parts[1];
        }
        return result;
    }

    private static string SerializeString(Dictionary<string, string> flags)
    {
        var parts = new List<string>();
        foreach (var kv in flags)
            parts.Add($"{kv.Key}={kv.Value}");
        return string.Join("|", parts);
    }
}