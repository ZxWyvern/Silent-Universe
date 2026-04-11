using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// SceneLifetimeScope — composition root untuk komponen per scene.
///
/// Fase 3 (sebelumnya):
///   - Register EnemyAI → inject NoiseTracker + SanitySystem dari ProjectLifetimeScope
///   - Register QuestUI + QuestTrigger[] → inject QuestManager dari ProjectLifetimeScope
///
/// Fase 4 (sekarang):
///   - Register semua komponen Player
///   - Register IPersistable untuk GameSaveService (PlayerInventory, PlayerDiskInventory,
///     PlayerBatteryInventory, PlayerEquipment, PlayerFuseInventory)
///   - FlashlightController tetap pakai GameSave.RegisterPersistCallback (shim sementara)
///
/// Setup di Unity Editor:
///   1. Buat/pilih GameObject "SceneLifetimeScope" di scene gameplay
///   2. Pasang script ini
///   3. Set Parent Scope ke ProjectLifetimeScope di Inspector
///   4. Drag semua komponen dari Player prefab dan scene ke field yang sesuai
///
/// PENTING: SceneLifetimeScope harus punya Parent Scope = ProjectLifetimeScope
/// agar bisa resolve NoiseTracker, SanitySystem, QuestManager dari parent.
/// </summary>
public class SceneLifetimeScope : LifetimeScope
{
    [Header("Fase 3 — Enemy & Quest")]
    [Tooltip("Drag EnemyAI component dari scene — bukan prefab")]
    [SerializeField] private EnemyAI enemyAI;

    [Tooltip("Drag QuestUI component dari scene — biasanya ada di Canvas")]
    [SerializeField] private QuestUI questUI;

    [Tooltip("Drag SEMUA QuestTrigger yang ada di scene ini")]
    [SerializeField] private QuestTrigger[] questTriggers;

    [Header("Fase 4 — Player Components")]
    [Tooltip("Drag PlayerInventory dari Player GameObject")]
    [SerializeField] private PlayerInventory playerInventory;

    [Tooltip("Drag PlayerDiskInventory dari Player GameObject")]
    [SerializeField] private PlayerDiskInventory playerDiskInventory;

    [Tooltip("Drag PlayerBatteryInventory dari Player GameObject")]
    [SerializeField] private PlayerBatteryInventory playerBatteryInventory;

    [Tooltip("Drag PlayerEquipment dari Player GameObject")]
    [SerializeField] private PlayerEquipment playerEquipment;

    [Tooltip("Drag PlayerFuseInventory dari Player GameObject")]
    [SerializeField] private PlayerFuseInventory playerFuseInventory;

    [Tooltip("Drag FlashlightController dari Player GameObject")]
    [SerializeField] private FlashlightController flashlightController;

    [Tooltip("Drag ItemDropper dari Player GameObject")]
    [SerializeField] private ItemDropper itemDropper;

    [Tooltip("Drag ItemInventoryUI dari Canvas")]
    [SerializeField] private ItemInventoryUI itemInventoryUI;

    protected override void Configure(IContainerBuilder builder)
    {
        // ── Fase 3 — EnemyAI ────────────────────────────────────────────────────
        if (enemyAI != null)
            builder.RegisterComponent(enemyAI);
        else
            Debug.LogWarning("[SceneLifetimeScope] enemyAI belum di-assign di Inspector!");

        // ── Fase 3 — QuestUI ────────────────────────────────────────────────────
        if (questUI != null)
            builder.RegisterComponent(questUI);
        else
            Debug.LogWarning("[SceneLifetimeScope] questUI belum di-assign di Inspector!");

        // ── Fase 3 — QuestTrigger[] ─────────────────────────────────────────────
        if (questTriggers != null)
            foreach (var qt in questTriggers)
                if (qt != null) builder.RegisterComponent(qt);
                else Debug.LogWarning("[SceneLifetimeScope] Salah satu entry questTriggers null — skip.");

        // ── Fase 4 — Player Components ──────────────────────────────────────────
        // Register sebagai tipe konkrit — untuk inject antar player components.
        if (playerInventory != null)        builder.RegisterComponent(playerInventory);
        else Debug.LogWarning("[SceneLifetimeScope] playerInventory belum di-assign!");

        if (playerDiskInventory != null)    builder.RegisterComponent(playerDiskInventory);
        else Debug.LogWarning("[SceneLifetimeScope] playerDiskInventory belum di-assign!");

        if (playerBatteryInventory != null) builder.RegisterComponent(playerBatteryInventory);
        else Debug.LogWarning("[SceneLifetimeScope] playerBatteryInventory belum di-assign!");

        if (playerEquipment != null)        builder.RegisterComponent(playerEquipment);
        else Debug.LogWarning("[SceneLifetimeScope] playerEquipment belum di-assign!");

        if (playerFuseInventory != null)    builder.RegisterComponent(playerFuseInventory);
        else Debug.LogWarning("[SceneLifetimeScope] playerFuseInventory belum di-assign!");

        if (flashlightController != null)   builder.RegisterComponent(flashlightController);
        else Debug.LogWarning("[SceneLifetimeScope] flashlightController belum di-assign!");

        if (itemDropper != null)            builder.RegisterComponent(itemDropper);
        else Debug.LogWarning("[SceneLifetimeScope] itemDropper belum di-assign!");

        if (itemInventoryUI != null)        builder.RegisterComponent(itemInventoryUI);
        else Debug.LogWarning("[SceneLifetimeScope] itemInventoryUI belum di-assign!");

        // ── Fase 4 — IPersistable untuk GameSaveService ─────────────────────────
        // Register setiap IPersistable secara eksplisit.
        // VContainer kumpulkan semua registrasi IReadOnlyList<IPersistable>
        // yang di-inject ke GameSaveService.
        //
        // CATATAN: FlashlightController tidak implement IPersistable —
        // ia masih pakai GameSave.RegisterPersistCallback() sebagai shim.
        // Tambahkan FlashlightController ke sini setelah IPersistable di-implement.
        if (playerInventory != null)
            builder.RegisterComponent<IPersistable>(playerInventory);

        if (playerDiskInventory != null)
            builder.RegisterComponent<IPersistable>(playerDiskInventory);

        if (playerBatteryInventory != null)
            builder.RegisterComponent<IPersistable>(playerBatteryInventory);

        if (playerEquipment != null)
            builder.RegisterComponent<IPersistable>(playerEquipment);

        if (playerFuseInventory != null)
            builder.RegisterComponent<IPersistable>(playerFuseInventory);
    }
}