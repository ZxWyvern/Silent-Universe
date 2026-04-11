using UnityEngine;

[CreateAssetMenu(fileName = "DampenerState", menuName = "Events/Dampener State")]
public class DampenerState : ScriptableObject
{
    [Header("Noise Modifiers saat Dampener ON")]
    [Tooltip("Multiplier decay noise (1.5 = 50% lebih cepat turun)")]
    public float decayMultiplier = 1.5f;
    [Tooltip("Noise penalty saat pertama kali ON")]
    public float noisePenalty    = 50f;
    [Tooltip("Berapa detik dampener aktif (0 = tidak ada batas)")]
    public float activeDuration  = 30f;

    // Baca dari JSON save via SaveFile
    public bool  IsOn           => SaveFile.Data.dampenerOn;
    public float PendingPenalty => SaveFile.Data.dampenerPendingPenalty;

    public bool IsExpired()
    {
        if (!IsOn) return false;
        if (activeDuration <= 0f) return false;

        // FIX — Sebelumnya menyimpan Time.time (detik sejak game start).
        // Time.time reset ke 0 setiap kali game di-launch ulang, sehingga jika
        // player quit saat dampener aktif lalu launch ulang, perbandingan
        // (Time.time - dampenerTurnOnTime) bisa negatif atau < activeDuration
        // selamanya → dampener tidak pernah expired setelah load.
        //
        // Fix: simpan dan bandingkan Unix timestamp (detik sejak epoch) via
        // DateTimeOffset.UtcNow.ToUnixTimeSeconds(). Nilai ini konsisten lintas
        // sesi, scene reload, dan OS sleep/wake.
        long now     = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long turnOn  = SaveFile.Data.dampenerTurnOnUnix;
        return (now - turnOn) >= (long)activeDuration;
    }

    public void TurnOn()
    {
        if (IsOn) return;
        var d = SaveFile.Data;
        d.dampenerOn             = true;
        // FIX — Simpan Unix timestamp, bukan Time.time.
        d.dampenerTurnOnUnix     = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        d.dampenerPendingPenalty = noisePenalty;
        SaveFile.Write();
        Debug.Log($"[Dampener] ON — penalty {noisePenalty} pending, durasi {activeDuration}s");
    }

    public void TurnOff()
    {
        var d = SaveFile.Data;
        d.dampenerOn             = false;
        d.dampenerTurnOnUnix     = 0L;
        d.dampenerPendingPenalty = 0f;
        SaveFile.Write();
        Debug.Log("[Dampener] OFF");
    }

    public void ConsumePenalty()
    {
        SaveFile.Data.dampenerPendingPenalty = 0f;
        SaveFile.Write();
    }

    public void ResetState()
    {
        var d = SaveFile.Data;
        d.dampenerOn             = false;
        d.dampenerTurnOnUnix     = 0L;
        d.dampenerPendingPenalty = 0f;
        SaveFile.Write();
    }
}
