Shader "Custom/EyeBrightnessControl"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LeftBrightness ("Left Eye Brightness", Range(0, 1)) = 1.0
        _RightBrightness ("Right Eye Brightness", Range(0, 1)) = 1.0
    }
    
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float _LeftBrightness;
            float _RightBrightness;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Unity stores UVs in 0-1 space. [0,0] represents the bottom-left corner of the texture, and [1,1] represents the top-right
                // 
                if (i.uv.x < 0.5)
                {
                    col.rgb *= _LeftBrightness;
                }
                else
                {
                    col.rgb *= _RightBrightness;
                }
                
                return col;
            }
            ENDCG
        }
    }
}