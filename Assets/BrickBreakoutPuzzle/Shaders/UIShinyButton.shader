Shader "UI/Shiny Button"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShineColor ("Shine Color", Color) = (1,1,1,1)
        _ShineStrength ("Shine Strength", Range(0, 2)) = 0.8
        _ShineWidth ("Shine Width", Range(0.01, 1)) = 0.12
        _ShineSoftness ("Shine Softness", Range(0.001, 0.5)) = 0.08
        _ShineScale ("Shine Scale", Range(0.25, 4)) = 1
        _ShineRandomness ("Shine Randomness", Range(0, 1)) = 0.25
        _ShineSpeed ("Shine Speed", Range(0, 3)) = 0.35
        _ShineAngle ("Shine Angle", Range(-180, 180)) = -25
        [HideInInspector] _ShineOffset ("Shine Phase Offset", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            Name "UIShinyButton"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            fixed4 _Color;
            fixed4 _ShineColor;
            float _ShineStrength;
            float _ShineWidth;
            float _ShineSoftness;
            float _ShineScale;
            float _ShineRandomness;
            float _ShineSpeed;
            float _ShineAngle;
            float _ShineOffset;
            float _ShineTime;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                float angle = radians(_ShineAngle);
                float2 direction = float2(cos(angle), sin(angle));
                float projectedUV = dot(i.texcoord - 0.5, direction);
                float cycle = _ShineTime * _ShineSpeed + _ShineOffset;
                float cycleIndex = floor(cycle);
                float cycleProgress = frac(cycle);
                float randomValue = frac(sin(cycleIndex * 12.9898 + 78.233) * 43758.5453);
                float randomScale = lerp(0.75, 1.25, randomValue * _ShineRandomness + (1.0 - _ShineRandomness) * 0.5);
                float shinePosition = lerp(-1.5, 1.5, cycleProgress);
                float distanceToBand = abs(projectedUV - shinePosition);
                float bandWidth = _ShineWidth * _ShineScale * randomScale;
                float bandSoftness = _ShineSoftness * _ShineScale * randomScale;
                float shine = 1.0 - smoothstep(bandWidth, bandWidth + bandSoftness, distanceToBand);
                shine *= color.a * _ShineColor.a * _ShineStrength * randomScale;
                color.rgb += _ShineColor.rgb * shine;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
