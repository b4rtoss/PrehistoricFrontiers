Shader "Prehistoric Frontiers/Meadow Grass"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0
        float4 _Clearings[64];
        int _ClearingCount;
        struct Input { float4 color : COLOR; };
        void vert(inout appdata_full v)
        {
            float2 world = mul(unity_ObjectToWorld, v.vertex).xz;
            float height = 1;
            for (int i = 0; i < _ClearingCount; i++)
                height = min(height, smoothstep(_Clearings[i].z, _Clearings[i].z + .4, distance(world, _Clearings[i].xy)));
            v.vertex.y *= height;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = IN.color.rgb;
            o.Smoothness = 0;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
