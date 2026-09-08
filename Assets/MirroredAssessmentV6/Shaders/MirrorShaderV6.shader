Shader "Meta/PCA/MirrorShaderV6"
{
    Properties
    {
        _MainTex  ("Texture", 2D) = "white" {}
        // > 1 zooms out (shows more of the camera image); < 1 zooms in.
        _UVScale  ("UV Scale", Float) = 1.0
        // Fraction of the LEFT screen that shows the mirrored reflection (0-0.5).
        // 0 = no mirror, 0.5 = left half mirrors right half.
        _SplitX   ("Mirror Split", Float) = 0.5
        // Rotation applied to the mirrored (left) side only, in degrees.
        _MirrorRotation ("Mirror Rotation", Float) = 0.0
    }
    SubShader
    {
        // Geometry queue so it composites naturally over the passthrough background.
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        LOD 100

        // Pass 1: paint the entire screen black before the floor mirror renders.
        // The floor plane's local vertices map to clip-space [-1,1] corners,
        // covering the full viewport per eye without needing a separate quad.
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

            struct appdata_b
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f_b
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f_b vert_black(appdata_b v)
            {
                v2f_b o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Unity Quad local verts are in [-0.5, 0.5]; multiply by 2 to reach
                // clip-space [-1, 1] corners — full screen, ignoring world transform.
                o.vertex = float4(v.vertex.xy * 2.0, 0, 1);
                return o;
            }

            fixed4 frag_black(v2f_b i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return fixed4(0, 0, 0, 1);
            }
            ENDCG
        }

        // Pass 2: draw the floor mirror; discard out-of-range fragments so the
        // black from Pass 1 shows through wherever the camera image doesn't reach.
        Pass
        {
            ZWrite On
            ZTest LEqual
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
                float4 vertex       : SV_POSITION;
                // Screen position computed from the MONO camera VP so both eyes
                // sample identical UVs — eliminates stereo double-vision.
                float4 monoScreenPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float     _UVScale;
            float     _SplitX;
            float     _MirrorRotation;
            // Set every frame from C# using the main camera's (mono) VP matrix.
            float4x4  _MonoCameraVP;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Per-eye clip position — correct stereo depth for the floor plane.
                o.vertex = UnityObjectToClipPos(v.vertex);

                // Mono UV: project world position through the shared center-camera VP
                // so the UV is identical for both eyes, preventing double vision.
                float4 worldPos  = mul(unity_ObjectToWorld, v.vertex);
                float4 monoClip  = mul(_MonoCameraVP, worldPos);
                o.monoScreenPos  = ComputeScreenPos(monoClip);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 screenUV = i.monoScreenPos.xy / i.monoScreenPos.w;

                // Draw a 2-pixel black line at the split boundary.
                float pixelWidth = fwidth(screenUV.x);
                if (abs(screenUV.x - _SplitX) < pixelWidth * 1)
                    return fixed4(0, 0, 0, 1);

                // Left of split: flip X to mirror what the camera currently sees.
                // Right of split: use X as-is for the normal passthrough view.
                // Both stable — only update when the camera image changes, not with head rotation.
                float uvX = (screenUV.x < _SplitX) ? (1.0 - screenUV.x) : screenUV.x;

                float2 uv = float2(uvX, 1.0 - screenUV.y);

                // Rotate the mirrored (left) side by _MirrorRotation degrees (clockwise).
                if (screenUV.x < _SplitX)
                {
                    float angle = _MirrorRotation * (3.14159265 / 180.0);
                    float cosA = cos(angle);
                    float sinA = sin(angle);
                    float2 c = uv - 0.5;
                    uv = float2(cosA * c.x + sinA * c.y, -sinA * c.x + cosA * c.y) + 0.5;
                }

                uv = (uv - 0.5) * _UVScale + 0.5;

                // Discard fragments outside the valid camera image to avoid stretched edges.
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    discard;

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
