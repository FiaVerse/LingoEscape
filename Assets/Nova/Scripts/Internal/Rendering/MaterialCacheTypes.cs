// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System;
using System.Runtime.CompilerServices;

namespace Nova.Internal.Rendering
{
    [Flags]
    internal enum MaterialModifier
    {
        None = 0,
        ClipRect = 1,
        ClipMask = ClipRect * 2,
        DynamicImage = ClipMask * 2,
        StaticImage = DynamicImage * 2,
        InnerShadow = StaticImage * 2,
        SuperSample = InnerShadow * 2,
        OuterBorder = SuperSample * 2,
        InnerBorder = OuterBorder * 2,
        CenterBorder = InnerBorder * 2,
        RadialFill = CenterBorder * 2,

        IMAGE_MASK = DynamicImage | StaticImage,
    }

    internal struct ShaderDescriptor : IEquatable<ShaderDescriptor>
    {
        public VisualType VisualType;
        public LightingModel LightingModel;
        public PassType PassType;

        public bool PremulColors
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (VisualType & (VisualType.UIBlock2D | VisualType.TEXT_MASK)) != 0;
        }

        public bool IsText
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (VisualType & VisualType.TEXT_MASK) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ShaderDescriptor other)
        {
            return
                VisualType == other.VisualType &&
                LightingModel == other.LightingModel &&
                PassType == other.PassType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetShaderName()
        {
            if (LightingModel == LightingModel.Unlit)
            {
                return $"Hidden/{Constants.ProjectName}/{Constants.ProjectName}{VisualType.ToShaderName()}{LightingModel.ToName()}";
            }
            else
            {
                return $"Hidden/{Constants.ProjectName}/{Constants.ProjectName}{VisualType.ToShaderName()}{LightingModel.ToName()}{PassType.ToName()}";
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + ((int)VisualType).GetHashCode();
            hash = (hash * 7) + ((int)LightingModel).GetHashCode();
            hash = (hash * 7) + ((int)PassType).GetHashCode();
            return hash;
        }
    }

    internal struct MaterialDescriptor : IEquatable<MaterialDescriptor>
    {
        public ShaderDescriptor ShaderDescriptor;
        public MaterialModifier MaterialModifiers;
        public int RenderQueue;
        public bool DisableZTest;
        public TextMaterialID TextMaterialID; // Only used for text

        public bool IsClip
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (MaterialModifiers & (MaterialModifier.ClipRect | MaterialModifier.ClipMask)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(MaterialDescriptor other)
        {
            return
                ShaderDescriptor.Equals(other.ShaderDescriptor) &&
                MaterialModifiers == other.MaterialModifiers &&
                RenderQueue == other.RenderQueue &&
                DisableZTest == other.DisableZTest &&
                TextMaterialID.Equals(other.TextMaterialID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + ShaderDescriptor.GetHashCode();
            hash = (hash * 7) + ((int)MaterialModifiers).GetHashCode();
            hash = (hash * 7) + RenderQueue.GetHashCode();
            hash = (hash * 7) + DisableZTest.GetHashCode();
            hash = (hash * 7) + TextMaterialID.GetHashCode();
            return hash;
        }
    }

    internal static class MaterialCacheTypeUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToShaderName(this VisualType visualType)
        {
            switch (visualType)
            {
                case VisualType.TextBlock:
                case VisualType.TextSubmesh:
                    return "TextBlock";
                case VisualType.UIBlock3D:
                    return "UIBlock3D";
                case VisualType.UIBlock2D:
                    return "UIBlock2D";
                case VisualType.DropShadow:
                    return "DropShadow";
                default:
                    return null;
            }
        }

        /// <summary>
        /// We need this to log correct names after obfuscation
        /// </summary>
        /// <param name="lightingModel"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToName(this LightingModel lightingModel)
        {
            switch (lightingModel)
            {
                case LightingModel.Unlit: return "Unlit";
                case LightingModel.Lambert: return "Lambert";
                case LightingModel.BlinnPhong: return "BlinnPhong";
                case LightingModel.Standard: return "Standard";
                case LightingModel.StandardSpecular: return "StandardSpecular";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// We need this because of obfuscation
        /// </summary>
        /// <param name="passType"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToName(this PassType passType)
        {
            return passType == PassType.Transparent ? "Transparent" : "Opaque";
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlockType ToBlockType(this VisualType visualType)
        {
            switch (visualType)
            {
                case VisualType.UIBlock2D:
                case VisualType.DropShadow:
                    return BlockType.UIBlock2D;
                case VisualType.UIBlock3D:
                    return BlockType.UIBlock3D;
                case VisualType.TextBlock:
                case VisualType.TextSubmesh:
                    return BlockType.Text;
                default:
                    return BlockType.Empty;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToName(this BlockType visualType)
        {
            switch (visualType)
            {
                case BlockType.UIBlock2D:
                    return "UIBlock2D";
                case BlockType.UIBlock3D:
                    return "UIBlock3D";
                case BlockType.Text:
                    return "TextBlock";
                case BlockType.Empty:
                default:
                    return string.Empty;
            }
        }
    }
}
