// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    #region UIBlock2D
    [StructLayout(LayoutKind.Sequential)]
    internal struct SubQuadVert
    {
        public float2 Pos;
        public ShaderIndex BlockDataIndex;
        public float EdgeSoftenMask;

        public float2 UVZoom;
        public float2 CenterUV;
    }

    /// <summary>
    /// This is the data for a 2D UIBlock that is non dependent on position
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct UIBlock2DShaderData
    {
        public float2 QuadSize;
        public float2 GradientCenter;

        public float2 GradientSizeReciprocal;
        public float2 GradientRotationSinCos;

        public float2 ShadowOffset;
        public float CornerRadius;
        public float ShadowWidth;

        public ShaderIndex TransformIndex;
        public ShaderIndex TexturePackSlice;
        public float BorderWidth;
        public float ShadowBlur;

        public float2 RadialFillCenter;
        public float RadialFillRotation;
        public float RadialFillAngle;

        public ShaderColor PrimaryColor;

        public ShaderColor GradientColor;

        public ShaderColor ShadowColor;

        public ShaderColor BorderColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PerInstanceDropShadowShaderData
    {
        public float2 Offset;
        public float2 HalfBlockQuadSize;

        public float Width;
        public float Blur;
        public float BlockClipRadius;
        public float RadialFillRotation;

        public ShaderIndex TransformIndex;
        public float EdgeSoftenMask;
        public float2 RadialFillCenter;

        public float RadialFillAngle;
        private float3 _padding;

        public ShaderColor Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PerQuadDropShadowShaderData
    {
        public float2 PositionInNode;
        public float2 QuadSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AllQuadsDropShadowShaderData
    {
        public PerQuadDropShadowShaderData V0;
        public PerQuadDropShadowShaderData V1;
        public PerQuadDropShadowShaderData V2;
        public PerQuadDropShadowShaderData V3;
        public PerQuadDropShadowShaderData V4;
        public PerQuadDropShadowShaderData V5;
        public PerQuadDropShadowShaderData V6;
        public PerQuadDropShadowShaderData V7;
    }
    #endregion

    #region UIBlock3D
    [StructLayout(LayoutKind.Sequential)]
    internal struct UIBlock3DVertData
    {
        public float3 VertPos;
        public float3 Normal;
        public float3 CornerOffsetDir;
        public float3 EdgeOffsetDir;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UIBlock3DShaderData
    {
        public float3 Size;
        public float CornerRadius;

        public float EdgeRadius;
        public ShaderIndex TransformIndex;
        private float2 _padding;

        public ShaderColor Color;
    }
    #endregion

    #region TextBlock
    [StructLayout(LayoutKind.Sequential)]
    internal struct PerVertTextShaderData
    {
        public float3 Position;
        public ShaderIndex TransformIndex;

        public float2 Texcoord0;
        public float2 Texcoord1;

        public float ScaleMultiplier;
        private float3 _padding;

        public ShaderColor Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PerCharacterTextShaderData
    {
        public PerVertTextShaderData V0;
        public PerVertTextShaderData V1;
        public PerVertTextShaderData V2;
        public PerVertTextShaderData V3;

        public ShaderIndex TransformIndex
        {
            set
            {
                V0.TransformIndex = value;
                V1.TransformIndex = value;
                V2.TransformIndex = value;
                V3.TransformIndex = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyScaleDelta(float delta)
        {
            V0.ScaleMultiplier = delta;
            V1.ScaleMultiplier = delta;
            V2.ScaleMultiplier = delta;
            V3.ScaleMultiplier = delta;
        }

        public unsafe ref PerVertTextShaderData this[int index]
        {
            get
            {
                fixed (PerCharacterTextShaderData* array = &this) { return ref ((PerVertTextShaderData*)array)[index]; }
            }
        }
    }
    #endregion

    #region Lighting Data
    [StructLayout(LayoutKind.Sequential)]
    internal struct BlinnPhongShaderData
    {
        public float Specular;
        public float Gloss;
        private float2 _padding;

        private float4 _padding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StandardShaderData
    {
        public float Smoothness;
        public float Metallic;
        private float2 _padding;

        private float4 _padding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StandardSpecularShaderData
    {
        public float Smoothness;
        private float3 _padding;

        public ShaderColor SpecularColor;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct LightingShaderDataUnion
    {
        [FieldOffset(0)]
        public BlinnPhongShaderData BlinnPhong;
        [FieldOffset(0)]
        public StandardShaderData Standard;
        [FieldOffset(0)]
        public StandardSpecularShaderData StandardSpecular;
    }
    #endregion

    #region Shared
    [StructLayout(LayoutKind.Sequential)]
    internal struct TransformAndLightingData
    {
        public float4x4 RootFromBlock;
        public LightingShaderDataUnion Lighting;
    }
    #endregion
}
