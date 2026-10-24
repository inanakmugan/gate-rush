// Gate Rush, Module 46: the board's lit sprites under the URP 2D Renderer.
// URP's Sprite-Unlit-Default with two changes, set per material by
// Gate Rush -> Generate Art:
//   _ColorScale multiplies the tinted colour, so a lit face or stud stored
//   divided by the art recipe's Shade Headroom comes back above 1 where lit;
//   _Premultiply 1 with Blend One OneMinusSrcAlpha adds the colour times its
//   alpha and leaves the target's alpha, for the white highlight.
// Unlit, so it needs no Light 2D. Like Sprite-Unlit-Default it declares no
// stencil, so a SpriteRenderer's mask interaction (the gate clip, the peel)
// applies to it unchanged; sorting, vertex colour and alpha fades too.
Shader "Gate Rush/Board Sprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorScale ("Color Scale", Float) = 1
        _Premultiply ("Premultiply", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

        // Legacy properties, as in Sprite-Unlit-Default.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // One layout for every material, so the SRP batcher can batch them.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _ColorScale;
                half _Premultiply;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);
                color.rgb *= _ColorScale;
                return half4(lerp(color.rgb, color.rgb * color.a, _Premultiply), lerp(color.a, 0, _Premultiply));
            }
            ENDHLSL
        }
    }
}
