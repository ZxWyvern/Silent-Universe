using UnityEngine;

public class LightAndEmissionFlicker : MonoBehaviour
{
    public Light redLight;

    [Header("Emission")]
    public Renderer targetRenderer;
    public Color emissionColor = Color.red;
    public float emissionMultiplier = 2f;

    [Header("Flicker Settings")]
    public float minIntensity = 0.2f;
    public float maxIntensity = 3f;
    public float flickerSpeed = 8f;

    private float targetIntensity;
    private Material mat;

    void Start()
    {
        if (redLight == null)
            redLight = GetComponent<Light>();

        if (targetRenderer != null)
        {
            mat = targetRenderer.material;
            mat.EnableKeyword("_EMISSION");
        }

        targetIntensity = Random.Range(minIntensity, maxIntensity);
    }

    void Update()
    {
        // Smooth flicker lampu
        redLight.intensity = Mathf.Lerp(
            redLight.intensity,
            targetIntensity,
            Time.deltaTime * flickerSpeed
        );

        // Sync emission dengan intensity lampu
        if (mat != null)
        {
            Color finalEmission = emissionColor * redLight.intensity * emissionMultiplier;
            mat.SetColor("_EmissionColor", finalEmission);
        }

        // Ganti target kalau sudah mendekati
        if (Mathf.Abs(redLight.intensity - targetIntensity) < 0.05f)
        {
            targetIntensity = Random.Range(minIntensity, maxIntensity);

            // kadang mati total (horror effect)
            if (Random.value > 0.95f)
            {
                targetIntensity = 0f;
            }
        }
    }
}