// CRTEffect.hlsl
// Full-screen CRT post-process pass for Unity 6 URP
// Used by CRTRendererFeature + CRTPass

#ifndef CRT_EFFECT_INCLUDED
#define CRT_EFFECT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// ── Textures ──────────────────────────────────────────────────────────────────
TEXTURE2D(_BlitTexture);
SAMPLER(sampler_BlitTexture);

// ── Properties ────────────────────────────────────────────────────────────────
CBUFFER_START(UnityPerMaterial)
    // Scanlines
    float _ScanlineIntensity;   // 0..1  strength of dark bands
    float _ScanlineCount;       // lines per screen height (e.g. 240, 480)

    // Shadow mask / phosphor grid
    float _MaskIntensity;       // 0..1
    float _MaskScale;           // pixel scale of RGB triads

    // Curvature
    float _CurvatureAmount;     // 0..1  0 = flat, 1 = heavy barrel

    // Chromatic aberration
    float _ChromaShift;         // UV offset magnitude (e.g. 0.002)

    // Vignette
    float _VignetteStrength;    // 0..1
    float _VignetteRadius;      // 0..1  inner radius of vignette

    // Jitter / noise
    float _JitterStrength;      // 0..0.005 horizontal scanline wobble
    float _NoiseStrength;       // 0..0.1  luminance grain
    float _Time_Custom;         // pass Time.time from C#
CBUFFER_END

// ── Helpers ───────────────────────────────────────────────────────────────────

// Barrel / pincushion distortion
float2 CurveUV(float2 uv, float amount)
{
    // Remap to -1..1
    float2 cc = uv * 2.0 - 1.0;
    // Barrel: shift outward proportional to distance from center
    float dist = dot(cc, cc);
    cc *= 1.0 + dist * amount * 0.3;
    // Back to 0..1
    return cc * 0.5 + 0.5;
}

// Hash-based noise (no texture needed)
float Hash(float2 p)
{
    p = frac(p * float2(443.8975, 397.2973));
    p += dot(p.xy, p.yx + 19.19);
    return frac(p.x * p.y);
}

// ── Main ──────────────────────────────────────────────────────────────────────
float4 CRTFrag(float2 uv)
{
    // ── 1. Curvature ─────────────────────────────────────────────────────────
    float2 curvedUV = CurveUV(uv, _CurvatureAmount);

    // Kill pixels outside the curved screen boundary
    if (curvedUV.x < 0.0 || curvedUV.x > 1.0 ||
        curvedUV.y < 0.0 || curvedUV.y > 1.0)
        return float4(0, 0, 0, 1);

    // ── 2. Scanline jitter (horizontal wobble per row) ────────────────────────
    float row        = floor(curvedUV.y * _ScanlineCount);
    float jitterSeed = Hash(float2(row, floor(_Time_Custom * 24.0)));
    float jitter     = (jitterSeed - 0.5) * 2.0 * _JitterStrength;
    float2 jitteredUV = curvedUV + float2(jitter, 0.0);

    // ── 3. Chromatic aberration ───────────────────────────────────────────────
    float2 offset = (jitteredUV - 0.5) * _ChromaShift;
    float r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, jitteredUV + offset).r;
    float g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, jitteredUV        ).g;
    float b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, jitteredUV - offset).b;
    float4 color = float4(r, g, b, 1.0);

    // ── 4. Scanlines ──────────────────────────────────────────────────────────
    // Dark band every other scan line
    float scanPos    = frac(curvedUV.y * _ScanlineCount);
    float scanLine   = smoothstep(0.0, 0.15, scanPos) * smoothstep(1.0, 0.85, scanPos);
    float scanFactor = 1.0 - _ScanlineIntensity * (1.0 - scanLine);
    color.rgb       *= scanFactor;

    // ── 5. Shadow mask / phosphor RGB triads ─────────────────────────────────
    // Each triad is 3 sub-pixels wide: R | G | B
    float2 maskUV    = curvedUV * _ScreenParams.xy / _MaskScale;
    float  subPixel  = frac(maskUV.x) * 3.0;          // 0..3 within one triad
    float3 maskColor = float3(
        smoothstep(0.0, 0.3, subPixel) * smoothstep(1.3, 1.0, subPixel),   // R
        smoothstep(1.0, 1.3, subPixel) * smoothstep(2.3, 2.0, subPixel),   // G
        smoothstep(2.0, 2.3, subPixel) * smoothstep(3.3, 3.0, subPixel)    // B
    );
    // Row mask: every other row is slightly dimmer
    float rowMask    = 0.7 + 0.3 * step(0.5, frac(maskUV.y * 0.5));
    maskColor       *= rowMask;
    // Blend mask with original — at 0 intensity, mask disappears
    color.rgb       *= lerp(float3(1, 1, 1), maskColor * 3.0, _MaskIntensity);

    // ── 6. Vignette ───────────────────────────────────────────────────────────
    float2 vigUV    = curvedUV * 2.0 - 1.0;
    float  vigDist  = length(vigUV);
    float  vignette = smoothstep(_VignetteRadius, _VignetteRadius + 0.3, vigDist);
    color.rgb      *= 1.0 - vignette * _VignetteStrength;

    // ── 7. Luminance grain / noise ────────────────────────────────────────────
    float noise    = Hash(curvedUV + frac(_Time_Custom * 0.1));
    color.rgb     += (noise - 0.5) * _NoiseStrength;

    // Clamp
    color.rgb = saturate(color.rgb);
    return color;
}

#endif
