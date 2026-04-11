using UnityEngine;
using VContainer;

/// <summary>
/// ItemSoundFeedback — pasang pada Player GameObject.
/// Play suara saat ambil item atau buang item.
///
/// Setup:
///   1. Pasang script ini ke Player
///   2. Assign AudioSource (bisa pakai AudioSource yang sudah ada di Player)
///   3. Drag AudioClip ke tiap slot di Inspector
///   4. Script otomatis subscribe ke semua inventory/equipment events
/// </summary>
public class ItemSoundFeedback : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Pickup Sounds")]
    [SerializeField] private AudioClip pickupKey;
    [SerializeField] private AudioClip pickupAxe;
    [SerializeField] private AudioClip pickupFlashlight;
    [SerializeField] private AudioClip pickupDisk;
    [SerializeField] private AudioClip pickupFuse;
    [SerializeField] private AudioClip pickupBattery;
    [Tooltip("Fallback jika clip spesifik tidak diisi")]
    [SerializeField] private AudioClip pickupGeneric;

    [Header("Drop Sounds")]
    [SerializeField] private AudioClip dropItem;
    [SerializeField] private AudioClip dropNothing;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float pickupVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float dropVolume   = 0.7f;

    [Inject] private PlayerInventory        _inventory;
    [Inject] private PlayerDiskInventory    _diskInventory;
    [Inject] private PlayerFuseInventory    _fuseInventory;
    [Inject] private PlayerBatteryInventory _batteryInventory;
    [Inject] private PlayerEquipment        _equipment;
    [Inject] private ItemDropper            _dropper;

    // Cached resolved clips — evaluated once in Awake, not on every event fire
    private AudioClip _cKey;
    private AudioClip _cAxe;
    private AudioClip _cFlashlight;
    private AudioClip _cDisk;
    private AudioClip _cFuse;
    private AudioClip _cBattery;

    // Cached references for unsubscribe
    private PlayerInventory        _inv;
    private PlayerDiskInventory    _disk;
    private PlayerFuseInventory    _fuse;
    private PlayerBatteryInventory _bat;
    private PlayerEquipment        _equip;
    private ItemDropper            _drop;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            audioSource.playOnAwake = false;

        // Resolve fallbacks once — no ?? per event call
        _cKey        = pickupKey        != null ? pickupKey        : pickupGeneric;
        _cAxe        = pickupAxe        != null ? pickupAxe        : pickupGeneric;
        _cFlashlight = pickupFlashlight != null ? pickupFlashlight : pickupGeneric;
        _cDisk       = pickupDisk       != null ? pickupDisk       : pickupGeneric;
        _cFuse       = pickupFuse       != null ? pickupFuse       : pickupGeneric;
        _cBattery    = pickupBattery    != null ? pickupBattery    : pickupGeneric;
    }

    private void Start()
    {
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        // Tunggu 1 frame agar LoadFromSave() di semua inventory selesai dulu
        // sebelum subscribe — mencegah suara pickup muncul saat restore save
        yield return null;

        _inv = _inventory ?? PlayerInventory.Instance;
        if (_inv != null)
            _inv.onKeyAdded.AddListener(OnKeyAdded);

        _disk = _diskInventory ?? PlayerDiskInventory.Instance;
        if (_disk != null)
            _disk.onDiskAdded.AddListener(OnDiskAdded);

        _fuse = _fuseInventory ?? PlayerFuseInventory.Instance;
        if (_fuse != null)
            _fuse.onFuseAdded.AddListener(OnFuseAdded);

        _bat = _batteryInventory ?? PlayerBatteryInventory.Instance;
        if (_bat != null)
            _bat.onBatteryAdded.AddListener(OnBatteryAdded);

        _equip = _equipment ?? PlayerEquipment.Instance;
        if (_equip != null)
        {
            _equip.onAxeEquipped.AddListener(OnAxeEquipped);
            _equip.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
        }

        _drop = _dropper ?? FindFirstObjectByType<ItemDropper>();
        if (_drop != null)
        {
            _drop.onItemDropped.AddListener(OnItemDropped);
            _drop.onNothingToDrop.AddListener(OnNothingToDrop);
        }
    }

    private void OnDestroy()
    {
        if (_inv   != null) _inv.onKeyAdded.RemoveListener(OnKeyAdded);
        if (_disk  != null) _disk.onDiskAdded.RemoveListener(OnDiskAdded);
        if (_fuse  != null) _fuse.onFuseAdded.RemoveListener(OnFuseAdded);
        if (_bat   != null) _bat.onBatteryAdded.RemoveListener(OnBatteryAdded);
        if (_equip != null)
        {
            _equip.onAxeEquipped.RemoveListener(OnAxeEquipped);
            _equip.onFlashlightEquipped.RemoveListener(OnFlashlightEquipped);
        }
        if (_drop != null)
        {
            _drop.onItemDropped.RemoveListener(OnItemDropped);
            _drop.onNothingToDrop.RemoveListener(OnNothingToDrop);
        }
    }

    // ── Handlers ──

    private void OnKeyAdded(string _)                   => Play(_cKey,        pickupVolume);
    private void OnDiskAdded(string _)                  => Play(_cDisk,       pickupVolume);
    private void OnFuseAdded(string _)                  => Play(_cFuse,       pickupVolume);
    private void OnBatteryAdded(string _)               => Play(_cBattery,    pickupVolume);
    private void OnAxeEquipped(AxeItem _)               => Play(_cAxe,        pickupVolume);
    private void OnFlashlightEquipped(FlashlightItem _) => Play(_cFlashlight, pickupVolume);
    private void OnItemDropped(string _)                => Play(dropItem,     dropVolume);
    private void OnNothingToDrop()                      => Play(dropNothing,  dropVolume);

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }
}