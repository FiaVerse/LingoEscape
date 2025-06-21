// Copyright (c) Supernova Technologies LLC
//#define LOG_COMPARISON_COUNT
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal static partial class CameraSorting
    {
        [BurstCompile]
        internal struct Runner : IInitializable
        {
            public CameraConfig Camera;
            /// <summary>
            /// The batch roots to try and sort
            /// </summary>
            public NativeList<DataStoreID> BatchRootsToSort;
            /// <summary>
            /// The batch roots that should be rendered
            /// </summary>
            public NativeList<DataStoreID> BatchRootsToRender;

            public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
            public NativeList<float4x4> LocalFromWorldMatrices;
            public NativeList<float4x4> WorldFromLocalMatrices;
            public NovaHashMap<DataStoreID, DrawCallSummary> DrawCallSummaries;
            public NovaHashMap<DataStoreID, SortGroupInfo> SortGroupInfos;
            public NovaHashMap<DataStoreID, NovaList<CoplanarSetID, CoplanarSet>> CoplanarSets;
            public NovaHashMap<DataStoreID, SortGroupHierarchyInfo> SortGroupHierarchyInfo;
            public NovaHashMap<DataStoreID, int> ScreenSpaceCameraTargets;
            public NovaHashMap<DataStoreID, NovaList<int>> ScreenSpaceAdditionalCameras;

            private NovaHashMap<DataStoreID, NovaList<DrawCallID, ProcessedDrawCall>> ProcessedDrawCalls;
            private NovaHashMap<DataStoreID, NovaList<CoplanarSetID, CoplanarSetLocation>> CoplanarSetInfo;

            private NativeList<NovaList<DrawCallID, ProcessedDrawCall>> drawCallBoundsPool;
            private NativeList<NovaList<CoplanarSetID, CoplanarSetLocation>> coplanarSetInfoPool;

            private NativeList<CoplanarSetIdentifier> transparentCoplanarSets;
            private RenderOrderDependencies renderOrderDependencies;
            private NativeList<CoplanarSetIdentifier> processingQueue;
            private NovaHashMap<CoplanarSetIdentifier, byte> processedCoplanarSets;
            private NovaHashMap<CoplanarSetIdentifier, byte> addedDependencies;
            private NativeList<CoplanarSetIdentifier> minSortedCoplanarSets;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Sort()
            {
                Camera.WorldFromCamera = math.inverse(Camera.CameraFromWorld);
                transparentCoplanarSets.Clear();
                BatchRootsToRender.Clear();

                // Go through all batch groups and add all coplanar sets if they should be rendered
                // with this camera
                for (int i = 0; i < BatchRootsToSort.Length; ++i)
                {
                    DataStoreID batchRootID = BatchRootsToSort[i];

                    // Check if it should be rendered for this camera
                    if (!ShouldRenderBatchGroup(batchRootID))
                    {
                        continue;
                    }

                    BatchRootsToRender.Add(batchRootID);
                    AddCoplanarSets(batchRootID);
                }

                // Sort the coplanar sets by min distance
                SortCoplanarSets();

                // Determines which coplanar sets need to render on top of others
                DetermineRenderOrderDependencies();

                // Updates the base bounds of every coplanar set, based on the render
                // order dependencies
                UpdateCoplanarSetBounds();

                // Updates the draw call bounds within each coplanar set based
                // on the new base bounds
                UpdateDrawCallBounds();

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ShouldRenderBatchGroup(DataStoreID batchRootID)
            {
                if (Camera.CameraType == CameraType.SceneView)
                {
                    // Render everything in scene view
                    return true;
                }

                if (!SortGroupHierarchyInfo.TryGetValue(batchRootID, out SortGroupHierarchyInfo hierarhcyInfo))
                {
                    return true;
                }


                if (ScreenSpaceCameraTargets.TryGetValue(hierarhcyInfo.HierarchyRoot, out int targetCameraID))
                {
                    // It's a screen space
                    if (targetCameraID == Camera.CameraInstanceID)
                    {
                        // This is the target camera
                        return true;
                    }

                    if (!ScreenSpaceAdditionalCameras.TryGetValue(hierarhcyInfo.HierarchyRoot, out NovaList<int> additionalCameras))
                    {
                        return false;
                    }

                    return additionalCameras.TryGetIndexOf(Camera.CameraInstanceID, out _);
                }
                else
                {
                    // Not a screen space
                    return true;
                }
            }

            /// <summary>
            /// Updates the bounds of every draw call within each coplanar set based on
            /// the updated base bounds of the coplanar set
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void UpdateDrawCallBounds()
            {
                for (int i = 0; i < BatchRootsToRender.Length; ++i)
                {
                    DataStoreID batchRootID = BatchRootsToRender[i];
                    if (!DrawCallSummaries.TryGetValue(batchRootID, out DrawCallSummary drawCallSummary))
                    {
                        continue;
                    }

                    NovaList<DrawCallID, ProcessedDrawCall> drawCallBounds = ProcessedDrawCalls.GetAndResize(batchRootID, drawCallSummary.DrawCallCount);
                    NovaList<CoplanarSetID, CoplanarSetLocation> perCameraSetInfos = CoplanarSetInfo[batchRootID];
                    NovaList<CoplanarSetID, CoplanarSet> coplanarSets = CoplanarSets[batchRootID];
                    for (int j = 0; j < drawCallSummary.DrawCalls.Length; ++j)
                    {
                        ref DrawCall drawCall = ref drawCallSummary.DrawCalls.ElementAt(j);
                        ref DrawCallDescriptor descriptor = ref drawCallSummary.DrawCallDescriptors.ElementAt(drawCall.DescriptorID);

                        ref ProcessedDrawCall perCameraInfo = ref drawCallBounds.ElementAt(drawCall.ID);

                        if (!descriptor.DrawCallType.Is2D())
                        {
                            // For 3D, we don't apply any offsetting so just convert the bounds
                            // to world space (we can't cache this since the root position may have changed
                            // without the engines re-running)
                            ref CoplanarSet coplanarSet = ref coplanarSets.ElementAt(drawCall.CoplanarSetID);
                            DataStoreIndex coplanarSetRootIndex = DataStoreIDToDataStoreIndex[coplanarSet.RootID];
                            ref float4x4 worldFromSet = ref WorldFromLocalMatrices.ElementAt(coplanarSetRootIndex);

                            perCameraInfo = new ProcessedDrawCall()
                            {
                                AdjustedBounds = AABB.Transform3D(ref worldFromSet, ref drawCall.CoplanarSpaceRenderBounds).ToBounds(),
                                ViewingFromBehind = false
                            };
                            continue;
                        }

                        // For transparent, apply the calculated offset
                        ref CoplanarSetLocation setInfo = ref perCameraSetInfos.ElementAt(drawCall.CoplanarSetID);

                        if (!setInfo.Distances.IsValid)
                        {
                            perCameraInfo = ProcessedDrawCall.DontRender;
                            continue;
                        }

                        float3 adjustmentAmountPerDrawCall = Constants.InverseDrawCallDistanceAdjustmentRatio * setInfo.ProcessedBounds.DrawCallOffsetDirection;
                        float3 center = drawCall.TransparentDrawCallOrderInCoplanarSet * adjustmentAmountPerDrawCall + setInfo.ProcessedBounds.AdjustedWorldSpaceCenter;

                        if (!Math.ValidAndFinite(ref center))
                        {
                            Debug.LogError($"BoundCenter for drawcall was invalid: {center}");
                        }

                        perCameraInfo = new ProcessedDrawCall()
                        {
                            AdjustedBounds = new Bounds(center, setInfo.WorldSpaceSize),
                            ViewingFromBehind = setInfo.ViewingFromBehind
                        };
                    }

                    ProcessedDrawCalls[batchRootID] = drawCallBounds;
                }
            }

            /// <summary>
            /// Updates the base bounds of all coplanar sets based on the render order
            /// dependencies
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void UpdateCoplanarSetBounds()
            {
                processingQueue.Clear();
                processedCoplanarSets.Clear();
                addedDependencies.Clear();
                processingQueue.AddRange(transparentCoplanarSets.AsArray());

                while (processingQueue.Length != 0)
                {
                    CoplanarSetIdentifier currentSet = processingQueue.Last();
                    ref CoplanarSetLocation perCameraSetInfo = ref CoplanarSetInfo.GetSet(ref currentSet);

                    if (processedCoplanarSets.ContainsKey(currentSet) || !perCameraSetInfo.Distances.IsValid)
                    {
                        processingQueue.RemoveLast();
                        continue;
                    }

                    // Ensure all of the dependencies have been processed
                    bool hasItemsToRenderOver = renderOrderDependencies.TryGetDependencies(currentSet, out NovaList<CoplanarSetIdentifier> dependencies);
                    if (hasItemsToRenderOver && !addedDependencies.ContainsKey(currentSet))
                    {
                        bool canProcess = true;
                        for (int i = 0; i < dependencies.Length; ++i)
                        {
                            if (!processedCoplanarSets.ContainsKey(dependencies[i]))
                            {
                                processingQueue.Add(dependencies[i]);
                                canProcess = false;
                            }
                        }

                        addedDependencies.Add(currentSet, 0);

                        if (!canProcess)
                        {
                            continue;
                        }
                    }

                    processingQueue.RemoveLast();
                    processedCoplanarSets.Add(currentSet, 1);

                    // Adjust the center based on anything we need to render over
                    if (hasItemsToRenderOver)
                    {
                        for (int i = 0; i < dependencies.Length; ++i)
                        {
                            ref CoplanarSetIdentifier dependencyIdentifier = ref dependencies.ElementAt(i);
                            ref CoplanarSetLocation dependency = ref CoplanarSetInfo.GetSet(ref dependencyIdentifier);
                            int dependencyDrawCalls = CoplanarSets.GetSet(ref dependencyIdentifier).TransparentDrawCallCount;
                            Camera.EnsureRenderOver(ref perCameraSetInfo, ref dependency, dependencyDrawCalls);
                        }
                    }

                    float3 offsetDirection = Camera.GetWorldSpaceOffsetDirection(ref perCameraSetInfo);
                    float3 centerInWorldSpace = math.transform(Camera.WorldFromCamera, perCameraSetInfo.Distances.CenterPoint);

                    if (!Math.ValidAndFinite(ref centerInWorldSpace))
                    {
                        Debug.LogError($"BoundCenter for CoplanarSet was invalid: {centerInWorldSpace}");
                    }

                    if (!Math.ValidAndFinite(ref offsetDirection))
                    {
                        Debug.LogError($"OffsetDirection for CoplanarSet was invalid: {offsetDirection}");
                    }

                    perCameraSetInfo.ProcessedBounds = new ProcessedCoplanarSet()
                    {
                        AdjustedWorldSpaceCenter = centerInWorldSpace,
                        DrawCallOffsetDirection = offsetDirection,
                    };
                }
            }

            /// <summary>
            /// Determines which coplanar sets should render on top of others
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DetermineRenderOrderDependencies()
            {
                renderOrderDependencies.Clear();

                for (int i = 0; i < minSortedCoplanarSets.Length; ++i)
                {
                    ref CoplanarSetIdentifier coplanarSet = ref minSortedCoplanarSets.ElementAt(i);

                    if (CoplanarSets.GetSet(ref coplanarSet).TransparentDrawCallCount == 0)
                    {
                        continue;
                    }

                    ref CoplanarSetLocation perCameraSetInfo = ref CoplanarSetInfo.GetSet(ref coplanarSet);
                    if (!perCameraSetInfo.Distances.IsValid)
                    {
                        continue;
                    }


                    SortGroupInfo sortGroupInfo = GetSortGroupInfo(coplanarSet.BatchRootID);

                    for (int j = i + 1; j < minSortedCoplanarSets.Length; ++j)
                    {
                        ref CoplanarSetIdentifier setToCheck = ref minSortedCoplanarSets.ElementAt(j);
                        if (CoplanarSets.GetSet(ref setToCheck).TransparentDrawCallCount == 0)
                        {
                            continue;
                        }

                        ref CoplanarSetLocation setToCheckInfo = ref CoplanarSetInfo.GetSet(ref setToCheck);
                        if (!setToCheckInfo.Distances.IsValid)
                        {
                            continue;
                        }

                        if ((Constants.DistanceComparisonThreshold * setToCheckInfo.Distances.MinDistance) > perCameraSetInfo.Distances.MaxDistance &&
                            !Math.ApproximatelyEqual(setToCheckInfo.Distances.MinDistance, perCameraSetInfo.Distances.MaxDistance))
                        {
                            // We've checked all elements with overlapping min/max, so break
                            break;
                        }

                        SortGroupInfo toCheckSortInfo = GetSortGroupInfo(setToCheck.BatchRootID);
                        if (toCheckSortInfo.RenderQueue != sortGroupInfo.RenderQueue)
                        {
                            // Their render queues are different, so we don't need to do anything ourselves
                            continue;
                        }

                        if (!perCameraSetInfo.NDCBounds.Overlaps2D(ref setToCheckInfo.NDCBounds))
                        {
                            // They don't overlap visually
                            continue;
                        }


                        GetRenderOver(ref perCameraSetInfo.CameraSpaceBounds, ref setToCheckInfo.CameraSpaceBounds, out bool visuallyOverlap, out bool shouldRenderOverToCheck);
                        if (!visuallyOverlap)
                        {
                            continue;
                        }

                        if (perCameraSetInfo.CameraSpaceBounds.Plane == setToCheckInfo.CameraSpaceBounds.Plane)
                        {
                            // They are coplanar
                            if (sortGroupInfo.SortingOrder != toCheckSortInfo.SortingOrder)
                            {
                                // Use the batch group sorting order, if they're not equal
                                shouldRenderOverToCheck = sortGroupInfo.SortingOrder > toCheckSortInfo.SortingOrder;
                            }
                            else
                            {
                                // If batch group sorting orders are equal, use hierarchy if they are in the same
                                // hierarchy root
                                SortGroupHierarchyInfo hierarchyInfo = SortGroupHierarchyInfo[coplanarSet.BatchRootID];
                                SortGroupHierarchyInfo toCheckHierarchyInfo = SortGroupHierarchyInfo[setToCheck.BatchRootID];

                                if (hierarchyInfo.HierarchyRoot == toCheckHierarchyInfo.HierarchyRoot)
                                {
                                    shouldRenderOverToCheck = hierarchyInfo.Order > toCheckHierarchyInfo.Order;
                                }
                            }

                            if (perCameraSetInfo.ViewingFromBehind && setToCheckInfo.ViewingFromBehind)
                            {
                                // If we are viewing both from behind, flip the order
                                shouldRenderOverToCheck = !shouldRenderOverToCheck;
                            }
                        }

                        if (shouldRenderOverToCheck)
                        {
                            renderOrderDependencies.AddDependency(coplanarSet, setToCheck);
                        }
                        else
                        {
                            renderOrderDependencies.AddDependency(setToCheck, coplanarSet);
                        }
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private SortGroupInfo GetSortGroupInfo(DataStoreID dataStoreID)
            {
                if (SortGroupInfos.TryGetValue(dataStoreID, out SortGroupInfo sortGroupInfo))
                {
                    return sortGroupInfo;
                }
                else
                {
                    return SortGroupInfo.Default;
                }
            }

            /// <summary>
            /// Determines if the two provided bounds visually overlap and, if so, which one should render
            /// on top
            /// </summary>
            /// <param name="a"></param>
            /// <param name="b"></param>
            /// <param name="visuallyOverlap"></param>
            /// <param name="aShouldRenderOverB"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void GetRenderOver(ref Quadrilateral3D a, ref Quadrilateral3D b, out bool visuallyOverlap, out bool aShouldRenderOverB)
            {
                ProjectedLocation4 bOnALocations = a.TryRaycastAllPoints(ref b.Points, out float3x4 bOnA);

                if (bOnALocations.Encaspulated)
                {
                    // A encapsulates B
                    float aSum = Math.SumLengthSq(ref bOnA);
                    float bSum = Math.SumLengthSq(ref b.Points);
                    visuallyOverlap = true;
                    aShouldRenderOverB = aSum < bSum;
                    return;
                }

                if (bOnALocations.AnyPointInBounds)
                {
                    // Use the sum of the points that *do* overlap
                    visuallyOverlap = true;
                    SumOverlappingPoints(ref bOnALocations, ref b, ref bOnA, out float bSum, out float projectedSum);
                    aShouldRenderOverB = projectedSum < bSum;
                    return;
                }

                ProjectedLocation4 aOnBLocations = b.TryRaycastAllPoints(ref a.Points, out float3x4 aOnB);
                if (aOnBLocations.Encaspulated)
                {
                    // B encapsulates A
                    float aSum = Math.SumLengthSq(ref a.Points);
                    float bSum = Math.SumLengthSq(ref aOnB);
                    visuallyOverlap = true;
                    aShouldRenderOverB = aSum < bSum;
                    return;
                }

                if (aOnBLocations.AnyPointInBounds)
                {
                    // Use the sum of the points that *do* overlap
                    visuallyOverlap = true;
                    SumOverlappingPoints(ref aOnBLocations, ref a, ref aOnB, out float aSum, out float projectedSum);
                    aShouldRenderOverB = aSum < projectedSum;
                    return;
                }

                // Try to see if any of the diagonals intersect, and if they do, use the
                // intersection point
                float3 zero = float3.zero;
                ref float3 p = ref a.Points[0];
                ref float3 p1 = ref a.Points[2];
                float3 hitOnA = default;
                float3 hitOnB = default;

                if (Math.TryIntersectLines(ref p, ref p1, ref bOnA[0], ref bOnA[2], out hitOnA) &&
                    b.Plane.TryRaycast(ref zero, ref hitOnA, out hitOnB))
                {
                    visuallyOverlap = true;
                    aShouldRenderOverB = hitOnA.ShorterThan(ref hitOnB);
                    return;
                }

                if (Math.TryIntersectLines(ref p, ref p1, ref bOnA[1], ref bOnA[3], out hitOnA) &&
                    b.Plane.TryRaycast(ref zero, ref hitOnA, out hitOnB))
                {
                    visuallyOverlap = true;
                    aShouldRenderOverB = hitOnA.ShorterThan(ref hitOnB);
                    return;
                }

                ref float3 p2 = ref a.Points[1];
                ref float3 p3 = ref a.Points[3];
                if (Math.TryIntersectLines(ref p2, ref p3, ref bOnA[0], ref bOnA[2], out hitOnA) &&
                    b.Plane.TryRaycast(ref zero, ref hitOnA, out hitOnB))
                {
                    visuallyOverlap = true;
                    aShouldRenderOverB = hitOnA.ShorterThan(ref hitOnB);
                    return;
                }
                if (Math.TryIntersectLines(ref p2, ref p3, ref bOnA[1], ref bOnA[3], out hitOnA) &&
                    b.Plane.TryRaycast(ref zero, ref hitOnA, out hitOnB))
                {
                    visuallyOverlap = true;
                    aShouldRenderOverB = hitOnA.ShorterThan(ref hitOnB);
                    return;
                }

                visuallyOverlap = false;
                aShouldRenderOverB = default;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void SumOverlappingPoints(ref ProjectedLocation4 locations, ref Quadrilateral3D quad, ref float3x4 projectedPoints, out float quadSum, out float projectedSum)
            {
                quadSum = 0;
                projectedSum = 0;

                for (int i = 0; i < 4; ++i)
                {
                    if (locations[i] != ProjectedLocation.Inside)
                    {
                        continue;
                    }

                    quadSum += math.lengthsq(quad.Points[i]);
                    projectedSum += math.lengthsq(projectedPoints[i]);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SortCoplanarSets()
            {
                minSortedCoplanarSets.CopyFrom(ref transparentCoplanarSets);

                minSortedCoplanarSets.Sort(new MinDistanceSorter()
                {
                    PerCameraSetInfo = CoplanarSetInfo,
                });
            }

            /// <summary>
            /// For the provided batch group, goes through all coplanar sets and gets the
            /// data needed to sort the coplanar sets correctly, such as bounds in camera space,
            /// distances from camera, etc.
            /// </summary>
            /// <param name="batchRootID"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AddCoplanarSets(DataStoreID batchRootID)
            {
                // Get the coplanar sets in the batch group
                if (!CoplanarSets.TryGetValue(batchRootID, out NovaList<CoplanarSetID, CoplanarSet> coplanarSets))
                {
                    return;
                }

                NovaList<CoplanarSetID, CoplanarSetLocation> perCameraInfos = CoplanarSetInfo.GetAndResize(batchRootID, coplanarSets.Length);
                NovaList<CoplanarSetID, CoplanarSet> renderCounts = CoplanarSets[batchRootID];

                for (int i = 0; i < coplanarSets.Length; ++i)
                {
                    if (renderCounts.ElementAt(i).TransparentDrawCallCount == 0)
                    {
                        // If no transparent draw calls, nothing to do
                        continue;
                    }

                    transparentCoplanarSets.Add(new CoplanarSetIdentifier()
                    {
                        BatchRootID = batchRootID,
                        CoplanarSetID = i
                    });

                    ref CoplanarSet coplanarSet = ref coplanarSets.ElementAt(i);
                    DataStoreIndex coplanarSetRootIndex = DataStoreIDToDataStoreIndex[coplanarSet.RootID];

                    ref float4x4 worldFromSet = ref WorldFromLocalMatrices.ElementAt(coplanarSetRootIndex);
                    ref float4x4 setFromWorld = ref LocalFromWorldMatrices.ElementAt(coplanarSetRootIndex);

                    float4x4 cameraFromSet = math.mul(Camera.CameraFromWorld, worldFromSet);
                    float4x4 cameraSpaceBoundCorners = math.mul(cameraFromSet, coplanarSet.CoplanarSpaceRenderBounds.GetCorners2D());
                    float4 z = cameraSpaceBoundCorners.GetRow(2);

                    ref CoplanarSetLocation perCameraInfo = ref perCameraInfos.ElementAt(i);
                    if (math.all(z < float4.zero))
                    {
                        // All points are behind camera
                        perCameraInfo.Distances = CoplanarSetDistances.Invalid;
                        continue;
                    }

                    NovaPlane plane = NovaPlane.Create(ref cameraFromSet);

                    perCameraInfo.WorldSpaceSize = AABB.Transform2D(ref worldFromSet, ref coplanarSet.CoplanarSpaceRenderBounds).GetSize();

                    // Get the bounds in camera space
                    perCameraInfo.CameraSpaceBounds = new Quadrilateral3D(ref cameraSpaceBoundCorners, ref plane);

                    // The projection matrix assumes that negative z is forward, so do a z-flip
                    cameraSpaceBoundCorners.c0.z = -cameraSpaceBoundCorners.c0.z;
                    cameraSpaceBoundCorners.c1.z = -cameraSpaceBoundCorners.c1.z;
                    cameraSpaceBoundCorners.c2.z = -cameraSpaceBoundCorners.c2.z;
                    cameraSpaceBoundCorners.c3.z = -cameraSpaceBoundCorners.c3.z;

                    float4x4 clipSpaceCorners = math.mul(Camera.ProjectionMatrix, cameraSpaceBoundCorners);
                    // Clip space -> NDC
                    clipSpaceCorners.c0 /= clipSpaceCorners.c0.w;
                    clipSpaceCorners.c1 /= clipSpaceCorners.c1.w;
                    clipSpaceCorners.c2 /= clipSpaceCorners.c2.w;
                    clipSpaceCorners.c3 /= clipSpaceCorners.c3.w;
                    perCameraInfo.NDCBounds = new AABB(ref clipSpaceCorners);

                    //Debug.Log($"{batchRootID}: {perCameraInfo.NDCBounds}");

                    // Determine if we are looking at the back of the coplanar set
                    // There's almost certainly a better way to do this...
                    float3 setSpaceCameraPos = Math.Transform(ref setFromWorld, ref Camera.WorldFromCamera, float3.zero);
                    perCameraInfo.ViewingFromBehind = setSpaceCameraPos.z > 0f;

                    perCameraInfo.Distances = Camera.GetDistances(ref perCameraInfo.CameraSpaceBounds);
                }

                CoplanarSetInfo[batchRootID] = perCameraInfos;
            }

            private struct MinDistanceSorter : IComparer<CoplanarSetIdentifier>
            {
                public NovaHashMap<DataStoreID, NovaList<CoplanarSetID, CoplanarSetLocation>> PerCameraSetInfo;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public int Compare(CoplanarSetIdentifier x, CoplanarSetIdentifier y)
                {
                    ref CoplanarSetDistances xMinMax = ref PerCameraSetInfo.GetSet(ref x).Distances;
                    ref CoplanarSetDistances yMinMax = ref PerCameraSetInfo.GetSet(ref y).Distances;
                    return (int)math.sign(xMinMax.MinDistance - yMinMax.MinDistance);
                }
            }

            [BurstCompile]
            [MonoPInvokeCallback(typeof(BurstMethod))]
            public static unsafe void DoSort(void* data)
            {
                UnsafeUtility.AsRef<Runner>(data).Sort();
            }

            #region Public
            /// <summary>
            /// Gets the processed draw call bounds for the provided batch group
            /// </summary>
            /// <param name="batchGroupID"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public NovaList<DrawCallID, ProcessedDrawCall> GetDrawCallBounds(DataStoreID batchGroupID) => ProcessedDrawCalls[batchGroupID];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddBatchGroup(DataStoreID batchRootID)
            {
                ProcessedDrawCalls.Add(batchRootID, drawCallBoundsPool.GetFromPoolOrInit());
                CoplanarSetInfo.Add(batchRootID, coplanarSetInfoPool.GetFromPoolOrInit());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveBatchGroup(DataStoreID batchRootID)
            {
                if (ProcessedDrawCalls.TryGetAndRemove(batchRootID, out NovaList<DrawCallID, ProcessedDrawCall> drawCallBounds))
                {
                    drawCallBoundsPool.ReturnToPool(ref drawCallBounds);
                }

                if (CoplanarSetInfo.TryGetAndRemove(batchRootID, out NovaList<CoplanarSetID, CoplanarSetLocation> coplanarSetInfo))
                {
                    coplanarSetInfoPool.ReturnToPool(ref coplanarSetInfo);
                }
            }

            public void Init()
            {
                BatchRootsToRender.Init();
                ProcessedDrawCalls.Init();
                CoplanarSetInfo.Init();

                drawCallBoundsPool.Init();
                coplanarSetInfoPool.Init();

                transparentCoplanarSets.Init();
                renderOrderDependencies.Init();
                processingQueue.Init();
                processedCoplanarSets.Init();
                addedDependencies.Init();

                minSortedCoplanarSets.Init();
            }

            public void Dispose()
            {
                BatchRootsToRender.Dispose();
                ProcessedDrawCalls.Dispose();
                CoplanarSetInfo.Dispose();

                drawCallBoundsPool.DisposeListAndElements();
                coplanarSetInfoPool.DisposeListAndElements();

                transparentCoplanarSets.Dispose();
                renderOrderDependencies.Dispose();
                processingQueue.Dispose();
                processedCoplanarSets.Dispose();
                addedDependencies.Dispose();

                minSortedCoplanarSets.Dispose();
            }
            #endregion
        }
    }
}

