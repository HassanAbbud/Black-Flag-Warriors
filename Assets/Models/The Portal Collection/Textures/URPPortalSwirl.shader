// URP Portal Swirl — animated, transparent swirl for a portal's center surface.
// Drop into your Assets folder (any folder, NOT the Editor folder).
// Create a material, set its shader to "Custom/URP Portal Swirl",
// and assign it to the portal's center mesh.

Shader "Custom/URP Portal Swirl"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.55, 0.15, 1.0, 1)
        _ColorB ("Color B", Color) = (0.1, 0.6, 1.0, 1)
        [HDR] _Glow ("Center Glow", Color) = (2, 1.5, 3, 1)
        _SwirlStrength ("Swirl Strength", Float) = 6
        _Speed ("Speed", Float) = 0.5
        _NoiseScale ("Noise Scale", Float) = 6
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.15
        _Alpha ("Opacity", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PortalSwirl"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half4 _Glow;
                float _SwirlStrength;
                float _Speed;
                float _NoiseScale;
                float _EdgeSoftness;
                float _Alpha;
            CBUFFER_END

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += amp * valueNoise(p);
                    p *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv - 0.5;
                float r = length(p) * 2.0;          // 0 at center, 1 at edge
                float t = _Time.y * _Speed;

                // Twist harder toward the center and spin over time
                float angle = atan2(p.y, p.x) + _SwirlStrength * (1.0 - r) + t * 2.0;
                float2 swirled = float2(cos(angle), sin(angle)) * r;

                float n = fbm(swirled * _NoiseScale * 0.5 + t);

                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, saturate(n * 1.5 - r * 0.5));
                col += _Glow.rgb * pow(saturate(1.0 - r), 3.0) * 0.5;

                // Soft fade at the rim so it blends into the frame
                float edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, r);

                return half4(col, _Alpha * edge);
            }
            ENDHLSL
        }
    }
}
