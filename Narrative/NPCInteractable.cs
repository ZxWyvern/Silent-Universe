using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData dialogueData;
    [Tooltip("Transform kepala NPC (misal: Head bone). Kamera player akan otomatis menghadap ke sini selama dialogue.")]
    [SerializeField] private Transform npcHeadTransform;

    [Header("Interact Settings")]
    [SerializeField] private string promptText  = "Tahan [E] untuk bicara";
    [SerializeField] private bool   canInteract = true;

    [Header("Choice NodeID Convention")]
    [Tooltip("nextNodeID yang dianggap 'terima'. Contoh: \"accept\"")]
    [SerializeField] private string acceptNodeID = "accept";
    [Tooltip("nextNodeID yang dianggap 'tolak'. Contoh: \"reject\"")]
    [SerializeField] private string rejectNodeID = "reject";

    [Header("Talking Sound")]
    [Tooltip("AudioSource pada GameObject NPC ini. Jika kosong, akan dicari otomatis.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("4 variasi suara berdetuk NPC. Akan diputar random dan berulang selama typewriter berjalan.")]
    [SerializeField] private AudioClip[] chattingSounds = new AudioClip[4];
    [Tooltip("Jeda antar suara (detik). Sesuaikan dengan durasi clip.")]
    [SerializeField] private float soundInterval = 0.12f;
    [Tooltip("Referensi ke DialogueUI di scene. Dipakai untuk deteksi typewriter selesai.")]
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Events")]
    public UnityEvent          onInteractBegin;
    public UnityEvent          onDialogueStarted;
    public UnityEvent<string>  onNodeShown;
    public UnityEvent          onDialogueEnded;

    public UnityEvent onAcceptChosen;
    public UnityEvent onRejectChosen;

    public string PromptText  => promptText;
    public bool   CanInteract => canInteract && !DialogueManager.Instance.IsActive;

    private bool      _isMyDialogue;
    private Coroutine _chatterRoutine;
    private int       _lastSoundIndex = -1;

    // True = loop berjalan, False = loop berhenti tapi clip boleh selesai sendiri
    private bool _isLooping;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<DialogueUI>();
    }

    private void Start()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;
        dm.onDialogueStart.AddListener(OnDMStart);
        dm.onNodeShow.AddListener(OnDMNodeShow);
        dm.onChoiceSelected.AddListener(OnDMChoiceSelected);
        dm.onDialogueEnd.AddListener(OnDMEnd);

        if (dialogueUI != null)
        {
            dialogueUI.onTypewriterStarted.AddListener(OnTypewriterStarted);
            dialogueUI.onTypewriterFinished.AddListener(OnTypewriterDone);
            dialogueUI.onTypewriterSkipped.AddListener(OnTypewriterDone);
        }
    }

    private void OnDestroy()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;
        dm.onDialogueStart.RemoveListener(OnDMStart);
        dm.onNodeShow.RemoveListener(OnDMNodeShow);
        dm.onChoiceSelected.RemoveListener(OnDMChoiceSelected);
        dm.onDialogueEnd.RemoveListener(OnDMEnd);

        if (dialogueUI != null)
        {
            dialogueUI.onTypewriterStarted.RemoveListener(OnTypewriterStarted);
            dialogueUI.onTypewriterFinished.RemoveListener(OnTypewriterDone);
            dialogueUI.onTypewriterSkipped.RemoveListener(OnTypewriterDone);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[NPCInteractable] DialogueManager tidak ditemukan di scene!");
            return;
        }

        onInteractBegin.Invoke();
        DialogueManager.Instance.StartDialogue(dialogueData, npcHeadTransform);
    }

    private void OnDMChoiceSelected(int index, string nextNodeID)
    {
        if (!_isMyDialogue) return;

        if (!string.IsNullOrEmpty(acceptNodeID) && nextNodeID == acceptNodeID)
            onAcceptChosen.Invoke();
        else if (!string.IsNullOrEmpty(rejectNodeID) && nextNodeID == rejectNodeID)
            onRejectChosen.Invoke();
    }

    private void OnDMStart(string npcName, string firstText)
    {
        if (DialogueManager.Instance.IsNarrator) return;
        if (npcName != dialogueData.npcName) return;

        _isMyDialogue = true;
        onDialogueStarted.Invoke();
    }

    private void OnDMNodeShow(string npcName, string text)
    {
        if (!_isMyDialogue) return;
        onNodeShown.Invoke(text);
    }

    private void OnDMEnd()
    {
        if (!_isMyDialogue) return;
        _isMyDialogue = false;
        onDialogueEnded.Invoke();
        // Dialogue tutup — hard stop, tidak perlu tunggu clip selesai
        ForceStopChatter();
    }

    // ── Typewriter Callbacks ──

    private void OnTypewriterStarted(string text)
    {
        if (!_isMyDialogue) return;
        StartChatter();
    }

    private void OnTypewriterDone()
    {
        if (!_isMyDialogue) return;
        // Hentikan loop tapi JANGAN stop AudioSource —
        // PlayOneShot yang sedang berjalan akan selesai secara natural.
        StopChatterLoop();
    }

    // ── Chatter Control ──

    private void StartChatter()
    {
        // Jika node berganti, stop loop lama + hard stop clip lama dulu
        ForceStopChatter();

        if (audioSource == null || chattingSounds == null || chattingSounds.Length == 0) return;

        _isLooping      = true;
        _chatterRoutine = StartCoroutine(ChatterRoutine());
    }

    /// Hentikan loop coroutine saja — clip yang sedang diputar dibiarkan selesai.
    private void StopChatterLoop()
    {
        _isLooping = false;

        if (_chatterRoutine != null)
        {
            StopCoroutine(_chatterRoutine);
            _chatterRoutine = null;
        }
        // audioSource TIDAK di-stop di sini
    }

    /// Hard stop total — dipakai saat dialogue tutup atau node berganti.
    private void ForceStopChatter()
    {
        _isLooping = false;

        if (_chatterRoutine != null)
        {
            StopCoroutine(_chatterRoutine);
            _chatterRoutine = null;
        }

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    private IEnumerator ChatterRoutine()
    {
        while (_isLooping)
        {
            AudioClip clip = PickRandomClip();
            if (clip != null)
                audioSource.PlayOneShot(clip);

            yield return new WaitForSeconds(soundInterval);
        }
    }

    private AudioClip PickRandomClip()
    {
        if (chattingSounds.Length == 1)
            return chattingSounds[0];

        int index;
        int attempts = 0;
        do
        {
            index = Random.Range(0, chattingSounds.Length);
            attempts++;
        }
        while (index == _lastSoundIndex && attempts < 10);

        _lastSoundIndex = index;
        return chattingSounds[index];
    }
}