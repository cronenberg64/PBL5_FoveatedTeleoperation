Shader "Teleoperation/FoveatedFeed"
{
    Properties
    {
        _MainTex ("Camera Feed (Periph)", 2D) = "white" {}
        _FoveaTex ("Fovea Patch", 2D) = "black" {}
        _CropRect ("Crop Rect (x,y,w,h)", Vector) = (0, 0, 0, 0)
        _GazePoint ("Gaze Point UV", Vector) = (0.5, 0.5, 0, 0)
        _FoveaRadius ("Fovea Radius", Range(0.01, 0.5)) = 0.15
        _TransitionWidth ("Transition Width", Range(0.01, 0.3)) = 0.1
        _PeripheryPixelSize ("Periphery Blur Radius", Range(1, 64)) = 15
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
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _FoveaTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // (1/width, 1/height, width, height)
            float4 _CropRect;
            float4 _GazePoint;         // xy = gaze UV, zw unused
            float _FoveaRadius;
            float _TransitionWidth;
            float _PeripheryPixelSize;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv = i.uv;
                float2 gazeUV = _GazePoint.xy;

                // Sync the mask with the actual received network payload, not instantaneous gaze
                float cropYFromBottom = _MainTex_TexelSize.w - _CropRect.y - _CropRect.w;
                if (_CropRect.z > 0.5) 
                {
                    gazeUV.x = (_CropRect.x + _CropRect.z * 0.5) / _MainTex_TexelSize.z;
                    gazeUV.y = (cropYFromBottom + _CropRect.w * 0.5) / _MainTex_TexelSize.w;
                }

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

                // ── Foveal sample (sharp crop or fallback) ─
                fixed4 fovealColor;
                if (_CropRect.z > 0.5) // if width > 0 (valid crop)
                {
                    // Map uv to FoveaTex coordinates
                    // OpenCV crop_y is from the TOP. Unity uv.y is from the BOTTOM.
                    // So cropY from bottom = ImageHeight - crop_y(top) - crop_h

                    float cropU = (uv.x * _MainTex_TexelSize.z - _CropRect.x) / _CropRect.z;
                    float cropV = (uv.y * _MainTex_TexelSize.w - cropYFromBottom) / _CropRect.w;

                    if (cropU >= 0.0 && cropU <= 1.0 && cropV >= 0.0 && cropV <= 1.0)
                    {
                        fovealColor = tex2D(_FoveaTex, float2(cropU, cropV));
                    }
                    else
                    {
                        fovealColor = tex2D(_MainTex, uv);
                    }
                }
                else
                {
                    fovealColor = tex2D(_MainTex, uv);
                }

                // ── Peripheral sample (smooth golden-angle spiral disk blur) ─
                float2 texelSize = _MainTex_TexelSize.xy * max(_PeripheryPixelSize, 0.5);
                fixed4 peripheryColor = fixed4(0, 0, 0, 0);
                float totalWeight = 0.0;
                
                // 16-tap golden angle spiral distribution for high-quality smooth blur
                const float goldenAngle = 2.39996323; // (3 - sqrt(5)) * pi
                
                for (int j = 0; j < 16; j++)
                {
                    float radius = sqrt((float)j / 15.0); // uniform area distribution
                    float angle = j * goldenAngle;
                    float2 offset = float2(cos(angle), sin(angle)) * radius * texelSize;
                    peripheryColor += tex2D(_MainTex, uv + offset);
                    totalWeight += 1.0;
                }
                peripheryColor /= totalWeight;

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
