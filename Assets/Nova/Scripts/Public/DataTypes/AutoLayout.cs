// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// X, Y, or Z axis.
    /// </summary>
    public enum Axis
    {
        /// <summary>
        /// Represents an invalid or disabled value.
        /// </summary>
        None = Internal.Axis.None,
        /// <summary>
        /// X Axis.
        /// </summary>
        X = Internal.Axis.X,
        /// <summary>
        /// Y Axis.
        /// </summary>
        Y = Internal.Axis.Y,
        /// <summary>
        /// Z Axis.
        /// </summary>
        Z = Internal.Axis.Z,
    }

    /// <summary>
    /// A static utility class to assist converting between an <see cref="Axis"/> and an index.
    /// </summary>
    public static class AxisIndex
    {
        /// <summary>
        /// Extension to convert an <see cref="Axis"/> to an index.
        /// </summary>
        /// <param name="axis">
        /// <see cref="Axis.X"/> => 0<br/>
        /// <see cref="Axis.Y"/> => 1<br/>
        /// <see cref="Axis.Z"/> => 2<br/>
        /// Anything else => -1
        /// </param>
        /// <returns>The index of the axis</returns>
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

        /// <summary>
        /// Extension to convert an <see cref="Axis"/> to an index when applicable.
        /// </summary>
        /// <param name="axis">      
        /// <see cref="Axis.X"/> => 0<br/>
        /// <see cref="Axis.Y"/> => 1<br/>
        /// <see cref="Axis.Z"/> => 2<br/></param>
        /// <param name="index">The index of <paramref name="axis"/></param>
        /// <returns><see langword="true"/> if <paramref name="axis"/> == <see cref="Axis.X"/>, <see cref="Axis.Y"/>, or <see cref="Axis.Z"/></returns>
        public static bool TryGetIndex(this Axis axis, out int index)
        {
            index = axis.Index();
            return index != -1;
        }

        /// <summary>
        /// Get the <see cref="Axis"/> mapped to the given <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The index to convert into an <see cref="Axis"/><br/>
        /// 0 => <see cref="Axis.X"/><br/>
        /// 1 => <see cref="Axis.Y"/><br/>
        /// 2 => <see cref="Axis.Z"/><br/>
        /// Anything else => <see cref="Axis.None"/></param>
        /// <returns><paramref name="index"/> as an <see cref="Axis"/></returns>
        public static Axis GetAxis(int index)
        {
            switch (index)
            {
                case 0:
                    return Axis.X;
                case 1:
                    return Axis.Y;
                case 2:
                    return Axis.Z;
            }

            return Axis.None;
        }
    }

    /// <summary>
    /// A secondary axis configuration of an <see cref="AutoLayout"/>. Children will first be positioned
    /// along the cross axis before wrapping to the primary axis, a.k.a. the <see cref="AutoLayout"/>.<see cref="AutoLayout.Axis">Axis</see>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [System.Serializable]
    public struct CrossLayout : System.IEquatable<CrossLayout>
    {
        /// <summary>
        /// The axis along which children are positioned before wrapping to the primary axis.
        /// </summary>
        [SerializeField]
        public Axis Axis;

        /// <summary>
        /// The <see cref="Length"/> configuration inserted between children along the cross axis.
        /// </summary>
        [SerializeField]
        public Length Spacing;

        /// <summary>
        /// The Min/Max values to clamp <see cref="Spacing"/>.
        /// </summary>
        [SerializeField]
        public MinMax SpacingMinMax;

        /// <summary>
        /// A flag to indicate <see cref="Spacing"/> should automatically adjust relative to the space available in the parent container.
        /// </summary>
        [SerializeField]
        public bool AutoSpace;

        /// <summary>
        /// Inverts the order children are positioned such that the child with the highest <c>transform.siblingIndex</c> is positioned first.
        /// </summary>
        [SerializeField]
        public bool ReverseOrder;

        [SerializeField, HideInInspector, NotKeyable]
        private int alignment;

        /// <summary>
        /// Makes the cross axis size of "Expanded" elements as well as "Auto" spacing more uniform, so the remaining overflow items better align to implicit grid cells.
        /// </summary>
        /// <remarks>
        /// If the configured minimum size of a given "Expanded" child is larger than the configured minimum size of one or more "Expanded" siblings, the child will span
        /// across multiple grid cells.<br/><br/>
        /// Does not impact the size of elements not set to <see cref="AutoSize.Expand"/> along the cross axis.
        /// </remarks>
        [SerializeField]
        public bool ExpandToGrid;

        /// <summary>
        /// Alignment [-1, 1] of the children along the <see cref="AutoLayout.Cross"/> <see cref="Axis">Axis</see>.
        /// </summary>
        /// 
        /// <seealso cref="HorizontalAlignment"/>
        /// <seealso cref="VerticalAlignment"/>
        /// <seealso cref="DepthAlignment"/>
        /// <seealso cref="Nova.Alignment"/>
        public int Alignment { get { return alignment; } set { alignment = Math.Clamp(value, -1, 1); } }

        /// <summary>
        /// False if Axis == Axis.None, otherwise true.
        /// </summary>
        public readonly bool Enabled
        {
            get
            {
                return Axis.TryGetIndex(out int _);
            }
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if all fields of <paramref name="lhs"/> are equal to all field of <paramref name="rhs"/></returns>
        public static bool operator ==(CrossLayout lhs, CrossLayout rhs)
        {
            return lhs.Axis == rhs.Axis &&
                   lhs.AutoSpace == rhs.AutoSpace &&
                   lhs.ReverseOrder == rhs.ReverseOrder &&
                   lhs.Alignment == rhs.Alignment &&
                   lhs.ExpandToGrid == rhs.ExpandToGrid &&
                   lhs.Spacing == rhs.Spacing &&
                   lhs.SpacingMinMax == rhs.SpacingMinMax;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if any field of <paramref name="lhs"/> is <b>not</b> equal to its corresponding field of <paramref name="rhs"/></returns>
        public static bool operator !=(CrossLayout lhs, CrossLayout rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The <see cref="CrossLayout"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public override bool Equals(object other)
        {
            if (other is CrossLayout autolayout)
            {
                return this == autolayout;
            }

            return false;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The <see cref="CrossLayout"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(CrossLayout other)
        {
            return this == other;
        }

        /// <summary>The hashcode for this <see cref="CrossLayout"/></summary>
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + ((int)Axis).GetHashCode();
            hash = (hash * 7) + Spacing.GetHashCode();
            hash = (hash * 7) + SpacingMinMax.GetHashCode();
            hash = (hash * 7) + AutoSpace.GetHashCode();
            hash = (hash * 7) + ReverseOrder.GetHashCode();
            hash = (hash * 7) + Alignment.GetHashCode();
            hash = (hash * 7) + ExpandToGrid.GetHashCode();
            return hash;
        }

        /// <summary>
        /// The string representation of this <see cref="CrossLayout"/>.
        /// </summary>
        public override string ToString()
        {
            if (Axis == Axis.None)
            {
                return "Disabled";
            }

            string order = ReverseOrder ? "Reverse" : "Default";
            string align = Axis == Axis.X ? ((HorizontalAlignment)Alignment).ToString() :
                           Axis == Axis.Y ? ((VerticalAlignment)Alignment).ToString() :
                           ((DepthAlignment)Alignment).ToString();

            string space = AutoSpace ? "Auto" : Spacing.ToString();

            return $"Axis = {Axis}, Align = {align}, Spacing = {space}, Order = {order}";
        }

        /// <summary>
        /// A disabled <see cref="CrossLayout"/>.
        /// </summary>
        public static readonly CrossLayout Disabled = new CrossLayout() { Alignment = -1, Spacing = Length.Zero, SpacingMinMax = MinMax.Positive };
    }

    /// <summary>
    /// Automatically position children sequentially along the X, Y, or Z axis
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [System.Serializable]
    public struct AutoLayout : System.IEquatable<AutoLayout>
    {
        /// <summary>
        /// The primary axis along which children are positioned.
        /// </summary>
        [SerializeField]
        public Axis Axis;

        /// <summary>
        /// The <see cref="Length"/> configuration inserted between children.
        /// </summary>
        [SerializeField]
        public Length Spacing;

        /// <summary>
        /// The Min/Max values to clamp <see cref="Spacing"/>.
        /// </summary>
        [SerializeField]
        public MinMax SpacingMinMax;

        /// <summary>
        /// A flag to indicate <see cref="Spacing"/> should automatically adjust relative to the space available in the parent container.
        /// </summary>
        [SerializeField]
        public bool AutoSpace;

        /// <summary>
        /// Inverts the order children are positioned such that the child with the highest <c>transform.siblingIndex</c> is positioned first.
        /// </summary>
        [SerializeField]
        public bool ReverseOrder;

        [SerializeField, HideInInspector, NotKeyable]
        private int alignment;

        /// <summary>
        /// Alignment [-1, 1] of the children along the <see cref="AutoLayout"/> <see cref="Axis">Axis</see>.
        /// </summary>
        /// 
        /// <seealso cref="HorizontalAlignment"/>
        /// <seealso cref="VerticalAlignment"/>
        /// <seealso cref="DepthAlignment"/>
        /// <seealso cref="Nova.Alignment"/>
        public int Alignment
        {
            readonly get
            {
                return alignment;
            }
            set
            {
                alignment = Math.Clamp(value, -1, 1);
            }
        }

        /// <summary>
        /// An offset applied to all children positioned by this <see cref="AutoLayout"/> along the <see cref="AutoLayout"/> <see cref="Axis">Axis</see>.
        /// </summary>
        [SerializeField]
        public float Offset;

        /// <summary>
        /// A secondary axis configuration of an <see cref="AutoLayout"/>. Children will first be positioned
        /// along the cross axis before wrapping to the primary axis, a.k.a. the <see cref="AutoLayout"/>.<see cref="AutoLayout.Axis">Axis</see>.
        /// </summary>
        [SerializeField]
        public CrossLayout Cross;

        /// <summary>
        /// False if Axis == Axis.None, otherwise true.
        /// </summary>
        public readonly bool Enabled
        {
            get
            {
                return Axis.TryGetIndex(out int _);
            }
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if all fields of <paramref name="lhs"/> are equal to all field of <paramref name="rhs"/></returns>
        public static bool operator ==(AutoLayout lhs, AutoLayout rhs)
        {
            return lhs.Axis == rhs.Axis &&
                   lhs.AutoSpace == rhs.AutoSpace &&
                   lhs.ReverseOrder == rhs.ReverseOrder &&
                   lhs.Alignment == rhs.Alignment &&
                   lhs.Offset == rhs.Offset &&
                   lhs.Spacing == rhs.Spacing &&
                   lhs.SpacingMinMax == rhs.SpacingMinMax &&
                   lhs.Cross == rhs.Cross;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if any field of <paramref name="lhs"/> is <b>not</b> equal to its corresponding field of <paramref name="rhs"/></returns>
        public static bool operator !=(AutoLayout lhs, AutoLayout rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The <see cref="AutoLayout"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public override bool Equals(object other)
        {
            if (other is AutoLayout autolayout)
            {
                return this == autolayout;
            }

            return false;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The <see cref="AutoLayout"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(AutoLayout other)
        {
            return this == other;
        }

        /// <summary>The hashcode for this <see cref="AutoLayout"/></summary>
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + ((int)Axis).GetHashCode();
            hash = (hash * 7) + Spacing.GetHashCode();
            hash = (hash * 7) + SpacingMinMax.GetHashCode();
            hash = (hash * 7) + AutoSpace.GetHashCode();
            hash = (hash * 7) + ReverseOrder.GetHashCode();
            hash = (hash * 7) + Alignment.GetHashCode();
            hash = (hash * 7) + Offset.GetHashCode();
            return hash;
        }

        /// <summary>
        /// The string representation of this <see cref="AutoLayout"/>.
        /// </summary>
        public override string ToString()
        {
            if (Axis == Axis.None)
            {
                return "Disabled";
            }

            string order = ReverseOrder ? "Reverse" : "Default";
            string align = Axis == Axis.X ? ((HorizontalAlignment)Alignment).ToString() :
                           Axis == Axis.Y ? ((VerticalAlignment)Alignment).ToString() :
                           ((DepthAlignment)Alignment).ToString();

            string space = AutoSpace ? "Auto" : Spacing.ToString();

            return $"Axis = {Axis}, Align = {align}, Spacing = {space}, Order = {order}";
        }

        #region Should these be public?
        /// <summary>
        /// The direction (in Unity's coordinate space) the system will position subsequent child elements along the given axis.
        /// X: Left -> Right == 1
        /// Y: Top -> Bottom == -1
        /// Z: Front -> Back == 1
        /// </summary>
        internal readonly int AxisDirection => Axis == Axis.Y ? -1 : 1;

        internal readonly int ContentDirection
        {
            get
            {
                int contentDirection = ReverseOrder ? -1 : 1;
                return AxisDirection * contentDirection;
            }
        }

        /// <summary>
        /// The direction (in Unity's coordinate space) from the point of alignment towards the owner center. Center Aligned => 1
        /// </summary>
        internal readonly int AlignmentPositiveDirection => Alignment == 1 ? -1 : 1;

        /// <summary>
        /// Tells the layout system to apply position offsets to child elements in reverse order
        /// </summary>
        internal readonly bool PositioningInverted
        {
            get
            {
                return (Axis == Axis.Y) ^ (Alignment == 1) ^ ReverseOrder;
            }
        }

        internal readonly bool CenterAligned => Enabled && Alignment == 0;
        #endregion

        /// <summary>
        /// An <see cref="AutoLayout"/> configured to position elements along the X axis.
        /// </summary>
        public static readonly AutoLayout Horizontal = new AutoLayout() { Axis = Axis.X, AutoSpace = true, SpacingMinMax = MinMax.Positive, Cross = CrossLayout.Disabled };

        /// <summary>
        /// An <see cref="AutoLayout"/> configured to position elements along the Y axis.
        /// </summary>
        public static readonly AutoLayout Vertical = new AutoLayout() { Axis = Axis.Y, AutoSpace = true, SpacingMinMax = MinMax.Positive, Cross = CrossLayout.Disabled };

        /// <summary>
        /// An <see cref="AutoLayout"/> configured to position elements along the Z axis.
        /// </summary>
        public static readonly AutoLayout Zed = new AutoLayout() { Axis = Axis.Z, AutoSpace = true, SpacingMinMax = MinMax.Positive, Cross = CrossLayout.Disabled };

        /// <summary>
        /// A disabled <see cref="AutoLayout"/>.
        /// </summary>
        public static readonly AutoLayout Disabled = new AutoLayout() { Alignment = -1, Spacing = Length.Zero, SpacingMinMax = MinMax.Positive, Cross = CrossLayout.Disabled };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref AutoLayout FromInternal(ref Internal.AutoLayout autoLayout)
        {
            return ref UnsafeUtility.As<Internal.AutoLayout, AutoLayout>(ref autoLayout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref Internal.AutoLayout ToInternal(ref AutoLayout autoLayout)
        {
            return ref UnsafeUtility.As<AutoLayout, Internal.AutoLayout>(ref autoLayout);
        }
    }
}