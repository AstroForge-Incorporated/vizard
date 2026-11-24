Shader "Vizard/XenonPlume"
{
    Properties
    {
        [PerRendererData] _Thrust ("Thrust fraction", Range(0, 1)) = 0
        [PerRendererData] _Tint ("Thruster color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 profile : TEXCOORD0;
                float ring : TEXCOORD1;
                float channelEnd : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _Thrust;
            fixed4 _Tint;

            Varyings vert(appdata_base v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = v.vertex.xyz;
                o.ring = v.normal.z > 0.5;
                o.profile = p.xy * 2;
                // The diameter-to-length ratio keeps the nozzle channel at a fixed physical length.
                o.channelEnd = 0.57 * length(unity_ObjectToWorld._m00_m10_m20)
                    / max(length(unity_ObjectToWorld._m02_m12_m22), 1e-6);
                if (v.normal.x > 0.5)
                {
                    // Turn one cube face toward the camera about the exhaust axis.
                    float2 eye = lerp(ObjSpaceViewDir(float4(0, 0, 0, 1)).xy,
                        mul((float3x3)unity_WorldToObject, UNITY_MATRIX_V[2].xyz).xy, unity_OrthoParams.w);
                    o.profile = float2(p.y * 2, 0.5 - p.z);
                    p.xy = float2(-eye.y, eye.x) * (p.y / max(length(eye), 1e-8));
                }
                else if (!o.ring)
                {
                    p = 0; // Only the ribbon and the outlet face contribute triangles.
                }
                o.position = UnityObjectToClipPos(float4(p, 1));
                return o;
            }

            fixed4 frag(Varyings i) : SV_Target
            {
                float density;
                float3 color;
                if (i.ring > 0.5)
                {
                    float edge = (length(i.profile) - 0.76) / 0.16;
                    density = exp(-edge * edge);
                    color = float3(0.75, 0.98, 1);
                }
                else
                {
                    float z = max(0, i.profile.y - i.channelEnd);
                    float width = exp(-2.6 * z);
                    float r = abs(i.profile.x) / width;
                    float core = exp(-8 * r * r);
                    density = (exp(-2 * r * r) + 0.6 * core) * exp(-5 * z)
                        * (1 - smoothstep(0.8, 1, abs(i.profile.x))) * (1 - smoothstep(0.65, 1, i.profile.y));
                    color = lerp(float3(0.35, 0.80, 1), float3(0.70, 0.95, 1), core * exp(-5 * z));
                }
                return fixed4(color * _Tint.rgb, saturate(density) * _Thrust * _Tint.a);
            }
            ENDCG
        }
    }
}
