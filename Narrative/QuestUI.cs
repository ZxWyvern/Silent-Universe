using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// QuestUI — tampilkan objective quest di HUD.
/// Tidak pakai [Inject] — subscribe langsung ke QuestManager.Instance
/// via Update() polling sampai QuestManager ready, lalu langsung tampil.
/// </summary>
public class QuestUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private CanvasGroup questPanel;
    [SerializeField] private TMP_Text    objectiveText;

    [Header("Warna")]
    [SerializeField] private Color objectiveColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("Animasi")]
    [SerializeField] private float fadeSpeed         = 4f;
    [SerializeField] private float holdAfterComplete = 1.2f;

    private Coroutine  _routine;
    private bool       _subscribed;
    private QuestManager _qm;

    private void Awake()
    {
        if (questPanel == null)    questPanel    = GetComponentInChildren<CanvasGroup>(true);
        if (objectiveText == null) objectiveText = GetComponentInChildren<TMP_Text>(true);

        // Panel mulai tersembunyi
        SetAlpha(0f);
    }

    private void Update()
    {
        // Polling sampai QuestManager.Instance tersedia, lalu subscribe sekali
        if (_subscribed) return;

        var qm = QuestManager.Instance;
        if (qm == null) return;

        _qm = qm;
        _qm.onStepStarted.AddListener(OnStepStarted);
        _qm.onStepCompleted.AddListener(OnStepCompleted);
        _qm.onAllQuestsCompleted.AddListener(OnAllQuestsCompleted);
        _subscribed = true;

        Debug.Log("[QuestUI] Subscribe ke QuestManager berhasil.");

        // Langsung cek apakah quest sudah aktif saat subscribe
        if (_qm.IsQuestActive)
        {
            string obj = _qm.CurrentObjective();
            if (!string.IsNullOrEmpty(obj))
            {
                Debug.Log($"[QuestUI] Quest sudah aktif saat subscribe: '{obj}'");
                ShowImmediate(obj);
            }
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnEnable()
    {
        // Kalau sudah pernah subscribe tapi di-disable, re-subscribe
        if (_subscribed && _qm != null)
        {
            _qm.onStepStarted.AddListener(OnStepStarted);
            _qm.onStepCompleted.AddListener(OnStepCompleted);
            _qm.onAllQuestsCompleted.AddListener(OnAllQuestsCompleted);
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _qm == null) return;
        _qm.onStepStarted.RemoveListener(OnStepStarted);
        _qm.onStepCompleted.RemoveListener(OnStepCompleted);
        _qm.onAllQuestsCompleted.RemoveListener(OnAllQuestsCompleted);
        _subscribed = false;
    }

    // ── Event Handlers ──

    private void OnStepStarted(string questTitle, string objective)
    {
        Debug.Log($"[QuestUI] OnStepStarted: '{objective}'");
        ShowImmediate(objective);
        Restart(FadeIn());
    }

    private void OnStepCompleted(string objective)
    {
        if (objectiveText == null) return;
        objectiveText.text  = $"<s>{objective}</s>";
        objectiveText.color = completedColor;
    }

    private void OnAllQuestsCompleted() => Restart(ShowAllDone());

    // ── Show ──

    /// Set text dan alpha langsung (tanpa animasi) — untuk restore state
    private void ShowImmediate(string objective)
    {
        if (objectiveText != null)
        {
            objectiveText.text  = objective;
            objectiveText.color = objectiveColor;
        }
        SetAlpha(1f);
    }

    /// Fade in panel setelah ShowImmediate
    private IEnumerator FadeIn()
    {
        if (questPanel == null) yield break;
        // Mulai dari 0, fade ke 1
        questPanel.alpha = 0f;
        while (!Mathf.Approximately(questPanel.alpha, 1f))
        {
            questPanel.alpha = Mathf.MoveTowards(questPanel.alpha, 1f, fadeSpeed * Time.deltaTime);
            yield return null;
        }
        questPanel.alpha = 1f;
    }

    private IEnumerator ShowAllDone()
    {
        yield return new WaitForSeconds(holdAfterComplete);
        if (objectiveText != null) objectiveText.text = "SEMUA MISI SELESAI";
        yield return new WaitForSeconds(holdAfterComplete);
        while (!Mathf.Approximately(questPanel.alpha, 0f))
        {
            questPanel.alpha = Mathf.MoveTowards(questPanel.alpha, 0f, fadeSpeed * Time.deltaTime);
            yield return null;
        }
        questPanel.alpha = 0f;
    }

    // ── Helpers ──

    private void Restart(IEnumerator routine)
    {
        if (!gameObject.activeInHierarchy) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    private void SetAlpha(float alpha)
    {
        if (questPanel == null) return;
        questPanel.alpha          = alpha;
        questPanel.interactable   = false;
        questPanel.blocksRaycasts = false;
    }
}