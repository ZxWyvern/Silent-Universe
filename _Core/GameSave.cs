using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameSave — save system berbasis JSON (via SaveFile).
///
/// BUG FIX #6: Ganti GameObject.FindWithTag("Player") dengan cache via
/// GameSave.RegisterPlayer() yang dipanggil oleh PlayerInventory saat Awake.
/// Ini menghindari O(n) scene search tanpa membuat circular dependency antara
/// Core ↔ InventorySystem.
///
/// BUG FIX ASMDEF: Gunakan Action delegate untuk sinkronisasi inventory.
/// Core tidak boleh referensikan InventorySystem langsung (circular dependency).
/// Inventory mendaftarkan callback-nya via RegisterPersistCallback() dari Awake().
///
/// Setup: Panggil GameSave.RegisterPlayer(gameObject) dari PlayerInventory.Awake().
/// </summary>
public static class GameSave
{
    // BUG FIX #6 — Cache referensi player yang di-register dari luar (InventorySystem → Core)
    private static GameObject _playerRef;

    // BUG FIX ASMDEF — Callback untuk flush inventory ke SaveFile.Data sebelum Write().
    // Inventory (InventorySystem assembly) mendaftarkan dirinya ke sini dari Awake(),
    // sehingga Core tidak perlu tahu tipe PlayerInventory / PlayerDiskInventory sama sekali.
    private static Action _persistCallbacks;

    // Getter camera pitch — didaftarkan oleh PlayerMovement agar Core tidak import Narrative.
    // PlayerMovement.Awake() → GameSave.RegisterCameraPitchGetter(() => _cameraPitch)
    private static Func<float> _cameraPitchGetter;

    public static void RegisterCameraPitchGetter(Func<float> getter)
    {
        _cameraPitchGetter = getter;
    }

    /// Dipanggil oleh PlayerInventory.Awake() untuk mendaftarkan dirinya.
    /// Dengan ini, GameSave tidak perlu FindWithTag sama sekali.
    public static void RegisterPlayer(GameObject player)
    {
        _playerRef = player;
    }

    /// Dipanggil oleh setiap inventory (PlayerInventory, PlayerDiskInventory, dll.)
    /// dari Awake() agar GameSave.Save() bisa flush semua inventory tanpa circular dep.
    /// Gunakan -= lalu += untuk cegah duplikasi registrasi.
    public static void RegisterPersistCallback(Action callback)
    {
        _persistCallbacks -= callback;
        _persistCallbacks += callback;
    }

    public static void UnregisterPersistCallback(Action callback)
    {
        _persistCallbacks -= callback;
    }

    public static void Save()
    {
        // BUG FIX #6 — Gunakan cache, fallback ke FindWithTag hanya jika belum di-register
        GameObject player = _playerRef;

        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[GameSave] Player tidak ditemukan! Panggil GameSave.RegisterPlayer() dari PlayerInventory.Awake().");
                return;
            }
            Debug.LogWarning("[GameSave] _playerRef null — fallback ke FindWithTag. Pastikan PlayerInventory memanggil GameSave.RegisterPlayer().");
        }

        Vector3 pos = player.transform.position;

        // Cegah death loop: jangan save posisi saat player sedang di udara.
        // QueryTriggerInteraction.Ignore agar trigger collider tidak dihitung sebagai tanah.
        bool groundFound = Physics.Raycast(
            pos + Vector3.up * 0.5f,
            Vector3.down,
            5f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        if (!groundFound)
        {
            Debug.LogWarning("[GameSave] Save diabaikan — player tidak menyentuh tanah (cegah death loop).");
            return;
        }

        var d = SaveFile.Data;
        d.hasSave      = true;
        d.sceneName    = SceneManager.GetActiveScene().name;
        d.playerX      = pos.x;
        d.playerY      = pos.y;
        d.playerZ      = pos.z;
        d.playerYaw    = player.transform.eulerAngles.y;
        d.playerPitch  = _cameraPitchGetter != null ? _cameraPitchGetter() : 0f;
        d.currentNoise = GameState.SavedNoise;

        // BUG FIX ASMDEF — Flush semua inventory ke SaveFile.Data via delegate.
        // Menggantikan panggilan langsung PlayerInventory.Instance?.PersistKeys()
        // yang tidak bisa dikompilasi karena Core tidak referensikan InventorySystem.
        _persistCallbacks?.Invoke();

        // Gunakan ForceWrite() di checkpoint (momen kritis)
        SaveFile.ForceWrite();
        Debug.Log($"[GameSave] Saved — Scene: {d.sceneName} | Pos: {pos} | Noise: {d.currentNoise:F1}");
    }

    public static void SaveKeys(string joinedKeyNames)
    {
        SaveFile.Data.inventoryKeys = joinedKeyNames;
        SaveFile.Write();
    }

    public static void Load()
    {
        if (!HasSave()) { Debug.LogWarning("[GameSave] Tidak ada save data."); return; }
        string sceneName = SaveFile.Data.sceneName;
        if (string.IsNullOrEmpty(sceneName)) { Debug.LogWarning("[GameSave] Scene name kosong."); return; }

        // OPSI C — Preload: baca ulang dari disk sekarang (main menu frame),
        // bukan nanti saat OnSceneLoaded. Data sudah siap di memory saat
        // sistem-sistem Start() berjalan di scene baru.
        SaveFile.Read();
        SaveFileAutoFlush.MarkPreloaded();

        Debug.Log($"[GameSave] Loading scene: {sceneName}");
        ScreenFader.FadeOutThenLoad(sceneName);
    }

    public static void ResetCheckpoint()
    {
        var d = SaveFile.Data;
        d.hasSave   = false;
        d.sceneName = "";
        d.playerX   = 0f;
        d.playerY   = 0f;
        d.playerZ   = 0f;
        d.playerYaw   = 0f;
        d.playerPitch = 0f;
        SaveFile.ForceWrite();
        Debug.Log("[GameSave] Checkpoint direset — progres quest dan inventory tetap tersimpan.");
    }

    public static Vector3 GetSavedPosition()
    {
        var d = SaveFile.Data;
        return new Vector3(d.playerX, d.playerY, d.playerZ);
    }

    public static float GetSavedYaw()   => SaveFile.Data.playerYaw;
    public static float GetSavedPitch() => SaveFile.Data.playerPitch;

    public static List<string> GetSavedKeyNames()
    {
        var result = new List<string>();
        string raw = SaveFile.Data.inventoryKeys ?? "";
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var name in raw.Split('|'))
            if (!string.IsNullOrEmpty(name)) result.Add(name);
        return result;
    }

    public static bool HasSave() => SaveFile.Data.hasSave;

    public static void DeleteSave()
    {
        SaveFile.Delete();
        Debug.Log("[GameSave] Save data dihapus.");
    }
}