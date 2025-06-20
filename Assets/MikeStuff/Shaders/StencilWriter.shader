// StencilWriter.shader
// This shader doesn't draw anything visible. Its only purpose is to write
// a specific reference value (in this case, 1) to the stencil buffer
// wherever the object it's attached to is rendered.

Shader "Unlit/StencilWriter"
{
    Properties
    {
        // No properties needed, as it's an invisible effect shader.
    }
    SubShader
    {
        // Set the render queue to Transparent to ensure it renders after opaque objects.
        Tags { "Queue"="Transparent" }
        LOD 100

        Pass
        {
            // Don't write to the color buffer (make it invisible).
            ColorMask 0
            // Don't write to the depth buffer, so it doesn't block objects behind it.
            ZWrite Off

            // --- STENCIL BLOCK ---
            // This is the core of the effect.
            Stencil
            {
                Ref 1             // Set the reference value to 1.
                Comp Always       // Always run the stencil check (and pass).
                Pass Replace      // If the check passes, replace the stencil buffer value with our Ref value (1).
            }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                // NEW: VR/Stereo Rendering Support
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // NEW: VR/Stereo Rendering Support
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                // NEW: VR/Stereo Rendering Support
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Return a blank color since this shader should be invisible
                return fixed4(0,0,0,0);
            }
            ENDCG
        }
    }
}
