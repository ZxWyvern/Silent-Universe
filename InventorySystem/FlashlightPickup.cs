using UnityEngine;
using UnityEngine.Events;

public class FlashlightPickup : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private FlashlightItem flashlightItem;

    [Header("Interact Settings")]
    [SerializeField] private string promptText       = "Tahan [E] untuk ambil senter";
    [SerializeField] private string promptAlreadyHas = "[Sudah membawa senter]";

    [Header("Settings")]
    [SerializeField] private bool hideOnPickup    = true;
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Events")]
    public UnityEvent          onPickedUp;
    public UnityEvent<string>  onPickedUpName;

    private float                                _savedBattery = -1f;
    private bool                                 _hasSavedState;
    private FlashlightController.FlashlightState _savedState;
    private bool                                 _pickedUp;
    private string                               _saveKey;

    public string PromptText  => PlayerEquipment.Instance != null && PlayerEquipment.Instance.HasFlashlight
                                 ? promptAlreadyHas : promptText;
    public bool   CanInteract => !_pickedUp &&
                                 (PlayerEquipment.Instance == null || !PlayerEquipment.Instance.HasFlashlight);

    private void Awake()
    {
        _saveKey = "FLP_" + SceneItemID.Of(gameObject);

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
        if (equip == null) return;

        _pickedUp = true;
        equip.EquipFlashlight(flashlightItem);
        WorldFlags.Set(_saveKey, true);

        var fc = FlashlightController.Instance;
        if (fc != null)
        {
            if (_hasSavedState)
                fc.RestoreState(_savedState);
            else if (_savedBattery >= 0f)
                fc.SetBattery(_savedBattery);
        }

        onPickedUp.Invoke();
        onPickedUpName.Invoke(flashlightItem != null ? flashlightItem.itemName : "Senter");

        if (destroyOnPickup) Destroy(gameObject);
        else if (hideOnPickup) gameObject.SetActive(false);
    }

    /// Dipanggil ItemDropper saat drop — simpan state baterai.
    /// TIDAK hapus WorldFlags — pickup asli di scene tetap hidden.
    public void SaveState(FlashlightController.FlashlightState state)
    {
        _savedState    = state;
        _hasSavedState = true;
        _savedBattery  = state.batteryRemaining;
    }

    public void SaveBattery(float remaining)
    {
        _savedBattery  = remaining;
        _hasSavedState = false;
    }

    /// Dipakai ItemDropper saat spawn prefab drop di dunia.
    /// TIDAK hapus WorldFlags — pickup asli di scene tetap hidden.
    public void PrepareAsDropped()
    {
        _pickedUp      = false;
        _hasSavedState = false;
        _savedBattery  = -1f;
        gameObject.SetActive(true);
    }

    /// Reset penuh — hapus WorldFlags, hanya untuk dev/cheat/editor reset.
    public void ResetPickup()
    {
        _pickedUp      = false;
        _hasSavedState = false;
        _savedBattery  = -1f;
        WorldFlags.Remove(_saveKey);
        gameObject.SetActive(true);
    }

    public void SetDestroyOnPickup(bool v) => destroyOnPickup = v;
}