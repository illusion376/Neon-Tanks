Shader "NeonRicochet/NeonPickup"
{
    Properties
    {
        [HDR] _Color ("Tint", Color) = (0,1,1,1)
        _Mode ("0 hex shield / 1 repair scan / 2 triple-shot icon", Float) = 0
        _Strength ("Shield visibility", Range(0,1)) = 1
        _Pulse ("Shield pulse", Range(0,1)) = 0
        _HexScale ("Hex density", Float) = 5
        _EffectTime ("Effect time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float3 localNormal : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
                float3 viewDir : TEXCOORD4;
                float4 color : COLOR;
            };
            float4 _Color;
            float _Mode, _Strength, _Pulse, _HexScale, _EffectTime;
            float _NeonDetailReduction;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.localPos = v.vertex.xyz;
                o.localNormal = v.normal;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            float HexLines(float2 p)
            {
                float2 cell = float2(1, 1.73205);
                float2 a = frac(p / cell) * cell - cell * .5;
                float2 b = frac((p + cell * .5) / cell) * cell - cell * .5;
                float2 nearest = dot(a, a) < dot(b, b) ? a : b;
                nearest = abs(nearest);
                float edge = .5 - max(nearest.x, dot(nearest, float2(.5, .866025)));
                float aa = max(fwidth(edge), .006);
                return 1 - smoothstep(.014 - aa, .014 + aa, edge);
            }

            float BulletIcon(float2 p)
            {
                float body = length(float2(p.x, p.y - clamp(p.y, -.3, .34))) - .095;
                float trail = length(float2(p.x, p.y + .52)) - .045;
                float tail = length(float2(p.x, p.y + .7)) - .03;
                return min(body, min(trail, tail));
            }

            half4 frag(v2f i) : SV_Target
            {
                float alpha;
                if (_Mode < .5)
                {
                    // Три проекции сохраняют размер сот на куполе без растяжения UV у полюса.
                    float3 weight = pow(abs(normalize(i.localNormal)), 6);
                    weight /= max(weight.x + weight.y + weight.z, .001);
                    float3 p = i.localPos * _HexScale;
                    float hex;
                    // На низкой графике соты считаются в одной проекции вместо трёх.
                    [branch] if (_NeonDetailReduction > 1.5) hex = HexLines(p.xz);
                    else hex = HexLines(p.yz) * weight.x + HexLines(p.xz) * weight.y + HexLines(p.xy) * weight.z;
                    float rim = pow(1 - saturate(abs(dot(normalize(i.worldNormal), normalize(i.viewDir)))), 3);
                    float breathe = .96 + .04 * sin(_EffectTime * 3);
                    alpha = (.018 + hex * .13 + rim * .42) * breathe;
                    alpha += _Pulse * (hex * .2 + rim * .35 + .025);
                    alpha *= _Strength;
                }
                else if (_Mode < 1.5)
                {
                    float2 p = (i.uv - .5) * 2;
                    float fade = smoothstep(0, .12, i.uv.y) * (1 - smoothstep(.8, 1, i.uv.y));
                    float column = pow(saturate(1 - abs(p.x) * 5), 2);
                    float dashUV = frac(i.uv.y * 8 - _EffectTime * 1.8);
                    float dash = smoothstep(.02, .12, dashUV) * (1 - smoothstep(.65, .8, dashUV));
                    float scanY = saturate(_EffectTime / .9);
                    float scan = 1 - smoothstep(.012, .045, abs(i.uv.y - scanY));
                    float panel = pow(saturate(1 - abs(p.x)), 2);
                    alpha = (column * (.1 + .9 * dash) + panel * (.04 + scan * .5)) * fade;
                }
                else
                {
                    float2 p = (i.uv - .5) * 2;
                    float2 left = p - float2(-.38, 0);
                    float2 right = p - float2(.38, 0);
                    // Три снаряда расходятся веером; один и тот же знак виден на арене и над танком.
                    left = float2(.927 * left.x + .375 * left.y, -.375 * left.x + .927 * left.y);
                    right = float2(.927 * right.x - .375 * right.y, .375 * right.x + .927 * right.y);
                    float edge = min(BulletIcon(p - float2(0, .13)), min(BulletIcon(left), BulletIcon(right)));
                    float aa = max(fwidth(edge), .003);
                    float core = 1 - smoothstep(-aa, aa, edge);
                    float glow = (1 - smoothstep(0, .09, edge)) * .12;
                    alpha = saturate(core + glow);
                }
                return half4(_Color.rgb * i.color.rgb, saturate(alpha * _Color.a * i.color.a));
            }
            ENDCG
        }
    }
}
