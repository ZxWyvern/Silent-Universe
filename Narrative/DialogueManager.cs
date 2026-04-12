using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// DialogueManager — Singleton
/// Pasang pada GameObject kosong di scene (misal "GameManager").
/// Mengatur alur dialogue, parsing node, dan memanggil event ke UI.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    // ── Singleton ──
    public static DialogueManager Instance { get; private set; }

    // ── Events ──
    public UnityEvent<string, string>   onDialogueStart;    // (npcName, npcText)
    public UnityEvent<string, string>   onNodeShow;         // (npcName, npcText)
    public UnityEvent<DialogueChoice[]> onChoicesShow;      // array pilihan
    public UnityEvent                   onDialogueEnd;
    /// Fire saat player pilih choice — SEBELUM pindah node.
    /// (index, nextNodeID) — dipakai NPCInteractable untuk detect accept/reject.
    public UnityEvent<int, string>      onChoiceSelected;

    // ── State ──
    private DialogueData                _data;
    private Dictionary<string, DialogueNode> _nodeMap = new();
    private bool                        _isActive;

    public bool IsActive    => _isActive;
    public bool IsNarrator  => _data != null && _data.isNarrator;

    // ── Lifecycle ──
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ──

    /// Mulai dialogue dengan data tertentu
    public void StartDialogue(DialogueData data)
    {
        if (_isActive) { Debug.LogWarning("[DialogueManager] Dialogue sudah aktif."); return; }
        if (data == null) { Debug.LogError("[DialogueManager] DialogueData null! Assign asset ke NPCInteractable."); return; }
        if (data.nodes == null || data.nodes.Length == 0) { Debug.LogError("[DialogueManager] DialogueData tidak punya nodes!"); return; }

        _data = data;
        _nodeMap.Clear();
        foreach (var node in data.nodes)
            _nodeMap[node.nodeID] = node;

        if (!_nodeMap.ContainsKey("start"))
        {
            Debug.LogError("[DialogueManager] Tidak ada node dengan ID 'start'! " +
                           "Node pertama HARUS punya nodeID = 'start'.");
            return;
        }

        Debug.Log($"[DialogueManager] StartDialogue: {data.npcName}, {_nodeMap.Count} nodes.");
        _isActive = true;

        // unlock cursor agar tombol UI bisa diklik
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // freeze player input
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.SetInputEnabled(false);

        onDialogueStart.Invoke(data.npcName, data.nodes[0].npcText);
        ShowNode("start");
    }

    /// Dipanggil saat player memilih pilihan
    public void SelectChoice(int index)
    {
        if (!_isActive) return;

        // cari node aktif saat ini via coroutine state — kita simpan
        // node aktif di field
        if (_currentNode == null) return;
        if (index < 0 || index >= _currentNode.choices.Length) return;

        string nextID = _currentNode.choices[index].nextNodeID;

        // Fire event SEBELUM pindah node — NPCInteractable baca nextNodeID di sini.
        onChoiceSelected.Invoke(index, nextID ?? string.Empty);

        if (string.IsNullOrEmpty(nextID) || !_nodeMap.ContainsKey(nextID))
            EndDialogue();
        else
            ShowNode(nextID);
    }

    public void EndDialogue()
    {
        if (!_isActive) return;
        _isActive    = false;
        _currentNode = null;

        // BUG FIX #2 — Jangan paksa Locked jika player sedang dalam mode CCTV.
        // CCTV membutuhkan Confined agar tombol kamera bisa diklik.
        // GameState ada di Core assembly — tidak butuh referensi ke GameSystems.
        if (GameState.IsCCTVActive)
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible   = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        // unfreeze player input
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.SetInputEnabled(true);

        onDialogueEnd.Invoke();
    }

    // ── Private ──
    private DialogueNode _currentNode;

    private void ShowNode(string nodeID)
    {
        if (!_nodeMap.TryGetValue(nodeID, out DialogueNode node))
        {
            Debug.LogError($"[DialogueManager] Node '{nodeID}' tidak ditemukan!");
            EndDialogue();
            return;
        }

        Debug.Log($"[DialogueManager] ShowNode: {nodeID} | teks: {node.npcText}");
        _currentNode = node;
        onNodeShow.Invoke(_data.npcName, node.npcText);

        if (node.choices == null || node.choices.Length == 0)
        {
            // tidak ada pilihan = otomatis tutup (atau tunggu input lanjut)
            onChoicesShow.Invoke(System.Array.Empty<DialogueChoice>());
        }
        else
        {
            onChoicesShow.Invoke(node.choices);
        }
    }
}