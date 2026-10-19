// The café regulars' surfaces: Unity's physically based lighting, with the colour painted on the model itself
// (skin tones, lips, blush, buttons), optional woven stripes (a galabeya), a soft sheen on cloth and skin,
// and a faint weave so fabric doesn't look like plastic.
Shader "KingsBattle/Character"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.35
        _Metallic ("Metallic", Range(0,1)) = 0
        _StripeColor ("Stripe colour (alpha = strength)", Color) = (0,0,0,0)
        _StripeFreq ("Stripes around", Float) = 60
        _StripeWidth ("Stripe width", Range(0,1)) = 0.22
        _Sheen ("Sheen", Range(0,1)) = 0.2
        _Weave ("Weave", Range(0,1)) = 0
        _MainTex ("Texture (eyes)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _StripeColor;
        half _Glossiness, _Metallic, _StripeFreq, _StripeWidth, _Sheen, _Weave;

        struct Input
        {
            float2 uv_MainTex;
            float4 vcol;
            float2 wrap;      // around the body (0..1), height (meters), from the rest pose
            float3 viewDir;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
            o.wrap = v.texcoord1.xy;
        }

        float hash21 (float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            fixed3 c = _Color.rgb * IN.vcol.rgb * tex.rgb;
            // woven stripes running down the garment
            float s = frac(IN.wrap.x * _StripeFreq);
            float stripe = smoothstep(_StripeWidth + 0.04, _StripeWidth, s) * smoothstep(0.0, 0.04, s);
            c = lerp(c, _StripeColor.rgb, stripe * _StripeColor.a);
            // a faint weave
            float w = hash21(floor(float2(IN.wrap.x * 900, IN.wrap.y * 140)));
            c *= 1 - _Weave * 0.12 * w;
            o.Albedo = c;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness * (1 - _Weave * 0.3 * w);
            // soft sheen at grazing angles (velvety cloth, the glow of skin)
            float rim = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = c * _Sheen * pow(rim, 3) * 0.6;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
