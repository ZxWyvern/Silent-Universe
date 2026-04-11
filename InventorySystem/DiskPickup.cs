using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// DiskPickup — pasang pada GameObject disk di scene.
/// Player hold E → disk masuk PlayerDiskInventory.
///
/// Save key sekarang pakai SceneItemID (GUID stabil) alih-alih gameObject.name.
/// Tambahkan komponen SceneItemID ke setiap DiskPickup GameObject di scene.
/// </summary>
public class DiskPickup : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private DiskItem diskItem;

    [Header("Interact Settings")]
    [SerializeField] private string promptText       = "Tahan [E] untuk ambil disk";
    [SerializeField] private string promptAlreadyHas = "[Disk sudah dimiliki]";

    [Header("Settings")]
    [SerializeField] private bool hideOnPickup    = true;
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Events")]
    public UnityEvent         onPickedUp;
    public UnityEvent<string> onPickedUpName;

    private bool   _pickedUp;
    private string _saveKey;

    public string PromptText  => PlayerDiskInventory.Instance != null &&
                                 PlayerDiskInventory.Instance.HasDisk(diskItem)
                                 ? promptAlreadyHas : promptText;

    public bool   CanInteract => !_pickedUp &&
                                 (PlayerDiskInventory.Instance == null ||
                                  !PlayerDiskInventory.Instance.HasDisk(diskItem));

    private void Awake()
    {
        _saveKey = "DP_" + SceneItemID.Of(gameObject);

        if (WorldFlags.Get(_saveKey))
        {
            _pickedUp = true;
            gameObject.SetActive(false);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;

        var inv = PlayerDiskInventory.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[DiskPickup] PlayerDiskInventory tidak ditemukan!");
            return;
        }

        _pickedUp = true;
        inv.AddDisk(diskItem);
        onPickedUp.Invoke();
        onPickedUpName.Invoke(diskItem != null ? diskItem.itemName : "Disk");

        WorldFlags.Set(_saveKey, true);
        Debug.Log($"[DiskPickup] Mengambil disk: {diskItem?.itemName} (id: {_saveKey})");

        if (destroyOnPickup) Destroy(gameObject);
        else if (hideOnPickup) gameObject.SetActive(false);
    }

    /// Dipakai ItemDropper saat spawn prefab drop di dunia.
    /// TIDAK hapus WorldFlags — pickup asli di scene tetap hidden.
    public void PrepareAsDropped()
    {
        _pickedUp = false;
        gameObject.SetActive(true);
    }

    /// Reset penuh — hapus WorldFlags, hanya untuk dev/cheat/editor reset.
    public void ResetPickup()
    {
        _pickedUp = false;
        WorldFlags.Remove(_saveKey);
        gameObject.SetActive(true);
    }

    public void SetDestroyOnPickup(bool value) => destroyOnPickup = value;
    public void SetDisk(DiskItem disk)         => diskItem = disk;
}