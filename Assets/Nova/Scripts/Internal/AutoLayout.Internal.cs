// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Nova.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct CrossLayout
    {
        public Axis Axis;
        public Length Spacing;
        public Length.MinMax SpacingMinMax;

        public bool AutoSpace;
        public bool ReverseOrder;
        public int Alignment;

        public bool ExpandToGrid;

        public readonly bool Enabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Axis.Index() != -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool3 GetAxisMask() => Enabled ? Axis.Index() == Math.AxisIndices : false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Length.Calculated Calc(float relativeTo)
        {
            return new Length.Calculated(Spacing, SpacingMinMax, relativeTo);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AutoLayout : IDependencyDiffable<AutoLayout>, IRuntimeProperty
    {
        public Axis Axis;
        public Length Spacing;
        public Length.MinMax SpacingMinMax;

        public bool AutoSpace;
        public bool ReverseOrder;
        public int Alignment;
        public float Offset;

        public CrossLayout Cross;

        /// <summary>
        /// False if Axis == Axis.None, otherwise true
        /// </summary>
        public readonly bool Enabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Axis.Index() != -1;
            }
        }

        public readonly bool CrossEnabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get 
            {
                return Enabled && Cross.Enabled && Cross.Axis != Axis;
            }
        }

        internal readonly int AxisDirection
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Axis == Axis.Y ? -1 : 1;
        }

        internal readonly int ContentDirection
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int contentDirection = ReverseOrder ? -1 : 1;
                return AxisDirection * contentDirection;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool3 GetAxisMask() => Enabled ? Axis.Index() == Math.AxisIndices : false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Length.Calculated Calc(float relativeTo)
        {
            return new Length.Calculated(Spacing, SpacingMinMax, relativeTo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HierarchyDependency ApplyDiff(ref AutoLayout valueToSet)
        {
            HierarchyDependency diff = DependencyDiff(ref valueToSet);

            if (!diff.HasDirectDependencies)
            {
                return diff;
            }

            Axis = valueToSet.Axis;
            Alignment = valueToSet.Alignment;
            Spacing = valueToSet.Spacing;
            SpacingMinMax = valueToSet.SpacingMinMax;
            AutoSpace = valueToSet.AutoSpace;
            ReverseOrder = valueToSet.ReverseOrder;
            Offset = valueToSet.Offset;
            Cross = valueToSet.Cross;

            return diff;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HierarchyDependency DependencyDiff(ref AutoLayout valueToDiff)
        {
            return this.IsDifferent(ref valueToDiff) ? HierarchyDependency.ParentAndChildren : HierarchyDependency.None;
        }
    }
}