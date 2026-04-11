using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// AxePickup — pasang pada GameObject kampak di scene.
/// Player hold E → kampak di-equip ke PlayerEquipment.
///
/// Save key sekarang pakai SceneItemID (GUID stabil) alih-alih gameObject.name.
/// Tambahkan komponen SceneItemID ke setiap AxePickup GameObject di scene.
/// </summary>
public class AxePickup : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private AxeItem axeItem;

    [Header("Interact Settings")]
    [SerializeField] private string promptText       = "Tahan [E] untuk ambil kampak";
    [SerializeField] private string promptAlreadyHas = "[Sudah membawa kampak]";

    [Header("Settings")]
    [SerializeField] private bool hideOnPickup    = true;
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Events")]
    public UnityEvent         onPickedUp;
    public UnityEvent<string> onPickedUpName;

    private bool   _pickedUp;
    private string _saveKey;

    public string PromptText  => PlayerEquipment.Instance != null && PlayerEquipment.Instance.HasAxe
                                 ? promptAlreadyHas : promptText;

    public bool   CanInteract => !_pickedUp &&
                                 (PlayerEquipment.Instance == null || !PlayerEquipment.Instance.HasAxe);

    private void Awake()
    {
        _saveKey = "AP_" + SceneItemID.Of(gameObject);

        if (WorldFlags.Get(_saveKey))
        {
            _pickedUp = true;
            gameObject.SetActive(false);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;

        var equip = PlayerEquipment.Instance;
        if (equip == null)
        {
            Debug.LogWarning("[AxePickup] PlayerEquipment tidak ditemukan!");
            return;
        }

        _pickedUp = true;
        equip.EquipAxe(axeItem);
        WorldFlags.Set(_saveKey, true);

        onPickedUp.Invoke();
        onPickedUpName.Invoke(axeItem != null ? axeItem.itemName : "Kampak");
        Debug.Log($"[AxePickup] Mengambil kampak: {axeItem?.itemName} (id: {_saveKey})");

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
}