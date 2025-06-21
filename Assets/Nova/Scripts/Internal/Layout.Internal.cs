// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal
{
    internal enum Axis
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 3
    }

    internal static class AxisIndex
    {
        public static int Index(this Axis axis)
        {
            switch (axis)
            {
                case Axis.X:
                    return 0;
                case Axis.Y:
                    return 1;
                case Axis.Z:
                    return 2;
            }

            return -1;
        }
    }

    internal enum AutoSize
    {
        None = 0,
        Expand = 1,
        Shrink = 2,
    }

    /// <summary>
    /// The configurable set of layout properties used for sizing/positioning elements in a hierarchy
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [System.Serializable]
    internal struct Layout
    {
        /// <summary>
        /// The 3D size configuration of this <see cref="Layout"/>
        /// </summary>
        public Length3 Size;
        /// <summary>
        /// The 3D position configuration of this <see cref="Layout"/>
        /// </summary>
        public Length3 Position;
        /// <summary>
        /// The 3D padding configuration of this <see cref="Layout"/>
        /// </summary>
        /// <remarks>
        /// Padding is space applied inwards from the size bounds
        /// </remarks>
        public LengthBounds Padding;
        /// <summary>
        /// The 3D margin configuration of this <see cref="Layout"/>
        /// </summary>
        /// <remarks>
        /// Margin is space applied outwards from the size bounds
        /// </remarks>
        public LengthBounds Margin;

        /// <summary>
        /// The min/max range used to clamp <see cref="Size"/> during the layout calculation process 
        /// </summary>
        public Length3.MinMax SizeMinMax;

        /// <summary>
        /// The min/max range used to clamp <see cref="Position"/> during the layout calculation process 
        /// </summary>
        public Length3.MinMax PositionMinMax;
        /// <summary>
        /// The min/max range used to clamp <see cref="Padding"/> during the layout calculation process 
        /// </summary>
        public LengthBounds.MinMax PaddingMinMax;
        /// <summary>
        /// The min/max range used to clamp <see cref="Margin"/> during the layout calculation process 
        /// </summary>
        public LengthBounds.MinMax MarginMinMax;

        /// <summary>
        /// The 3D alignment configuration for this <see cref="Layout"/>
        /// </summary>
        /// <remarks><see cref="Position"/> is a configured offset from the point of alignment per-axis </remarks>
        public int3 Alignment;

        /// <summary>
        /// Allows <see cref="Size"/> to automatically adapt to the size of a parent <see cref="Layout"/> or set of child <see cref="Layout">Layouts</see>
        /// </summary>
        public AutoSize3 AutoSize;

        /// <summary>
        /// A flag indicating to the layout system to account for the owner's rotation when performing certain <see cref="Size"/> and <see cref="Position"/> calculations 
        /// </summary>
        public bool RotateSize;

        /// <summary>
        /// The aspect ratio of this UIBlock
        /// </summary>
        public AspectRatio AspectRatio;
    }

#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    [StructLayout(LayoutKind.Sequential)]
    internal struct AspectRatio
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        public float3 Ratio;
        public Axis Axis;

        public bool IsLocked => Axis == Axis.X || Axis == Axis.Y || Axis == Axis.Z;

        public int2 ConstrainedAxesIndices
        {
            get
            {
                switch (Axis)
                {
                    case Axis.Y:
                        return Math.AxisIndices.xz;
                    case Axis.Z:
                        return Math.AxisIndices.xy;
                    default:
                        return Math.AxisIndices.yz;
                }
            }
        }

        public static bool operator ==(AspectRatio lhs, AspectRatio rhs)
        {
            return lhs.Axis == rhs.Axis && math.all(lhs.Ratio == rhs.Ratio);
        }

        public static bool operator !=(AspectRatio lhs, AspectRatio rhs)
        {
            return lhs.Axis != rhs.Axis || math.any(lhs.Ratio != rhs.Ratio);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    [System.Serializable]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    internal struct AutoSize3
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        internal const int SizeOfAutoSize = sizeof(AutoSize);
        internal const int SizeOf = 3 * SizeOfAutoSize;

        [FieldOffset(0 * SizeOfAutoSize)]
        [SerializeField]
        public AutoSize X;

        [FieldOffset(1 * SizeOfAutoSize)]
        [SerializeField]
        public AutoSize Y;

        [FieldOffset(2 * SizeOfAutoSize)]
        [SerializeField]
        public AutoSize Z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AutoSize3(AutoSize all)
        {
            X = all;
            Y = all;
            Z = all;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AutoSize3(AutoSize x, AutoSize y, AutoSize z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public AutoSize this[int axis]
        {
            readonly get
            {
                switch (axis)
                {
                    case 0:
                        return X;
                    case 1:
                        return Y;
                    case 2:
                        return Z;

                }

                return AutoSize.None;
            }
            set
            {
                switch (axis)
                {
                    case 0:
                        X = value;
                        break;
                    case 1:
                        Y = value;
                        break;
                    case 2:
                        Z = value;
                        break;

                }
            }
        }

        public static implicit operator AutoSize3(AutoSize autoSize)
        {
            return new AutoSize3(autoSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 operator ==(AutoSize l, AutoSize3 rhs)
        {
            return new bool3(rhs.X == l, rhs.Y == l, rhs.Z == l);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 operator !=(AutoSize l, AutoSize3 rhs)
        {
            return new bool3(rhs.X != l, rhs.Y != l, rhs.Z != l);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 operator ==(AutoSize3 lhs, AutoSize rhs)
        {
            return new bool3(lhs.X == rhs, lhs.Y == rhs, lhs.Z == rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 operator !=(AutoSize3 lhs, AutoSize rhs)
        {
            return new bool3(lhs.X != rhs, lhs.Y != rhs, lhs.Z != rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(AutoSize3 lhs, AutoSize3 rhs)
        {
            return lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(AutoSize3 lhs, AutoSize3 rhs)
        {
            return !(lhs == rhs);
        }

        public readonly bool3 None
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this == AutoSize.None;
        }

        public readonly bool3 Shrink
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this == AutoSize.Shrink;
        }

        public readonly bool3 Expand
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this == AutoSize.Expand;
        }

        public readonly bool3 RelativeToParent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this != AutoSize.None & this != AutoSize.Shrink;
        }

        public override string ToString()
        {
            return $"AutoSize3({X.ToString()}, {Y.ToString()}, {Z.ToString()})";
        }
    }
}
