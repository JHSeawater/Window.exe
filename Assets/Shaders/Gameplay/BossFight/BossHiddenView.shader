Shader "WINDOW.exe/BossHiddenView"
{
    Properties { _MainTex("Sprite",2D)="white"{} _BossHiddenTexture("Hidden layer",2D)="black"{} }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Tags {"LightMode"="Universal2D"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BossHiddenTexture);SAMPLER(sampler_BossHiddenTexture);
            CBUFFER_START(UnityPerMaterial)
            float4 _BossWorldRect;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            Varyings Vert(Attributes v)
            {
                Varyings o;float3 world=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world);o.uv=(world.xy-_BossWorldRect.xy)/_BossWorldRect.zw;o.color=v.color;return o;
            }
            half4 Frag(Varyings i):SV_Target{return SAMPLE_TEXTURE2D(_BossHiddenTexture,sampler_BossHiddenTexture,i.uv)*i.color;}
            ENDHLSL
        }
    }
}
