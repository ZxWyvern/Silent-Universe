Shader "Hidden/CRTEffect"
{
    Properties
    {
        _BlitTexture        ("Source",              2D)      = "white" {}

        [Header(Scanlines)]
        _ScanlineIntensity  ("Intensity",       Range(0,1))  = 0.4
        _ScanlineCount      ("Lines",           Range(60,1080)) = 240

        [Header(Shadow Mask)]
        _MaskIntensity      ("Intensity",       Range(0,1))  = 0.3
        _MaskScale          ("Scale (px)",      Range(1,8))  = 2

        [Header(Curvature)]
        _CurvatureAmount    ("Amount",          Range(0,1))  = 0.2

        [Header(Chromatic Aberration)]
        _ChromaShift        ("Shift",           Range(0,0.02)) = 0.004

        [Header(Vignette)]
        _VignetteStrength   ("Strength",        Range(0,1))  = 0.5
        _VignetteRadius     ("Inner Radius",    Range(0,1))  = 0.6

        [Header(Noise and Jitter)]
        _JitterStrength     ("Jitter",          Range(0,0.005)) = 0.001
        _NoiseStrength      ("Grain",           Range(0,0.2)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "CRTEffect"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CRTEffect.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                // Unity 6 Blitter menggunakan vertexID, bukan posisi 3D (positionOS)
                OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
                OUT.uv         = GetFullScreenTriangleTexCoord(IN.vertexID);
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                return CRTFrag(IN.uv);
            }
            ENDHLSL
        }
    }
}
