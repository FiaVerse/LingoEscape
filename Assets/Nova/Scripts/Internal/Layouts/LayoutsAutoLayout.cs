// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Layouts
{
    internal partial class LayoutCore
    {
        public static class AutoLayoutUtils
        {
            private const int MaxIterations = 10;
            private const float Epsilon_ResolveExpand = 5e-4f;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ApplyAutoLayout(LayoutAccess.Properties childLayout, ref AutoLayoutContext ctx, float shift, float firstExpandSize, ref NativeList<ExpandableTrack> tracks, out float sizeAlongAxis)
            {
                int firstAxisIndex = ctx.FirstAxis.Index();
                float baseOffset = ctx.AutoLayout.Offset;
                float autolayoutAlignment = ctx.FirstAxisAlignment;

                bool centerAligned = autolayoutAlignment == 0;

                bool wrap = ctx.AutoLayout.CrossEnabled;
                float newPosition = wrap ? 0 : baseOffset;
                int secondAxisIndex = ctx.SecondAxis.Index();

                bool invertPositions = wrap ? false : ctx.InvertFirstAxisPositions;
                bool invertWrapping = ctx.InvertFirstAxisPositions;

                bool clearFormerAxis = math.any(ctx.FormerAxisIndexMask);
                float3 formerAutoLayoutAxisMask = clearFormerAxis ? Math.Mask(!ctx.FormerAxisIndexMask) : Math.float3_One;

                int increment = math.select(1, -1, invertPositions);
                int startIndex = math.select(0, ctx.ParentElement.Children.Length - 1, invertPositions);
                int endIndex = math.select(ctx.ParentElement.Children.Length, -1, invertPositions);

                float parentSize = ctx.ParentSize[firstAxisIndex];

                tracks.Clear();

                if (wrap)
                {
                    tracks.Add(new ExpandableTrack() { StartIndex = startIndex, ExpandAxis = firstAxisIndex, FillableLength = parentSize });
                }

                float finalSize = 0;

                float spacing = ctx.AutoLayout.CrossEnabled && ctx.FirstAxisAutoSpace ? ctx.FirstSpacingMinMax.Min : ctx.FirstAxisCalculatedSpacing.Value;

                bool expandToGrid = ctx.AutoLayout.Cross.ExpandToGrid;

                for (int i = startIndex; i != endIndex; i += increment)
                {
                    childLayout.Index = ctx.ParentElement.Children[i];

                    bool expand = wrap && childLayout.AutoSize[firstAxisIndex] == AutoSize.Expand;

                    float adjustedExpand = firstExpandSize;
                    int span = 1;

                    if (expand)
                    {
                        float min = childLayout.SizeMinMax[firstAxisIndex].Min;

                        adjustedExpand = expandToGrid ? ctx.GetSpannedSize(min, out span) : min;

                        ref ExpandableTrack track = ref tracks.ElementAt(tracks.Length - 1);
                        track.PreallocatedSpace += adjustedExpand;
                        track.SpanCount += span;
                        track.ExpandedCount++;
                    }

                    ExpandChild(childLayout, ref ctx, firstAxisIndex, adjustedExpand, secondAxisIndex, wrap ? ctx.ParentSize[secondAxisIndex] : 0, ref ctx.ParentSize);

                    float childSize = childLayout.RotatedSize[firstAxisIndex];

                    childSize += childLayout.CalculatedMargin.Size[firstAxisIndex];

                    // the child's position needs to be shifted an extra half of its length
                    // in center-aligned arrangements. Otherwise we'd position siblings only relative
                    // to the size of their neighbor siblings, rather than accounting for this child's 
                    // size, which can lead to undesired overlap.
                    float childExtraLength = math.select(0, 0.5f * childSize, centerAligned);

                    // update offset and alignment for arranged axis
                    childLayout.Alignment[firstAxisIndex] = autolayoutAlignment;

                    // We'll clear the former auto layout axis position to zero, otherwise we attempt to preserve manually assigned positions.
                    ref Length3 childPosition = ref childLayout.Position;

                    // Complete current and create new track as needed
                    if (wrap && newPosition + childSize > parentSize && newPosition != 0)
                    {
                        ref ExpandableTrack track = ref tracks.ElementAt(tracks.Length - 1);
                        track.PreallocatedSpace -= expand ? adjustedExpand : 0;
                        track.SpanCount -= expand ? span : 0;
                        track.ExpandedCount -= expand ? 1 : 0;
                        track.ChildLength = newPosition - spacing;
                        track.EndIndex = invertWrapping ? track.StartIndex - increment : i;
                        track.StartIndex = invertWrapping ? i - increment : track.StartIndex;

                        finalSize = math.max(track.ChildLength, finalSize);

                        // Bump to 0 for wrapping
                        newPosition = 0;
                        tracks.Add(new ExpandableTrack()
                        {
                            StartIndex = i,
                            SpanCount = expand ? span : 0,
                            ExpandedCount = expand ? 1 : 0,
                            PreallocatedSpace = expand ? adjustedExpand : 0,
                            ExpandAxis = firstAxisIndex,
                            FillableLength = parentSize,
                        });
                    }

                    childPosition[firstAxisIndex] = new Length() { Type = LengthType.Value, Raw = newPosition + childExtraLength + shift };

                    if (clearFormerAxis)
                    {
                        childPosition.Raw *= formerAutoLayoutAxisMask;

                        // calculate new values because config changed
                        childLayout.CalculatePosition(childLayout.RelativeToSize);
                    }
                    else
                    {
                        // calculate new value because config changed
                        childLayout.CalculatePosition(parentSize, firstAxisIndex);
                    }

                    // increment total offset
                    newPosition += childSize + spacing;
                }

                if (wrap)
                {
                    ref ExpandableTrack track = ref tracks.ElementAt(tracks.Length - 1);
                    track.ChildLength = newPosition - spacing;
                    track.EndIndex = invertWrapping ? track.StartIndex - increment : endIndex;
                    track.StartIndex = invertWrapping ? endIndex - increment : track.StartIndex;

                    finalSize = math.max(track.ChildLength, finalSize);
                }

                sizeAlongAxis = wrap ? finalSize : newPosition - spacing - baseOffset;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ApplyCrossLayout(LayoutAccess.Properties childLayout, ref AutoLayoutContext ctx, ref NativeList<ExpandableTrack> tracks, ref NativeList<ExpandableRange> ranges, out float sizeAlongSecondAxis)
            {
                PopulateCrossRanges(ref childLayout, ref ctx, ref tracks, ref ranges, out float secondExpandSize);
                ApplyExpandAndPosition(ref childLayout, ref ctx, ref tracks, ref ranges, secondExpandSize, out sizeAlongSecondAxis);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ExpandChild(LayoutAccess.Properties childLayout, ref AutoLayoutContext ctx, int firstAxisIndex, float expandFirstSize, int secondAxisIndex, float expandSecondSize, ref float3 parentSize)
            {
                bool expandPrimary = firstAxisIndex >= 0 && childLayout.AutoSize[firstAxisIndex] == AutoSize.Expand;
                bool expandCross = secondAxisIndex >= 0 && childLayout.AutoSize[secondAxisIndex] == AutoSize.Expand;

                if (!expandCross && !expandPrimary)
                {
                    return;
                }

                float3 min = childLayout.SizeMinMax.Min;

                bool3 axisIndexMask = Math.bool3_False;

                if (expandPrimary)
                {
                    childLayout.AutoSize[firstAxisIndex] = AutoSize.None;
                    Length expandLength = childLayout.Size[firstAxisIndex];
                    expandLength.Type = LengthType.Percent;
                    expandLength.Raw = expandFirstSize / parentSize[firstAxisIndex];
                    childLayout.Size[firstAxisIndex] = expandLength;

                    if (ctx.AutoLayout.CrossEnabled && ctx.AutoLayout.Cross.ExpandToGrid)
                    {
                        // Temporarily override
                        float3 newMin = min;
                        newMin[firstAxisIndex] = math.max(min[firstAxisIndex], ctx.BaseCellSize);
                        childLayout.SizeMinMax.Min = newMin;
                    }

                    axisIndexMask[firstAxisIndex] = true;
                }

                if (expandCross)
                {
                    childLayout.AutoSize[secondAxisIndex] = AutoSize.None;
                    Length expandLength = childLayout.Size[secondAxisIndex];
                    expandLength.Type = LengthType.Percent;
                    expandLength.Raw = expandSecondSize / parentSize[secondAxisIndex];
                    childLayout.Size[secondAxisIndex] = expandLength;

                    axisIndexMask[secondAxisIndex] = true;
                }

                childLayout.CalculateSize(parentSize, childLayout.AspectRatio.IsLocked ? true : axisIndexMask);

                if (expandPrimary)
                {
                    childLayout.AutoSize[firstAxisIndex] = AutoSize.Expand;

                    // Restore
                    childLayout.SizeMinMax.Min = min;
                }

                if (expandCross)
                {
                    childLayout.AutoSize[secondAxisIndex] = AutoSize.Expand;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float GetExpandableSize(LayoutAccess.Properties childLayout, ref AutoLayoutContext ctx, ref ExpandableTrack track, out float remainingSpace)
            {
                int startIndex = track.StartIndex;
                float childLength = track.ChildLength;
                float preallocatedSpace = track.PreallocatedSpace;
                float totalExpandCount = track.SpanCount;
                int endIndex = track.EndIndex;
                int expandAxis = track.ExpandAxis;
                float parentLength = track.FillableLength;

                float availableSpace = parentLength - childLength;

                remainingSpace = availableSpace;

                if (totalExpandCount == 0 || availableSpace <= 0)
                {
                    return 0;
                }

                remainingSpace = 0;

                int attempts = 0;
                float totalAllocatedSpace = 0;

                float currentExpandCount = 0;
                float currentApproximation = availableSpace / totalExpandCount;
                float2 lookback = currentApproximation;

                int increment = endIndex > startIndex ? 1 : -1;

                // This technically will go up to MaxIterations for the really complex
                // cases where there are several unique min/max ranges, but in practice
                // it's often much less. In the simple case it will resolve in 1 attempt.
                for (int i = 0; i < MaxIterations; ++i)
                {
                    float prevResult = totalAllocatedSpace - preallocatedSpace - availableSpace;

                    currentExpandCount = 0;
                    totalAllocatedSpace = 0;

                    attempts = i + 1;

                    float clampedByMaxCount = 0;

                    float minAbove = float.MaxValue;
                    float maxBelow = float.MinValue;

                    for (int childIndex = startIndex; childIndex != endIndex; childIndex += increment)
                    {
                        childLayout.Index = ctx.ParentElement.Children[childIndex];

                        if (childLayout.AutoSize[expandAxis] != AutoSize.Expand)
                        {
                            continue;
                        }

                        Length.MinMax range = childLayout.SizeMinMax[expandAxis];

                        float span = 1;

                        if (currentApproximation < range.Min)
                        {
                            minAbove = math.min(minAbove, range.Min);
                        }
                        else if (currentApproximation > range.Max)
                        {
                            maxBelow = math.max(maxBelow, range.Max);
                        }

                        float val = range.Clamp(currentApproximation);

                        if (Math.ApproximatelyEqual(val, currentApproximation, Epsilon_ResolveExpand))
                        {
                            currentExpandCount += span;
                        }
                        else if (Math.ApproximatelyEqual(val, range.Max))
                        {
                            clampedByMaxCount += span;
                        }

                        totalAllocatedSpace += val;
                    }

                    float result = totalAllocatedSpace - preallocatedSpace - availableSpace;

                    if (result <= 0 && clampedByMaxCount == totalExpandCount)
                    {
                        // clamped entirely by max values,
                        // so don't keep trying to adjust
                        remainingSpace = -result;
                        break;
                    }

                    float countToUse = currentExpandCount == 0 ? totalExpandCount : currentExpandCount;

                    lookback[0] = lookback[1];
                    lookback[1] = currentApproximation;

                    if (Math.ApproximatelyEqual(prevResult, result))
                    {
                        // if we're here, we got stuck, so we'll jump to the nearest
                        // limiting value to ensure we break past the threshold

                        currentApproximation = result < 0 ? minAbove - (result / countToUse) : maxBelow - (result / countToUse);
                    }
                    else
                    {
                        currentApproximation -= result / countToUse;
                    }

                    if (Math.ApproximatelyEqual(currentApproximation, lookback[0], Epsilon_ResolveExpand))
                    {
                        // lookback used to avoid flipping back/forth between two values
                        // here we average the two values we're flipping between to get unstuck
                        currentApproximation = (currentApproximation + lookback[1]) * 0.5f;
                    }

                    currentApproximation = math.max(currentApproximation, 0);


                    if (Math.ApproximatelyZero(result, Epsilon_ResolveExpand))
                    {
                        break;
                    }
                }

                return currentApproximation;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float GetExpandableSize(ref NativeList<ExpandableRange> ranges, int expandCount, float childLength, float preallocatedSpace, float parentLength, out float remainingSpace)
            {
                int totalExpandCount = expandCount;

                float availableSpace = parentLength - childLength;

                remainingSpace = availableSpace;

                if (totalExpandCount == 0 || availableSpace <= 0)
                {
                    return 0;
                }

                int attempts = 0;
                float totalAllocatedSpace = 0;
                remainingSpace = 0;

                int currentExpandCount = 0;
                float currentApproximation = availableSpace / totalExpandCount;
                float2 lookback = currentApproximation;

                int startIndex = 0;
                int endIndex = ranges.Length;

                // This technically will go up to MaxIterations for the really complex
                // cases where there are several unique min/max ranges, but in practice
                // it's often much less. In the simple case it will resolve in 1 attempt.
                for (int i = 0; i < MaxIterations; ++i)
                {
                    float prevResult = totalAllocatedSpace - preallocatedSpace - availableSpace;

                    currentExpandCount = 0;
                    totalAllocatedSpace = 0;

                    attempts = i + 1;

                    int clampedByMaxCount = 0;

                    float minAbove = float.MaxValue;
                    float maxBelow = float.MinValue;

                    for (int childIndex = startIndex; childIndex != endIndex; ++childIndex)
                    {
                        ExpandableRange trackRange = ranges[childIndex];

                        if (!trackRange.Expand)
                        {
                            continue;
                        }

                        Length.MinMax range = trackRange.MinMax;

                        if (currentApproximation < range.Min)
                        {
                            minAbove = math.min(minAbove, range.Min);
                        }
                        else if (currentApproximation > range.Max)
                        {
                            maxBelow = math.max(maxBelow, range.Max);
                        }

                        float val = range.Clamp(currentApproximation);

                        if (Math.ApproximatelyEqual(val, currentApproximation, Epsilon_ResolveExpand))
                        {
                            currentExpandCount++;
                        }
                        else if (Math.ApproximatelyEqual(val, range.Max))
                        {
                            clampedByMaxCount++;
                        }

                        totalAllocatedSpace += val;
                    }

                    float result = totalAllocatedSpace - preallocatedSpace - availableSpace;

                    if (result <= 0 && clampedByMaxCount == totalExpandCount)
                    {
                        // clamped entirely by max values,
                        // so don't keep trying to adjust
                        remainingSpace = -result;
                        break;
                    }

                    int countToUse = currentExpandCount == 0 ? totalExpandCount : currentExpandCount;

                    lookback[0] = lookback[1];
                    lookback[1] = currentApproximation;

                    if (Math.ApproximatelyEqual(prevResult, result))
                    {
                        // if we're here, we got stuck, so we'll jump to the nearest
                        // limiting value to ensure we break past the threshold

                        currentApproximation = result < 0 ? minAbove - (result / countToUse) : maxBelow - (result / countToUse);
                    }
                    else
                    {
                        currentApproximation -= result / countToUse;
                    }

                    if (Math.ApproximatelyEqual(currentApproximation, lookback[0], Epsilon_ResolveExpand))
                    {
                        // lookback used to avoid flipping back/forth between two values
                        // here we average the two values we're flipping between to get unstuck
                        currentApproximation = (currentApproximation + lookback[1]) * 0.5f;
                    }

                    currentApproximation = math.max(currentApproximation, 0);


                    if (Math.ApproximatelyZero(result, Epsilon_ResolveExpand))
                    {
                        break;
                    }
                }

                return currentApproximation;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void PopulateCrossRanges(ref LayoutAccess.Properties childLayout, ref AutoLayoutContext ctx, ref NativeList<ExpandableTrack> tracks, ref NativeList<ExpandableRange> ranges, out float crossExpandSize)
            {
                int trackCount = tracks.Length;
                int secondAxisIndex = ctx.SecondAxis.Index();

                float preallocSize = 0;
                float totalSize = 0;
                int expandCount = 0;

                ranges.Clear();

                float crossSpacing = ctx.SecondAxisAutoSpace ? ctx.SecondSpacingMinMax.Min : ctx.SecondAxisCalculatedSpacing.Value;

                int increment = tracks[0].StartIndex <= tracks[0].EndIndex ? 1 : -1;

                bool preProcessExpandCrossAxis = false;

                for (int trackIndex = 0; trackIndex < trackCount; ++trackIndex)
                {
                    ref ExpandableTrack track = ref tracks.ElementAt(trackIndex);

                    float trackSize = 0;
                    bool expandAny = false;
                    bool preProcessAny = false;

                    Length.MinMax trackMinMax = new Length.MinMax();

                    for (int i = track.StartIndex; i != track.EndIndex; i += increment)
                    {
                        childLayout.Index = ctx.ParentElement.Children[i];

                        bool expand = childLayout.AutoSize[secondAxisIndex] == AutoSize.Expand;

                        expandAny |= expand;

                        preProcessAny |= expand && childLayout.AspectRatio.Axis.Index() == secondAxisIndex;

                        Length.MinMax childMinMax = expand ? childLayout.SizeMinMax[secondAxisIndex] : (Length.MinMax)childLayout.RotatedSize[secondAxisIndex];

                        trackMinMax.Min = math.max(trackMinMax.Min, childMinMax.Min);
                        trackMinMax.Max = math.max(trackMinMax.Max, childMinMax.Max);
                        trackSize = math.max(trackSize, math.max(childMinMax.Min, 0) + childLayout.CalculatedMargin.Size[secondAxisIndex]);
                    }

                    if (expandAny)
                    {
                        expandCount++;
                        preallocSize += trackMinMax.Min;
                        preProcessExpandCrossAxis |= preProcessAny;
                    }

                    totalSize += trackSize + crossSpacing;

                    ranges.Add(new ExpandableRange() { MinMax = trackMinMax, Expand = expandAny, ExpectedSize = trackSize, PreProcess = preProcessAny });
                }

                totalSize -= crossSpacing;

                crossExpandSize = GetExpandableSize(ref ranges, expandCount, totalSize, preallocSize, ctx.ParentSize[secondAxisIndex], out float extraCrossSpace);

                if (ctx.SecondAxisAutoSpace)
                {
                    float preallocatedSpacing = math.max(ctx.SecondSpacingMinMax.Min, 0) * (trackCount - 1);
                    ctx.SecondAxisSpacing = new Length() { Raw = (extraCrossSpace + preallocatedSpacing) / math.max(trackCount - 1, 1) };
                    ctx.SecondAxisCalculatedSpacing = ctx.CalculateSecondSpacing(ctx.ParentSize[secondAxisIndex]);
                }

                if (!preProcessExpandCrossAxis)
                {
                    return;
                }

                // If one or more objects has a locked aspect ratio on the
                // wrap axis AND expands on the wrap axis, we need to pre
                // apply the expanded value to account for any size changes
                // along the main axis.

                for (int trackIndex = 0; trackIndex < trackCount; ++trackIndex)
                {
                    ref ExpandableRange range = ref ranges.ElementAt(trackIndex);

                    if (!range.PreProcess)
                    {
                        continue;
                    }

                    ref ExpandableTrack track = ref tracks.ElementAt(trackIndex);

                    for (int i = track.StartIndex; i != track.EndIndex; i += increment)
                    {
                        childLayout.Index = ctx.ParentElement.Children[i];

                        if (childLayout.AutoSize[secondAxisIndex] != AutoSize.Expand ||
                            childLayout.AspectRatio.Axis.Index() != secondAxisIndex)
                        {
                            continue;
                        }

                        track.ChildLength -= childLayout.RotatedSize[track.ExpandAxis];
                        ExpandChild(childLayout, ref ctx, -1, 0, secondAxisIndex, range.MinMax.Clamp(crossExpandSize), ref ctx.ParentSize);
                        track.ChildLength += childLayout.RotatedSize[track.ExpandAxis];
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void ApplyExpandAndPosition(ref LayoutAccess.Properties childLayout, ref AutoLayoutContext ctx, ref NativeList<ExpandableTrack> tracks, ref NativeList<ExpandableRange> ranges, float crossExpandSize, out float sizeAlongCrossAxis)
            {
                int firstAxisIndex = ctx.FirstAxis.Index();
                int secondAxisIndex = ctx.SecondAxis.Index();
                float baseOffset = ctx.AutoLayout.Offset;
                float autolayoutAlignment = ctx.SecondAxisAlignment;

                int trackCount = tracks.Length;

                bool expandToGrid = ctx.AutoLayout.Cross.ExpandToGrid;
                float cellSize = 0;

                if (expandToGrid)
                {
                    cellSize = GetGridCellSize(ref ctx, ref tracks);

                    if (trackCount > 1)
                    {
                        GridifyAutoSpacing(ref childLayout, ref ctx, ref tracks, cellSize);
                    }
                }

                float2 newPosition = new float2(0, baseOffset);

                int trackIncrement = math.select(1, -1, ctx.InvertSecondAxisPositions);
                int trackStartIndex = math.select(0, trackCount - 1, ctx.InvertSecondAxisPositions);
                int trackEndIndex = math.select(trackCount, -1, ctx.InvertSecondAxisPositions);

                bool primaryAxisCenterAligned = ctx.FirstAxisCenterAligned;
                bool crossAxisCenterAligned = ctx.SecondAxisCenterAligned;

                for (int trackIndex = trackStartIndex; trackIndex != trackEndIndex; trackIndex += trackIncrement)
                {
                    ref ExpandableTrack track = ref tracks.ElementAt(trackIndex);

                    float extraSpace = 0;
                    float expandableSize = 0;

                    if (expandToGrid && trackCount > 1)
                    {
                        float spacingOffset = ctx.FirstAxisAutoSpace ? (ctx.FirstAxisCalculatedSpacing.Value - ctx.FirstSpacingMinMax.Min) * (track.ChildCount - 1) : 0;
                        float totalSpan = (track.SpanCount * (cellSize + ctx.FirstAxisCalculatedSpacing.Value)) - (track.ExpandedCount * ctx.FirstAxisCalculatedSpacing.Value);
                        extraSpace = track.FillableLength - (track.ChildLength - track.PreallocatedSpace + totalSpan) - spacingOffset;
                    }
                    else
                    {
                        expandableSize = GetExpandableSize(childLayout, ref ctx, ref track, out extraSpace);
                        ctx.RecalculateFirstAxisAutoSpace(ref track, ref extraSpace);
                    }

                    float shift = ctx.FirstAxisCenterAligned ? -0.5f * (track.FillableLength - extraSpace) : 0;

                    float crossMax = 0;

                    int increment = track.StartIndex <= track.EndIndex ? 1 : -1;

                    ref ExpandableRange range = ref ranges.ElementAt(trackIndex);

                    for (int i = track.StartIndex; i != track.EndIndex; i += increment)
                    {
                        childLayout.Index = ctx.ParentElement.Children[i];

                        float adjustedExpand = expandToGrid ? ctx.GetSpannedSize(childLayout.SizeMinMax[firstAxisIndex].Min, cellSize, out _) : expandableSize;

                        ExpandChild(childLayout, ref ctx, firstAxisIndex, adjustedExpand, secondAxisIndex, range.MinMax.Clamp(crossExpandSize), ref ctx.ParentSize);

                        float3 childSize = childLayout.RotatedSize;
                        childSize += childLayout.CalculatedMargin.Size;

                        // the child's position needs to be shifted an extra half of its length
                        // in center-aligned arrangements. Otherwise we'd position siblings only relative
                        // to the size of their neighbor siblings, rather than accounting for this child's 
                        // size, which can lead to undesired overlap.
                        float childExtraLength = primaryAxisCenterAligned ? 0.5f * childSize[firstAxisIndex] : 0;

                        crossMax = math.max(childSize[secondAxisIndex], crossMax);

                        // update offset and alignment for arranged axis
                        childLayout.Alignment[secondAxisIndex] = autolayoutAlignment;

                        ref Length3 childPosition = ref childLayout.Position;

                        float newPos = newPosition[0] + childExtraLength + shift;

                        childPosition[firstAxisIndex] = new Length() { Type = LengthType.Value, Raw = newPos };

                        childLayout.CalculatePosition(ctx.ParentSize[firstAxisIndex], firstAxisIndex);

                        childPosition[secondAxisIndex] = new Length() { Type = LengthType.Value, Raw = newPosition[1] };

                        if (!crossAxisCenterAligned)
                        {
                            // calculate new value because config changed
                            childLayout.CalculatePosition(ctx.ParentSize[secondAxisIndex], secondAxisIndex);
                        }

                        newPosition[0] += childSize[firstAxisIndex] + ctx.FirstAxisCalculatedSpacing.Value;
                    }

                    range.ExpectedSize = crossMax;

                    // increment total offset
                    newPosition[1] += crossMax + ctx.SecondAxisCalculatedSpacing.Value;
                    newPosition[0] = 0;
                }

                sizeAlongCrossAxis = newPosition[1] - ctx.SecondAxisCalculatedSpacing.Value - baseOffset;

                if (!crossAxisCenterAligned)
                {
                    // only needs a position adjustment
                    // pass if center aligned
                    return;
                }

                float crossShift = -0.5f * sizeAlongCrossAxis;

                for (int trackIndex = trackStartIndex; trackIndex != trackEndIndex; trackIndex += trackIncrement)
                {
                    ref ExpandableTrack track = ref tracks.ElementAt(trackIndex);

                    int increment = track.StartIndex <= track.EndIndex ? 1 : -1;

                    float extraCrossLength = 0.5f * ranges[trackIndex].ExpectedSize;

                    for (int i = track.StartIndex; i != track.EndIndex; i += increment)
                    {
                        childLayout.Index = ctx.ParentElement.Children[i];

                        ref Length3 childPosition = ref childLayout.Position;

                        Length crossAxisPosition = childPosition[secondAxisIndex];
                        crossAxisPosition.Raw += crossShift + extraCrossLength;
                        childPosition[secondAxisIndex] = crossAxisPosition;

                        // calculate new value because config changed
                        childLayout.CalculatePosition(ctx.ParentSize[secondAxisIndex], secondAxisIndex);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float GetGridCellSize(ref AutoLayoutContext ctx, ref NativeList<ExpandableTrack> tracks)
            {
                float cellSize = float.MaxValue;
                int firstAxisIndex = ctx.FirstAxis.Index();
                int trackCount = tracks.Length;

                float spacing = ctx.FirstAxisAutoSpace ? ctx.FirstSpacingMinMax.Min : ctx.FirstAxisCalculatedSpacing.Value;

                for (int i = 0; i < trackCount; ++i)
                {
                    ref ExpandableTrack track = ref tracks.ElementAt(i);

                    if (track.SpanCount == 0)
                    {
                        continue;
                    }

                    float singleSpan = track.GetSingleSpanSize(ctx.ParentSize[firstAxisIndex], spacing);

                    cellSize = math.min(cellSize, singleSpan);

                    if (cellSize < ctx.BaseCellSize)
                    {
                        break;
                    }
                }

                return math.max(ctx.BaseCellSize, cellSize);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void GridifyAutoSpacing(ref LayoutAccess.Properties childLayout, ref AutoLayoutContext ctx, ref NativeList<ExpandableTrack> tracks, float cellSize)
            {
                if (!ctx.FirstAxisAutoSpace)
                {
                    return;
                }

                int trackCount = tracks.Length;
                int increment = tracks[0].StartIndex < tracks[0].EndIndex ? 1 : -1;
                float minSpace = float.MaxValue;
                bool allTracksHaveOneChild = true;

                for (int trackIndex = 0; trackIndex < trackCount; ++trackIndex)
                {
                    ref ExpandableTrack track = ref tracks.ElementAt(trackIndex);

                    allTracksHaveOneChild &= track.ChildCount == 1;

                    float totalChildSize = 0;

                    for (int i = track.StartIndex; i != track.EndIndex; i += increment)
                    {
                        childLayout.Index = ctx.ParentElement.Children[i];

                        float childSize = childLayout.CalculatedSize[track.ExpandAxis].Value;

                        if (childLayout.AutoSize[track.ExpandAxis] == AutoSize.Expand)
                        {
                            Length.MinMax minMax = childLayout.SizeMinMax[track.ExpandAxis];
                            childSize = minMax.Clamp(ctx.GetSpannedSize(minMax.Min, cellSize, out _));
                        }

                        childSize += childLayout.CalculatedMargin.Size[track.ExpandAxis];

                        totalChildSize += childSize;
                    }

                    minSpace = math.min((track.FillableLength - totalChildSize) / math.max(track.ChildCount - 1, 1), minSpace);
                }

                ctx.FirstAxisSpacing = new Length() { Raw = allTracksHaveOneChild ? 0 : minSpace };
                ctx.FirstAxisCalculatedSpacing = ctx.CalculateFirstSpacing(tracks[0].FillableLength);
            }
        }

        internal struct AutoLayoutContext
        {
            public AutoLayout AutoLayout;
            public bool3 FormerAxisIndexMask;
            public HierarchyElement ParentElement;
            public Length2.Calculated CalculatedSpacing;
            public float3 ParentSize;
            public float BaseCellSize;

            private bool Cross => AutoLayout.Cross.Enabled && AutoLayout.Cross.Axis != AutoLayout.Axis;

            public bool FirstAxisAutoSpace => Cross ? AutoLayout.Cross.AutoSpace : AutoLayout.AutoSpace;
            public bool SecondAxisAutoSpace => Cross ? AutoLayout.AutoSpace : false;

            public Axis FirstAxis => Cross ? AutoLayout.Cross.Axis : AutoLayout.Axis;
            public Axis SecondAxis => Cross ? AutoLayout.Axis : Axis.None;

            public int FirstAxisAlignment => Cross ? AutoLayout.Cross.Alignment : AutoLayout.Alignment;
            public int SecondAxisAlignment => AutoLayout.Alignment;

            public bool FirstAxisCenterAligned => FirstAxisAlignment == 0;
            public bool SecondAxisCenterAligned => SecondAxisAlignment == 0;

            public bool InvertFirstAxisOrder => Cross ? AutoLayout.Cross.ReverseOrder : AutoLayout.ReverseOrder;
            public bool InvertSecondAxisOrder => Cross ? AutoLayout.ReverseOrder : false;

            public bool InvertFirstAxisPositions => (FirstAxis == Axis.Y) ^ (FirstAxisAlignment == 1) ^ InvertFirstAxisOrder;
            public bool InvertSecondAxisPositions => (SecondAxis == Axis.Y) ^ (SecondAxisAlignment == 1) ^ InvertSecondAxisOrder;

            public Length.Calculated FirstAxisCalculatedSpacing
            {
                get
                {
                    return Cross ? CalculatedSpacing.Second : CalculatedSpacing.First;
                }
                set
                {
                    if (Cross)
                    {
                        CalculatedSpacing.Second = value;
                    }
                    else
                    {
                        CalculatedSpacing.First = value;
                    }
                }
            }

            public Length.Calculated SecondAxisCalculatedSpacing
            {
                get
                {
                    return AutoLayout.CrossEnabled ? CalculatedSpacing.First : default;
                }
                set
                {
                    if (Cross)
                    {
                        CalculatedSpacing.First = value;
                    }
                }
            }

            public Length FirstAxisSpacing
            {
                get
                {
                    return Cross ? AutoLayout.Cross.Spacing : AutoLayout.Spacing;
                }
                set
                {
                    if (Cross)
                    {
                        AutoLayout.Cross.Spacing = value;
                    }
                    else
                    {
                        AutoLayout.Spacing = value;
                    }
                }
            }

            public Length SecondAxisSpacing
            {
                get
                {
                    return Cross ? AutoLayout.Spacing : default;
                }
                set
                {
                    if (Cross)
                    {
                        AutoLayout.Spacing = value;
                    }
                }
            }

            public Length.MinMax FirstSpacingMinMax => Cross ? AutoLayout.Cross.SpacingMinMax : AutoLayout.SpacingMinMax;
            public Length.MinMax SecondSpacingMinMax => Cross ? AutoLayout.SpacingMinMax : default;

            public Length.Calculated CalculateFirstSpacing(float relativeTo) => Cross ? AutoLayout.Cross.Calc(relativeTo) : AutoLayout.Calc(relativeTo);
            public Length.Calculated CalculateSecondSpacing(float relativeTo) => Cross ? AutoLayout.Calc(relativeTo) : default;

            public float GetSpannedSize(float minSize, out int spans) => GetSpannedSize(minSize, BaseCellSize, out spans);

            public float GetSpannedSize(float minSize, float cellSize, out int spans)
            {
                if (!AutoLayout.CrossEnabled || !AutoLayout.Cross.ExpandToGrid)
                {
                    spans = 0;
                    return minSize;
                }

                if (Math.ApproximatelyLessThan(BaseCellSize, 0))
                {
                    spans = 1;
                    return cellSize;
                }

                float spanSize = cellSize + FirstAxisCalculatedSpacing.Value;

                spans = (int)math.max(math.ceil(minSize / BaseCellSize), 1);

                return (spans * spanSize) - FirstAxisCalculatedSpacing.Value;
            }

            public void RecalculateFirstAxisAutoSpace(ref ExpandableTrack track, ref float remainingSpacePostExpand)
            {
                if (!FirstAxisAutoSpace)
                {
                    return;
                }


                float minSpace = math.max(FirstSpacingMinMax.Min, 0);
                float preallocatedSpacing = minSpace * (track.ChildCount - 1);
                FirstAxisSpacing = new Length() { Raw = (remainingSpacePostExpand + preallocatedSpacing) / (track.ChildCount - 1) };
                FirstAxisCalculatedSpacing = CalculateFirstSpacing(track.FillableLength);
                remainingSpacePostExpand -= (FirstAxisCalculatedSpacing.Value - minSpace) * (track.ChildCount - 1);
            }
        }

        internal struct ExpandableTrack
        {
            public int StartIndex;
            public int SpanCount;
            public int ExpandedCount;
            public float ChildLength;
            public float PreallocatedSpace;
            public int EndIndex;
            public int ExpandAxis;
            public float FillableLength;

            public int ChildCount => math.abs(StartIndex - EndIndex);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetSingleSpanSize(float parentSize, float spacing)
            {
                float spaceAdjustment = ChildCount == 1 ? 0 : (SpanCount - ExpandedCount) * spacing;

                return (parentSize - ChildLength + PreallocatedSpace - spaceAdjustment) / SpanCount;
            }
        }

        internal struct ExpandableRange
        {
            public Length.MinMax MinMax;
            public float ExpectedSize;
            public bool Expand;
            public bool PreProcess;
        }
    }
}
