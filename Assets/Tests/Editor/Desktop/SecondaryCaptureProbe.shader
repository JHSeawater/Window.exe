Shader "Hidden/WindowExe/SecondaryCaptureProbe"
{
    Properties { _SampleUV ("Sample UV", Vector) = (0.5,0.5,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CameraSortingLayerTexture); SAMPLER(sampler_CameraSortingLayerTexture);
            float4 _SampleUV;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes input) { Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); return output; }
            half4 Frag(Varyings input):SV_Target { return SAMPLE_TEXTURE2D(_CameraSortingLayerTexture,sampler_CameraSortingLayerTexture,_SampleUV.xy); }
            ENDHLSL
        }
    }
}
