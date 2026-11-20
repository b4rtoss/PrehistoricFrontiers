Shader "Hidden/PrehistoricFrontiers/FogOfWar"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} _FogMask ("Visibility", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _FogMask;
            float3 _BottomLeft, _BottomRight, _TopLeft;
            fixed4 frag(v2f_img input) : SV_Target
            {
                float3 world = _BottomLeft + (_BottomRight - _BottomLeft) * input.uv.x + (_TopLeft - _BottomLeft) * input.uv.y;
                float2 uv = (world.xz + 25) / 50;
                float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                float2 mask = tex2D(_FogMask, uv).rg * inside;
                fixed4 scene = tex2D(_MainTex, input.uv);
                float explored = smoothstep(.1, .9, mask.g), visible = smoothstep(.1, .9, mask.r);
                float3 unknown = float3(.018, .025, .03);
                float luminance = dot(scene.rgb, float3(.2126, .7152, .0722));
                // Explored terrain stays readable; only current vision reveals enemies.
                float3 memory = lerp(scene.rgb, luminance.xxx, .15) * .78;
                return fixed4(lerp(lerp(unknown, memory, explored), scene.rgb, visible), 1);
            }
            ENDCG
        }
    }
}
