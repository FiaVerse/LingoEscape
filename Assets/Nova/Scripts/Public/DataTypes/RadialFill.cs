// Copyright (c) Supernova Technologies LLC
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RadialFill : IEquatable<RadialFill>
    {
        /// <summary>
        /// The <see cref="Length2"/> configuration for the center position/origin of the radial fill.
        /// </summary>
        [SerializeField]
        public Length2 Center;
        /// <summary>
        /// The angle (in degrees from the positive x-axis) that serves as the basis rotation from which
        /// <see cref="FillAngle">FillAngle</see> will apply.<br/>
        /// - Positive => counter-clockwise<br/>
        /// - Negative => clockwise
        /// </summary>
        /// <remarks>
        /// The value is clamped between <c>-360</c> and <c>360</c>.
        /// </remarks>
        [SerializeField]
        [Range(-360f, 360f)]
        public float Rotation;
        /// <summary>
        /// The arc angle (in degrees) of the fill.<br/>
        /// - Positive => counter-clockwise<br/>
        /// - Negative => clockwise
        /// </summary>
        /// <remarks>
        /// The value is clamped between <c>-360</c> and <c>360</c>.
        /// </remarks>
        [SerializeField]
        [Range(-360f, 360f)]
        public float FillAngle;
        /// <summary>
        /// A flag to enable/disable the radial fill.
        /// </summary>
        [SerializeField]
        public bool Enabled;

        /// <summary>
        /// The end angle of the visible arc (where <see cref="Rotation">Rotation</see> is the start angle). Mathematically: <br/>
        /// <see cref="EndAngle">EndAngle</see> = <see cref="Rotation">Rotation</see> + <see cref="FillAngle">FillAngle</see>
        /// </summary>
        public float EndAngle => Rotation + FillAngle;

        public bool Equals(RadialFill other)
        {
            return
                Center.Equals(other.Center) &&
                Rotation.Equals(other.Rotation) &&
                FillAngle.Equals(other.FillAngle) &&
                Enabled.Equals(other.Enabled);
        }

        public static bool operator ==(RadialFill lhs, RadialFill rhs) => lhs.Equals(rhs);
        public static bool operator !=(RadialFill lhs, RadialFill rhs) => !lhs.Equals(rhs);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Center.GetHashCode();
            hash = (hash * 7) + Rotation.GetHashCode();
            hash = (hash * 7) + FillAngle.GetHashCode();
            hash = (hash * 7) + Enabled.GetHashCode();
            return hash;
        }

        public override bool Equals(object other)
        {
            if (other is RadialFill asType)
            {
                return this == asType;
            }

            return false;
        }

        public override string ToString()
        {
            if (!Enabled)
            {
                return "Disabled";
            }

            return $"Center = {Center}, Rotation = {Rotation}, FillAngle = {FillAngle}";
        }

        internal Calculated Calc(Vector2 relativeTo)
        {
            return new Calculated(this, relativeTo);
        }

        [Obfuscation]
        internal readonly struct Calculated
        {
            public readonly Length2.Calculated Center;

            internal Calculated(RadialFill fill, Vector2 relativeTo)
            {
                var center = Internal.Length2.Calc(fill.Center.ToInternal(), Internal.Length2.MinMax.Unclamped, relativeTo);
                Center = center.ToPublic();
            }
        }

        internal static readonly RadialFill Default = new RadialFill()
        {
            FillAngle = 90f,
            Enabled = false,
            Center = Length2.Percentage(0, 0)
        };
    }
}
