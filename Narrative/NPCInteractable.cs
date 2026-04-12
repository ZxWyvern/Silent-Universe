using UnityEngine;
using UnityEngine.Events;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData dialogueData;

    [Header("Interact Settings")]
    [SerializeField] private string promptText  = "Tahan [E] untuk bicara";
    [SerializeField] private bool   canInteract = true;

    [Header("Choice NodeID Convention")]
    [Tooltip("nextNodeID yang dianggap 'terima'. Contoh: \"accept\"")]
    [SerializeField] private string acceptNodeID = "accept";
    [Tooltip("nextNodeID yang dianggap 'tolak'. Contoh: \"reject\"")]
    [SerializeField] private string rejectNodeID = "reject";

    [Header("Events")]
    public UnityEvent          onInteractBegin;
    public UnityEvent          onDialogueStarted;
    public UnityEvent<string>  onNodeShown;
    public UnityEvent          onDialogueEnded;

    /// Hubungkan ke QuestTrigger.TryStartQuest() di Inspector.
    public UnityEvent onAcceptChosen;

    /// Hubungkan ke apapun saat player tolak (boleh kosong).
    public UnityEvent onRejectChosen;

    public string PromptText  => promptText;
    public bool   CanInteract => canInteract && !DialogueManager.Instance.IsActive;

    private bool _isMyDialogue;

    private void Start()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;
        dm.onDialogueStart.AddListener(OnDMStart);
        dm.onNodeShow.AddListener(OnDMNodeShow);
        dm.onChoiceSelected.AddListener(OnDMChoiceSelected);
        dm.onDialogueEnd.AddListener(OnDMEnd);
    }

    private void OnDestroy()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;
        dm.onDialogueStart.RemoveListener(OnDMStart);
        dm.onNodeShow.RemoveListener(OnDMNodeShow);
        dm.onChoiceSelected.RemoveListener(OnDMChoiceSelected);
        dm.onDialogueEnd.RemoveListener(OnDMEnd);
    }

    public void OnInteract(GameObject interactor)
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[NPCInteractable] DialogueManager tidak ditemukan di scene!");
            return;
        }

        onInteractBegin.Invoke();
        DialogueManager.Instance.StartDialogue(dialogueData);
    }

    private void OnDMChoiceSelected(int index, string nextNodeID)
    {
        // Guard: hanya proses saat ini dialogue NPC ini yang aktif
        if (!_isMyDialogue) return;

        Debug.Log($"[NPCInteractable] Choice dipilih — nextNodeID: '{nextNodeID}'");

        if (!string.IsNullOrEmpty(acceptNodeID) && nextNodeID == acceptNodeID)
        {
            Debug.Log("[NPCInteractable] → onAcceptChosen");
            onAcceptChosen.Invoke();
        }
        else if (!string.IsNullOrEmpty(rejectNodeID) && nextNodeID == rejectNodeID)
        {
            Debug.Log("[NPCInteractable] → onRejectChosen");
            onRejectChosen.Invoke();
        }
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
    }
}