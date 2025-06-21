// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nova.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RadialFill : IEquatable<RadialFill>
    {
        public Length2 Center;
        /// <summary>
        /// In degrees
        /// </summary>
        public float Rotation;
        /// <summary>
        /// In degrees. Positive is counter-clockwise, negative is clockwise.
        /// </summary>
        public float FillAngle;
        private bool Enabled;

        public bool EnabledAndNot360
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Enabled && !Utilities.Math.ApproximatelyEqual(Utilities.Math.Abs(FillAngle), 360f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RadialFill other)
        {
            return
                Center.Equals(ref other.Center) &&
                Rotation.Equals(other.Rotation) &&
                FillAngle.Equals(other.FillAngle) &&
                Enabled.Equals(other.Enabled);
        }
    }
}

