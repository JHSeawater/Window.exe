// WINDOW.exe — 화면 전체 CRT/VHS 노이즈 오버레이 (UI용)
// 맨 위 Canvas의 전체 화면 RawImage에 이 셰이더 머티리얼을 넣어 사용.
// Screen Space Overlay shader for the project URP 2D renderer.
// 아래 화면을 "덮는" 방식이라 주사선·노이즈·줄·비네트·색조는 되지만, 화면 왜곡(휘어짐, 색수차)은 안 됨.
Shader "WindowExe/CRTOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}

        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.25
        _ScanlinePeriod ("Scanline Period (px @1080p)", Float) = 3

        _GrainStrength ("Grain Strength", Range(0, 1)) = 0.08

        _StreakStrength ("Streak Strength", Range(0, 1)) = 0.35
        _StreakDensity ("Streak Density", Range(0, 0.3)) = 0.03
        _StreakSpeed ("Streak Change Speed", Float) = 10

        _RollStrength ("Rolling Bar Strength", Range(0, 1)) = 0.05
        _RollSpeed ("Rolling Bar Speed", Float) = 0.12

        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.35

        _TintColor ("Tint Color", Color) = (0.8, 0, 0, 1)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0

        _EffectTime ("Unscaled Effect Time", Float) = 0
        _Simple ("Simple Quality", Float) = 0
        // Actual display pixel dimensions
        _Resolution ("Resolution (set by script)", Vector) = (1920, 1080, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            float _ScanlineStrength, _ScanlinePeriod;
            float _GrainStrength;
            float _StreakStrength, _StreakDensity, _StreakSpeed;
            float _RollStrength, _RollSpeed;
            float _VignetteStrength;
            float4 _TintColor;
            float _TintAmount;
            float4 _Resolution;
            float _EffectTime, _Simple;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;          // RawImage가 화면 전체를 덮으므로 uv = 화면 좌표(0~1)
                o.color = v.color;
                return o;
            }

            float hash (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 res = max(_Resolution.xy, float2(1, 1));
                float2 pix = floor(i.uv * res);   // 실제 픽셀 좌표 (정수)
                float t = _EffectTime;

                float dark = 0;
                float light = 0;

                // 1) 주사선: 해상도에 맞춰 정수 픽셀 간격으로 그려서 모아레 방지
                float period = max(2.0, round(_ScanlinePeriod * res.y / 1080.0));
                float scan = step(fmod(pix.y, period), 0.5);
                dark += scan * _ScanlineStrength;

                // 2) 입자 노이즈 (매 프레임 바뀜)
                if (_Simple < 0.5)
                {
                float g = hash(pix + floor(t * 30.0) * 17.0);
                light += g * _GrainStrength * 0.5;
                dark  += (1.0 - g) * _GrainStrength * 0.5;

                // 3) 가로 노이즈 줄: 일부 행에만 짧은 흰 줄이 깜빡임
                float row = floor(pix.y / 2.0);
                float tStep = floor(t * _StreakSpeed);
                float rowOn = step(hash(float2(row, tStep)), _StreakDensity);
                float seg = step(0.6, hash(float2(floor(pix.x / 60.0), row + tStep * 3.1)));
                float dots = hash(float2(floor(pix.x / 3.0), row));
                light += rowOn * seg * dots * _StreakStrength;

                // 4) 천천히 내려가는 밝은 띠 (오래된 모니터 느낌)
                float roll = frac(i.uv.y - t * _RollSpeed);
                float bar = exp(-pow((roll - 0.5) * 18.0, 2.0));
                light += bar * _RollStrength;

                }
                // 5) 비네트 (가장자리 어둡게)
                float2 c = i.uv - 0.5;
                float vig = saturate(dot(c, c) * 2.5);
                dark += vig * vig * _VignetteStrength;

                // 합치기: 밝게 할 부분은 흰색, 어둡게 할 부분은 검은색으로 덮음
                float a = saturate(light + dark);
                float3 col = (light / max(light + dark, 1e-4)).xxx;

                // 6) 오염도용 색조
                col = lerp(col, _TintColor.rgb, _TintAmount);
                a = saturate(a + _TintAmount * 0.35);

                return fixed4(col, a * i.color.a);
            }
            ENDCG
        }
    }
}
