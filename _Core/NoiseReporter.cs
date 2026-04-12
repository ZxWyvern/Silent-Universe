using System;

/// <summary>
/// NoiseReporter — static bridge di Core agar assembly seperti InventorySystem
/// bisa menambah noise tanpa referensi langsung ke GameSystems (NoiseTracker).
///
/// NoiseTracker subscribe ke OnNoiseAdded saat Awake dan unsubscribe saat OnDestroy.
/// InventorySystem cukup panggil NoiseReporter.Add(amount).
/// </summary>
public static class NoiseReporter
{
    public static event Action<float> OnNoiseAdded;

    public static void Add(float amount)
    {
        if (amount > 0f)
            OnNoiseAdded?.Invoke(amount);
    }
}