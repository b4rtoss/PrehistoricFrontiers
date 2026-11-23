Shader "Prehistoric Frontiers/Ground Surface"
{
    Properties { _MainTex ("Ground map", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        struct Input { float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = tex2D(_MainTex, (IN.worldPos.xz + 25) / 50).rgb;
            o.Smoothness = 0;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
