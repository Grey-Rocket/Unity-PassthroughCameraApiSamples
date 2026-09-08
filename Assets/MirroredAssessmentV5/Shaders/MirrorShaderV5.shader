Shader "Meta/PCA/MirrorShaderV5"
{
    Properties
    {
        _MainTex     ("Texture", 2D)    = "white" {}
        // > 1 zooms out (shows more of the camera image); < 1 zooms in.
        _UVScale     ("UV Scale", Float) = 2.0
        // 0 = left half is source, right half is its mirror (default).
        // 1 = right half is source, left half is its mirror.
        _MirrorRight ("Mirror Right Side", Float) = 0.0
        // Positive = gap between the two halves; negative = they overlap at centre.
        _Separation  ("Separation", Float) = 0.0
        // Half-extents of the centre square in quad UV space (set from C#).
        _SquareHalfX ("Square Half X", Float) = 0.133
        _SquareHalfY ("Square Half Y", Float) = 0.133
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        LOD 100

        // Pass 1: fill the entire quad solid black.
        // This guarantees the border area is opaque black in the framebuffer,
        // blocking any passthrough compositor layer behind it.
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert_black
            #pragma fragment frag_black
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_black
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f_black
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f_black vert_black(appdata_black v)
            {
                v2f_black o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag_black(v2f_black i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return fixed4(0, 0, 0, 1);
            }
            ENDCG
        }

        // Pass 2: draw the camera panels inside the centre square; discard everything
        // else so Pass 1's black remains visible in the border area.
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
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float     _UVScale;
            float     _MirrorRight;
            float     _Separation;
            float     _SquareHalfX;
            float     _SquareHalfY;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv = i.uv;

                // Discard outside the centre square — Pass 1's black shows through.
                if (uv.x < 0.5 - _SquareHalfX || uv.x > 0.5 + _SquareHalfX ||
                    uv.y < 0.5 - _SquareHalfY  || uv.y > 0.5 + _SquareHalfY)
                    discard;

                // _Separation shifts the two panels apart (positive) or together (negative).
                float halfSep    = _Separation * 0.5;
                float leftBound  = 0.5 - halfSep;
                float rightBound = 0.5 + halfSep;

                float uvX;
                if (_MirrorRight < 0.5)
                {
                    // Left panel: source half of camera (0 → 0.5).
                    // Right panel: mirror of source (0.5 → 0).
                    if (uv.x <= leftBound)
                    {
                        float t = (leftBound > 0.0) ? (uv.x / leftBound) : 0.0;
                        uvX = t * 0.5;
                    }
                    else if (uv.x >= rightBound)
                    {
                        float t = (rightBound < 1.0) ? ((uv.x - rightBound) / (1.0 - rightBound)) : 1.0;
                        uvX = (1.0 - t) * 0.5;
                    }
                    else
                        discard;
                }
                else
                {
                    // Left panel: mirror of right source (1 → 0.5).
                    // Right panel: source half of camera (0.5 → 1).
                    if (uv.x <= leftBound)
                    {
                        float t = (leftBound > 0.0) ? (uv.x / leftBound) : 0.0;
                        uvX = 1.0 - t * 0.5;
                    }
                    else if (uv.x >= rightBound)
                    {
                        float t = (rightBound < 1.0) ? ((uv.x - rightBound) / (1.0 - rightBound)) : 1.0;
                        uvX = 0.5 + t * 0.5;
                    }
                    else
                        discard;
                }

                float2 camUV = float2(uvX, uv.y);

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
