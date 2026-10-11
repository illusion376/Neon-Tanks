Shader "NeonRicochet/NeonSurface"
{
    Properties
    {
        _Color ("Surface", Color) = (.04,.08,.16,1)
        [HDR] _EmissionColor ("HDR emission", Color) = (0,0,0,1)
        _Rim ("Rim intensity", Range(0,2)) = .25
        _Metallic ("Metallic", Range(0,1)) = .4
        _Glossiness ("Glossiness", Range(0,1)) = .6
        _AmbientLift ("Scenery ambient fill", Range(0,1)) = 0
        _ReceiveProjectileLights ("Receive projectile lights", Float) = 1
        _Grid ("Floor grid", Float) = 0
        [HDR] _GridColor ("Purple floor grid", Color) = (.45,.12,.58,1)
        _GridScale ("Grid cells per meter", Float) = .7407407
        _GridEmission ("Grid brightness", Float) = .30
        _ArenaBounds ("Arena min X / max X / min Z / max Z", Vector) = (-11.2,11.2,-5.4,5.7)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };
            float4 _Color, _EmissionColor, _GridColor, _ArenaBounds;
            float _Rim, _Metallic, _Glossiness, _Grid, _GridScale, _GridEmission;
            float _AmbientLift, _ReceiveProjectileLights;
            // One bounded set of short-range lights shared by all scene materials.
            float4 _NeonProjectileLights[8];
            float4 _NeonProjectileColors[8];
            int _NeonProjectileLightCount;
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal); return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                // All color inputs are linear radiance. Runtime materials use SetVector so HDR
                // intensity is multiplied after sRGB decoding, never decoded a second time.
                float3 albedo=max(_Color.rgb,0);
                float3 n=normalize(i.normal), v=normalize(_WorldSpaceCameraPos-i.world);
                float metallic=saturate(_Metallic), smoothness=saturate(_Glossiness);
                float3 key=normalize(float3(-.52,.78,-.35));
                float3 fill=normalize(float3(.62,.32,.71));
                float ndl=saturate(dot(n,key)), ndv=saturate(dot(n,v));
                float hemisphere=saturate(n.y*.5+.5);

                // The key separates top/front/side planes. Restrained hemispheric and rear fill
                // keep the unlit faces readable without flattening every face into a pink wash.
                float3 ambient=lerp(float3(.055,.045,.085),float3(.19,.25,.36),hemisphere);
                // Scenery gets a cool room fill; gameplay materials retain the original contrast.
                ambient+=float3(.72,.82,1)*saturate(_AmbientLift);
                float3 lighting=ambient+float3(.76,.86,1)*ndl*3.2;
                lighting+=float3(.21,.12,.32)*saturate(dot(n,fill))*.65;
                float3 color=albedo*lighting*(1-metallic*.38);

                // A broad polished lobe plus a smaller gloss lobe reveal the imported bevels.
                // No screen-space noise or fine procedural stripes: roughness remains stable on mobile.
                float3 halfDirection=normalize(key+v);
                float ndh=saturate(dot(n,halfDirection));
                float broad=pow(ndh,lerp(8,28,smoothness));
                float gloss=pow(ndh,lerp(20,128,smoothness));
                float3 reflectance=lerp(float3(.04,.04,.04),albedo,metallic);
                float fresnel=pow(1-ndv,5);
                reflectance=lerp(reflectance,float3(.7,.78,.9),fresnel*.45);
                color+=reflectance*float3(.76,.86,1)*(broad*.65+gloss*2.6)*ndl;
                float rim=pow(1-ndv,3)*max(_Rim,0);
                color+=float3(.075,.11,.19)*rim*(.35+.65*hemisphere);

                // Only a narrow, normal-dependent frame bounce remains. The previous large corner
                // lights added magenta even to surfaces facing away and destroyed material depth.
                float edgeDistance=min(min(abs(i.world.x-_ArenaBounds.x),abs(i.world.x-_ArenaBounds.y)),
                    min(abs(i.world.z-_ArenaBounds.z),abs(i.world.z-_ArenaBounds.w)));
                float frameBounce=exp2(-edgeDistance*1.8)*(.2+.8*saturate(n.y));
                color+=albedo*float3(.21,.045,.32)*frameBounce;
                // Decorative blocks and the lower plane use the room fill instead of short-range
                // projectile lights. Their shared materials skip this expensive full-screen work.
                [branch] if(_ReceiveProjectileLights>.5)
                {
                    [unroll] for(int lightIndex=0;lightIndex<8;lightIndex++)
                    {
                        if(lightIndex<_NeonProjectileLightCount)
                        {
                            float3 offset=_NeonProjectileLights[lightIndex].xyz-i.world;
                            float distanceSquared=max(dot(offset,offset),.001);
                            float radius=max(_NeonProjectileLights[lightIndex].w,.01);
                            float falloff=saturate(1-distanceSquared/(radius*radius));
                            falloff*=falloff;
                            float lambert=saturate(dot(n,offset*rsqrt(distanceSquared)));
                            float3 radiance=_NeonProjectileColors[lightIndex].rgb*_NeonProjectileColors[lightIndex].w;
                            color+=radiance*falloff*lambert*(albedo*2.4+reflectance*.45);
                        }
                    }
                }
                if(_Grid>.5)
                {
                    float2 p=i.world.xz*_GridScale;
                    float2 g=abs(frac(p-.5)-.5)/max(fwidth(p),.0001);
                    float grid=1-saturate(min(g.x,g.y));
                    color+=grid*_GridColor.rgb*_GridEmission;
                }
                // Emissive rails stay independent of surface orientation and remain distinctly colored.
                return half4(color+max(_EmissionColor.rgb,0),1);
            }
            ENDCG
        }
    }
}
