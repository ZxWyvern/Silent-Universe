using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>
/// ItemDropper — pasang pada Player. Tekan Q → drop 1 item terbaru (LIFO).
///
/// Fase 4 — Migrasi VContainer:
///   - Hapus semua .Instance calls untuk player components
///   - [Inject] semua inventory yang dibutuhkan
///   - Subscribe ke events di Start() via injected fields
///   - SyncPickupOrderFromCurrentState() pakai injected fields
///   - Fallback shim ke .Instance hanya sebagai safety net editor
/// </summary>
public class ItemDropper : MonoBehaviour
{
    [Header("Drop Prefabs")]
    [SerializeField] private GameObject axeDropPrefab;
    [SerializeField] private GameObject keyDropPrefab;
    [SerializeField] private GameObject flashlightDropPrefab;
    [SerializeField] private GameObject diskDropPrefab;
    [SerializeField] private GameObject fuseDropPrefab;

    [Header("Throw Settings")]
    [SerializeField] private float spawnDistance = 0.8f;
    [SerializeField] private float throwForce    = 4f;
    [SerializeField] private float throwUpForce  = 2f;
    [SerializeField] private float torqueForce   = 3f;

    [Header("Events")]
    public UnityEvent<string> onItemDropped;
    public UnityEvent         onNothingToDrop;

    // Fase 4 — Inject dari SceneLifetimeScope
    [Inject] private PlayerEquipment        _equipment;
    [Inject] private PlayerInventory        _inventory;
    [Inject] private PlayerDiskInventory    _diskInventory;
    [Inject] private PlayerBatteryInventory _batteryInventory;
    [Inject] private PlayerFuseInventory    _fuseInventory;
    [Inject] private FlashlightController   _flashlight;

    private readonly Stack<string> _pickupOrder = new();

    private const char ENTRY_SEP = ';';
    private const char FIELD_SEP = '|';

    // ──────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────

    private void Start()
    {
        // Fase 4: Subscribe ke events via injected fields.
        // Fallback ke .Instance sebagai safety net editor tanpa container.
        var equip = _equipment ?? PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onAxeEquipped.AddListener(OnAxeEquipped);
            equip.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
        }

        var inv = _inventory ?? PlayerInventory.Instance;
        if (inv != null)
            inv.onKeyAdded.AddListener(OnKeyAdded);

        var diskInv = _diskInventory ?? PlayerDiskInventory.Instance;
        if (diskInv != null)
            diskInv.onDiskAdded.AddListener(OnDiskAdded);

        var fuseInv = _fuseInventory ?? PlayerFuseInventory.Instance;
        if (fuseInv != null)
            fuseInv.onFuseAdded.AddListener(OnFuseAdded);

        SyncPickupOrderFromCurrentState();
        StartCoroutine(RestoreDroppedItemsNextFrame());
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        string raw = SaveFile.Data.droppedItems ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        string prefix = scene.name + FIELD_SEP;
        var entries = new List<string>(raw.Split(ENTRY_SEP));
        int before = entries.Count;
        entries.RemoveAll(e => e.StartsWith(prefix));

        if (entries.Count != before)
        {
            SaveFile.Data.droppedItems = string.Join(ENTRY_SEP.ToString(), entries);
            SaveFile.Write();
        }
    }

    private void OnAxeEquipped(AxeItem axe)              => _pickupOrder.Push("axe");
    private void OnFlashlightEquipped(FlashlightItem fl) => _pickupOrder.Push("flashlight");
    private void OnKeyAdded(string keyName)              => _pickupOrder.Push($"key:{keyName}");
    private void OnDiskAdded(string diskName)            => _pickupOrder.Push($"disk:{diskName}");
    private void OnFuseAdded(string fuseName)            => _pickupOrder.Push($"fuse:{fuseName}");

    // ── Input ──

    public void OnDrop(InputValue value)
    {
        if (!value.isPressed) return;
        DropLatest();
    }

    // ──────────────────────────────────────────
    // Drop Logic
    // ──────────────────────────────────────────

    private void SyncPickupOrderFromCurrentState()
    {
        var inv = _inventory ?? PlayerInventory.Instance;
        if (inv != null)
            foreach (var key in inv.GetAllKeys())
                if (key != null) _pickupOrder.Push($"key:{key.keyName}");

        var diskInv = _diskInventory ?? PlayerDiskInventory.Instance;
        if (diskInv != null)
            foreach (var disk in diskInv.GetAll())
                if (disk != null) _pickupOrder.Push($"disk:{disk.itemName}");

        var fuseInv = _fuseInventory ?? PlayerFuseInventory.Instance;
        if (fuseInv != null)
            for (int i = 0; i < fuseInv.Count; i++)
                _pickupOrder.Push("fuse");

        var equip = _equipment ?? PlayerEquipment.Instance;
        if (equip != null)
        {
            if (equip.HasAxe)        _pickupOrder.Push("axe");
            if (equip.HasFlashlight) _pickupOrder.Push("flashlight");
        }
    }

    public void DropLatest()
    {
        while (_pickupOrder.Count > 0)
        {
            string top = _pickupOrder.Pop();

            var equip = _equipment ?? PlayerEquipment.Instance;

            if (top == "axe")
            {
                if (equip != null && equip.HasAxe) { DropAxe(equip); return; }
                continue;
            }

            if (top == "flashlight")
            {
                if (equip != null && equip.HasFlashlight) { DropFlashlight(equip); return; }
                continue;
            }

            if (top.StartsWith("fuse:"))
            {
                string fuseName = top.Substring(5);
                var fuseInv     = _fuseInventory ?? PlayerFuseInventory.Instance;
                if (fuseInv != null && fuseInv.HasAnyFuse) { DropFuse(fuseName, fuseInv); return; }
                continue;
            }

            if (top.StartsWith("disk:"))
            {
                string diskName = top.Substring(5);
                var diskInv     = _diskInventory ?? PlayerDiskInventory.Instance;
                if (diskInv != null)
                {
                    DiskItem found = null;
                    foreach (var d in diskInv.GetAll())
                        if (d.itemName == diskName) { found = d; break; }
                    if (found != null) { DropDisk(found, diskInv); return; }
                }
                continue;
            }

            if (top.StartsWith("key:"))
            {
                string keyName = top.Substring(4);
                var inv        = _inventory ?? PlayerInventory.Instance;
                if (inv != null)
                {
                    KeyItem found = null;
                    foreach (var k in inv.GetAllKeys())
                        if (k.keyName == keyName) { found = k; break; }
                    if (found != null) { DropKey(found, inv); return; }
                }
                continue;
            }
        }

        onNothingToDrop.Invoke();
    }

    // ──────────────────────────────────────────
    // Private drop methods
    // ──────────────────────────────────────────

    private void DropAxe(PlayerEquipment equip)
    {
        if (axeDropPrefab == null) { Debug.LogWarning("[ItemDropper] Axe Drop Prefab belum diassign!"); equip.UnequipAxe(); return; }

        string itemName = equip.EquippedAxe != null ? equip.EquippedAxe.itemName : "Kampak";
        equip.UnequipAxe();

        GameObject dropped = SpawnDropped(axeDropPrefab);
        var pickup = dropped.GetComponent<AxePickup>();
        if (pickup != null) pickup.PrepareAsDropped();
        ThrowObject(dropped);

        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem("axe", savedPos);
        if (pickup != null) pickup.onPickedUp.AddListener(() => RemoveDroppedItem("axe", savedPos));

        onItemDropped.Invoke(itemName);
    }

    private void DropFuse(string fuseName, PlayerFuseInventory inv)
    {
        if (fuseDropPrefab == null) { Debug.LogWarning("[ItemDropper] Fuse Drop Prefab belum diassign!"); inv.TakeFirst(); return; }

        FuseItem fuse = null;
        foreach (var f in inv.GetAll())
            if (f != null && f.itemName == fuseName) { fuse = f; break; }
        if (fuse == null) fuse = inv.GetAll().Length > 0 ? inv.GetAll()[0] : null;
        if (fuse == null) return;

        inv.RemoveFuse(fuse);

        GameObject dropped = SpawnDropped(fuseDropPrefab);
        var pickup = dropped.GetComponent<FusePickup>();
        if (pickup != null) { pickup.SetFuseItem(fuse); pickup.PrepareAsDropped(); pickup.SetDestroyOnPickup(false); }
        ThrowObject(dropped);

        string typeKey   = $"fuse:{fuse.itemName}";
        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem(typeKey, savedPos);
        if (pickup != null) pickup.onPickedUp.AddListener(() => RemoveDroppedItem(typeKey, savedPos));

        onItemDropped.Invoke(fuse.itemName);
    }

    private void DropDisk(DiskItem disk, PlayerDiskInventory inv)
    {
        if (diskDropPrefab == null) { Debug.LogWarning("[ItemDropper] Disk Drop Prefab belum diassign!"); inv.RemoveDisk(disk); return; }

        inv.RemoveDisk(disk);

        GameObject dropped = SpawnDropped(diskDropPrefab);
        var pickup = dropped.GetComponent<DiskPickup>();
        if (pickup != null) { pickup.SetDisk(disk); pickup.PrepareAsDropped(); }
        ThrowObject(dropped);

        string typeKey   = $"disk:{(disk != null ? disk.itemName : "")}";
        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem(typeKey, savedPos);
        if (pickup != null) pickup.onPickedUp.AddListener(() => RemoveDroppedItem(typeKey, savedPos));

        onItemDropped.Invoke(disk != null ? disk.itemName : "Disk");
    }

    private void DropFlashlight(PlayerEquipment equip)
    {
        if (flashlightDropPrefab == null) { Debug.LogWarning("[ItemDropper] Flashlight Drop Prefab belum diassign!"); equip.UnequipFlashlight(); return; }

        // Fase 4: Pakai injected _flashlight, tidak ada .Instance
        var controller = _flashlight ?? FlashlightController.Instance;
        if (controller != null && controller.IsOn) controller.TurnOff();

        var state = controller != null
            ? controller.GetState()
            : new FlashlightController.FlashlightState { batteryRemaining = -1f };

        string itemName = equip.EquippedFlashlight != null ? equip.EquippedFlashlight.itemName : "Senter";
        equip.UnequipFlashlight();

        GameObject dropped = SpawnDropped(flashlightDropPrefab);
        var pickup = dropped.GetComponent<FlashlightPickup>();
        if (pickup != null) { pickup.PrepareAsDropped(); pickup.SaveState(state); }
        ThrowObject(dropped);

        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem("flashlight", savedPos);
        if (pickup != null) pickup.onPickedUp.AddListener(() => RemoveDroppedItem("flashlight", savedPos));

        onItemDropped.Invoke(itemName);
    }

    private void DropKey(KeyItem key, PlayerInventory inv)
    {
        if (keyDropPrefab == null) { Debug.LogWarning("[ItemDropper] Key Drop Prefab belum diassign!"); inv.RemoveKey(key); return; }

        inv.RemoveKey(key);

        GameObject dropped = SpawnDropped(keyDropPrefab);
        var pickup = dropped.GetComponent<KeyPickup>();
        if (pickup != null) { pickup.SetKey(key); pickup.PrepareAsDropped(); }
        ThrowObject(dropped);

        string typeKey   = $"key:{(key != null ? key.keyName : "")}";
        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem(typeKey, savedPos);
        if (pickup != null) pickup.onKeyPickedUp.AddListener((_) => RemoveDroppedItem(typeKey, savedPos));

        onItemDropped.Invoke(key != null ? key.keyName : "Kunci");
    }

    // ──────────────────────────────────────────
    // Spawn Helpers
    // ──────────────────────────────────────────

    private GameObject SpawnDropped(GameObject prefab)
    {
        Camera  cam     = Camera.main;
        Vector3 origin  = cam != null ? cam.transform.position  : transform.position + Vector3.up * 0.5f;
        Vector3 forward = cam != null ? cam.transform.forward   : transform.forward;

        float   radius   = 0.15f;
        Vector3 spawnPos = origin + forward * spawnDistance + Vector3.up * 0.1f;

        if (Physics.SphereCast(origin, radius, forward, out RaycastHit hit, spawnDistance, ~0, QueryTriggerInteraction.Ignore))
            spawnPos = hit.point - forward * radius + Vector3.up * 0.05f;

        int safetyIter = 0;
        while (Physics.CheckSphere(spawnPos, radius, ~0, QueryTriggerInteraction.Ignore) && safetyIter < 5)
        {
            spawnPos   += Vector3.up * (radius * 2f);
            safetyIter++;
        }

        GameObject obj = Instantiate(prefab, spawnPos, transform.rotation);
        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null) { rb.collisionDetectionMode = CollisionDetectionMode.Continuous; rb.interpolation = RigidbodyInterpolation.Interpolate; }

        obj.GetComponent<AxePickup>()?.SetDestroyOnPickup(true);
        obj.GetComponent<KeyPickup>()?.SetDestroyOnPickup(true);
        return obj;
    }

    private void ThrowObject(GameObject obj)
    {
        Camera  cam     = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.AddComponent<Rigidbody>();

        Vector3 throwDir = (forward + Vector3.up * (throwUpForce / Mathf.Max(throwForce, 0.1f))).normalized;
        rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
    }

    // ──────────────────────────────────────────
    // Dropped Items Persistence
    // ──────────────────────────────────────────

    private void SaveDroppedItem(string type, Vector3 pos)
    {
        string scene = SceneManager.GetActiveScene().name;
        string entry = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}{1}{2}{1}{3:F3}{1}{4:F3}{1}{5:F3}", scene, FIELD_SEP, type, pos.x, pos.y, pos.z);

        string existing = SaveFile.Data.droppedItems ?? "";
        SaveFile.Data.droppedItems = string.IsNullOrEmpty(existing) ? entry : existing + ENTRY_SEP + entry;
        SaveFile.Write();
    }

    private void RemoveDroppedItem(string type, Vector3 pos)
    {
        string scene  = SceneManager.GetActiveScene().name;
        string target = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}{1}{2}{1}{3:F3}{1}{4:F3}{1}{5:F3}", scene, FIELD_SEP, type, pos.x, pos.y, pos.z);

        string existing = SaveFile.Data.droppedItems ?? "";
        if (string.IsNullOrEmpty(existing)) return;

        var entries = new List<string>(existing.Split(ENTRY_SEP));
        entries.RemoveAll(e => e == target);
        SaveFile.Data.droppedItems = string.Join(ENTRY_SEP.ToString(), entries);
        SaveFile.Write();
    }

    private IEnumerator RestoreDroppedItemsNextFrame()
    {
        yield return null;
        RestoreDroppedItems();
    }

    private void RestoreDroppedItems()
    {
        string raw = SaveFile.Data.droppedItems ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        string currentScene = SceneManager.GetActiveScene().name;
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        foreach (var entry in raw.Split(ENTRY_SEP))
        {
            if (string.IsNullOrEmpty(entry)) continue;
            var fields = entry.Split(FIELD_SEP);
            if (fields.Length < 5) continue;
            if (fields[0] != currentScene) continue;

            string type = fields[1];
            if (!float.TryParse(fields[2], System.Globalization.NumberStyles.Float, culture, out float x)) continue;
            if (!float.TryParse(fields[3], System.Globalization.NumberStyles.Float, culture, out float y)) continue;
            if (!float.TryParse(fields[4], System.Globalization.NumberStyles.Float, culture, out float z)) continue;

            SpawnRestoredItem(type, new Vector3(x, y, z));
        }
    }

    private IEnumerator EnableRigidbodyNextFrame(Rigidbody rb)
    {
        yield return null;
        if (rb == null) yield break;
        rb.isKinematic = false;
        rb.useGravity  = true;
    }

    private void SpawnRestoredItem(string type, Vector3 pos)
    {
        GameObject prefab = null;
        if (type == "axe")             prefab = axeDropPrefab;
        else if (type == "flashlight") prefab = flashlightDropPrefab;
        else if (type.StartsWith("key:"))  prefab = keyDropPrefab;
        else if (type.StartsWith("disk:")) prefab = diskDropPrefab;
        else if (type.StartsWith("fuse:")) prefab = fuseDropPrefab;

        if (prefab == null) { Debug.LogWarning($"[ItemDropper] Prefab tidak ditemukan untuk restore type: {type}"); return; }

        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);
        var rbRestore = obj.GetComponent<Rigidbody>();
        if (rbRestore != null)
        {
            rbRestore.isKinematic = true;
            rbRestore.useGravity  = false;

            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit surfaceHit, 5.5f, ~0, QueryTriggerInteraction.Ignore))
                obj.transform.position = surfaceHit.point + Vector3.up * 0.05f;

            StartCoroutine(EnableRigidbodyNextFrame(rbRestore));
        }

        if (type.StartsWith("key:"))
        {
            var pickup = obj.GetComponent<KeyPickup>();
            if (pickup != null) { pickup.PrepareAsDropped(); pickup.SetDestroyOnPickup(true); pickup.onKeyPickedUp.AddListener((_) => RemoveDroppedItem(type, pos)); }
        }
        else if (type.StartsWith("disk:"))
        {
            var pickup = obj.GetComponent<DiskPickup>();
            if (pickup != null)
            {
                string diskName = type.Substring(5);
                DiskItem diskAsset = DiskRegistry.Find(diskName);
                if (diskAsset != null) pickup.SetDisk(diskAsset);
                else Debug.LogWarning($"[ItemDropper] Disk '{diskName}' tidak ditemukan di DiskRegistry.");
                pickup.PrepareAsDropped();
                pickup.onPickedUp.AddListener(() => RemoveDroppedItem(type, pos));
            }
        }
        else if (type == "flashlight")
        {
            var pickup = obj.GetComponent<FlashlightPickup>();
            if (pickup != null) { pickup.PrepareAsDropped(); pickup.onPickedUp.AddListener(() => RemoveDroppedItem(type, pos)); }
        }
        else if (type == "axe")
        {
            var pickup = obj.GetComponent<AxePickup>();
            if (pickup != null) { pickup.PrepareAsDropped(); pickup.SetDestroyOnPickup(true); pickup.onPickedUp.AddListener(() => RemoveDroppedItem(type, pos)); }
        }
        else if (type.StartsWith("fuse:"))
        {
            var pickup = obj.GetComponent<FusePickup>();
            if (pickup != null) { pickup.PrepareAsDropped(); pickup.onPickedUp.AddListener(() => RemoveDroppedItem(type, pos)); }
        }

        Debug.Log($"[ItemDropper] Restored dropped item: {type} at {pos}");
    }
}