using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// KeyPickup — pasang pada GameObject kunci di scene.
/// Hanya bisa diambil jika player belum punya key yang sama.
///
/// Save key sekarang pakai SceneItemID (GUID stabil) alih-alih gameObject.name.
/// Tambahkan komponen SceneItemID ke setiap KeyPickup GameObject di scene.
/// </summary>
public class KeyPickup : MonoBehaviour, IInteractable
{
    [Header("Key")]
    [SerializeField] private KeyItem keyItem;

    [Header("Interact Settings")]
    [SerializeField] private string promptText       = "Tahan [E] untuk ambil";
    [SerializeField] private string promptAlreadyHas = "[Sudah dimiliki]";

    [Header("Settings")]
    [SerializeField] private bool hideOnPickup    = true;
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Events")]
    public UnityEvent<string> onKeyPickedUp;
    public UnityEvent         onAlreadyOwned;

    private bool   _pickedUp;
    private string _saveKey;

    public string PromptText  => PlayerInventory.Instance != null &&
                                 keyItem != null &&
                                 PlayerInventory.Instance.HasKey(keyItem)
                                 ? promptAlreadyHas : promptText;

    public bool   CanInteract => !_pickedUp &&
                                 (PlayerInventory.Instance == null ||
                                  keyItem == null ||
                                  !PlayerInventory.Instance.HasKey(keyItem));

    private void Awake()
    {
        _saveKey = "KP_" + SceneItemID.Of(gameObject);

        if (WorldFlags.Get(_saveKey))
        {
            _pickedUp = true;
            gameObject.SetActive(false);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;

        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[KeyPickup] PlayerInventory tidak ditemukan!");
            return;
        }

        _pickedUp = true;
        inventory.AddKey(keyItem);
        WorldFlags.Set(_saveKey, true);
        onKeyPickedUp.Invoke(keyItem != null ? keyItem.keyName : "");
        Debug.Log($"[KeyPickup] Mengambil key: {keyItem?.keyName} (id: {_saveKey})");

        if (destroyOnPickup)
            Destroy(gameObject);
        else if (hideOnPickup)
            gameObject.SetActive(false);
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

    public void SetKey(KeyItem key)            => keyItem = key;
    public void SetDestroyOnPickup(bool value) => destroyOnPickup = value;
}