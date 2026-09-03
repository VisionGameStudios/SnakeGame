Shader "Snake/SkinColor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SkinColor ("Skin Color", Color) = (0.52, 0.9, 0.3, 1)
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
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
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _SkinColor;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                #ifdef PIXELSNAP_ON
                output.vertex = UnityPixelSnap(output.vertex);
                #endif
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.texcoord) * input.color;
                fixed strongestOther = max(source.r, source.b);
                fixed greenAmount = saturate((source.g - strongestOther) * 5.0);
                fixed brightness = max(source.r, max(source.g, source.b));
                fixed targetPeak = max(_SkinColor.r, max(_SkinColor.g, _SkinColor.b));
                fixed3 recolored = _SkinColor.rgb * (brightness / max(targetPeak, 0.001));
                source.rgb = lerp(source.rgb, recolored, greenAmount);
                source.rgb *= source.a;
                return source;
            }
            ENDCG
        }
    }
}
