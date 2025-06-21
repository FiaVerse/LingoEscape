// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    namespace Internal.Layouts
    {
        internal static class LayoutAccess
        {
            // offset for length3
            private const int SizeOffset = 0;
            private const int PositionOffset = 1;

            // offset for length3x2
            private const int PaddingOffset = 1;
            private const int MarginOffset = 2;

            // num length3's per element
            public const int Length3SliceSize = 6;

            // num length3x2's per element
            public const int Length3x2SliceSize = 3;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Properties Get(DataStoreIndex elementIndex, ref NativeList<Length3> lengths)
            {
                return new Properties(elementIndex, ref lengths);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ReadOnly Read(DataStoreIndex elementIndex, ref NativeList<Length3> lengths, ref NativeList<Length3.MinMax> minMaxes)
            {
                return new ReadOnly(elementIndex, ref lengths, ref minMaxes);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Properties Get(DataStoreIndex elementIndex, ref NativeList<Length3> lengths, ref NativeList<Length3.Calculated> calcs)
            {
                Properties props = new Properties(elementIndex, ref lengths);
                props.WrapCalculated(calcs);

                return props;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe Properties GetUnsafe(DataStoreIndex elementIndex, Length3* lengths)
            {
                return new Properties(elementIndex, lengths);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Calculated Get(DataStoreIndex elementIndex, ref NativeList<Length3.Calculated> calculatedLengths)
            {
                return new Calculated(elementIndex, ref calculatedLengths);
            }

            public unsafe struct ReadOnly
            {
                private int index;

                [NoAlias]
                private Length3* lengthReadOnlyPtr;
                [NoAlias]
                private Length3.MinMax* minMaxReadOnlyPtr;

                public Length3 Size
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return UnsafeUtility.AsRef<Length3>(SizePtr);

                    }
                }

                public Length3.MinMax SizeMinMax
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return UnsafeUtility.AsRef<Length3.MinMax>(SizeMinMaxPtr);

                    }
                }

                public Length3 Position
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {

                        return UnsafeUtility.AsRef<Length3>(PositionPtr);

                    }
                }

                public LengthBounds Padding
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {

                        return UnsafeUtility.AsRef<LengthBounds>(PaddingPtr);

                    }
                }

                public LengthBounds Margin
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return UnsafeUtility.AsRef<LengthBounds>(MarginPtr);
                    }
                }

                private int Length3Index
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => index * Length3SliceSize;
                }

                private int Length3x2Index
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => index * Length3x2SliceSize;
                }

                private Length3* SizePtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => lengthReadOnlyPtr + Length3Index + SizeOffset;
                }

                private Length3.MinMax* SizeMinMaxPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => minMaxReadOnlyPtr + Length3Index + SizeOffset;
                }

                private Length3* PositionPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => lengthReadOnlyPtr + Length3Index + PositionOffset;
                }

                private LengthBounds* PaddingPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ((LengthBounds*)lengthReadOnlyPtr) + Length3x2Index + PaddingOffset;
                }

                private LengthBounds* MarginPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ((LengthBounds*)lengthReadOnlyPtr) + Length3x2Index + MarginOffset;
                }

                public ReadOnly(DataStoreIndex layoutIndex, ref NativeList<Length3> lengths, ref NativeList<Length3.MinMax> minMaxes)
                {
                    index = layoutIndex;
                    lengthReadOnlyPtr = lengths.GetRawReadonlyPtr();
                    minMaxReadOnlyPtr = minMaxes.GetRawReadonlyPtr();
                }
            }

            internal unsafe struct Properties
            {
                private int index;
                public DataStoreIndex Index
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => index;
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    set => index = value;
                }

                [NoAlias]
                private Length3* lengthPtr;
                [NoAlias]
                private Length3.MinMax* minMaxPtr;
                [NoAlias]
                private Length3.Calculated* calcPtr;
                [NoAlias]
                private AutoSize3* autoSizesPtr;
                [NoAlias]
                private AspectRatio* aspectRatiosPtr;
                [NoAlias]
                private float3* alignmentsPtr;
                [NoAlias]
                private bool* useRotationsPtr;
                [NoAlias]
                private float3* relativeSizesPtr;
                [NoAlias]
                private quaternion* rotationsPtr;

                public ref AutoSize3 AutoSize
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ref UnsafeUtility.AsRef<AutoSize3>(autoSizesPtr + Index);
                }

                public ref float3 Alignment
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ref UnsafeUtility.AsRef<float3>(alignmentsPtr + Index);
                }

                public ref bool RotateSize
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ref UnsafeUtility.AsRef<bool>(useRotationsPtr + Index);
                }

                public ref AspectRatio AspectRatio
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ref UnsafeUtility.AsRef<AspectRatio>(aspectRatiosPtr + Index);
                }

                private int Length3Index
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => index * Length3SliceSize;
                }

                private int Length3x2Index
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => index * Length3x2SliceSize;
                }

                public ref Length3 Size
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<Length3>(SizePtr);
                        }
                    }
                }

                public ref Length3 Position
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<Length3>(PositionPtr);
                        }
                    }
                }

                public ref LengthBounds Padding
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<LengthBounds>(PaddingPtr);
                        }
                    }
                }

                public ref LengthBounds Margin
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<LengthBounds>(MarginPtr);
                        }
                    }
                }

                public ref Length3.MinMax SizeMinMax
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<Length3.MinMax>(SizeMinMaxPtr);
                        }
                    }
                }

                public ref Length3.MinMax PositionMinMax
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<Length3.MinMax>(PositionMinMaxPtr);
                        }
                    }
                }

                private ref LengthBounds.MinMax PaddingMinMax
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<LengthBounds.MinMax>(PaddingMinMaxPtr);
                        }
                    }
                }

                public ref LengthBounds.MinMax MarginMinMax
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<LengthBounds.MinMax>(MarginMinMaxPtr);
                        }
                    }
                }

                private unsafe Length3* SizePtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => lengthPtr + Length3Index + SizeOffset;
                }

                private unsafe Length3* PositionPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => lengthPtr + Length3Index + PositionOffset;
                }

                private unsafe LengthBounds* PaddingPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ((LengthBounds*)lengthPtr) + Length3x2Index + PaddingOffset;
                }

                private unsafe LengthBounds* MarginPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ((LengthBounds*)lengthPtr) + Length3x2Index + MarginOffset;
                }

                private unsafe Length3.MinMax* SizeMinMaxPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => minMaxPtr + Length3Index + SizeOffset;
                }

                private unsafe Length3.MinMax* PositionMinMaxPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => minMaxPtr + Length3Index + PositionOffset;
                }

                private unsafe LengthBounds.MinMax* PaddingMinMaxPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ((LengthBounds.MinMax*)minMaxPtr) + Length3x2Index + PaddingOffset;
                }

                private unsafe LengthBounds.MinMax* MarginMinMaxPtr
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => ((LengthBounds.MinMax*)minMaxPtr) + Length3x2Index + MarginOffset;
                }

                public ref Length3.Calculated CalculatedSize
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<Length3.Calculated>(calcPtr + Length3Index + SizeOffset);
                        }
                    }
                }

                public ref Length3.Calculated CalculatedPosition
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<Length3.Calculated>(calcPtr + Length3Index + PositionOffset);
                        }
                    }
                }

                public ref LengthBounds.Calculated CalculatedPadding
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<LengthBounds.Calculated>(((LengthBounds.Calculated*)calcPtr) + Length3x2Index + PaddingOffset);
                        }
                    }
                }

                public ref LengthBounds.Calculated CalculatedMargin
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return ref UnsafeUtility.AsRef<LengthBounds.Calculated>(((LengthBounds.Calculated*)calcPtr) + Length3x2Index + MarginOffset);
                        }
                    }
                }

                public bool3 SizeIsRelativeToParent
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return Size.IsRelative | autoSizesPtr[Index].RelativeToParent;
                    }
                }

                public bool3 IsRelative
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return Size.IsRelative | Position.IsRelative | Padding.IsRelative | Margin.IsRelative | autoSizesPtr[Index].RelativeToParent;
                    }
                }

                public float3 PaddedSize
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return CalculatedSize.Value - CalculatedPadding.Size;
                    }
                }

                public ref float3 RelativeToSize
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return ref UnsafeUtility.AsRef<float3>(relativeSizesPtr + Index);
                    }
                }

                public float3 RotatedSize
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return RotateSize ? LayoutUtils.RotateSize(CalculatedSize.Value, *(rotationsPtr + Index)) : CalculatedSize.Value;
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public float3 GetRotatedSize(ref NativeList<quaternion> rotations, ref NativeList<bool> useRotations)
                {
                    if (!useRotations[Index])
                    {
                        return CalculatedSize.Value;
                    }

                    return LayoutUtils.RotateSize(CalculatedSize.Value, rotations[Index]);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Calc(float3 relativeTo, bool3 calculateSize)
                {
                    ref Length3 size = ref Size;

                    // position must be set before size in case of Expand
                    CalculatedPosition = Length3.Calc(Position, PositionMinMax, relativeTo);

                    // margin must be set before size in case of Expand
                    CalculatedMargin = LengthBounds.Calc(Margin, MarginMinMax, relativeTo);

                    if (math.any(calculateSize))
                    {
                        // size must be set after margin to account for Expand
                        CalculateSize(relativeTo, calculateSize);
                    }

                    // padding must be set after size
                    CalculatedPadding = LengthBounds.Calc(Padding, PaddingMinMax, CalculatedSize.Value);

                    RelativeToSize = relativeTo;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void CalculatePadding()
                {
                    CalculatedPadding = LengthBounds.Calc(Padding, PaddingMinMax, CalculatedSize.Value);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void CalculatePosition(float3 relativeTo)
                {
                    CalculatedPosition = Length3.Calc(Position, PositionMinMax, relativeTo);
                    RelativeToSize = relativeTo;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void CalculatePosition(float relativeTo, int axis)
                {
                    CalculatedPosition[axis] = new Length.Calculated(Position[axis], PositionMinMax[axis], relativeTo);
                    RelativeToSize[axis] = relativeTo;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void CalculateSize(float3 relativeTo, bool3 axes)
                {
                    ApplyConstraintsToSize(ref Size, relativeTo);
                    Length3.Calculated current = CalculatedSize;
                    Length3.Calculated updated = Length3.Calc(Size, SizeMinMax, relativeTo);

                    CalculatedSize = new Length3.Calculated()
                    {
                        X = axes.x ? updated.X : current.X,
                        Y = axes.y ? updated.Y : current.Y,
                        Z = axes.z ? updated.Z : current.Z,
                    };

                    RelativeToSize = relativeTo;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                private void ApplyConstraintsToSize(ref Length3 size, float3 relativeTo)
                {
                    ref AutoSize3 autosize = ref AutoSize;
                    ref AspectRatio aspectRatio = ref AspectRatio;

                    if (!math.any(autosize.RelativeToParent))
                    {
                        LockAspectRatio(ref size, ref aspectRatio, ref autosize, relativeTo);
                    }
                    else
                    {

                        size.IsRelative = size.IsRelative | autosize.Expand;
                        size.Raw = math.select(size.Raw, Math.float3_One - CalculatedMargin.RelativeSize, autosize.Expand);

                        LockAspectRatio(ref size, ref aspectRatio, ref autosize, relativeTo);

                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                private void LockAspectRatio(ref Length3 size, ref AspectRatio aspectRatio, ref AutoSize3 autosize, float3 relativeTo)
                {
                    if (!aspectRatio.IsLocked)
                    {
                        return;
                    }

                    int primaryAxis = aspectRatio.Axis.Index();
                    int2 constrainedAxes = aspectRatio.ConstrainedAxesIndices;

                    Length.Calculated baseline = new Length.Calculated(size[primaryAxis], SizeMinMax[primaryAxis], relativeTo[primaryAxis]);

                    for (int i = 0; i < 2; ++i)
                    {
                        int axis = constrainedAxes[i];
                        Length constrainedAxis = size[axis];

                        // Need to kick these into Fixed mode, otherwise if
                        // relativeTo[axis] == 0, our aspect ratio won't be honored.
                        constrainedAxis.Type = LengthType.Value;

                        constrainedAxis.Raw = baseline.Value * aspectRatio.Ratio[axis];
                        constrainedAxis.Raw = Math.ValidAndFinite(constrainedAxis.Raw) ? constrainedAxis.Raw : 0;

                        size[axis] = constrainedAxis;

                        autosize[axis] = Internal.AutoSize.None;
                    }
                }


                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public HierarchyDependency CalculatedDependencyDiff(ref CalculatedSnapshot snapshot)
                {
                    if (!CalculatedSize.Value.Equals(snapshot.Size.Value) ||
                        !CalculatedPadding.Value.Equals(snapshot.Padding.Value))
                    {
                        return HierarchyDependency.ParentAndChildren;
                    }

                    if (!CalculatedPosition.Value.Equals(snapshot.Position.Value) ||
                        !CalculatedMargin.Value.Equals(snapshot.Margin.Value))
                    {
                        return HierarchyDependency.Parent;
                    }

                    return HierarchyDependency.None;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public HierarchyDependency ApplyDiff(ref PropertySnapshot snapShot, bool excludePosition = false)
                {
                    HierarchyDependency dependent = HierarchyDependency.None;

                    if (!Position.Equals(ref snapShot.Position) ||
                        !PositionMinMax.Equals(ref snapShot.PositionMinMax) ||
                        !Margin.Equals(ref snapShot.Margin) ||
                        !MarginMinMax.Equals(ref snapShot.MarginMinMax) ||
                        math.any(Alignment != snapShot.Alignment))
                    {
                        if (!excludePosition)
                        {
                            Position = snapShot.Position;
                        }

                        PositionMinMax = snapShot.PositionMinMax;
                        Margin = snapShot.Margin;
                        MarginMinMax = snapShot.MarginMinMax;
                        Alignment = snapShot.Alignment;
                        dependent = HierarchyDependency.Parent;
                    }

                    if (!Size.Equals(ref snapShot.Size) ||
                        !SizeMinMax.Equals(ref snapShot.SizeMinMax) ||
                        !Padding.Equals(ref snapShot.Padding) ||
                        !PaddingMinMax.Equals(ref snapShot.PaddingMinMax) ||
                        AutoSize != snapShot.AutoSize ||
                        AspectRatio != snapShot.AspectRatio ||
                        RotateSize != snapShot.RotateSize)
                    {
                        Size = snapShot.Size;
                        SizeMinMax = snapShot.SizeMinMax;
                        Padding = snapShot.Padding;
                        PaddingMinMax = snapShot.PaddingMinMax;
                        AutoSize = snapShot.AutoSize;
                        RotateSize = snapShot.RotateSize;
                        AspectRatio = snapShot.AspectRatio;

                        dependent = HierarchyDependency.ParentAndChildren;
                    }

                    return dependent;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public CalculatedSnapshot GetCalculatedSnapshot()
                {
                    return new CalculatedSnapshot()
                    {
                        Size = CalculatedSize,
                        Position = CalculatedPosition,
                        Padding = CalculatedPadding,
                        Margin = CalculatedMargin,
                        AutoSize = AutoSize,
                    };
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void CopyTo(ref Layout props)
                {
                    Layout* propsPtr = (Layout*)UnsafeUtility.AddressOf(ref props);
                    UnsafeUtility.MemCpy(&propsPtr->Size, lengthPtr + (Length3SliceSize * Index), Length3SliceSize * Length3.SizeOf);
                    UnsafeUtility.MemCpy(&propsPtr->SizeMinMax, minMaxPtr + (Length3SliceSize * Index), Length3SliceSize * Length3.MinMax.SizeOf);

                    props.AutoSize = *(autoSizesPtr + Index);
                    props.Alignment = (int3)(*(alignmentsPtr + Index));
                    props.AspectRatio = *(aspectRatiosPtr + Index);
                    props.RotateSize = *(useRotationsPtr + Index);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static ref PropertySnapshot AsSnapshot(ref Layout properties)
                {
                    return ref UnsafeUtility.As<Layout, PropertySnapshot>(ref properties);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapAutoSizes(ref NativeList<AutoSize3> autosizes)
                {
                    autoSizesPtr = autosizes.GetRawPtr();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapAutoSizes(AutoSize3* autosizes)
                {
                    autoSizesPtr = autosizes;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapAspectRatios(ref NativeList<AspectRatio> aspectRatios)
                {
                    aspectRatiosPtr = aspectRatios.GetRawPtr();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapAspectRatios(AspectRatio* aspectRatios)
                {
                    aspectRatiosPtr = aspectRatios;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapAlignments(ref NativeList<float3> alignments)
                {
                    alignmentsPtr = alignments.GetRawPtr();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapAlignments(float3* alignments)
                {
                    alignmentsPtr = alignments;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapRelativeSizes(ref NativeList<float3> relativeSizes)
                {
                    relativeSizesPtr = relativeSizes.GetRawPtr();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapUseRotations(ref NativeList<bool> useRotations)
                {
                    useRotationsPtr = useRotations.GetRawPtr();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapUseRotations(bool* useRotations)
                {
                    useRotationsPtr = useRotations;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapRotations(ref NativeList<quaternion> rotations)
                {
                    rotationsPtr = rotations.GetRawPtr();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapRotations(quaternion* rotations)
                {
                    rotationsPtr = rotations;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapCalculated(NativeList<Length3.Calculated> calcs)
                {
                    calcPtr = calcs.GetRawPtr();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapCalculated(Length3.Calculated* calcs)
                {
                    calcPtr = calcs;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapMinMaxes(ref NativeList<Length3.MinMax> minMaxes)
                {
                    minMaxPtr = minMaxes.GetRawPtr();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void WrapMinMaxes(Length3.MinMax* minMaxes)
                {
                    minMaxPtr = minMaxes;
                }

                public Properties(DataStoreIndex layoutIndex, ref NativeList<Length3> lengths)
                {
                    index = layoutIndex;
                    lengthPtr = lengths.GetRawPtr();
                    minMaxPtr = null;
                    calcPtr = null;
                    alignmentsPtr = null;
                    autoSizesPtr = null;
                    aspectRatiosPtr = null;
                    useRotationsPtr = null;
                    relativeSizesPtr = null;
                    rotationsPtr = null;
                }

                public Properties(DataStoreIndex layoutIndex, Length3* lengths)
                {
                    index = layoutIndex;
                    lengthPtr = lengths;
                    minMaxPtr = null;
                    calcPtr = null;
                    alignmentsPtr = null;
                    autoSizesPtr = null;
                    aspectRatiosPtr = null;
                    useRotationsPtr = null;
                    relativeSizesPtr = null;
                    rotationsPtr = null;
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct PropertySnapshot
            {
                public Length3 Size;
                public Length3 Position;
                public LengthBounds Padding;
                public LengthBounds Margin;

                public Length3.MinMax SizeMinMax;
                public Length3.MinMax PositionMinMax;
                public LengthBounds.MinMax PaddingMinMax;
                public LengthBounds.MinMax MarginMinMax;

                public int3 Alignment;
                public AutoSize3 AutoSize;
                public bool RotateSize;
                public AspectRatio AspectRatio;

                public bool3 IsRelative
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return Size.IsRelative | Position.IsRelative | Padding.IsRelative | Margin.IsRelative | !AutoSize.None;
                    }
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct CalculatedSnapshot
            {
                public Length3.Calculated Size;
                public Length3.Calculated Position;
                public LengthBounds.Calculated Padding;
                public LengthBounds.Calculated Margin;
                public AutoSize3 AutoSize;
            }

            /// <summary>
            /// The accessor to use when the NativeContainers/Job are in a ReadOnly context
            /// </summary>
            internal unsafe ref struct Calculated
            {
                public DataStoreIndex Index;

                [NoAlias]
                private readonly Length3.Calculated* lengthReadOnlyPtr;
                private readonly LengthBounds.Calculated* length3x2Ptr => (LengthBounds.Calculated*)lengthReadOnlyPtr;

                private readonly int Length3Index
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => Index * Length3SliceSize;
                }

                private readonly int Length3x2Index
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => Index * Length3x2SliceSize;
                }

                public readonly Length3.Calculated Size
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => lengthReadOnlyPtr[Length3Index + SizeOffset];
                }

                public readonly Length3.Calculated Position
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => lengthReadOnlyPtr[Length3Index + PositionOffset];
                }

                public readonly LengthBounds.Calculated Padding
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return length3x2Ptr[Length3x2Index + PaddingOffset];
                        }
                    }
                }

                public readonly LengthBounds.Calculated Margin
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        unsafe
                        {
                            return length3x2Ptr[Length3x2Index + MarginOffset];
                        }
                    }
                }

                public readonly float3 PaddedSize
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return Size.Value - Padding.Size;
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly float3 GetRotatedSize(ref NativeList<quaternion> rotations, ref NativeList<bool> useRotations)
                {
                    if (!useRotations[Index])
                    {
                        return Size.Value;
                    }

                    return LayoutUtils.RotateSize(Size.Value, rotations[Index]);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly float3 GetLayoutSize(ref NativeList<quaternion> rotations, ref NativeList<bool> useRotations)
                {
                    return GetRotatedSize(ref rotations, ref useRotations) + Margin.Size;
                }

                public Calculated(DataStoreIndex elementIndex, ref NativeList<Length3.Calculated> lengths)
                {
                    Index = elementIndex;
                    lengthReadOnlyPtr = lengths.GetRawReadonlyPtr();
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CalculatedLayout
        {
            public Length3.Calculated Size;
            public Length3.Calculated Position;
            public LengthBounds.Calculated Padding;
            public LengthBounds.Calculated Margin;

            public Vector3 PaddedSize => Size.Value - Padding.Size;
        }

        internal unsafe struct LayoutPointer
        {
            public DataStoreID ID;
            public ulong GCHandle;
            public Layout* Layout;
            public AutoLayout* AutoLayout;
        }
    }
}