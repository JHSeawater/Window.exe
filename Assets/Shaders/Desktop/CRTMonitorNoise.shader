Shader "WindowExe/CRTMonitorNoise"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(CRT Noise)]
        _NoiseColor ("Noise Color", Color) = (0.75, 0.85, 1.0, 0.85)
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.55
        _NoiseSpeed ("Noise Speed", Range(0, 50)) = 25.0

        [Header(Scanlines and Glitch)]
        _ScanlineDensity ("Scanline Density", Range(0, 1000)) = 320.0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.35
        _GlitchJitter ("Glitch Jitter Intensity", Range(0, 0.2)) = 0.03
        _GlitchSpeed ("Glitch Frequency/Speed", Range(0, 20)) = 8.0

        [Header(Screen Shape)]
        _CornerRadius ("Corner Radius (0 = Sharp, 0.5 = Full Round)", Range(0, 0.5)) = 0.15
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.1)) = 0.015
        _Curvature ("Screen Curvature (Barrel Distortion)", Range(0, 0.5)) = 0.12
        _Vignette ("Vignette Intensity", Range(0, 2)) = 0.6

        // UI Mask / Stencil Support
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _NoiseColor;
            float _NoiseIntensity;
            float _NoiseSpeed;
            float _ScanlineDensity;
            float _ScanlineIntensity;
            float _GlitchJitter;
            float _GlitchSpeed;
            float _CornerRadius;
            float _EdgeSoftness;
            float _Curvature;
            float _Vignette;

            // Pseudo-random hash
            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 1. CRT Barrel Curvature (볼록한 브라운관 왜곡)
                float2 centeredUV = uv - 0.5;
                float r2 = dot(centeredUV, centeredUV);
                uv = uv + centeredUV * (r2 * _Curvature);

                // 2. Rounded Box SDF Masking (둥근 모서리 마스킹)
                // 모서리가 둥근 사각형 거리 계산
                float2 d = abs(uv - 0.5) - (float2(0.5, 0.5) - _CornerRadius);
                float dist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - _CornerRadius;
                float mask = 1.0 - smoothstep(0.0, _EdgeSoftness, dist);

                // 화면 밖이면 즉시 투명 처리
                if (mask <= 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // 3. Glitch / Horizontal Jitter (가로선 찢김 효과)
                float glitchTime = floor(_Time.y * _GlitchSpeed);
                float glitchLine = hash21(float2(floor(uv.y * 30.0), glitchTime));
                if (glitchLine > 0.85)
                {
                    uv.x += (hash21(float2(glitchTime, uv.y)) - 0.5) * _GlitchJitter;
                }

                // 4. White Static Noise (치지직 정전기 노이즈)
                float noiseTime = floor(_Time.y * _NoiseSpeed);
                float noise = hash21(uv * 500.0 + float2(noiseTime * 17.0, noiseTime * 31.0));

                // 5. Scanlines (가로 주사선)
                float scanline = sin((uv.y + _Time.x * 2.0) * _ScanlineDensity * 3.14159);
                scanline = (scanline + 1.0) * 0.5;
                float scanlineFactor = lerp(1.0, scanline, _ScanlineIntensity);

                // 6. Vignette (가장자리 어두워짐)
                float vignette = 1.0 - smoothstep(0.3, 0.7, length(centeredUV) * _Vignette);

                // 베이스 텍스처(있다면) 샘플링
                fixed4 texColor = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                // 노이즈와 주사선 결합
                fixed4 resultColor = _NoiseColor;
                resultColor.rgb = lerp(resultColor.rgb * (0.8 + 0.4 * noise), fixed3(noise, noise, noise), _NoiseIntensity);
                resultColor.rgb *= scanlineFactor * vignette;
                resultColor.a = _NoiseColor.a * mask * (_NoiseIntensity * 0.6 + noise * 0.4);

#ifdef UNITY_UI_CLIP_RECT
                resultColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
#endif
#ifdef UNITY_UI_ALPHACLIP
                clip(resultColor.a - 0.001);
#endif

                return resultColor;
            }
            ENDCG
        }
    }
}

