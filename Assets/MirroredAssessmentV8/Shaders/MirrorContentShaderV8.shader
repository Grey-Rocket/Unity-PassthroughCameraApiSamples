// Replacement shader for the mirror's content camera. Everything that camera draws
// is rendered with this, whatever material the object actually carries.
//
// Two reasons it has to work this way rather than by assigning a material:
//
// 1. Alpha. MirrorShaderV8 composites virtual content over the passthrough image
//    using this texture's alpha as the mask, and the stock hand material
//    (OculusSampleAlphaHandOutline) blends with
//    "Blend SrcAlpha OneMinusSrcAlpha, Zero One" — the "Zero One" half never writes
//    alpha, so hands drawn with it stay invisible in the composite.
// 2. OVRMeshRenderer and OVRSkeletonRenderer cache their material at startup and
//    re-assign it every frame, so swapping sharedMaterial is reverted a frame later.
//    A replacement shader bypasses the material entirely, so they cannot fight it.
//
// Applied with an empty replacement tag, so every object uses the single SubShader
// below regardless of its own RenderType.
Shader "Meta/PCA/MirrorContentV8"
{
    Properties
    {
        // Under a replacement shader the defaults below are never applied. A property
        // comes from the replaced object's own material if it defines it, and from the
        // *global* shader value otherwise. The cube's material defines _BaseColor, so
        // it keeps its colour; the hand material does not, so hands read the global
        // _BaseColor, which MirroredAssessmentManagerV8 sets to its hand colour (an
        // unset global is black). That is where the hand/target colour split comes from.
        _BaseColor ("Base Color", Color) = (1.0, 0.85, 0.72, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            // Base pass so this draws exactly once per object in forward rendering.
            Tags { "LightMode"="ForwardBase" }
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex      : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _BaseColor;

            // Floor on the shading term, so faces turned away stay readable. A constant
            // rather than a property, since no material would supply it.
            static const float kAmbient = 0.35;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Hemisphere shading off the world normal rather than the scene's
                // lights: the mirror only needs readable shape, and this cannot be
                // broken by light setup or by which lighting pass gets selected.
                float up = dot(normalize(i.worldNormal), float3(0, 1, 0)) * 0.5 + 0.5;
                fixed3 col = _BaseColor.rgb * lerp(kAmbient, 1.0, up);

                // Alpha 1: this is the mask MirrorShaderV8 blends with.
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
