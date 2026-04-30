using UnityEngine;

/// <summary>
/// GlobalDoorSoundFeedback - Pasang di Player atau GameManager.
/// Hanya butuh SATU AudioSource di object ini. Pintu-pintu di scene TIDAK butuh AudioSource sama sekali.
/// </summary>
public class GlobalDoorSoundFeedback : MonoBehaviour
{
    [Header("Audio Source (Global)")]
    [SerializeField] private AudioSource audioSource;

    [Header("Door Sounds")]
    [SerializeField] private AudioClip soundOpen;
    [SerializeField] private AudioClip soundClose;
    [SerializeField] private AudioClip soundLocked;
    [SerializeField] private AudioClip soundUnlocked;
    [SerializeField] private AudioClip soundWrongKey;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float volume = 0.8f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
            
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        
        // Daftarkan ke AudioManager global (menggunakan kategori Item)
        AudioServices.Manager?.RegisterSource(AudioCategory.Item, audioSource);
    }

    private void Start()
    {
        // Cari semua pintu di scene sekali saja saat Start
        var allDoors = FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);

        foreach (var door in allDoors)
        {
            // Subscribe event ke pintu, tapi play suaranya terpusat di sini
            door.onDoorOpened.AddListener(() => PlaySound(soundOpen));
            door.onDoorClosed.AddListener(() => PlaySound(soundClose));
            door.onInteractLocked.AddListener(() => PlaySound(soundLocked));
            door.onDoorUnlocked.AddListener(() => PlaySound(soundUnlocked));
            door.onWrongKey.AddListener(() => PlaySound(soundWrongKey));
        }

        Debug.Log($"[GlobalDoorSound] Menangani suara untuk {allDoors.Length} pintu secara terpusat.");
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
}