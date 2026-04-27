Shader "Teleoperation/FoveatedFeed"
{
    Properties
    {
        _MainTex ("Camera Feed", 2D) = "white" {}
        _GazePoint ("Gaze Point UV", Vector) = (0.5, 0.5, 0, 0)
        _FoveaRadius ("Fovea Radius", Range(0.01, 0.5)) = 0.15
        _TransitionWidth ("Transition Width", Range(0.01, 0.3)) = 0.1
        _PeripheryPixelSize ("Periphery Pixel Size", Range(2, 64)) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // (1/width, 1/height, width, height)
            float4 _GazePoint;         // xy = gaze UV, zw unused
            float _FoveaRadius;
            float _TransitionWidth;
            float _PeripheryPixelSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 gazeUV = _GazePoint.xy;

                // ── Distance from gaze point ────────────────────
                // Correct for aspect ratio so the fovea is circular, not elliptical
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                float2 delta = uv - gazeUV;
                delta.x *= aspect;
                float dist = length(delta);

                // ── Fovea mask ──────────────────────────────────
                // 1.0 inside fovea (sharp), 0.0 in periphery (pixelated)
                float foveaMask = 1.0 - smoothstep(
                    _FoveaRadius,
                    _FoveaRadius + _TransitionWidth,
                    dist
                );

                // ── Foveal sample (original quality — "Quality 50") ─
                fixed4 fovealColor = tex2D(_MainTex, uv);

                // ── Peripheral sample (pixelated — "Quality 5") ─────
                // Snap UV to a coarse grid to create the pixelation effect
                float2 texRes = _MainTex_TexelSize.zw; // (width, height)
                float pixSize = max(_PeripheryPixelSize, 1.0);
                float2 pixelatedUV = floor(uv * texRes / pixSize) * pixSize / texRes;
                // Offset to center of the pixel block
                pixelatedUV += (pixSize * 0.5) / texRes;
                fixed4 peripheryColor = tex2D(_MainTex, pixelatedUV);

                // ── Blend ───────────────────────────────────────
                fixed4 finalColor = lerp(peripheryColor, fovealColor, foveaMask);

                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
