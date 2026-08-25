Shader "Meta/PCA/MirrorShaderV3"
{
    Properties
    {
        _MainTex  ("Texture", 2D) = "white" {}
        // > 1 zooms out (shows more of the camera image); < 1 zooms in.
        _UVScale  ("UV Scale", Float) = 1.5
        // Fraction of the LEFT screen that shows the mirrored reflection (0-0.5).
        // 0 = no mirror, 0.5 = left half mirrors right half.
        _SplitX   ("Mirror Split", Float) = 0.5
    }
    SubShader
    {
        // Geometry queue so it composites naturally over the passthrough background.
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        LOD 100

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

                // Left of split: flip X so this region mirrors the right half.
                // Right of split: use X as-is for the normal camera view.
                float uvX = (screenUV.x < _SplitX) ? (1.0 - screenUV.x) : screenUV.x;

                float2 uv;
                uv.x = uvX;
                uv.y = 1.0 - screenUV.y;  // Quest camera is vertically flipped
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
