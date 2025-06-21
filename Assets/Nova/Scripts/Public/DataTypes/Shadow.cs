// Copyright (c) Supernova Technologies LLC
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Specifies the direction for a shadow to expand.
    /// </summary>
    /// <seealso cref="UIBlock2D.Shadow"/>
    /// <seealso cref="Shadow"/>
    public enum ShadowDirection
    {
        /// <summary>
        /// The shadow expands outward from the edge of the body as a drop shadow.
        /// </summary>
        Out = Internal.ShadowDirection.Out,
        /// <summary>
        /// The shadow expands inward from the edge of the body as an inner shadow.
        /// </summary>
        In = Internal.ShadowDirection.In,
    }

    /// <summary>
    /// A drop shadow, inner shadow, or glow effect.
    /// </summary>
    /// <seealso cref="UIBlock2D.Shadow"/>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Shadow : IEquatable<Shadow>
    {
        /// <summary>
        /// The color of the shadow. Darker colors create a more shadow-like effect, whereas brighter colors create a more glow-like effect.
        /// </summary>
        [SerializeField]
        public Color Color;
        /// <summary>
        /// The <see cref="Length"/> configuration for the width of the shadow <i>before</i> the <see cref="Blur"/> is applied.
        /// </summary>
        [SerializeField]
        public Length Width;
        /// <summary>
        /// The <see cref="Length"/> configuration for the blur of the shadow. A larger blur leads to a softer effect, whereas a smaller blur leads to a sharper effect.
        /// </summary>
        [SerializeField]
        public Length Blur;
        /// <summary>
        /// The <see cref="Length2"/> configuration for the center offset of the shadow in its parent element's local space.
        /// </summary>
        [SerializeField]
        public Length2 Offset;
        /// <summary>
        /// A flag to toggle the visibility of the shadow.
        /// </summary>
        [SerializeField]
        public bool Enabled;
        /// <summary>
        /// The direction the shadow will expand as <see cref="Width"/> or <see cref="Blur"/> increases. Either <see cref="ShadowDirection.In"/> or <see cref="ShadowDirection.Out"/>.
        /// </summary>
        [SerializeField]
        public ShadowDirection Direction;

        /// <summary>
        /// Constructs a new <see cref="Shadow"/>
        /// </summary>
        /// <param name="color"></param>
        /// <param name="width"></param>
        /// <param name="blur"></param>
        /// <param name="offset"></param>
        /// <param name="shadowDirection"></param>
        /// <param name="enabled"></param>
        public Shadow(Color color, Length width, Length blur, Length2 offset, ShadowDirection shadowDirection = ShadowDirection.Out, bool enabled = true)
        {
            Color = color;
            Width = width;
            Blur = blur;
            Offset = offset;
            Direction = shadowDirection;
            Enabled = enabled;
        }

        public static bool operator ==(Shadow lhs, Shadow rhs)
        {
            return
                lhs.Color.Equals(rhs.Color) &&
                lhs.Width.Equals(rhs.Width) &&
                lhs.Blur.Equals(rhs.Blur) &&
                lhs.Offset.Equals(rhs.Offset) &&
                lhs.Enabled.Equals(rhs.Enabled) &&
                lhs.Direction == rhs.Direction;
        }
        public static bool operator !=(Shadow lhs, Shadow rhs) => !(rhs == lhs);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Color.GetHashCode();
            hash = (hash * 7) + Width.GetHashCode();
            hash = (hash * 7) + Blur.GetHashCode();
            hash = (hash * 7) + Offset.GetHashCode();
            hash = (hash * 7) + Enabled.GetHashCode();
            hash = (hash * 7) + Direction.GetHashCode();
            return hash;
        }

        public override bool Equals(object other)
        {
            if (other is Shadow asType)
            {
                return this == asType;
            }

            return false;
        }

        /// <summary>
        /// The string representation of this <see cref="Shadow"/>.
        /// </summary>
        public override string ToString()
        {
            if (!Enabled)
            {
                return "Disabled";
            }

            return $"Color = {Color}, Width = {Width}, Blur = {Blur}, Direction = {Direction}";
        }

        /// <summary>
        /// Equality compare.
        /// </summary>
        /// <param name="other">The other <see cref="Shadow"/> to compare.</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(Shadow other) => this == other;

        internal Calculated Calc(Vector2 relativeTo)
        {
            return new Calculated(this, relativeTo);
        }

        [Obfuscation]
        internal readonly struct Calculated
        {
            public readonly Length.Calculated Width;
            public readonly Length.Calculated Blur;
            public readonly Length2.Calculated Offset;

            internal Calculated(Shadow shadow, Vector2 relativeTo)
            {
                float relative1D = math.cmin(relativeTo) * 0.5f;
                var width = new Internal.Length.Calculated(shadow.Width.ToInternal(), Internal.Length.MinMax.Unclamped, relative1D);
                Width = width.ToPublic();
                var blur = new Internal.Length.Calculated(shadow.Blur.ToInternal(), Internal.Length.MinMax.Positive, relative1D);
                Blur = blur.ToPublic();
                var offset = Internal.Length2.Calc(shadow.Offset.ToInternal(), Internal.Length2.MinMax.Unclamped, relativeTo);
                Offset = offset.ToPublic();
            }
        }

        internal readonly static Shadow Default = new Shadow()
        {
            Enabled = false,
            Color = new Color(0, 0, 0, 0.5f),
            Direction = ShadowDirection.Out,
            Offset = Length2.FixedValue(0, -1),
            Width = Length.Zero,
            Blur = Length.FixedValue(2),
        };
    }
}