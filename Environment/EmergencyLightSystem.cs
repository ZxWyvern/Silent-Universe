using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EmergencyLightSystem — trigger via UnityEvent atau public method.
/// </summary>
public class EmergencyLightSystem : MonoBehaviour
{
    // ── Lights & Renderers ────────────────────────────────────────

    [Header("Lights & Renderers")]
    [Tooltip("Komponen Light yang akan dikontrol")]
    [SerializeField] private Light[] lights;
    
    [Tooltip("Mesh Renderer dari lampu untuk mengatur Emission")]
    [SerializeField] private Renderer[] lightRenderers;

    // ── Global Light Settings ─────────────────────────────────────

    [Header("Emergency Color & Intensity")]
    [Tooltip("Gunakan warna murni (Misal R:1, G:0, B:0) agar tengahnya tidak jadi putih saat kena Bloom")]
    [SerializeField] private Color  emergencyColor  = new Color(1f, 0f, 0f); 
    [SerializeField] private float  onIntensity     = 1.5f;
    
    [Tooltip("Pengali khusus untuk Emission Mesh (Makin tinggi = makin glow/bloom)")]
    [SerializeField] private float  emissionMultiplier = 5f;

    [Header("Fade Loop (Phase 4)")]
    [Tooltip("Intensitas minimum saat redup (0 = mati total)")]
    [Range(0f, 1f)]
    [SerializeField] private float  minIntensity    = 0f;

    [Tooltip("Durasi fase ON sebelum fade out")]
    [SerializeField] private float  onDuration      = 0.4f;

    [Tooltip("Durasi fase OFF sebelum fade in")]
    [SerializeField] private float  offDuration     = 0.6f;

    [Tooltip("Kecepatan fade (lebih tinggi = lebih cepat)")]
    [SerializeField] private float  fadeSpeed       = 6f;

    [Header("Phase 2 — Fliker (putih, chaos)")]
    [SerializeField] private float  flickerDuration = 1.5f;
    [SerializeField] private float  flickerSpeed    = 20f;

    [Header("Phase 3 — Merah stabil")]
    [SerializeField] private float  redHoldDuration = 0.5f;

    [Header("Events")]
    public UnityEvent onEmergencyStarted;
    public UnityEvent onEmergencyStopped;

    // ── Private State ─────────────────────────────────────────────

    private Color[] _originalColors;
    private float[] _originalIntensities;
    
    private Material[] _materials;
    private Color[] _originalEmissionColors;

    private bool       _active;
    private Coroutine  _mainRoutine;
    private Coroutine[] _flickerRoutines;

    // ── Public API ────────────────────────────────────────────────

    public void TriggerEmergency()
    {
        if (_active) return;
        _active = true;
        if (_mainRoutine != null) StopCoroutine(_mainRoutine);
        _mainRoutine = StartCoroutine(EmergencyRoutine());
    }

    public void StopEmergency()
    {
        if (!_active) return;
        _active = false;

        StopAllFlickers();
        if (_mainRoutine != null) { StopCoroutine(_mainRoutine); _mainRoutine = null; }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                StartCoroutine(RestoreLight(i));
        }

        onEmergencyStopped.Invoke();
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        int count = lights != null ? lights.Length : 0;

        _originalColors         = new Color[count];
        _originalIntensities    = new float[count];
        _flickerRoutines        = new Coroutine[count];
        _materials              = new Material[count];
        _originalEmissionColors = new Color[count];

        for (int i = 0; i < count; i++)
        {
            if (lights[i] == null) continue;
            
            _originalColors[i]      = lights[i].color;
            _originalIntensities[i] = lights[i].intensity;

            if (lightRenderers != null && i < lightRenderers.Length && lightRenderers[i] != null)
            {
                _materials[i] = lightRenderers[i].material;
                _materials[i].EnableKeyword("_EMISSION"); 
                
                if (_materials[i].HasProperty("_EmissionColor"))
                {
                    _originalEmissionColors[i] = _materials[i].GetColor("_EmissionColor");
                }
            }
        }
    }

    // ── Main Routine ──────────────────────────────────────────────

    private IEnumerator EmergencyRoutine()
    {
        onEmergencyStarted.Invoke();

        yield return PhaseFlicker();
        yield return PhaseRedHold();

        PhaseStartFadeLoop();
    }

    // ── Phase 2: Fliker putih ─────────────────────────────────────

    private IEnumerator PhaseFlicker()
    {
        float elapsed = 0f;
        while (elapsed < flickerDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;

                float seed  = i * 17.3f;
                float noise = Mathf.PerlinNoise(Time.time * flickerSpeed + seed, 0f);
                float currentIntensity = Mathf.Lerp(0f, _originalIntensities[i], noise);

                lights[i].intensity = currentIntensity;
                lights[i].color     = _originalColors[i];
                
                // Gunakan 0 sebagai dasar minIntensity saat flicker normal
                UpdateEmission(i, _originalColors[i], currentIntensity, 0f, _originalIntensities[i]);
            }

            yield return null;
        }
    }

    // ── Phase 3: Snap merah, tahan ────────────────────────────────

    private IEnumerator PhaseRedHold()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            lights[i].color     = emergencyColor;
            lights[i].intensity = onIntensity;
            
            UpdateEmission(i, emergencyColor, onIntensity, minIntensity, onIntensity);
        }

        yield return new WaitForSeconds(redHoldDuration);
    }

    // ── Phase 4: Fade loop ────────────────────────────────────────

    private void PhaseStartFadeLoop()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;

            float startOffset   = Random.Range(0f, offDuration);
            _flickerRoutines[i] = StartCoroutine(FadeLoopRoutine(i, startOffset));
        }
    }

    private IEnumerator FadeLoopRoutine(int index, float startDelay)
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            yield return Fade(index, onIntensity, minIntensity, offDuration);
            yield return new WaitForSeconds(offDuration * Random.Range(0.5f, 2f));

            yield return Fade(index, minIntensity, onIntensity, onDuration);
            yield return new WaitForSeconds(onDuration * Random.Range(0.5f, 1.5f));
        }
    }

    private IEnumerator Fade(int index, float from, float to, float duration)
    {
        Light light   = lights[index];
        float elapsed = 0f;
        float t       = 0f;

        while (t < 1f)
        {
            elapsed        += Time.deltaTime;
            t               = Mathf.Clamp01(elapsed * fadeSpeed / duration);
            float currentInt = Mathf.Lerp(from, to, EaseInOut(t));
            
            light.intensity = currentInt;
            light.color     = emergencyColor;
            
            // Update emission, akan otomatis jadi Color.black kalau menyentuh minIntensity
            UpdateEmission(index, emergencyColor, currentInt, minIntensity, onIntensity);
            
            yield return null;
        }

        light.intensity = to;
        UpdateEmission(index, emergencyColor, to, minIntensity, onIntensity);
    }

    // ── Restore ───────────────────────────────────────────────────

    private IEnumerator RestoreLight(int index)
    {
        Light  light    = lights[index];
        float  elapsed  = 0f;
        float  duration = 0.5f;
        Color  startCol = light.color;
        float  startInt = light.intensity;
        
        Color startEmission = Color.black;
        if (_materials[index] != null && _materials[index].HasProperty("_EmissionColor"))
        {
            startEmission = _materials[index].GetColor("_EmissionColor");
        }

        while (elapsed < duration)
        {
            elapsed        += Time.deltaTime;
            float t         = elapsed / duration;
            
            light.color     = Color.Lerp(startCol, _originalColors[index],      t);
            light.intensity = Mathf.Lerp(startInt, _originalIntensities[index], t);
            
            if (_materials[index] != null && _materials[index].HasProperty("_EmissionColor"))
            {
                Color lerpedEmission = Color.Lerp(startEmission, _originalEmissionColors[index], t);
                _materials[index].SetColor("_EmissionColor", lerpedEmission);
            }
            
            yield return null;
        }

        light.color     = _originalColors[index];
        light.intensity = _originalIntensities[index];
        
        if (_materials[index] != null && _materials[index].HasProperty("_EmissionColor"))
        {
            _materials[index].SetColor("_EmissionColor", _originalEmissionColors[index]);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Mengatur warna emission dengan meng-Lerp dari Hitam mutlak ke Warna Target HDR
    /// berdasarkan posisi lightIntensity di antara rentang rangeMin dan rangeMax.
    /// </summary>
    private void UpdateEmission(int index, Color baseColor, float currentIntensity, float rangeMin, float rangeMax)
    {
        if (_materials[index] != null && _materials[index].HasProperty("_EmissionColor"))
        {
            // InverseLerp memastikan bahwa jika currentIntensity == rangeMin, rasionya 0. 
            // Jika rasionya 0, mesh akan benar-benar di-set ke Color.black (hitam mati total).
            float ratio = Mathf.InverseLerp(rangeMin, rangeMax, currentIntensity);

            // Kalikan dengan emissionMultiplier agar mendapatkan glow/HDR yang kuat
            Color targetHDR = baseColor * emissionMultiplier;

            // Transisi mulus antara mati total dan nyala terang
            Color finalEmission = Color.Lerp(Color.black, targetHDR, ratio);

            _materials[index].SetColor("_EmissionColor", finalEmission);
        }
    }

    private void StopAllFlickers()
    {
        if (_flickerRoutines == null) return;
        for (int i = 0; i < _flickerRoutines.Length; i++)
        {
            if (_flickerRoutines[i] != null)
            {
                StopCoroutine(_flickerRoutines[i]);
                _flickerRoutines[i] = null;
            }
        }
    }

    private static float EaseInOut(float t) => t * t * (3f - 2f * t);
}