Shader "Storm Gambit/Lobby Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Blur ("Softness", Range(0,0.03)) = 0.012
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Blur;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 d = float2(_Blur, _Blur);
                half3 c = tex2D(_MainTex, i.uv).rgb * .20;
                c += tex2D(_MainTex, i.uv + float2(d.x,0)).rgb * .12;
                c += tex2D(_MainTex, i.uv - float2(d.x,0)).rgb * .12;
                c += tex2D(_MainTex, i.uv + float2(0,d.y)).rgb * .12;
                c += tex2D(_MainTex, i.uv - float2(0,d.y)).rgb * .12;
                c += tex2D(_MainTex, i.uv + d).rgb * .08;
                c += tex2D(_MainTex, i.uv - d).rgb * .08;
                c += tex2D(_MainTex, i.uv + float2(d.x,-d.y)).rgb * .08;
                c += tex2D(_MainTex, i.uv + float2(-d.x,d.y)).rgb * .08;
                return fixed4(lerp(c, half3(.025,.045,.075), .76), .98) * i.color;
            }
            ENDCG
        }
    }
}
