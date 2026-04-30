using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// NarratorDialogueTrigger — pasang pada GameObject dengan Collider (Is Trigger = true).
/// Saat player masuk trigger, narrator text tampil tanpa mengunci player input.
/// Tidak lagi melalui DialogueManager — langsung drive NarratorUI.
/// </summary>
public class NarratorDialogueTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData narratorData;

    [Header("References")]
    [Tooltip("Drag NarratorUI dari scene ke sini. Jika kosong, auto-find.")]
    [SerializeField] private NarratorUI narratorUI;

    [Header("Settings")]
    [SerializeField] private string playerTag   = "Player";
    [Tooltip("Hanya trigger sekali. Matikan untuk bisa trigger berkali-kali.")]
    [SerializeField] private bool   triggerOnce = true;

    [Header("Events")]
    public UnityEvent onPlayerEntered;
    public UnityEvent onNarratorStarted;
    public UnityEvent onNarratorEnded;
    public UnityEvent onPlayerExited;

    private bool _hasTriggered;

    private void Awake()
    {
        if (narratorUI == null)
            narratorUI = FindFirstObjectByType<NarratorUI>();

        if (narratorUI == null)
            Debug.LogError("[NarratorTrigger] NarratorUI tidak ditemukan di scene!", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        onPlayerEntered.Invoke();

        if (triggerOnce && _hasTriggered) return;
        if (narratorUI == null) return;
        if (narratorUI.IsPlaying) return;
        if (narratorData == null)
        {
            Debug.LogWarning("[NarratorTrigger] narratorData belum di-assign!", this);
            return;
        }

        _hasTriggered = true;
        onNarratorStarted.Invoke();

        narratorUI.OnNarratorCompleted += HandleNarratorEnd;
        narratorUI.Play(narratorData);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        onPlayerExited.Invoke();
    }

    private void HandleNarratorEnd()
    {
        onNarratorEnded.Invoke();
        if (narratorUI != null)
            narratorUI.OnNarratorCompleted -= HandleNarratorEnd;
    }

    public void ResetTrigger() => _hasTriggered = false;
}