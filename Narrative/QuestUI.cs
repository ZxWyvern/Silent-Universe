using UnityEngine;
using TMPro;
using System.Collections;
using VContainer;

/// <summary>
/// QuestUI — tampilkan objective quest di HUD.
///
/// FIX: Tambah [Inject] QuestManager agar tidak bergantung pada QuestManager.Instance
/// yang belum tersedia saat OnEnable() dipanggil (race condition VContainer).
///
/// Urutan Unity lifecycle dengan VContainer:
///   Awake() → VContainer inject → Start() → OnEnable (jika aktif sejak awal)
/// Karena OnEnable() dipanggil SEBELUM inject selesai, subscribe dipindah ke Start().
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

    // FIX: Inject QuestManager — tidak bergantung pada .Instance yang belum ada saat OnEnable
    [Inject] private QuestManager _questManager;

    private Coroutine _routine;
    private bool      _subscribed;

    private void Awake()
    {
        if (questPanel == null)    questPanel    = GetComponentInChildren<CanvasGroup>(true);
        if (objectiveText == null) objectiveText = GetComponentInChildren<TMP_Text>(true);
        SetAlpha(0f);
    }

    // FIX: Subscribe di Start() bukan OnEnable()
    // OnEnable() dipanggil sebelum VContainer selesai inject — _questManager masih null.
    // Start() dijamin dipanggil setelah semua [Inject] fields sudah terisi.
    private void Start()
    {
        Subscribe();
        RestoreState();
    }

    private void OnEnable()
    {
        // Hanya re-subscribe jika Start() sudah pernah jalan (re-enable setelah disable)
        if (_subscribed)
        {
            Subscribe();
            RestoreState();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // ── Subscribe / Unsubscribe ──

    private void Subscribe()
    {
        // Fallback ke Instance untuk editor testing tanpa container
        var qm = _questManager ?? QuestManager.Instance;
        if (qm == null || _subscribed) return;

        qm.onStepStarted.AddListener(OnStepStarted);
        qm.onStepCompleted.AddListener(OnStepCompleted);
        qm.onAllQuestsCompleted.AddListener(OnAllQuestsCompleted);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        var qm = _questManager ?? QuestManager.Instance;
        if (qm == null) return;

        qm.onStepStarted.RemoveListener(OnStepStarted);
        qm.onStepCompleted.RemoveListener(OnStepCompleted);
        qm.onAllQuestsCompleted.RemoveListener(OnAllQuestsCompleted);
        _subscribed = false;
    }

    // ── Event Handlers ──

    private void OnStepStarted(string questTitle, string objective)
    {
        Restart(ShowStep(objective));
    }

    private void OnStepCompleted(string objective)
    {
        if (objectiveText == null) return;
        objectiveText.text  = $"<s>{objective}</s>";
        objectiveText.color = completedColor;
    }

    private void OnAllQuestsCompleted() => Restart(ShowAllDone());

    // ── Restore ──

    private void RestoreState()
    {
        var qm = _questManager ?? QuestManager.Instance;
        if (qm == null || !qm.IsQuestActive) return;

        string objective = qm.CurrentObjective();
        if (string.IsNullOrEmpty(objective)) return;

        if (objectiveText != null)
        {
            objectiveText.text  = objective;
            objectiveText.color = objectiveColor;
        }
        SetAlpha(1f);
    }

    // ── Coroutines ──

    private IEnumerator ShowStep(string objective)
    {
        if (questPanel != null && questPanel.alpha > 0f)
        {
            yield return Fade(0f);
            yield return new WaitForSeconds(0.15f);
        }

        if (objectiveText != null)
        {
            objectiveText.text  = objective;
            objectiveText.color = objectiveColor;
        }

        yield return Fade(1f);
    }

    private IEnumerator ShowAllDone()
    {
        yield return new WaitForSeconds(holdAfterComplete);
        if (objectiveText != null) objectiveText.text = "SEMUA MISI SELESAI";
        yield return new WaitForSeconds(holdAfterComplete);
        yield return Fade(0f);
    }

    private IEnumerator Fade(float target)
    {
        if (questPanel == null) yield break;
        while (!Mathf.Approximately(questPanel.alpha, target))
        {
            questPanel.alpha = Mathf.MoveTowards(questPanel.alpha, target, fadeSpeed * Time.deltaTime);
            yield return null;
        }
        questPanel.alpha = target;
    }

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
