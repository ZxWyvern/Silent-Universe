using UnityEngine;

/// <summary>
/// Moves left across the screen. Registers itself with GameManager when it
/// overlaps the pressing area, and deregisters / scores a miss when it exits.
/// </summary>
public class Tile : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────
    [Tooltip("Move speed in UI units (pixels) per second.")]
    [SerializeField] private float speed = 300f;

    [Header("Score")]
    [SerializeField] private float scoreValue = 10f;

    [Header("Noise")]
    [Tooltip("Noise yang ditambah ke NoiseTracker setiap note ditekan (saat dampener aktif)")]
    [SerializeField] private float noisePerHit = 5f;
    [Tooltip("Noise yang ditambah saat dampener TIDAK aktif — lebih besar agar kondisi tanpa dampener lebih berbahaya")]
    [SerializeField] private float noisePerHitNoDampener = 10f;

    // ── Runtime (set via Init) ────────────────────────────────────
    private GameManager    gm;
    private RectTransform  pressingArea;
    private RectTransform  rt;
    private int            laneIndex = -1;
    private float          _speedMultiplier = 1f;

    // Cached pressing-area bounds in the tile's own parent space
    // Recalculated once per Init call (pressing area doesn't move)
    private float areaLeft;
    private float areaRight;

    // State
    private bool isRegistered;
    private bool hasEnteredArea;

    public bool IsInPressArea => isRegistered;

    // ─────────────────────────────────────────────────────────────
    /// <summary>Called by Spawner right after instantiation.</summary>
    public void Init(GameManager gameManager, RectTransform pressingAreaRect, int lane, float speedMultiplier = 1f)
    {
        gm               = gameManager;
        pressingArea      = pressingAreaRect;
        laneIndex         = lane;
        _speedMultiplier  = speedMultiplier;
        rt                = GetComponent<RectTransform>();

        if (rt == null)           { Debug.LogError("Tile: No RectTransform found."); return; }
        if (pressingArea == null) { Debug.LogError("Tile: pressingAreaRect is null."); return; }

        CacheAreaBounds();
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = Mathf.Max(0.01f, multiplier);
    }

    // ── Unity ────────────────────────────────────────────────────
    private void Update()
    {
        if (rt == null) return;
        if (ScoreManager.Instance != null && !ScoreManager.Instance.IsPlaying) return;

        // ── Move ─────────────────────────────────────────────────
        rt.anchoredPosition += Vector2.left * (speed * _speedMultiplier * Time.deltaTime);

        // ── Overlap check (cheap: single float comparison) ────────
        float tileX = GetTileCanvasX();

        bool inside = tileX >= areaLeft && tileX <= areaRight;

        if (inside && !isRegistered)
        {
            gm?.RegisterTile(this, laneIndex);
            isRegistered  = true;
            hasEnteredArea = true;
        }
        else if (!inside && isRegistered)
        {
            gm?.UnregisterTile(this, laneIndex);
            isRegistered = false;
        }

        // ── Miss detection ────────────────────────────────────────
        // Only after tile has fully passed the area to the left
        if (hasEnteredArea && !isRegistered && tileX < areaLeft)
        {
            Miss();
        }
    }

    private void OnDisable()
    {
        if (isRegistered && gm != null)
            gm.UnregisterTile(this, laneIndex);
    }

    // ── Public API ───────────────────────────────────────────────

    /// <summary>Called by GameManager when the correct lane key is pressed.</summary>
    public void OnHit()
    {
        if (ScoreManager.Instance != null && !ScoreManager.Instance.IsPlaying) return;
        ScoreManager.Instance?.AddScore(scoreValue, false);

        // Tambah noise setiap note ditekan.
        // Jika dampener tidak aktif, pakai noisePerHitNoDampener yang lebih besar
        // agar kondisi tanpa dampener benar-benar lebih berbahaya.
        if (NoiseTracker.Instance != null)
        {
            bool dampenerOn = NoiseTracker.Instance.IsDampenerOn;
            float noise     = dampenerOn ? noisePerHit : noisePerHitNoDampener;
            NoiseTracker.Instance.AddNoise(noise);
        }

        // Notify sanity system — mulai hitung sanity dari note pertama
        SanitySystem.Instance?.NotifyFirstHit();
        NoiseTracker.Instance?.NotifyFirstTileHit();

        gm?.UnregisterTile(this, laneIndex);
        Release();
    }

    // ── Private helpers ──────────────────────────────────────────
    private void Miss()
    {
        ScoreManager.Instance?.AddScore(-scoreValue, true);
        Release();
    }

    /// <summary>Return to pool if possible, otherwise destroy.</summary>
    private void Release()
    {
        isRegistered   = false;
        hasEnteredArea = false;
        if (Spawner.Instance != null)
            Spawner.Instance.ReturnToPool(this);
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// Cache the pressing area's left/right edges in world space.
    /// Called once per Init — pressing area never moves, so this is safe.
    /// </summary>
    private void CacheAreaBounds()
    {
        // GetWorldCorners: [0]=BL [1]=TL [2]=TR [3]=BR
        Vector3[] corners = new Vector3[4];
        pressingArea.GetWorldCorners(corners);
        areaLeft  = corners[0].x; // bottom-left x (world)
        areaRight = corners[2].x; // top-right  x (world)
    }

    /// <summary>Returns tile's current world-space X position.</summary>
    private float GetTileCanvasX() => rt.position.x;
}