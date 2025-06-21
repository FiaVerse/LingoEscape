// Copyright (c) Supernova Technologies LLC
//#define DEBUG_EXPAND
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Layouts
{
    internal partial class LayoutCore
    {
        /// <summary>
        /// Does a bottom-up pass to auto-layout children and applies shrink behavior on the parent
        /// </summary>
        internal struct BuildLeafUp
        {
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
            public NativeList<quaternion> Rotations;

            [ReadOnly]
            public NativeList<TransformProxy> TransformProxies;

            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

            [ReadOnly]
            public NativeList<AutoLayout> AutoLayouts;
            [NativeDisableParallelForRestriction]
            public NativeList<Length2.Calculated> CalculatedSpacing;
            [ReadOnly]
            public NovaHashMap<DataStoreIndex, Axes> FormerAutoLayoutAxes;

            public NativeList<ExpandableTrack> AutoLayoutTrackCache;
            public NativeList<ExpandableRange> AutoLayoutRangeCache;

            [ReadOnly]
            public NovaHashMap<DataStoreID, SizeOverride> ShrinkSizeOverrides;

            [NativeDisableParallelForRestriction]
            public NativeList<HierarchyDependency> DirtyDependencies;

            private DataStoreIndex parentIndex;
            private LayoutAccess.CalculatedSnapshot layoutProperties;
            private bool secondPass;
            private AutoLayoutContext ctx;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(DataStoreIndex layoutIndex, bool isSecondPass, bool forceRun = false)
            {
                ctx = default;
                secondPass = isSecondPass;
                parentIndex = layoutIndex;
                ctx.ParentElement = Hierarchy[parentIndex];

                LayoutAccess.Properties parentLayout = LayoutAccess.Get(parentIndex, ref LengthConfigs, ref CalculatedLengths);
                parentLayout.WrapMinMaxes(ref LengthRanges);
                parentLayout.WrapAutoSizes(ref AutoSizes);

                layoutProperties = parentLayout.GetCalculatedSnapshot();

                bool3 parentShrink = layoutProperties.AutoSize.Shrink;

                bool shrinkAny = math.any(parentShrink);

                ctx.AutoLayout = AutoLayouts[parentIndex];

                if (!shrinkAny && !ctx.AutoLayout.Enabled)
                {
                    return;
                }

                if (!forceRun && !ChildrenNeedProcessing())
                {
                    return;
                }

                ctx.ParentSize = math.max(math.select(layoutProperties.Size.Value, parentLayout.SizeMinMax.Min, parentShrink) - layoutProperties.Padding.Size, 0);
                ctx.CalculatedSpacing = CalculatedSpacing[parentIndex];

                int mainAxisIndex = ctx.FirstAxis.Index();
                int crossAxisIndex = ctx.SecondAxis.Index();

                float3 estimatedChildSize = GetEstimatedChildSize(parentLayout, ctx.ParentSize, out float minExpandableLength, out int expandCount);

                SizeOverride shrinkSizeOverride = default;

                if (shrinkAny)
                {
                    if (ShrinkSizeOverrides.TryGetValue(ctx.ParentElement.ID, out shrinkSizeOverride))
                    {
                        estimatedChildSize = math.select(estimatedChildSize, shrinkSizeOverride.Size, shrinkSizeOverride.Mask);
                    }

                    float3 adjustedParentSize = math.max(parentLayout.SizeMinMax.Clamp(estimatedChildSize + layoutProperties.Padding.Size) - layoutProperties.Padding.Size, 0);
                    ctx.ParentSize = math.select(ctx.ParentSize, adjustedParentSize, parentShrink);

                    RecalculateChildSizes(ref ctx.ParentSize);
                }

                if (ctx.AutoLayout.Enabled)
                {
                    ExpandableTrack track = new ExpandableTrack()
                    {
                        StartIndex = 0,
                        SpanCount = expandCount,
                        ExpandedCount = expandCount,
                        ChildLength = estimatedChildSize[mainAxisIndex],
                        PreallocatedSpace = minExpandableLength,
                        ExpandAxis = mainAxisIndex,
                        FillableLength = ctx.ParentSize[mainAxisIndex],
                        EndIndex = ctx.ParentElement.ChildCount,
                    };

                    ApplyAutoLayout(ref track, out float sizeAlongMainAxis, out float sizeAlongCrossAxis);

                    if (!shrinkSizeOverride.Mask[mainAxisIndex])
                    {
                        // Update as long as there's no shrink override
                        estimatedChildSize[mainAxisIndex] = sizeAlongMainAxis;
                    }

                    if (ctx.AutoLayout.CrossEnabled && !shrinkSizeOverride.Mask[crossAxisIndex])
                    {
                        // Update as long as there's no shrink override
                        estimatedChildSize[crossAxisIndex] = sizeAlongCrossAxis;
                    }
                }

                CalculatedSpacing.ElementAt(parentIndex) = ctx.CalculatedSpacing;

                bool parentDirtied = ShrinkToTotalChildSize(ref estimatedChildSize);

                if (parentDirtied)
                {
                    DirtyDependencies[parentIndex] = HierarchyDependency.ParentAndChildren;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float3 GetEstimatedChildSize(LayoutAccess.Properties parentLayout, float3 currentParentSize, out float minExpandableLength, out int expandCount)
            {
                int childCount = ctx.ParentElement.ChildCount;
                bool parentIsVirtual = TransformProxies[parentIndex].IsVirtual;

                int axisIndex = ctx.FirstAxis.Index();

                if (!parentIsVirtual && ctx.AutoLayout.Enabled && FormerAutoLayoutAxes.TryGetValue(parentIndex, out Axes formerAxis))
                {
                    ctx.FormerAxisIndexMask = formerAxis.Primary == Axis.None ? axisIndex != Math.AxisIndices : formerAxis.Primary.Index() == Math.AxisIndices;
                }
                else
                {
                    ctx.FormerAxisIndexMask = Math.bool3_False;
                }

                bool3 shrinkMask = parentLayout.AutoSize.Shrink;

                LayoutAccess.Properties childLayout = LayoutAccess.Get(DataStoreIndex.Invalid, ref LengthConfigs, ref CalculatedLengths);
                childLayout.WrapAutoSizes(ref AutoSizes);
                childLayout.WrapMinMaxes(ref LengthRanges);
                childLayout.WrapAspectRatios(ref AspectRatios);
                childLayout.WrapRelativeSizes(ref ParentSizes);

                GetMaxChildBounds(0, childCount - 1, childLayout, shrinkMask, out float3x2 minMax, out float2 totalChildLength, out expandCount);

                minExpandableLength = totalChildLength.y;

                float3 childBounds = math.max(Math.float3_Zero, minMax.c1 - minMax.c0);

                if (!ctx.AutoLayout.Enabled || ctx.AutoLayout.CrossEnabled)
                {
                    return childBounds;
                }

                float calculatedSize = totalChildLength.x;
                bool fillAvailableSpace = ctx.FirstAxisAutoSpace;

                if (fillAvailableSpace)
                {
                    float childLength = expandCount == 0 ? calculatedSize : currentParentSize[axisIndex];
                    float newSpace = (currentParentSize[axisIndex] - childLength) / math.max(childCount - 1, 1);
                    ctx.FirstAxisSpacing = new Length() { Raw = ctx.FirstSpacingMinMax.Clamp(newSpace) };
                }

                // Assumes non relative, which is why we're clamping
                float estimatedSpacing = ctx.FirstSpacingMinMax.Clamp(ctx.FirstAxisSpacing.Raw);

                if (ctx.FirstAxisSpacing.IsRelative)
                {
                    estimatedSpacing = shrinkMask[axisIndex] ? ctx.FirstSpacingMinMax.Clamp(calculatedSize * ctx.FirstAxisSpacing.Raw) : ctx.FirstSpacingMinMax.Clamp(currentParentSize[axisIndex] * ctx.FirstAxisSpacing.Raw);
                }

                calculatedSize += (childCount - 1) * estimatedSpacing;

                float relativeSize = shrinkMask[axisIndex] ? calculatedSize : currentParentSize[axisIndex];
                ctx.FirstAxisCalculatedSpacing = ctx.CalculateFirstSpacing(relativeSize);

                childBounds[axisIndex] = calculatedSize;

                return childBounds;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyAutoLayout(ref ExpandableTrack track, out float sizeAlongMainAxis, out float sizeAlongCrossAxis)
            {
                LayoutAccess.Properties childLayout = LayoutAccess.Get(DataStoreIndex.Invalid, ref LengthConfigs, ref CalculatedLengths);
                childLayout.WrapMinMaxes(ref LengthRanges);
                childLayout.WrapAutoSizes(ref AutoSizes);
                childLayout.WrapAspectRatios(ref AspectRatios);
                childLayout.WrapRelativeSizes(ref ParentSizes);
                childLayout.WrapAlignments(ref Alignments);
                childLayout.WrapRotations(ref Rotations);
                childLayout.WrapUseRotations(ref UseRotations);

                bool cross = ctx.AutoLayout.CrossEnabled;

                float mainExpandSize = 0;
                float shift = 0;

                if (!cross)
                {
                    if (track.ExpandedCount > 0)
                    {
                        mainExpandSize = GetExpandableSize(ref track, out float extraSpace);
                        ctx.RecalculateFirstAxisAutoSpace(ref track, ref extraSpace);
                        track.ChildLength = track.FillableLength - extraSpace;
                    }

                    shift = ctx.FirstAxisCenterAligned ? -0.5f * track.ChildLength : 0;
                }

                AutoLayoutUtils.ApplyAutoLayout(childLayout, ref ctx, shift, mainExpandSize, ref AutoLayoutTrackCache, out sizeAlongMainAxis);

                sizeAlongCrossAxis = 0;

                if (cross)
                {
                    AutoLayoutUtils.ApplyCrossLayout(childLayout, ref ctx, ref AutoLayoutTrackCache, ref AutoLayoutRangeCache, out sizeAlongCrossAxis);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float GetExpandableSize(ref ExpandableTrack track, out float remainingSpace)
            {
                LayoutAccess.Properties childLayout = LayoutAccess.Get(DataStoreIndex.Invalid, ref LengthConfigs, ref CalculatedLengths);
                childLayout.WrapAutoSizes(ref AutoSizes);
                childLayout.WrapMinMaxes(ref LengthRanges);

                return AutoLayoutUtils.GetExpandableSize(childLayout, ref ctx, ref track, out remainingSpace);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void GetMaxChildBounds(int startIndex, int endIndex, LayoutAccess.Properties childLayout, bool3 shrinkMask, out float3x2 minMax, out float2 totalChildLength, out int expandCount)
            {
                int childCount = endIndex - startIndex + 1;

                minMax = new float3x2(float.MaxValue, float.MinValue);

                int mainAxis = ctx.FirstAxis.Index();
                bool expandable = mainAxis >= 0;

                totalChildLength = 0;
                expandCount = 0;

                ctx.BaseCellSize = float.MaxValue;

                if (expandable && ctx.AutoLayout.CrossEnabled && ctx.AutoLayout.Cross.ExpandToGrid)
                {
                    for (int i = 0; i < childCount; ++i)
                    {
                        childLayout.Index = ctx.ParentElement.Children[i];

                        bool expand = childLayout.AutoSize[mainAxis] == AutoSize.Expand;

                        if (!expand)
                        {
                            continue;
                        }

                        ctx.BaseCellSize = math.min(ctx.BaseCellSize, childLayout.SizeMinMax[mainAxis].Min);
                    }
                }

                if (math.any(shrinkMask))
                {
                    for (int i = 0; i < childCount; ++i)
                    {
                        childLayout.Index = ctx.ParentElement.Children[i];

                        float3 size = GetChildShrinkableSize(ref childLayout, shrinkMask, mainAxis, out bool expand);

                        minMax.c0 = math.min(minMax.c0, size * Math.Extents.c0);
                        minMax.c1 = math.max(minMax.c1, size * Math.Extents.c1);

                        if (!expandable)
                        {
                            continue;
                        }

                        totalChildLength.x += size[mainAxis];

                        if (expand)
                        {
                            expandCount++;
                            totalChildLength.y += math.max(size[mainAxis], 0);
                        }
                    }

                    return;
                }

                for (int i = 0; i < childCount; ++i)
                {
                    childLayout.Index = ctx.ParentElement.Children[i];

                    float3 size = GetChildFixedSize(ref childLayout, mainAxis, out bool expand);

                    minMax.c0 = math.min(minMax.c0, size * Math.Extents.c0);
                    minMax.c1 = math.max(minMax.c1, size * Math.Extents.c1);

                    if (!expandable)
                    {
                        continue;
                    }

                    totalChildLength.x += size[mainAxis];

                    if (expand)
                    {
                        expandCount++;
                        totalChildLength.y += math.max(childLayout.SizeMinMax[mainAxis].Min, 0);
                    }
                }
            }

            /// <summary>
            /// Sums up the given child's layout property values and percentages
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float3 GetChildShrinkableSize(ref LayoutAccess.Properties childLayout, bool3 parentShrinkMask, int mainAxis, out bool expandMainAxis)
            {
                bool3 valueMask = !parentShrinkMask;

                expandMainAxis = mainAxis >= 0 && childLayout.AutoSize[mainAxis] == AutoSize.Expand;

                bool rotateSize = UseRotations[childLayout.Index];
                quaternion rotation = rotateSize ? Rotations[childLayout.Index] : Math.quaterion_Indentity;

                int aspectRatioAxis = childLayout.AspectRatio.Axis.Index();

                if (secondPass && aspectRatioAxis >= 0)
                {
                    SecondPassAspectLock(ref childLayout, aspectRatioAxis);
                }

                float3 sizePercent = childLayout.Size.Percent;
                float3 childSize = childLayout.CalculatedSize.Value * Math.Mask(!childLayout.SizeIsRelativeToParent | valueMask);

                if (expandMainAxis)
                {
                    childSize[mainAxis] = 0;
                    sizePercent[mainAxis] = 0;

                    if (aspectRatioAxis == mainAxis)
                    {
                        childSize = 0;
                        sizePercent = 0;
                    }
                }

                float3 sizeValue = rotateSize ? LayoutUtils.RotateSize(childSize, rotation) : childSize;

                float3x2 marginValue = childLayout.CalculatedMargin.Value;
                marginValue.c0 *= Math.Mask(!childLayout.Margin.MinIsRelative | valueMask);
                marginValue.c1 *= Math.Mask(!childLayout.Margin.MaxIsRelative | valueMask);
                float3x2 marginPercent = childLayout.Margin.Percent;

                LengthBounds.MinMax marginMinMax = childLayout.MarginMinMax;
                float3x2 marginMin = marginMinMax.Min;
                marginMin.c0 = math.max(marginMin.c0, Math.float3_Zero);
                marginMin.c1 = math.max(marginMin.c1, Math.float3_Zero);
                marginMinMax.Min = marginMin;

                Length3.MinMax sizeMinMax = childLayout.SizeMinMax;
                sizeMinMax.Min = math.max(sizeMinMax.Min, Math.float3_Zero);

                Length3.MinMax minMax = sizeMinMax + marginMinMax.MinEdges + marginMinMax.MaxEdges;

                float3 totalValue = sizeValue + marginValue.c0 + marginValue.c1;
                float3 totalPercent = (sizePercent + marginPercent.c0 + marginPercent.c1) * Math.Mask(parentShrinkMask);

                return minMax.Clamp(GetFlexibleSize(ref totalValue, ref totalPercent));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float3 GetChildFixedSize(ref LayoutAccess.Properties childLayout, int expandAxis, out bool expand)
            {
                expand = expandAxis >= 0 && childLayout.AutoSize[expandAxis] == AutoSize.Expand;

                bool rotateSize = UseRotations[childLayout.Index];
                quaternion rotation = rotateSize ? Rotations[childLayout.Index] : Math.quaterion_Indentity;

                int aspectRatioAxis = childLayout.AspectRatio.Axis.Index();

                if (secondPass && aspectRatioAxis >= 0)
                {
                    SecondPassAspectLock(ref childLayout, aspectRatioAxis);
                }

                float3 childSize = childLayout.CalculatedSize.Value;

                if (expand)
                {
                    childSize[expandAxis] = childLayout.SizeMinMax[expandAxis].Clamp(0);
                }

                childSize = rotateSize ? LayoutUtils.RotateSize(childSize, rotation) : childSize;

                return childSize + childLayout.CalculatedMargin.Size;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float3 GetFlexibleSize(ref float3 value, ref float3 percent)
            {
                return new float3(GetFlexibleSize(value.x, percent.x),
                                  GetFlexibleSize(value.y, percent.y),
                                  GetFlexibleSize(value.z, percent.z));
            }

            /// <summary>
            /// In the second pass this step actually runs top-down, 
            /// and we need to ensure the calculated aspect-locked sizes
            /// are updated before we use them.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SecondPassAspectLock(ref LayoutAccess.Properties childLayout, int aspectRatioAxis)
            {
                if (layoutProperties.AutoSize[aspectRatioAxis] != AutoSize.Expand || !childLayout.SizeIsRelativeToParent[aspectRatioAxis])
                {
                    return;
                }

                childLayout.CalculateSize(layoutProperties.Size.Value, true);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float GetFlexibleSize(float value, float percent)
            {
                bool oneHundredPercent = !Math.ApproximatelyLessThan(percent, 1f);

                float calculatedValue = value / (1f - percent);
                float clampedValue = math.select(calculatedValue, value, oneHundredPercent);

                return math.max(value, clampedValue);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecalculateChildSizes(ref float3 updatedParentSize)
            {
                LayoutAccess.Properties childLayout = LayoutAccess.Get(DataStoreIndex.Invalid, ref LengthConfigs, ref CalculatedLengths);
                childLayout.WrapMinMaxes(ref LengthRanges);
                childLayout.WrapAutoSizes(ref AutoSizes);
                childLayout.WrapAspectRatios(ref AspectRatios);
                childLayout.WrapRelativeSizes(ref ParentSizes);

                int childCount = ctx.ParentElement.ChildCount;

                for (int i = 0; i < childCount; ++i)
                {
                    childLayout.Index = ctx.ParentElement.Children[i];
                    float3 relativeSize = childLayout.RelativeToSize;

                    if (updatedParentSize.Equals(relativeSize))
                    {
                        continue;
                    }

                    childLayout.Calc(updatedParentSize, Math.bool3_True);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ShrinkToTotalChildSize(ref float3 directContentSize)
            {
                bool3 parentShrink = AutoSizes.ElementAt(parentIndex).Shrink;
                LayoutAccess.Properties parentLayout = LayoutAccess.Get(parentIndex, ref LengthConfigs, ref CalculatedLengths);

                if (!math.any(parentShrink))
                {
                    return false;
                }

                parentLayout.WrapMinMaxes(ref LengthRanges);
                parentLayout.WrapAutoSizes(ref AutoSizes);
                parentLayout.WrapAspectRatios(ref AspectRatios);
                parentLayout.WrapRelativeSizes(ref ParentSizes);
                parentLayout.CalculatePadding();

                ref LengthBounds padding = ref parentLayout.Padding;

                float3 paddingSize = parentLayout.CalculatedPadding.Size;
                float3 shrinkableSize = directContentSize + paddingSize;

                float3 previousSize = parentLayout.Size.Raw;
                parentLayout.Size.IsRelative = parentLayout.Size.IsRelative & !parentShrink;
                parentLayout.Size.Raw = math.select(previousSize, shrinkableSize, parentShrink);

                float3 relativeSize = parentLayout.RelativeToSize;

                parentLayout.CalculateSize(relativeSize, parentShrink);
                parentLayout.CalculatePadding();

                // return "parent changed"
                return math.any(parentLayout.Size.Raw != previousSize);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ChildrenNeedProcessing()
            {
                if (DirtyDependencies[parentIndex] == HierarchyDependency.ParentAndChildren)
                {
                    return true;
                }

                bool childrenNeedProcessing = false;
                int childCount = ctx.ParentElement.ChildCount;

                for (int i = 0; i < childCount; ++i)
                {
                    DataStoreIndex childIndex = ctx.ParentElement.Children[i];

                    if (DirtyDependencies[childIndex].HasDirectDependencies)
                    {
                        childrenNeedProcessing = true;
                        break;
                    }
                }

                return childrenNeedProcessing;
            }
        }
    }
}
