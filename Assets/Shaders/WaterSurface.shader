Shader "Custom/WaterSurface"
{
    Properties
    {
        [Header(Colors)]
        _ShallowColor       ("Shallow Color",        Color)  = (0.1, 0.5, 0.6, 0.7)
        _DeepColor          ("Deep Color",            Color)  = (0.01, 0.1, 0.25, 1.0)
        _DepthFogDistance   ("Depth Fog Distance",    Float)  = 3.0
        _SurfaceBrightness  ("Surface Brightness",   Float)  = 0.5

        [Header(Waves)]
        _NormalMap          ("Normal Map",            2D)     = "bump" {}
        _WaveSpeed          ("Wave Speed",            Float)  = 0.05
        _WaveScale          ("Wave Scale",            Float)  = 1.0

        [Header(Refraction)]
        _RefractionStrength ("Refraction Strength",  Float)  = 0.02

        // Internal
        _SrcBlend ("__src", Float) = 5.0
        _DstBlend ("__dst", Float) = 10.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"  = "Transparent"
            "Queue"       = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        GrabPass { "_WaterGrab" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_WaterGrab);   SAMPLER(sampler_WaterGrab);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthFogDistance;
                float  _SurfaceBrightness;
                float4 _NormalMap_ST;
                float  _WaveSpeed;
                float  _WaveScale;
                float  _RefractionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS      : SV_POSITION;
                float3 posWS      : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                float3 viewDir    : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.posCS     = pos.positionCS;
                OUT.posWS     = pos.positionWS;
                OUT.uv        = IN.uv * _WaveScale;
                OUT.screenPos = ComputeScreenPos(pos.positionCS);
                OUT.normalWS  = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDir   = GetWorldSpaceViewDir(pos.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Animated normal map (two layers, opposite directions)
                float2 uvA = IN.uv + _Time.y * _WaveSpeed * float2(1, 0.5);
                float2 uvB = IN.uv + _Time.y * _WaveSpeed * float2(-0.5, 1);
                half3 nA = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA));
                half3 nB = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB));
                half3 waveNormal = normalize(nA + nB);

                // Screen UV for depth + refraction
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // Refraction offset
                float2 refrUV = screenUV + waveNormal.xy * _RefractionStrength;

                // Scene depth (underwater depth fog)
                float rawDepth    = SampleSceneDepth(screenUV);
                float sceneDepth  = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfDepth   = IN.screenPos.w;
                float depthDiff   = saturate((sceneDepth - surfDepth) / _DepthFogDistance);

                // Depth-based color blend
                half4 waterColor = lerp(_ShallowColor, _DeepColor, depthDiff);

                // Grab refracted background
                half4 grab = SAMPLE_TEXTURE2D(_WaterGrab, sampler_WaterGrab, refrUV);
                grab = lerp(grab, waterColor, waterColor.a);

                // Fresnel
                float3 viewDir = normalize(IN.viewDir);
                float  fresnel = pow(1.0 - saturate(dot(viewDir, IN.normalWS)), 3.0);
                fresnel *= _SurfaceBrightness;

                // Main light specular highlight
                Light mainLight  = GetMainLight();
                float3 halfVec   = normalize(viewDir + mainLight.direction);
                float  spec      = pow(saturate(dot(IN.normalWS + waveNormal * 0.3, halfVec)), 64.0);
                half3  specColor = mainLight.color * spec * _SurfaceBrightness;

                half4 finalColor = grab;
                finalColor.rgb  += fresnel * 0.5 + specColor;
                finalColor.a     = waterColor.a;

                return finalColor;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
