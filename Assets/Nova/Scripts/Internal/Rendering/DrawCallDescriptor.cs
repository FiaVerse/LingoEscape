// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// A description for one or more draw calls. This helps with knowing 
    /// which shader properties/key words to use for draw calls and determining
    /// if two blocks can be batched (if their DrawCallDescriptors match).
    /// </summary>
    internal struct DrawCallDescriptor : IEquatable<DrawCallDescriptor>, IEquatable<TexturePackID>, IEquatable<TextMaterialID>
    {
        public VisualModifierID VisualModifierID;
        public int GameObjectLayer;
        public VisualType DrawCallType;
        public MaterialModifier MaterialModifiers;
        public UIBlock2DDescriptor UIBlock2D;
        public UIBlock3DDescriptor UIBlock3D;
        public TextDescriptor Text;
        public SurfaceDescriptor Surface;

        public static readonly DrawCallDescriptor Default = new DrawCallDescriptor()
        {
            VisualModifierID = VisualModifierID.Invalid,
            GameObjectLayer = 0,
            DrawCallType = VisualType.Invalid,
            MaterialModifiers = MaterialModifier.None,
            UIBlock2D = UIBlock2DDescriptor.Default,
            UIBlock3D = UIBlock3DDescriptor.Default,
            Text = TextDescriptor.Default,
            Surface = SurfaceDescriptor.Default,
        };

        public PassType PassType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => DrawCallType == VisualType.UIBlock3D ? UIBlock3D.PassType : PassType.Transparent;
        }

        public bool HasVisualModifier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => VisualModifierID.IsValid;
        }

        public bool IsLit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Surface.Model != LightingModel.Unlit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Equals(DrawCallDescriptor other)
        {
            if (VisualModifierID != other.VisualModifierID ||
                GameObjectLayer != other.GameObjectLayer ||
                DrawCallType != other.DrawCallType ||
                MaterialModifiers != other.MaterialModifiers)
            {
                return false;
            }

            if (!Surface.Equals(other.Surface))
            {
                return false;
            }

            switch (DrawCallType)
            {
                case VisualType.UIBlock2D:
                    return UIBlock2D.Equals(other.UIBlock2D);
                case VisualType.UIBlock3D:
                    return UIBlock3D.Equals(other.UIBlock3D);
                case VisualType.TextBlock:
                case VisualType.TextSubmesh:
                    return Text.Equals(other.Text);
                case VisualType.DropShadow:
                    return true;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TexturePackID other)
        {
            if (DrawCallType != VisualType.UIBlock2D || (MaterialModifiers & MaterialModifier.StaticImage) == 0)
            {
                return false;
            }
            return UIBlock2D.TexturePackID == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TextMaterialID other)
        {
            if ((DrawCallType & VisualType.TEXT_MASK) == 0)
            {
                return false;
            }

            return Text.Equals(other);
        }

        public struct SurfaceDescriptor : IEquatable<SurfaceDescriptor>
        {
            public LightingModel Model;
            public ShadowCastingMode ShadowCastingMode;
            public bool ReceiveShadows;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(SurfaceDescriptor other)
            {
                return Model == other.Model &&
                    ShadowCastingMode == other.ShadowCastingMode &&
                    ReceiveShadows == other.ReceiveShadows;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator SurfaceDescriptor(Surface surface)
            {
                if (surface.LightingModel == LightingModel.Unlit)
                {
                    return new SurfaceDescriptor()
                    {
                        Model = LightingModel.Unlit,
                        ReceiveShadows = false,
                        ShadowCastingMode = ShadowCastingMode.Off,
                    };
                }
                else
                {
                    return new SurfaceDescriptor()
                    {
                        Model = surface.LightingModel,
                        ReceiveShadows = surface.ReceiveShadows,
                        ShadowCastingMode = surface.ShadowCastingMode,
                    };
                }
            }

            public static readonly SurfaceDescriptor Default = new SurfaceDescriptor()
            {
                Model = LightingModel.Unlit,
                ReceiveShadows = false,
                ShadowCastingMode = ShadowCastingMode.Off,
            };
        }

        public struct UIBlock2DDescriptor : IEquatable<UIBlock2DDescriptor>
        {
            /// <summary>
            /// Only valid if TextureType is static
            /// </summary>
            public TexturePackID TexturePackID;
            /// <summary>
            /// Only valid if texture type is dynamic
            /// </summary>
            public TextureID TextureID;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(UIBlock2DDescriptor other)
            {
                return TexturePackID == other.TexturePackID && TextureID == other.TextureID;
            }

            public static readonly UIBlock2DDescriptor Default = new UIBlock2DDescriptor()
            {
                TexturePackID = TexturePackID.Invalid,
                TextureID = TextureID.Invalid,
            };
        }

        public struct UIBlock3DDescriptor : IEquatable<UIBlock3DDescriptor>
        {
            public PassType PassType;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(UIBlock3DDescriptor other) => PassType == other.PassType;

            public static readonly UIBlock3DDescriptor Default = new UIBlock3DDescriptor()
            {
                PassType = PassType.Opaque,
            };
        }

        public struct TextDescriptor : IEquatable<TextDescriptor>, IEquatable<TextMaterialID>
        {
            public TextMaterialID MaterialID;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(TextDescriptor other)
            {
                return MaterialID == other.MaterialID;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(TextMaterialID other)
            {
                return MaterialID == other;
            }

            public static readonly TextDescriptor Default = new TextDescriptor()
            {
                MaterialID = TextMaterialID.Invalid,
            };
        }
    }
}