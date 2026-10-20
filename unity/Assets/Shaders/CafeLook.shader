// The café's look, applied to the whole picture after it's drawn: a soft glow round bright things (bloom),
// filmic tone mapping, a warm grade with a little more colour, and a gentle darkening at the edges.
Shader "KingsBattle/CafeLook"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Bloom ("Glow", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _Bloom;
    float4 _MainTex_TexelSize;
    float _Threshold, _BloomAmount, _Exposure, _Saturation, _Vignette;
    float3 _Warm;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    // 1: keep only what's brighter than the threshold (softly), at a quarter size
    half4 fragBright (v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        half3 c = (tex2D(_MainTex, i.uv + d * float2(-1, -1)).rgb + tex2D(_MainTex, i.uv + d * float2(1, -1)).rgb
                 + tex2D(_MainTex, i.uv + d * float2(-1, 1)).rgb + tex2D(_MainTex, i.uv + d * float2(1, 1)).rgb) * 0.25;
        c = min(c, 2.5);   // one tiny very bright highlight must not bloom into a cloud
        half b = max(c.r, max(c.g, c.b));
        half k = saturate((b - _Threshold) / max(0.0001, b));
        return half4(c * k, 1);
    }
    // 2: blur (run across, then down, by the script)
    float2 _Dir;
    half4 fragBlur (v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy * _Dir;
        half3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
        c += (tex2D(_MainTex, i.uv + d * 1.385).rgb + tex2D(_MainTex, i.uv - d * 1.385).rgb) * 0.316;
        c += (tex2D(_MainTex, i.uv + d * 3.231).rgb + tex2D(_MainTex, i.uv - d * 3.231).rgb) * 0.070;
        return half4(c, 1);
    }
    // 3: put it together
    half3 ACES (half3 x) { return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)); }
    half4 fragFinal (v2f i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_Bloom, i.uv).rgb * _BloomAmount;
        c *= _Exposure * _Warm;
        c = ACES(c);
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(l.xxx, c, _Saturation);
        float2 v = i.uv - 0.5;
        c *= 1 - _Vignette * dot(v, v) * 1.6;
        return half4(c, 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBright
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFinal
            ENDCG }
    }
    FallBack Off
}
