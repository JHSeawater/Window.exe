Shader "WindowExe/DesktopViewport"
{
    Properties { _WindowDesktopTexture ("Desktop reveal", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_WindowDesktopTexture); SAMPLER(sampler_WindowDesktopTexture);
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float4 screenPosition:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv = input.screenPosition.xy / input.screenPosition.w;
                return half4(SAMPLE_TEXTURE2D(_WindowDesktopTexture, sampler_WindowDesktopTexture, uv).rgb, 1);
            }
            ENDHLSL
        }
    }
}
