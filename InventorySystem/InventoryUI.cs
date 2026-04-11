using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;

/// <summary>
/// ItemInventoryUI — tampil di kiri atas, menampilkan semua item yang dimiliki player.
///
/// Fase 4 — Migrasi VContainer:
///   - [Inject] semua inventory dan equipment — tidak ada .Instance lagi
///   - Subscribe ke events di Start() via injected fields
///   - SyncFromCurrentState() pakai injected fields
///   - Fallback shim ke .Instance untuk safety net editor
///   - FindFirstObjectByType<FlashlightController>() tetap — FC tidak di-inject karena
///     ini UI scene component, dan FC adalah player component yang sudah di-inject terpisah
///     oleh SceneLifetimeScope. Ganti ke [Inject] setelah confirm SceneLifetimeScope setup.
/// </summary>
public class ItemInventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject itemListPanel;
    [SerializeField] private GameObject slotPrefab;

    [Header("Icons (opsional)")]
    [SerializeField] private Sprite keyIcon;
    [SerializeField] private Sprite axeIcon;
    [SerializeField] private Sprite flashlightOnIcon;
    [SerializeField] private Sprite flashlightOffIcon;
    [SerializeField] private Sprite diskIcon;
    [SerializeField] private Sprite fuseIcon;
    [SerializeField] private Sprite defaultIcon;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration  = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    // Fase 4 — Inject dari SceneLifetimeScope
    [Inject] private PlayerEquipment     _equipment;
    [Inject] private PlayerInventory     _inventory;
    [Inject] private PlayerDiskInventory _diskInventory;
    [Inject] private PlayerFuseInventory _fuseInventory;
    [Inject] private FlashlightController _flashlight;

    private CanvasGroup                             _canvasGroup;
    private Coroutine                               _fadeRoutine;
    private readonly Dictionary<string, GameObject> _slots = new();
    private string _equippedAxeName;
    private string _equippedFlashlightName;

    private void Awake()
    {
        _canvasGroup = itemListPanel != null
            ? itemListPanel.GetComponent<CanvasGroup>() ?? itemListPanel.AddComponent<CanvasGroup>()
            : null;
        HideImmediate();
    }

    private void Start()
    {
        var equip = _equipment ?? PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onAxeEquipped.AddListener(OnAxeEquipped);
            equip.onAxeUnequipped.AddListener(OnAxeUnequipped);
            equip.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
            equip.onFlashlightUnequipped.AddListener(OnFlashlightUnequipped);
        }

        var inv = _inventory ?? PlayerInventory.Instance;
        if (inv != null)
        {
            inv.onKeyAdded.AddListener(OnKeyAdded);
            inv.onKeyRemoved.AddListener(OnKeyRemoved);
        }

        var diskInv = _diskInventory ?? PlayerDiskInventory.Instance;
        if (diskInv != null)
        {
            diskInv.onDiskAdded.AddListener(OnDiskAdded);
            diskInv.onDiskRemoved.AddListener(OnDiskRemoved);
        }

        var fuseInv = _fuseInventory ?? PlayerFuseInventory.Instance;
        if (fuseInv != null)
        {
            fuseInv.onFuseAdded.AddListener(OnFuseAdded);
            fuseInv.onFuseRemoved.AddListener(OnFuseRemoved);
        }

        var fl = _flashlight ?? FindFirstObjectByType<FlashlightController>();
        if (fl != null)
        {
            fl.onFlashlightOn.AddListener(OnFlashlightToggled);
            fl.onFlashlightOff.AddListener(OnFlashlightToggled);
        }

        SyncFromCurrentState();
    }

    private void SyncFromCurrentState()
    {
        var inv = _inventory ?? PlayerInventory.Instance;
        if (inv != null)
            foreach (var key in inv.GetAllKeys())
                if (key != null) OnKeyAdded(key.keyName);

        var diskInv = _diskInventory ?? PlayerDiskInventory.Instance;
        if (diskInv != null)
            foreach (var disk in diskInv.GetAll())
                if (disk != null) OnDiskAdded(disk.itemName);

        var fuseInv = _fuseInventory ?? PlayerFuseInventory.Instance;
        if (fuseInv != null)
            for (int i = 0; i < fuseInv.Count; i++)
                OnFuseAdded("Fuse");

        var equip = _equipment ?? PlayerEquipment.Instance;
        if (equip != null)
        {
            if (equip.HasAxe)        OnAxeEquipped(equip.EquippedAxe);
            if (equip.HasFlashlight) OnFlashlightEquipped(equip.EquippedFlashlight);
        }
    }

    private void OnDestroy()
    {
        var equip = _equipment ?? PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onAxeEquipped.RemoveListener(OnAxeEquipped);
            equip.onAxeUnequipped.RemoveListener(OnAxeUnequipped);
            equip.onFlashlightEquipped.RemoveListener(OnFlashlightEquipped);
            equip.onFlashlightUnequipped.RemoveListener(OnFlashlightUnequipped);
        }

        var inv = _inventory ?? PlayerInventory.Instance;
        if (inv != null)
        {
            inv.onKeyAdded.RemoveListener(OnKeyAdded);
            inv.onKeyRemoved.RemoveListener(OnKeyRemoved);
        }

        var diskInv = _diskInventory ?? PlayerDiskInventory.Instance;
        if (diskInv != null)
        {
            diskInv.onDiskAdded.RemoveListener(OnDiskAdded);
            diskInv.onDiskRemoved.RemoveListener(OnDiskRemoved);
        }

        var fuseInv = _fuseInventory ?? PlayerFuseInventory.Instance;
        if (fuseInv != null)
        {
            fuseInv.onFuseAdded.RemoveListener(OnFuseAdded);
            fuseInv.onFuseRemoved.RemoveListener(OnFuseRemoved);
        }
    }

    // ── Public API ──

    public void ShowItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName) || _slots.ContainsKey(itemName)) return;
        AddSlot(itemName, keyIcon);
        FadeIn();
    }

    public void RemoveItem(string itemName)
    {
        if (_slots.TryGetValue(itemName, out var slot)) { Destroy(slot); _slots.Remove(itemName); }
        if (_slots.Count == 0) StartFadeOut();
    }

    public void RemoveKey(KeyItem key) { if (key != null) RemoveItem(key.keyName); }
    public void HideImmediate()
    {
        if (itemListPanel != null) itemListPanel.SetActive(false);
        if (_canvasGroup  != null) _canvasGroup.alpha = 0f;
    }

    // ── Callbacks ──

    private void OnKeyAdded(string keyName)
    {
        if (string.IsNullOrEmpty(keyName) || _slots.ContainsKey(keyName)) return;
        AddSlot(keyName, keyIcon); FadeIn();
    }

    private void OnKeyRemoved(string keyName) => RemoveItem(keyName);

    private void OnAxeEquipped(AxeItem axe)
    {
        if (axe == null) return;
        _equippedAxeName = axe.itemName;
        if (_slots.ContainsKey(_equippedAxeName)) return;
        AddSlot(_equippedAxeName, axeIcon); FadeIn();
    }

    private void OnAxeUnequipped()
    {
        if (string.IsNullOrEmpty(_equippedAxeName)) return;
        RemoveItem(_equippedAxeName);
        _equippedAxeName = null;
    }

    private void OnFlashlightEquipped(FlashlightItem fl)
    {
        if (fl == null) return;
        _equippedFlashlightName = fl.itemName;
        if (_slots.ContainsKey(_equippedFlashlightName)) return;
        AddSlot(_equippedFlashlightName, flashlightOffIcon); FadeIn();
    }

    private void OnFlashlightUnequipped()
    {
        if (string.IsNullOrEmpty(_equippedFlashlightName)) return;
        RemoveItem(_equippedFlashlightName);
        _equippedFlashlightName = null;
    }

    private void OnFlashlightToggled()
    {
        if (string.IsNullOrEmpty(_equippedFlashlightName)) return;
        if (!_slots.TryGetValue(_equippedFlashlightName, out var slot)) return;

        var fl = _flashlight ?? FindFirstObjectByType<FlashlightController>();
        if (fl == null) return;

        var img = slot.GetComponentInChildren<Image>();
        if (img != null)
        {
            Sprite target = fl.IsOn ? flashlightOnIcon : flashlightOffIcon;
            if (target != null) img.sprite = target;
        }
    }

    private void OnDiskAdded(string diskName)
    {
        if (string.IsNullOrEmpty(diskName) || _slots.ContainsKey(diskName)) return;

        Sprite icon = diskIcon;
        var diskInv = _diskInventory ?? PlayerDiskInventory.Instance;
        if (diskInv != null)
            foreach (var d in diskInv.GetAll())
                if (d.itemName == diskName && d.icon != null) { icon = d.icon; break; }

        AddSlot(diskName, icon); FadeIn();
    }

    private void OnDiskRemoved(string diskName) => RemoveItem(diskName);

    private void OnFuseAdded(string fuseName)
    {
        int index = 0;
        while (_slots.ContainsKey($"fuse_{index}")) index++;
        AddSlot($"fuse_{index}", fuseIcon, fuseName); FadeIn();
    }

    private void OnFuseRemoved(string fuseName)
    {
        int index = 0;
        while (_slots.ContainsKey($"fuse_{index}")) index++;
        index--;
        if (index >= 0) RemoveItem($"fuse_{index}");
    }

    // ── Slot Management ──

    private void AddSlot(string slotKey, Sprite icon, string displayLabel = null)
    {
        if (slotPrefab == null || itemListPanel == null) return;
        GameObject slot = Instantiate(slotPrefab, itemListPanel.transform);
        _slots[slotKey] = slot;

        var img = slot.GetComponentInChildren<Image>();
        if (img != null) { img.sprite = icon != null ? icon : defaultIcon; img.enabled = img.sprite != null; }

        var txt = slot.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = displayLabel ?? slotKey;
    }

    // ── Fade ──

    private void FadeIn()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeInRoutine());
    }

    private void StartFadeOut()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        itemListPanel.SetActive(true);
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutRoutine()
    {
        float start = _canvasGroup != null ? _canvasGroup.alpha : 1f;
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(start, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        itemListPanel.SetActive(false);
    }
}