// Copyright (c) Supernova Technologies LLC
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Specifies the direction for a border to expand.
    /// </summary>
    /// <seealso cref="UIBlock2D.Border"/>
    /// <seealso cref="Border"/>
    public enum BorderDirection
    {
        /// <summary>
        /// The border expands outward.
        /// </summary>
        Out = Internal.BorderDirection.Out,
        /// <summary>
        /// The border is centered on the edge, expands both inward and outward.
        /// </summary>
        Center = Internal.BorderDirection.Center,
        /// <summary>
        /// The border expands inward.
        /// </summary>
        In = Internal.BorderDirection.In,
    }

    /// <summary>
    /// A visual border to draw around a parent element.
    /// </summary>
    /// <seealso cref="UIBlock2D.Border"/>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Border : IEquatable<Border>
    {
        /// <summary>
        /// The color of the border.
        /// </summary>
        [SerializeField]
        public Color Color;
        /// <summary>
        /// The <see cref="Length"/> configuration for the width of the border.
        /// </summary>
        [SerializeField]
        public Length Width;
        /// <summary>
        /// A flag to toggle the visibility of the border.
        /// </summary>
        [SerializeField]
        public bool Enabled;
        /// <summary>
        /// The direction the border will expand as <see cref="Width"/> increases. Either <see cref="BorderDirection.In"/>, <see cref="BorderDirection.Center"/>, or <see cref="BorderDirection.Out"/>.
        /// </summary>
        [SerializeField]
        public BorderDirection Direction;

        /// <summary>
        /// Constructs a new <see cref="Border"/>.
        /// </summary>
        /// <param name="color"></param>
        /// <param name="width"></param>
        /// <param name="enabled"></param>
        /// <param name="borderDirection"></param>
        public Border(Color color, Length width, bool enabled = true, BorderDirection borderDirection = BorderDirection.Out)
        {
            Color = color;
            Width = width;
            Enabled = enabled;
            Direction = borderDirection;
        }

        internal static readonly Border Default = new Border()
        {
            Enabled = false,
            Color = Color.white,
            Width = Length.One,
            Direction = BorderDirection.In,
        };

        public static bool operator ==(Border lhs, Border rhs)
        {
            return
                lhs.Color.Equals(rhs.Color) &&
                lhs.Width.Equals(rhs.Width) &&
                lhs.Enabled.Equals(rhs.Enabled) &&
                lhs.Direction == rhs.Direction;

        }

        public static bool operator !=(Border lhs, Border rhs) => !(rhs == lhs);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Color.GetHashCode();
            hash = (hash * 7) + Width.GetHashCode();
            hash = (hash * 7) + Enabled.GetHashCode();
            hash = (hash * 7) + Direction.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Equality compare.
        /// </summary>
        /// <param name="other">The other <see cref="Border"/> to compare.</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public override bool Equals(object other)
        {
            if (other is Border border)
            {
                return this == border;
            }

            return false;
        }

        /// <summary>
        /// Equality compare.
        /// </summary>
        /// <param name="other">The other <see cref="Border"/> to compare.</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(Border other) => this == other;

        /// <summary>
        /// The string representation of this <see cref="Border"/>.
        /// </summary>
        public override string ToString()
        {
            if (!Enabled)
            {
                return "Disabled";
            }

            return $"Color = {Color}, Width = {Width}, Direction = {Direction}";
        }

        internal Calculated Calc(float relativeTo)
        {
            return new Calculated(Width, relativeTo);
        }

        [Obfuscation]
        internal readonly struct Calculated
        {
            public readonly Length.Calculated Width;

            internal Calculated(Length borderWidth, float relativeTo)
            {
                var internalLength = new Internal.Length.Calculated(borderWidth.ToInternal(), Internal.Length.MinMax.Positive, relativeTo);
                Width = internalLength.ToPublic();
            }
        }
    }
}