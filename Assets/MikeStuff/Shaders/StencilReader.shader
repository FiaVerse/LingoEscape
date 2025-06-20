// StencilReader.shader
// This is a simple unlit shader that displays a texture, but with one key difference:
// It checks the stencil buffer before drawing. It will only render a pixel
// if the stencil buffer's value matches our reference value (1).

Shader "Unlit/StencilReader"
{
    Properties
    {
        // A main texture property so you can apply an image to your clue.
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            // Standard blend mode for transparency.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            // --- STENCIL BLOCK ---
            // This is the core of the reveal effect.
            Stencil
            {
                Ref 1             // The reference value we are looking for.
                Comp Equal        // The comparison function: only pass if the buffer value is EQUAL to our Ref value.
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                // VR/Stereo Rendering Support
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                // VR/Stereo Rendering Support
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                // VR/Stereo Rendering Support
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the texture and apply the color tint.
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                return col;
            }
            ENDCG
        }
    }
}
