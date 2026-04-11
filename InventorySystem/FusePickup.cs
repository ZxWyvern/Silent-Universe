using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// FusePickup — pasang pada GameObject fuse di scene.
/// Player hold E → fuse masuk PlayerFuseInventory.
///
/// Save key sekarang pakai SceneItemID (GUID stabil) alih-alih gameObject.name.
/// Tambahkan komponen SceneItemID ke setiap FusePickup GameObject di scene.
/// </summary>
public class FusePickup : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private FuseItem fuseItem;

    [Header("Fuse Box (opsional)")]
    [Tooltip("Assign GameObject FuseBox yang menerima fuse ini. " +
             "Saat fuse sudah terpasang, prompt berubah ke Prompt Installed.")]
    [SerializeField] private UnityEngine.Object targetFuseBoxObj;

    [Header("Interact Settings")]
    [SerializeField] private string promptText      = "Tahan [E] untuk ambil fuse";
    [SerializeField] private string promptInstalled = "Fuse Sudah Dipasang";

    [Header("Settings")]
    [SerializeField] private bool hideOnPickup    = true;
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Events")]
    public UnityEvent         onPickedUp;
    public UnityEvent<string> onPickedUpName;

    private bool   _pickedUp;
    private string _saveKey;

    private IFuseReceiver TargetFuseBox   => targetFuseBoxObj as IFuseReceiver;
    private bool          FuseIsInstalled => TargetFuseBox != null && TargetFuseBox.FuseInstalled;

    public string PromptText  => FuseIsInstalled ? promptInstalled : promptText;
    public bool   CanInteract => !_pickedUp && !FuseIsInstalled;

    private void Awake()
    {
        _saveKey = "FP_" + SceneItemID.Of(gameObject);

        if (WorldFlags.Get(_saveKey))
        {
            _pickedUp = true;
            gameObject.SetActive(false);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;

        var inv = PlayerFuseInventory.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[FusePickup] PlayerFuseInventory tidak ditemukan!");
            return;
        }

        _pickedUp = true;
        inv.AddFuse(fuseItem);
        onPickedUp.Invoke();
        onPickedUpName.Invoke(fuseItem != null ? fuseItem.itemName : "Fuse");

        WorldFlags.Set(_saveKey, true);
        Debug.Log($"[FusePickup] Mengambil fuse: {fuseItem?.itemName} (id: {_saveKey})");

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

    public void SetFuseItem(FuseItem fuse)     => fuseItem = fuse;
    public void SetDestroyOnPickup(bool value) => destroyOnPickup = value;
}