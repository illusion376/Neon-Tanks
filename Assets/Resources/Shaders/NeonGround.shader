Shader "NeonRicochet/NeonGround"
{
    Properties
    {
        [HDR] _Color ("Tint", Color) = (0,1,1,1)
        _Mode ("0 glow / 1 ring / 2 solid / 3 beam / 4 spark / 5 shadow", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            float4 _Color; float _Mode;
            v2f vert(appdata v)
            { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            half4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2; float radius=length(p); float a;
                if(_Mode<.5) a=pow(saturate(1-radius),2.6);
                else if(_Mode<1.5)
                {
                    a=1-smoothstep(.025,.075,abs(radius-.84));
                    float angle=atan2(p.y,p.x);
                    a*=step(.22,frac(angle*1.90986));
                }
                else if(_Mode<2.5) a=1;
                else if(_Mode<3.5) a=pow(saturate(1-abs(p.y)),1.5);
                else if(_Mode<4.5)
                {
                    float rays=pow(saturate(1-abs(p.x)*7),2)*saturate(1-abs(p.y));
                    rays+=pow(saturate(1-abs(p.y)*7),2)*saturate(1-abs(p.x));
                    a=saturate(rays+pow(saturate(1-radius),5));
                }
                else
                {
                    // Contact shadows use ordinary alpha blending (SrcAlpha, OneMinusSrcAlpha).
                    // Fade to exact zero before the quad border so there is no rectangular silhouette.
                    a=exp2(-radius*radius*4)*(1-smoothstep(.65,1,radius));
                    return half4(0,0,0,a*saturate(_Color.a*i.color.a));
                }
                return half4(_Color.rgb*i.color.rgb,a*saturate(_Color.a*i.color.a));
            }
            ENDCG
        }
    }
}
