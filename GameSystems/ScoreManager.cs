using System;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────
    public static ScoreManager Instance { get; private set; }

    // ── Game State ───────────────────────────────────────────────
    public enum GameState { Playing, Won, Lost }
    public GameState State { get; private set; } = GameState.Playing;
    public bool IsPlaying => State == GameState.Playing;

    // ── Inspector ────────────────────────────────────────────────
    [Header("Score Settings")]
    [SerializeField] private float maxScore = 100f;
    [SerializeField] private int maxChances = 3;

    [Header("Win Transition")]
    [SerializeField] private Animator transitionAnimator;
    [SerializeField] private string transitionTrigger = "Start";

    // ── Runtime ──────────────────────────────────────────────────
    public float MaxScore => maxScore;
    public float CurrentScore { get; private set; }
    public int RemainingChances { get; private set; }

    // ── Events ───────────────────────────────────────────────────
    public event Action<float> OnScoreChanged;   // current score
    public event Action<int>   OnChancesChanged; // remaining chances
    public event Action        OnWin;
    public event Action        OnLose;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        RemainingChances = maxChances;
    }

    // ── Public API ───────────────────────────────────────────────

    /// <summary>Add delta to score. Pass negative delta + missed=true for a miss.</summary>
    public void AddScore(float delta, bool missed)
    {
        if (!IsPlaying) return;

        float previousScore = CurrentScore;
        CurrentScore = Mathf.Clamp(CurrentScore + delta, 0f, maxScore);
        OnScoreChanged?.Invoke(CurrentScore);

        // BUG FIX #23 — Score 0 Langsung Kurangi Nyawa:
        // Bug asli: CurrentScore=0, miss pertama → CurrentScore + delta ≤ 0 → RemainingChances--
        // Sekarang: nyawa hanya berkurang jika skor sudah 0 SEBELUM hit ini (previousScore <= 0),
        // bukan hanya karena delta negatif membuat skor menjadi 0.
        if (missed && delta < 0f && previousScore <= 0f && CurrentScore <= 0f)
        {
            RemainingChances--;
            OnChancesChanged?.Invoke(RemainingChances);
            if (RemainingChances <= 0) { TriggerLose(); return; }
        }

        if (CurrentScore >= maxScore) TriggerWin();
    }

    // ── Private ──────────────────────────────────────────────────
    private void TriggerWin()
    {
        State = GameState.Won;
        transitionAnimator?.SetTrigger(transitionTrigger);
        OnWin?.Invoke();
        Debug.Log("[ScoreManager] You Win!");
    }

    private void TriggerLose()
    {
        // BUG FIX D1 — If game over already triggered by another system, abort.
        State = GameState.Lost;
        OnLose?.Invoke();
        Debug.Log("[ScoreManager] You Lose!");

    }
}