// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Layouts
{
    internal partial class LayoutCore
    {
        /// <summary>
        /// Does a top-down pass to calculate percent-based child layout properties
        /// </summary>
        [BurstCompile]
        internal struct BuildRootDown
        {
            public bool PreviewSizesAvailable;
            [NativeDisableParallelForRestriction]
            public NovaHashMap<DataStoreID, PreviewSize> PreviewSizes;
            [NativeDisableParallelForRestriction]
            public NativeList<Length3> LengthConfigs;
            [NativeDisableParallelForRestriction]
            public NativeList<Length3.MinMax> LengthRanges;
            [NativeDisableParallelForRestriction]
            public NativeList<Length3.Calculated> CalculatedLengths;
            [NativeDisableParallelForRestriction]
            public NativeList<bool> UseRotations;
            [NativeDisableParallelForRestriction]
            public NativeList<float3> Alignments;
            [NativeDisableParallelForRestriction]
            public NativeList<AutoSize3> AutoSizes;
            [NativeDisableParallelForRestriction]
            public NativeList<AspectRatio> AspectRatios;
            [NativeDisableParallelForRestriction]
            public NativeList<float3> ParentSizes;
            [NativeDisableParallelForRestriction]
            public NativeList<AutoLayout> AutoLayouts;
            [NativeDisableParallelForRestriction]
            public NativeList<Length2.Calculated> CalculatedSpacing;

            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

            [ReadOnly]
            public NativeList<TransformProxy> TransformProxies;
            [NativeDisableParallelForRestriction]
            public NativeList<HierarchyDependency> DirtyDependencies;

            [ReadOnly]
            public NativeList<quaternion> LocalRotations;
            [ReadOnly]
            public NativeList<float3> LocalPositions;
            [ReadOnly]
            public NativeList<bool> UsingTransformPositions;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DataStoreIndex Execute(DataStoreIndex layoutIndex, bool isSecondPass, out bool hasCycle, out bool hasRelativeSize, bool forceRun = false)
            {
                HierarchyElement child = Hierarchy[layoutIndex];
                DataStoreID parentID = child.ParentID;

                bool hasParent = parentID.IsValid;
                DataStoreIndex parentLayoutIndex = hasParent ? HierarchyLookup[parentID] : DataStoreIndex.Invalid;

                bool dirtiedByParent = false;

                hasCycle = false;
                hasRelativeSize = false;

                if (!forceRun)
                {
                    bool childDirtied = DirtyDependencies[layoutIndex].HasDirectDependencies;

                    HierarchyDependency parentDependencies = !hasParent ? HierarchyDependency.None : DirtyDependencies[parentLayoutIndex];
                    dirtiedByParent = parentDependencies == HierarchyDependency.ParentAndChildren;

                    if (!childDirtied && !dirtiedByParent)
                    {
                        if (parentDependencies.IsDirty)
                        {
                            DirtyDependencies[layoutIndex] = HierarchyDependency.Self;
                        }

                        return parentLayoutIndex;
                    }
                }

                LayoutAccess.Properties childLayout = LayoutAccess.Get(layoutIndex, ref LengthConfigs, ref CalculatedLengths);
                childLayout.WrapMinMaxes(ref LengthRanges);
                childLayout.WrapAutoSizes(ref AutoSizes);
                childLayout.WrapAlignments(ref Alignments);
                childLayout.WrapUseRotations(ref UseRotations);
                childLayout.WrapAspectRatios(ref AspectRatios);
                childLayout.WrapRelativeSizes(ref ParentSizes);

                float3 parentSize = hasParent ? GetParentSize(parentLayoutIndex) : Math.float3_Zero;
                bool3 parentAutoLayoutAxes = Math.bool3_False;

                bool3 cycles = hasParent ? HasCyclicalDependentAxes(ref childLayout, parentLayoutIndex, out hasRelativeSize, out parentAutoLayoutAxes) : false;

                hasCycle = math.any(cycles);

                // sync properties
                LayoutAccess.CalculatedSnapshot unmodified = childLayout.GetCalculatedSnapshot();
                childLayout.Calc(parentSize, calculateSize: !(isSecondPass & cycles));

                if (PreviewSizesAvailable && !hasParent)
                {
                    ApplyPreviewSizeOverrides(ref childLayout, child.ID);
                }

                if (UsingTransformPositions[layoutIndex])
                {
                    // parent layout properties
                    float3 parentPaddingOffset = Math.float3_Zero;

                    if (hasParent)
                    {
                        LayoutAccess.Properties parentLayout = LayoutAccess.Get(parentLayoutIndex, ref LengthConfigs, ref CalculatedLengths);
                        parentLayout.WrapMinMaxes(ref LengthRanges);

                        parentPaddingOffset = parentLayout.CalculatedPadding.Offset;
                    }

                    // child layout properties
                    float3 totalSize = childLayout.GetRotatedSize(ref LocalRotations, ref UseRotations) + childLayout.CalculatedMargin.Size;
                    float3 marginOffset = childLayout.CalculatedMargin.Offset;

                    float3 childPosition = LayoutUtils.LocalPositionToLayoutOffset(LocalPositions[layoutIndex], totalSize, marginOffset, parentSize, parentPaddingOffset, Alignments[layoutIndex]);
                    float3 rawPosition = Length3.GetRawFromValue(childPosition, ref childLayout.Position, ref childLayout.PositionMinMax, parentSize);

                    childLayout.Position.Raw = math.select(rawPosition, childLayout.Position.Raw, parentAutoLayoutAxes);

                    childLayout.CalculatePosition(parentSize);
                }

                // sync autolayout
                ref AutoLayout childAutoLayout = ref AutoLayouts.ElementAt(layoutIndex);
                ref Length2.Calculated calculatedSpacing = ref CalculatedSpacing.ElementAt(layoutIndex);
                float2 unmodifedSpaceBetween = calculatedSpacing.Value;

                if (childAutoLayout.Enabled)
                {
                    UpdateAutoLayout(ref childAutoLayout, ref calculatedSpacing, layoutIndex);
                }

                // Diff and update dirty dependencies
                HierarchyDependency propsDependent = hasCycle ? HierarchyDependency.ParentAndChildren : childLayout.CalculatedDependencyDiff(ref unmodified);

                HierarchyDependency autolayoutDependent = math.any(unmodifedSpaceBetween != calculatedSpacing.Value) ? HierarchyDependency.ParentAndChildren : HierarchyDependency.None;
                HierarchyDependency newDependents = HierarchyDependency.Max(propsDependent, autolayoutDependent);
                newDependents = dirtiedByParent ? HierarchyDependency.Max(newDependents, HierarchyDependency.Parent) : newDependents;

                DirtyDependencies[layoutIndex] = HierarchyDependency.Max(newDependents, DirtyDependencies[layoutIndex]);

                return parentLayoutIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float3 GetParentSize(DataStoreIndex parentLayoutIndex)
            {
                LayoutAccess.Calculated parentLayout = LayoutAccess.Get(parentLayoutIndex, ref CalculatedLengths);
                return parentLayout.PaddedSize;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void UpdateAutoLayout(ref AutoLayout autoLayoutRef, ref Length2.Calculated spacingRef, DataStoreIndex childIndex)
            {
                LayoutAccess.Calculated childLayout = LayoutAccess.Get(childIndex, ref CalculatedLengths);

                float3 paddedSize = childLayout.PaddedSize;

                int autoLayoutAxis = autoLayoutRef.Axis.Index();
                float paddedAxisSize = paddedSize[autoLayoutAxis];

                if (autoLayoutRef.AutoSpace)
                {
                    Length space = default;
                    spacingRef.First = new Length.Calculated(space, autoLayoutRef.SpacingMinMax, paddedAxisSize);
                }
                else
                {
                    spacingRef.First = autoLayoutRef.Calc(paddedAxisSize);
                }

                if (autoLayoutRef.CrossEnabled)
                {
                    int wrapAxis = autoLayoutRef.Cross.Axis.Index();
                    float paddedWrapAxisSize = paddedSize[wrapAxis];

                    if (autoLayoutRef.Cross.AutoSpace)
                    {
                        Length space = default;
                        spacingRef.Second = new Length.Calculated(space, autoLayoutRef.Cross.SpacingMinMax, paddedWrapAxisSize);
                    }
                    else
                    {
                        spacingRef.Second = autoLayoutRef.Cross.Calc(paddedWrapAxisSize);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyPreviewSizeOverrides(ref LayoutAccess.Properties childLayout, DataStoreID childID)
            {
                if (!PreviewSizes.TryGetValue(childID, out PreviewSize preview) || !preview.Available)
                {
                    return;
                }

                AspectRatio aspectRatio = childLayout.AspectRatio;
                if (aspectRatio.IsLocked)
                {
                    int axis = aspectRatio.Axis.Index();
                    float3 size = preview.Size;
                    size[axis] = childLayout.Size[axis].IsRelative || childLayout.AutoSize[axis] == AutoSize.Expand ? preview.Size[axis] / childLayout.Size[axis].Raw : size[axis];

                    childLayout.CalculateSize(size, true);
                }
                else
                {
                    childLayout.CalculatedSize = new Length3.Calculated()
                    {
                        X = new Length.Calculated(preview.Size.x, childLayout.Size.X.IsRelative ? childLayout.Size.X.Raw : 1),
                        Y = new Length.Calculated(preview.Size.y, childLayout.Size.Y.IsRelative ? childLayout.Size.Y.Raw : 1),
                        Z = new Length.Calculated(preview.Size.z, childLayout.Size.Z.IsRelative ? childLayout.Size.Z.Raw : 1),
                    };
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool3 HasCyclicalDependentAxes(ref LayoutAccess.Properties childLayout, DataStoreIndex parentIndex, out bool hasRelativeSize, out bool3 parentAutoLayout)
            {
                bool3 parentShrink = AutoSizes.ElementAt(parentIndex).Shrink;

                ref AutoLayout parentLayout = ref AutoLayouts.ElementAt(parentIndex);
                parentAutoLayout = parentLayout.GetAxisMask() | parentLayout.Cross.GetAxisMask();

                bool3 childIsRelative = childLayout.IsRelative;
                bool3 childExpand = childLayout.AutoSize.Expand;

                hasRelativeSize = math.any(childLayout.Size.IsRelative | childExpand);

                return (parentShrink & childIsRelative) | (parentAutoLayout & childExpand);
            }
        }
    }
}
