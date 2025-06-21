// Copyright (c) Supernova Technologies LLC
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// A radial gradient to blend with the body color of a parent element.
    /// </summary>
    /// <seealso cref="UIBlock2D.Gradient"/>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RadialGradient : IEquatable<RadialGradient>
    {
        /// <summary>
        /// The color of the gradient to blend with the body color of its parent element.
        /// </summary>
        [SerializeField]
        public Color Color;
        /// <summary>
        /// The <see cref="Length2"/> configuration for the center position of the gradient in its parent element's local space.
        /// </summary>
        [SerializeField]
        public Length2 Center;
        /// <summary>
        /// A <see cref="Length2"/> configuration for the radii along the gradient's X and Y axes, determines the gradient's size. 
        /// </summary>
        /// <remarks>
        /// Setting a radius to <c>0</c> is equivalent to setting the radius to <c>float.PositiveInfinity</c> and
        /// can be used to achieve a linear-style gradient.
        /// </remarks>
        [SerializeField]
        public Length2 Radius;
        /// <summary>
        /// The counter-clockwise rotation of the gradient (in degrees) around its <see cref="Center"/>.
        /// </summary>
        [SerializeField]
        public float Rotation;
        /// <summary>
        /// A flag to toggle the gradient's visibility.
        /// </summary>
        [SerializeField]
        public bool Enabled;

        public static bool operator ==(RadialGradient lhs, RadialGradient rhs)
        {
            return
                lhs.Color.Equals(rhs.Color) &&
                lhs.Center.Equals(rhs.Center) &&
                lhs.Radius.Equals(rhs.Radius) &&
                lhs.Rotation.Equals(rhs.Rotation) &&
                lhs.Enabled.Equals(rhs.Enabled);
        }
        public static bool operator !=(RadialGradient lhs, RadialGradient rhs) => !(rhs == lhs);

        /// <summary>
        /// Constructs a new <see cref="RadialGradient"/>.
        /// </summary>
        /// <param name="color"></param>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="rotation"></param>
        /// <param name="enabled"></param>
        public RadialGradient(Color color, Length2 center, Length2 radius, float rotation = 0f, bool enabled = true)
        {
            Color = color;
            Center = center;
            Radius = radius;
            Rotation = rotation;
            Enabled = enabled;
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Color.GetHashCode();
            hash = (hash * 7) + Center.GetHashCode();
            hash = (hash * 7) + Radius.GetHashCode();
            hash = (hash * 7) + Rotation.GetHashCode();
            hash = (hash * 7) + Enabled.GetHashCode();
            return hash;
        }

        public override bool Equals(object other)
        {
            if (other is RadialGradient asType)
            {
                return this == asType;
            }

            return false;
        }

        /// <summary>
        /// Equality compare.
        /// </summary>
        /// <param name="other">The other <see cref="RadialGradient"/> to compare.</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(RadialGradient other) => this == other;

        /// <summary>
        /// The string representation of this <see cref="RadialGradient"/>.
        /// </summary>
        public override string ToString()
        {
            if (!Enabled)
            {
                return "Disabled";
            }

            return $"Color = {Color}, Radius = {Radius}";
        }

        internal Calculated Calc(Vector2 relativeTo)
        {
            return new Calculated(this, relativeTo);
        }

        [Obfuscation]
        internal readonly struct Calculated
        {
            public readonly Length2.Calculated Center;
            public readonly Length2.Calculated Radius;

            internal Calculated(RadialGradient gradient, Vector2 relativeTo)
            {
                var center = Internal.Length2.Calc(gradient.Center.ToInternal(), Internal.Length2.MinMax.Unclamped, relativeTo);
                Center = center.ToPublic();

                var radius = Internal.Length2.Calc(gradient.Radius.ToInternal(), Internal.Length2.MinMax.Positive, relativeTo);
                Radius = radius.ToPublic();
            }
        }

        internal static readonly RadialGradient Default = new RadialGradient()
        {
            Enabled = false,
            Color = Color.white,
            Center = new Length2(Length.Percentage(0f), Length.Percentage(0f)),
            Radius = new Length2(Length.Percentage(0.5f), Length.Percentage(0.5f)),
            Rotation = 0f,
        };
    }
}