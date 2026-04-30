using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class NarratorUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject narratorPanel;

    [Header("Text")]
    [SerializeField] private TMP_Text narratorText;

    [Header("Timing")]
    [Tooltip("Jeda antar karakter typewriter (detik).")]
    [SerializeField] private float charDelay       = 0.05f;
    [Tooltip("Berapa lama teks ditahan setelah selesai diketik sebelum lanjut (detik).")]
    [SerializeField] private float displayDuration = 3f;

    [Header("Events")]
    public UnityEvent onPanelOpened;
    public UnityEvent onPanelClosed;

    public event Action OnNarratorCompleted;

    public bool IsPlaying => _activeData != null;

    private Coroutine    _sequenceRoutine;
    private DialogueData _activeData;

    private void Start() => HideImmediate();

    public void Play(DialogueData data)
    {
        if (data == null) { Debug.LogError("[NarratorUI] DialogueData null!", this); return; }
        if (data.nodes == null || data.nodes.Length == 0) { Debug.LogError("[NarratorUI] DialogueData tidak punya nodes!", this); return; }

        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);

        _activeData = data;
        narratorPanel.SetActive(true);
        onPanelOpened.Invoke();

        _sequenceRoutine = StartCoroutine(PlaySequence());
    }

    public void Stop()
    {
        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = null;
        _activeData      = null;
        HideImmediate();
    }

    private IEnumerator PlaySequence()
    {
        for (int i = 0; i < _activeData.nodes.Length; i++)
        {
            narratorText.text = string.Empty;

            // Typewriter
            foreach (char c in _activeData.nodes[i].npcText)
            {
                narratorText.text += c;
                yield return new WaitForSeconds(charDelay);
            }

            // Tahan teks sebelum lanjut ke node berikutnya
            yield return new WaitForSeconds(displayDuration);
        }

        _activeData      = null;
        _sequenceRoutine = null;
        HideImmediate();
        onPanelClosed.Invoke();
        OnNarratorCompleted?.Invoke();
    }

    private void HideImmediate()
    {
        if (narratorPanel != null) narratorPanel.SetActive(false);
        if (narratorText  != null) narratorText.text = string.Empty;
    }
}