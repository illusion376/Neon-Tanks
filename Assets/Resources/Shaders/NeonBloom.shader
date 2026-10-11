Shader "Hidden/NeonRicochet/Bloom"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} _BloomTex ("Bloom", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        float4 _MainTex_TexelSize;
        float4 _PrefilterTexelSize;
        float4 _Settings;
        half3 Box(float2 uv, float2 d)
        {
            return (tex2D(_MainTex, uv + d * float2(-.5,-.5)).rgb +
                    tex2D(_MainTex, uv + d * float2(.5,-.5)).rgb +
                    tex2D(_MainTex, uv + d * float2(-.5,.5)).rgb +
                    tex2D(_MainTex, uv + d * float2(.5,.5)).rgb) * .25;
        }
        half4 Extract(v2f_img i) : SV_Target
        {
            // Match the footprint to the capped pyramid, not just to one source pixel.
            half3 color = max(Box(i.uv, _PrefilterTexelSize.xy), 0);
            // Limit firefly energy without clipping channels independently and changing their hue.
            color *= min(1.0,30.0/max(max(color.r,max(color.g,color.b)),.0001));
            half brightness = max(color.r, max(color.g, color.b));
            half knee = _Settings.x * .5;
            half soft = clamp(brightness - _Settings.x + knee, 0, 2 * knee);
            soft = soft * soft / (4 * knee + .0001);
            half contribution = max(soft, brightness - _Settings.x) / max(brightness, .0001);
            return half4(color * contribution, 1);
        }
        half4 Downsample(v2f_img i) : SV_Target { return half4(Box(i.uv, _MainTex_TexelSize.xy), 1); }
        half4 Upsample(v2f_img i) : SV_Target
        {
            return half4(Box(i.uv, _MainTex_TexelSize.xy) + tex2D(_BloomTex, i.uv).rgb * _Settings.w, 1);
        }
        half4 Composite(v2f_img i) : SV_Target
        {
            float3 c = max((tex2D(_MainTex, i.uv).rgb + tex2D(_BloomTex, i.uv).rgb * _Settings.y) * _Settings.z,0);
            // Apply the filmic shoulder to peak intensity, then scale RGB together. Per-channel
            // tone mapping turned the magenta rails pastel/white whenever their green channel grew.
            float peak=max(max(c.r,c.g),c.b);
            float x=min(peak,1000.0);
            float mapped=saturate((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14));
            return half4(c*(mapped/max(peak,.0001)),1);
        }
        float Luma(float3 color) { return dot(color, float3(.299,.587,.114)); }
        half4 Antialias(v2f_img i) : SV_Target
        {
            float2 d = _MainTex_TexelSize.xy;
            float3 center = tex2D(_MainTex, i.uv).rgb;
            float nw = Luma(tex2D(_MainTex, i.uv + d * float2(-1,1)).rgb);
            float ne = Luma(tex2D(_MainTex, i.uv + d * float2(1,1)).rgb);
            float sw = Luma(tex2D(_MainTex, i.uv + d * float2(-1,-1)).rgb);
            float se = Luma(tex2D(_MainTex, i.uv + d * float2(1,-1)).rgb);
            float middle = Luma(center);
            float minimum = min(middle, min(min(nw,ne), min(sw,se)));
            float maximum = max(middle, max(max(nw,ne), max(sw,se)));
            if (maximum-minimum < max(.025, maximum*.12)) return half4(center,1);
            float2 direction = float2(sw+se-nw-ne, ne+se-nw-sw);
            float reduction = max((nw+ne+sw+se)*.03125, .0078125);
            direction = clamp(direction / (min(abs(direction.x),abs(direction.y))+reduction), -6.0, 6.0)*d;
            float3 inner = .5*(tex2D(_MainTex,i.uv-direction/6).rgb+tex2D(_MainTex,i.uv+direction/6).rgb);
            float3 outer = inner*.5+.25*(tex2D(_MainTex,i.uv-direction*.5).rgb+tex2D(_MainTex,i.uv+direction*.5).rgb);
            float value = Luma(outer);
            return half4(value<minimum || value>maximum ? inner : outer, 1);
        }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Extract
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Downsample
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Upsample
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Composite
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Antialias
            #pragma target 3.0
            ENDCG
        }
    }
    Fallback Off
}
