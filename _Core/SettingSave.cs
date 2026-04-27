using System;
using System.IO;
using UnityEngine;

/// <summary>
/// SettingsSave — file JSON terpisah (settings.json) khusus untuk menyimpan
/// pengaturan player: sensitivity mouse, keybind overrides, dan tonemapping mode.
///
/// Dipisah dari save.json agar:
///   • Settings tidak ikut terhapus saat New Game / Delete Save
///   • Bisa di-reset independen via [DEV] Delete Settings
///   • Lebih clean dari sisi data separation
///
/// Pola: mirip SaveFile.cs — atomic write via .tmp, backup .bak.
/// </summary>
public static class SettingsSave
{
    // ── File Paths ────────────────────────────────────────────────
    private static readonly string FilePath   = Path.Combine(Application.persistentDataPath, "settings.json");
    private static readonly string BackupPath = Path.Combine(Application.persistentDataPath, "settings.json.bak");
    private static readonly string TempPath   = Path.Combine(Application.persistentDataPath, "settings.json.tmp");

    private static SettingsData _data;

    // ── Public API ────────────────────────────────────────────────

    public static SettingsData Data
    {
        get
        {
            if (_data == null) Read();
            return _data;
        }
    }

    public static void Read()
    {
        // Bersihkan .tmp sisa crash
        if (File.Exists(TempPath)) { try { File.Delete(TempPath); } catch { } }

        if (File.Exists(FilePath) && TryRead(FilePath, out _data))
        {
            Debug.Log($"[SettingsSave] Dimuat dari {FilePath}");
        }
        else if (File.Exists(BackupPath) && TryRead(BackupPath, out _data))
        {
            Debug.LogWarning("[SettingsSave] settings.json corrupt — recovered dari backup.");
        }
        else
        {
            _data = new SettingsData();
            Debug.Log("[SettingsSave] Tidak ada settings.json — pakai default.");
        }

        MigrateFromPlayerPrefsIfNeeded();
    }

    public static void Write()
    {
        if (_data == null) return;
        try
        {
            string json = JsonUtility.ToJson(_data, prettyPrint: true);

            File.WriteAllText(TempPath, json);

            if (File.Exists(FilePath))
                File.Copy(FilePath, BackupPath, overwrite: true);

            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(TempPath, FilePath);

            Debug.Log("[SettingsSave] settings.json disimpan.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SettingsSave] Gagal tulis settings.json: {e.Message}");
        }
    }

    /// Hapus settings.json dan reset ke default.
    public static void Delete()
    {
        _data = new SettingsData();

        if (File.Exists(FilePath))   { File.Delete(FilePath);   Debug.Log("[SettingsSave] settings.json dihapus."); }
        if (File.Exists(BackupPath)) { File.Delete(BackupPath); Debug.Log("[SettingsSave] settings.json.bak dihapus."); }
        if (File.Exists(TempPath))   { try { File.Delete(TempPath); } catch { } }
    }

    public static bool Exists() => File.Exists(FilePath);

    // ── Private ───────────────────────────────────────────────────

    private static bool TryRead(string path, out SettingsData data)
    {
        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<SettingsData>(json) ?? new SettingsData();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SettingsSave] Gagal baca {path}: {e.Message}");
            data = null;
            return false;
        }
    }

    /// Migrasi satu kali dari PlayerPrefs lama ke settings.json.
    private static void MigrateFromPlayerPrefsIfNeeded()
    {
        bool migrated = false;

        if (_data.sensitivity < 0f && PlayerPrefs.HasKey("MouseSensitivity"))
        {
            float old = PlayerPrefs.GetFloat("MouseSensitivity", SettingsData.DefaultSensitivity);
            if (!float.IsNaN(old) && !float.IsInfinity(old) && old > 0f)
            {
                _data.sensitivity = old;
                PlayerPrefs.DeleteKey("MouseSensitivity");
                migrated = true;
                Debug.Log($"[SettingsSave] Migrasi sensitivity dari PlayerPrefs: {old}");
            }
        }

        if (string.IsNullOrEmpty(_data.keyBindings) && PlayerPrefs.HasKey("KeyBindings"))
        {
            string old = PlayerPrefs.GetString("KeyBindings", "");
            if (!string.IsNullOrEmpty(old))
            {
                _data.keyBindings = old;
                PlayerPrefs.DeleteKey("KeyBindings");
                migrated = true;
                Debug.Log("[SettingsSave] Migrasi keyBindings dari PlayerPrefs.");
            }
        }

        if (migrated)
        {
            PlayerPrefs.Save();
            Write();
        }
    }
}

// ── Data Class ────────────────────────────────────────────────────

[Serializable]
public class SettingsData
{
    public const float DefaultSensitivity    = 0.15f;
    public const int   DefaultTonemapping    = 0; // 0=ACES, 1=Neutral, 2=None

    /// Sensitivity mouse. -1 = belum pernah disimpan → pakai DefaultSensitivity.
    public float  sensitivity    = -1f;

    /// JSON dari InputActionAsset.SaveBindingOverridesAsJson(). Kosong = pakai default binding.
    public string keyBindings    = "";

    /// Index tonemapping: 0=ACES, 1=Neutral, 2=None. Default ACES.
    public int    tonemappingMode = DefaultTonemapping;
}