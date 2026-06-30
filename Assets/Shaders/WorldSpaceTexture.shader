Shader "Custom/WorldSpaceTexture"
{
    Properties
    {
        _DiffuseTex  ("Diffuse",          2D)          = "white" {}
        _NormalTex   ("Normal Map",       2D)          = "bump"  {}
        _NormalStr   ("Normal Strength",  Range(0,2))  = 1.0
        _RoughTex    ("Roughness",        2D)          = "white" {}
        _AOTex       ("Ambient Occlusion",2D)          = "white" {}
        _AOStr       ("AO Strength",      Range(0,1))  = 1.0
        _TileSize    ("Tile Size (m)",    Float)        = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DiffuseTex); SAMPLER(sampler_DiffuseTex);
            TEXTURE2D(_NormalTex);  SAMPLER(sampler_NormalTex);
            TEXTURE2D(_RoughTex);   SAMPLER(sampler_RoughTex);
            TEXTURE2D(_AOTex);      SAMPLER(sampler_AOTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DiffuseTex_ST;
                float  _TileSize;
                float  _NormalStr;
                float  _AOStr;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
                float4 tangOS   : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS  : SV_POSITION;
                float3 posWS  : TEXCOORD0;
                float3 nWS    : TEXCOORD1;
                float3 tWS    : TEXCOORD2;
                float3 btWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.posOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS, IN.tangOS);
                OUT.posCS = p.positionCS;
                OUT.posWS = p.positionWS;
                OUT.nWS   = n.normalWS;
                OUT.tWS   = n.tangentWS;
                OUT.btWS  = n.bitangentWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv = IN.posWS.xz / _TileSize;

                half3 albedo    = SAMPLE_TEXTURE2D(_DiffuseTex, sampler_DiffuseTex, uv).rgb;
                half  roughness = SAMPLE_TEXTURE2D(_RoughTex,   sampler_RoughTex,   uv).r;
                half  ao        = lerp(1.0h, SAMPLE_TEXTURE2D(_AOTex, sampler_AOTex, uv).r, _AOStr);

                half4 nSamp  = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv);
                half3 nTS    = UnpackNormalScale(nSamp, _NormalStr);
                half3 nWS    = normalize(TransformTangentToWorld(nTS,
                                   half3x3(IN.tWS, IN.btWS, IN.nWS)));

                float4 shadowCoord = TransformWorldToShadowCoord(IN.posWS);
                Light  light       = GetMainLight(shadowCoord);
                half   NdotL       = saturate(dot(nWS, light.direction));
                half3  lit         = light.color * light.shadowAttenuation * NdotL;
                half3  ambient     = SampleSH(nWS);

                return half4(albedo * ao * (lit + ambient), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
