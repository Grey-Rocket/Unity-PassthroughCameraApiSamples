Shader "Meta/PCA/MirrorShaderV5"
{
    Properties
    {
        _MainTex     ("Texture", 2D)    = "white" {}
        // > 1 zooms out (shows more of the camera image); < 1 zooms in.
        _UVScale     ("UV Scale", Float) = 2.0
        // 0 = left panel is normal, right panel is mirrored (default).
        // 1 = left panel is mirrored, right panel is normal.
        _MirrorRight ("Mirror Right Side", Float) = 0.0
        // Positive = gap between panels; negative = panels overlap at centre.
        _Separation  ("Separation", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        LOD 100

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float     _UVScale;
            float     _MirrorRight;
            float     _Separation;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex    = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 screen = i.screenPos.xy / i.screenPos.w;

                // ComputeScreenPos returns per-eye [0,1] on Quest — use directly.
                float eyeX = screen.x;
                float eyeY = screen.y;

                // Panel bounds — _Separation opens (positive) or closes (negative) the gap.
                float halfSep    = _Separation * 0.5;
                float leftBound  = 0.5 - halfSep;
                float rightBound = 0.5 + halfSep;

                // t = normalised position within the panel (0→1).
                // Each panel samples the FULL camera width so neither feels thin.
                float t;
                if (eyeX <= leftBound)
                    t = (leftBound > 0.0) ? (eyeX / leftBound) : 0.0;
                else if (eyeX >= rightBound)
                    t = (rightBound < 1.0) ? ((eyeX - rightBound) / (1.0 - rightBound)) : 1.0;
                else
                    discard;

                // Left panel = normal view, right panel = mirrored (swap with _MirrorRight).
                float uvX;
                if (eyeX <= leftBound)
                    uvX = (_MirrorRight < 0.5) ? t : (1.0 - t);
                else
                    uvX = (_MirrorRight < 0.5) ? (1.0 - t) : t;

                float2 camUV = float2(uvX, eyeY);

                // Apply zoom around the centre of the camera image.
                camUV = (camUV - 0.5) * _UVScale + 0.5;

                if (camUV.x < 0.0 || camUV.x > 1.0 || camUV.y < 0.0 || camUV.y > 1.0)
                    discard;

                return tex2D(_MainTex, camUV);
            }
            ENDCG
        }
    }
}
