// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Nova.Internal.Layouts
{
    internal partial class LayoutCore
    {
        [BurstCompile]
        public struct DiffAndDirty : INovaJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeList<Length3> LengthConfigs;
            [NativeDisableParallelForRestriction]
            public NativeList<Length3.MinMax> LengthRanges;
            [NativeDisableParallelForRestriction]
            public NativeList<bool> UseRotations;
            [NativeDisableParallelForRestriction]
            public NativeList<float3> Alignments;
            [NativeDisableParallelForRestriction]
            public NativeList<AutoSize3> Autosizes;
            [NativeDisableParallelForRestriction]
            public NativeList<AspectRatio> AspectRatios;
            [NativeDisableParallelForRestriction]
            public NativeList<AutoLayout> AutoLayouts;
            [NativeDisableParallelForRestriction]
            public unsafe NovaHashMap<DataStoreIndex, Axes> FormerAutoLayoutAxes;
            [NativeDisableParallelForRestriction]
            public NativeList<quaternion> TransformRotations;

            [NativeDisableParallelForRestriction]
            public NativeList<LayoutPointer> DirtyLayouts;
            [NativeDisableParallelForRestriction]
            public NativeList<HierarchyDependency> DirtyDependencies;

            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
            [ReadOnly]
            public NativeList<bool> UsingTransformPosition;

            [ReadOnly]
            public NovaHashMap<DataStoreID, PreviewSize> PreviewSizes;

            public unsafe void Execute(int index)
            {
                ref LayoutPointer dirtyLayout = ref DirtyLayouts.ElementAt(index);
                DataStoreID dirtyElementID = dirtyLayout.ID;

                if (!dirtyElementID.IsValid)
                {
                    return;
                }

                if (!HierarchyLookup.TryGetValue(dirtyElementID, out DataStoreIndex layoutIndex))
                {
                    Debug.LogError($"Failed to get index for {dirtyElementID}");
                    return;
                }

                LayoutAccess.Properties layout = LayoutAccess.Get(layoutIndex, ref LengthConfigs);
                layout.WrapMinMaxes(ref LengthRanges);
                layout.WrapAutoSizes(ref Autosizes);
                layout.WrapAlignments(ref Alignments);
                layout.WrapUseRotations(ref UseRotations);
                layout.WrapAspectRatios(ref AspectRatios);

                ref LayoutAccess.PropertySnapshot snapshot = ref LayoutAccess.Properties.AsSnapshot(ref UnsafeUtility.AsRef<Layout>(dirtyLayout.Layout));
                HierarchyDependency propertyDependents = layout.ApplyDiff(ref snapshot, excludePosition: UsingTransformPosition[layoutIndex]);

                ref AutoLayout autoLayout = ref AutoLayouts.ElementAt(layoutIndex);
                ref AutoLayout dirtyAutoLayout = ref UnsafeUtility.AsRef<AutoLayout>(dirtyLayout.AutoLayout);

                bool primaryDirty = autoLayout.Axis != dirtyAutoLayout.Axis;
                bool crossDirty = autoLayout.Cross.Axis != dirtyAutoLayout.Cross.Axis;

                if (primaryDirty || crossDirty)
                {
                    Axis primaryAxis = primaryDirty ? autoLayout.Axis : Axis.None;
                    Axis crossAxis = crossDirty ? autoLayout.Cross.Axis : Axis.None;

                    // track former axis, so we can clear it
                    FormerAutoLayoutAxes.Add(layoutIndex, new Axes() { Primary = primaryAxis, Cross = crossAxis });
                }

                HierarchyDependency autoLayoutDependents = autoLayout.ApplyDiff(ref dirtyAutoLayout);

                HierarchyDependency maxDependency = HierarchyDependency.Max(propertyDependents, autoLayoutDependents);

                if (NovaApplication.ConstIsEditor)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    HierarchyElement element = Hierarchy[layoutIndex];

                    if (!element.ParentID.IsValid && PreviewSizes.TryGetValue(element.ID, out PreviewSize preview) && preview.Available && preview.Dirty)
                    {
                        maxDependency = HierarchyDependency.ParentAndChildren;
                    }
#pragma warning restore CS0162 // Unreachable code detected
                }

                if (maxDependency.IsDirty)
                {
                    DirtyDependencies[layoutIndex] = HierarchyDependency.Max(maxDependency, DirtyDependencies[layoutIndex]);
                }
            }
        }

        [BurstCompile]
        public struct DirtyDependencies : INovaJob
        {
            [ReadOnly]
            public NativeList<AutoSize3> AutoSizes;

            public NativeList<AutoLayout> AutoLayouts;

            public NativeList<DataStoreIndex> DirtyIndices;
            public NativeList<HierarchyDependency> DirtyDependencyStates;

            [ReadOnly]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

            public void Execute()
            {
                int count = DirtyIndices.Length;
                for (int i = 0; i < count; ++i)
                {
                    DataStoreIndex layoutIndex = DirtyIndices[i];

                    HierarchyDependency dependency = DirtyDependencyStates[layoutIndex];

                    if (!dependency.HasDirectDependencies)
                    {
                        continue;
                    }

                    HierarchyElement element = Hierarchy[layoutIndex];

                    // Need to mark all shrinking parents dirty as well
                    if (TryDirtyParent(ref element))
                    {
                        count++;
                    }

                    if (dependency > HierarchyDependency.Parent)
                    {
                        count += TryDirtyChildren(ref element);
                    }
                }
            }

            private bool TryDirtyParent(ref HierarchyElement childElement)
            {
                DataStoreID parentID = childElement.ParentID;

                if (!parentID.IsValid || !HierarchyLookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
                {
                    return false;
                }

                bool maxDirtyParent = AutoLayouts.ElementAt(parentIndex).Enabled || math.any(AutoSizes[parentIndex].Shrink);

                HierarchyDependency dirtyDependency = maxDirtyParent ? HierarchyDependency.ParentAndChildren : HierarchyDependency.Self;
                return TryMarkDirty(parentIndex, dirtyDependency);
            }

            private int TryDirtyChildren(ref HierarchyElement parentElement)
            {
                NovaList<DataStoreIndex> children = parentElement.Children;
                int childCount = parentElement.ChildCount;

                int dirtyCount = 0;
                for (int i = 0; i < childCount; ++i)
                {
                    DataStoreIndex child = children[i];

                    if (TryMarkDirty(child, HierarchyDependency.Self))
                    {
                        dirtyCount++;
                    }
                }

                return dirtyCount;
            }

            private bool TryMarkDirty(DataStoreIndex indexToDirty, HierarchyDependency dependency)
            {
                HierarchyDependency currentDependency = DirtyDependencyStates[indexToDirty];

                if (currentDependency == HierarchyDependency.ParentAndChildren)
                {
                    return false;
                }

                bool added = false;
                if (!currentDependency.IsDirty)
                {
                    DirtyIndices.Add(indexToDirty);
                    added = true;
                }

                DirtyDependencyStates[indexToDirty] = HierarchyDependency.Max(currentDependency, dependency);

                return added;
            }
        }

        /// <summary>
        /// Need a better solution... this will iterate over all elements.
        /// It's still pretty fast, but we shouldn't have to do that...
        /// </summary>
        [BurstCompile]
        public unsafe struct FilterCleanElements : INovaJob
        {
            [WriteOnly]
            public NativeList<DataStoreIndex> DirtyIndices;

            [ReadOnly]
            public NativeList<HierarchyDependency> DirtyDependencies;

            public NativeReference<UnsafeAtomicCounter32> ActiveDirtyCount;
            public NativeReference<int> DirtyCountBeforeAnyUpdate;
            public NativeReference<bool> DirtyCountChanged;

            public void Execute()
            {
                DirtyCountChanged.Value = false;

                unsafe
                {
                    int currentDirtyCount = *ActiveDirtyCount.Value.Counter;
                    if (currentDirtyCount == 0)
                    {
                        // Nothing has been dirtied since we started our update
                        return;
                    }

                    DirtyCountChanged.Value = DirtyCountBeforeAnyUpdate.Value != currentDirtyCount;
                }

                bool appended = false;
                int total = DirtyDependencies.Length;
                for (int i = 0; i < total; ++i)
                {
                    if (!DirtyDependencies[i].IsDirty)
                    {
                        continue;
                    }

                    appended = true;
                    DirtyIndices.Add(i);
                }

                DirtyCountChanged.Value |= appended;
            }
        }

        [BurstCompile]
        public struct GetDependencies : IJob
        {
            public DataStoreID ElementID;

            // Not marked as readonly so we can get refs
            public NativeList<Length3> LengthConfigs;
            public NativeList<Length3.MinMax> LengthRanges;
            public NativeList<AutoSize3> AutoSizes;
            public NativeList<float3> Alignments;
            public NativeList<bool> UseRotations;
            public NativeList<AspectRatio> AspectRatios;
            public NativeList<AutoLayout> AutoLayouts;
            public NovaHashMap<DataStoreIndex, Axes> FormerAutoLayoutAxes;
            public NativeList<quaternion> TransformRotations;

            public NativeList<HierarchyElement> Hierarchy;
            public NativeList<LayoutPointer> DirtyLayouts;
            public NativeList<HierarchyDependency> DirtyDependencies;
            public NovaHashMap<DataStoreIndex, DataStoreID> AncestorBuffer;

            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

            public NativeList<DataStoreIndex> Dependencies;

            public unsafe void Execute()
            {
                Dependencies.Clear();

                if (!HierarchyLookup.TryGetValue(ElementID, out DataStoreIndex elementIndex))
                {
                    return;
                }

                DataStoreIndex rootIndex = GetDependencyRoot(elementIndex);
                Dependencies.Add(rootIndex);

                LayoutAccess.Properties elementProps = LayoutAccess.Get(0, ref LengthConfigs);
                elementProps.WrapAutoSizes(ref AutoSizes);
                elementProps.WrapMinMaxes(ref LengthRanges);
                elementProps.WrapAlignments(ref Alignments);
                elementProps.WrapUseRotations(ref UseRotations);
                elementProps.WrapAspectRatios(ref AspectRatios);

                int dependencyCount = Dependencies.Length;

                for (int i = 0; i < dependencyCount; ++i)
                {
                    DataStoreIndex layoutIndex = Dependencies[i];
                    ref HierarchyElement layoutElement = ref Hierarchy.ElementAt(layoutIndex);

                    elementProps.Index = layoutIndex;

                    ref LayoutPointer layout = ref DirtyLayouts.ElementAt(layoutIndex);

                    bool isTargetElement = layout.ID == layoutElement.ID;

                    if (isTargetElement)
                    {
                        HierarchyDependency propertyDependents = elementProps.ApplyDiff(ref LayoutAccess.Properties.AsSnapshot(ref UnsafeUtility.AsRef<Layout>(layout.Layout)));

                        ref AutoLayout autoLayout = ref AutoLayouts.ElementAt(layoutIndex);
                        ref AutoLayout dirtyAutoLayout = ref UnsafeUtility.AsRef<AutoLayout>(layout.AutoLayout);

                        bool primaryDirty = autoLayout.Axis != dirtyAutoLayout.Axis;
                        bool crossDirty = autoLayout.Cross.Axis != dirtyAutoLayout.Cross.Axis;

                        if (primaryDirty || crossDirty)
                        {
                            Axis primaryAxis = primaryDirty ? autoLayout.Axis : Axis.None;
                            Axis crossAxis = crossDirty ? autoLayout.Cross.Axis : Axis.None;

                            // track former axis, so we can clear it
                            FormerAutoLayoutAxes.Add(layoutIndex, new Axes() { Primary = primaryAxis, Cross = crossAxis });
                        }

                        HierarchyDependency autoLayoutDependents = autoLayout.ApplyDiff(ref dirtyAutoLayout);
                        HierarchyDependency maxDependents = HierarchyDependency.Max(propertyDependents, autoLayoutDependents);
                        DirtyDependencies[layoutIndex] = HierarchyDependency.Max(DirtyDependencies[layoutIndex], maxDependents);
                    }

                    if (AutoLayouts.ElementAt(layoutIndex).Enabled || math.any(elementProps.AutoSize.Shrink))
                    {
                        int childCount = layoutElement.ChildCount;

                        unsafe
                        {
                            Dependencies.AddRange(layoutElement.Children.Ptr, childCount);
                        }

                        dependencyCount += childCount;
                    }
                    else
                    {
                        if (layoutIndex == elementIndex)
                        {
                            break;
                        }

                        int childCount = layoutElement.ChildCount;
                        for (int j = 0; j < childCount; ++j)
                        {
                            DataStoreIndex ancestorIndex = layoutElement.Children[j];

                            if (AncestorBuffer.ContainsKey(ancestorIndex))
                            {
                                Dependencies.Add(ancestorIndex);
                                dependencyCount++;
                                break;
                            }
                        }
                    }
                }
            }

            private DataStoreIndex GetDependencyRoot(DataStoreIndex elementIndex)
            {
                bool3 isRelative = true;

                ref HierarchyElement element = ref Hierarchy.ElementAt(elementIndex);
                DataStoreIndex rootIndex = elementIndex;

                AncestorBuffer.Clear();

                AncestorBuffer.Add(elementIndex, element.ID);


                while (element.ParentID.IsValid && math.any(isRelative))
                {
                    rootIndex = HierarchyLookup[element.ParentID];
                    AncestorBuffer.Add(rootIndex, element.ParentID);

                    LayoutAccess.Properties elementProps = LayoutAccess.Get(rootIndex, ref LengthConfigs);
                    elementProps.WrapAutoSizes(ref AutoSizes);


                    isRelative = isRelative & (elementProps.IsRelative | elementProps.AutoSize.Shrink);

                    element = ref Hierarchy.ElementAt(rootIndex);
                }

                return rootIndex;
            }
        }

        [BurstCompile]
        public unsafe struct Register
        {
            public NativeList<Length3> LengthConfigs;
            public NativeList<Length3.MinMax> LengthMinMaxes;
            public NativeList<Length3.Calculated> CalculatedLengths;

            public NativeList<bool> UseRotations;
            public NativeList<float3> Alignments;
            public NativeList<AutoSize3> AutoSizes;

            public NativeList<AutoLayout> AutoLayouts;
            public NativeList<Length2.Calculated> CalculatedSpacing;
            public NovaHashMap<DataStoreIndex, Axes> FormerAutoLayoutAxes;
            public NativeList<float3> ParentSizes;

            public NativeList<float3> DirectContentSizes;
            public NativeList<float3> DirectContentOffsets;
            public NativeList<float3> TotalContentSizes;
            public NativeList<float3> TotalContentOffsets;

            public NativeList<float3> TransformLocalPositions;
            public NativeList<quaternion> TransformLocalRotations;
            public NativeList<float3> TransformLocalScales;
            public NativeList<bool> UsingTransformPositions;

            public NativeList<AspectRatio> AspectRatios;

            public NativeList<SpatialPartitionMask> SpatialPartitions;
            public NativeList<LayoutPointer> AccessedLayouts;

            public NativeBitList ReceivedFullEngineUpdate;

            public NativeList<float4x4> LocalToWorldMatrices;
            public NativeList<float4x4> WorldToLocalMatrices;
            public NativeList<TransformProxy> TransformProxies;
            public NativeList<DataStoreIndex> PhysicalToSharedIndexMap;
            public NativeList<DataStoreIndex> VirtualToSharedIndexMap;

            public bool TransformIsVirtual;
            public int TransformIndex;

            public AutoLayout AutoLayout;
            public Layout Layout;
            public Vector3 TransformPosition;
            public Quaternion TransformRotation;
            public Vector3 TransformScale;

            [NativeDisableUnsafePtrRestriction]
            public Length3* LengthPropertiesPtr;
            [NativeDisableUnsafePtrRestriction]
            public Length3.MinMax* RangePropertiesPtr;
            [NativeDisableUnsafePtrRestriction]
            public Length3.Calculated* CalcPropertiesPtr;
            [NativeDisableUnsafePtrRestriction]
            public Length2.Calculated* CalcSpacingPtr;
            [NativeDisableUnsafePtrRestriction]
            public bool* UseRotationsPtr;
            [NativeDisableUnsafePtrRestriction]
            public float3* AlignmentsPtr;
            [NativeDisableUnsafePtrRestriction]
            public AutoSize3* AutosizesPtr;
            [NativeDisableUnsafePtrRestriction]
            public AutoLayout* AutoLayoutsPtr;
            [NativeDisableUnsafePtrRestriction]
            public AspectRatio* AspectRatiosPtr;

            public void Run()
            {
                AutoLayouts.Add(AutoLayout);
                CalculatedSpacing.Add(default);

                LengthConfigs.Add(Layout.Size);
                LengthConfigs.Add(Layout.Position);
                LengthConfigs.Add(Layout.Padding);
                LengthConfigs.Add(Layout.Margin);

                LengthMinMaxes.Add(Layout.SizeMinMax);
                LengthMinMaxes.Add(Layout.PositionMinMax);
                LengthMinMaxes.Add(Layout.PaddingMinMax);
                LengthMinMaxes.Add(Layout.MarginMinMax);

                CalculatedLengths.AddEmpty(count: 6);

                UseRotations.Add(Layout.RotateSize);
                Alignments.Add(Layout.Alignment);
                ParentSizes.Add(float3.zero);

                AutoSizes.Add(Layout.AutoSize);
                AspectRatios.Add(Layout.AspectRatio);

                DirectContentSizes.Add(0);
                DirectContentOffsets.Add(0);
                TotalContentSizes.Add(0);
                TotalContentOffsets.Add(0);
                SpatialPartitions.Add(SpatialPartitionMask.Empty);

                TransformLocalPositions.Add(TransformPosition);
                TransformLocalRotations.Add(TransformRotation);
                TransformLocalScales.Add(TransformScale);
                UsingTransformPositions.Add(false);
                AccessedLayouts.Add(default(LayoutPointer));

                ReceivedFullEngineUpdate.Add(false);

                if (TransformIsVirtual)
                {
                    // Just adding a dummy transform for now.
                    // This will have real values after the 
                    // LayoutEngine processes it.
                    VirtualToSharedIndexMap.Add(TransformProxies.Length);
                }
                else // real transform
                {
                    PhysicalToSharedIndexMap.Add(TransformProxies.Length);
                }

                TransformProxies.Add(new TransformProxy()
                {
                    Index = TransformIndex,
                    IsVirtual = TransformIsVirtual
                });

                LocalToWorldMatrices.Add(float4x4.identity);
                WorldToLocalMatrices.Add(float4x4.identity);

                AutoLayoutsPtr = AutoLayouts.GetRawPtr();
                CalcSpacingPtr = CalculatedSpacing.GetRawPtr();

                LengthPropertiesPtr = LengthConfigs.GetRawPtr();
                RangePropertiesPtr = LengthMinMaxes.GetRawPtr();
                CalcPropertiesPtr = CalculatedLengths.GetRawPtr();

                UseRotationsPtr = UseRotations.GetRawPtr();
                AlignmentsPtr = Alignments.GetRawPtr();
                AutosizesPtr = AutoSizes.GetRawPtr();
                AspectRatiosPtr = AspectRatios.GetRawPtr();
            }

            [BurstCompile]
            [MonoPInvokeCallback(typeof(BurstMethod))]
            public static unsafe void Run(void* jobData)
            {
                UnsafeUtility.AsRef<Register>(jobData).Run();
            }
        }

        [BurstCompile]
        public struct Unregister
        {
            public NativeList<Length3> LengthConfigs;
            public NativeList<Length3.MinMax> LengthMinMaxes;
            public NativeList<Length3.Calculated> CalculatedLengths;

            public NativeList<bool> UseRotations;
            public NativeList<float3> Alignments;
            public NativeList<AutoSize3> AutoSizes;

            public NativeList<AutoLayout> AutoLayouts;
            public NativeList<Length2.Calculated> CalculatedSpacing;
            public NovaHashMap<DataStoreIndex, Axes> FormerAutoLayoutAxes;
            public NativeList<float3> ParentSizes;

            public NovaHashMap<DataStoreID, SizeOverride> ShrinkSizeOverrides;

            public NativeList<float3> DirectContentSizes;
            public NativeList<float3> DirectContentOffsets;
            public NativeList<float3> TotalContentSizes;
            public NativeList<float3> TotalContentOffsets;

            public NativeList<float3> TransformLocalPositions;
            public NativeList<quaternion> TransformLocalRotations;
            public NativeList<float3> TransformLocalScales;
            public NativeList<bool> UsingTransformPositions;

            public NativeList<float4x4> LocalToWorldMatrices;
            public NativeList<float4x4> WorldToLocalMatrices;
            public NativeList<TransformProxy> TransformProxies;
            public NativeList<DataStoreIndex> PhysicalToSharedIndexMap;
            public NativeList<DataStoreIndex> VirtualToSharedIndexMap;
            public TransformAccessArray PhysicalTransforms;

            public NativeList<LayoutPointer> AccessedLayouts;

            public NativeList<AspectRatio> AspectRatios;

            public NativeList<SpatialPartitionMask> SpatialPartitions;

            public NativeBitList ReceivedFullEngineUpdate;

            public DataStoreIndex IndexToRemove;
            public DataStoreID IDToRemove;

            public TransformProxy ProxyToRemove;

            public NativeReference<UnsafeAtomicCounter32> AccessedLayoutCount;

            public void Run()
            {
                AutoLayouts.RemoveAtSwapBack(IndexToRemove);
                CalculatedSpacing.RemoveAtSwapBack(IndexToRemove);

                LengthConfigs.RemoveRangeSwapBack(IndexToRemove * LayoutAccess.Length3SliceSize, LayoutAccess.Length3SliceSize);
                LengthMinMaxes.RemoveRangeSwapBack(IndexToRemove * LayoutAccess.Length3SliceSize, LayoutAccess.Length3SliceSize);
                CalculatedLengths.RemoveRangeSwapBack(IndexToRemove * LayoutAccess.Length3SliceSize, LayoutAccess.Length3SliceSize);

                UseRotations.RemoveAtSwapBack(IndexToRemove);
                Alignments.RemoveAtSwapBack(IndexToRemove);
                AutoSizes.RemoveAtSwapBack(IndexToRemove);
                AspectRatios.RemoveAtSwapBack(IndexToRemove);

                DirectContentSizes.RemoveAtSwapBack(IndexToRemove);
                DirectContentOffsets.RemoveAtSwapBack(IndexToRemove);
                TotalContentSizes.RemoveAtSwapBack(IndexToRemove);
                TotalContentOffsets.RemoveAtSwapBack(IndexToRemove);
                SpatialPartitions.RemoveAtSwapBack(IndexToRemove);

                TransformLocalPositions.RemoveAtSwapBack(IndexToRemove);
                TransformLocalRotations.RemoveAtSwapBack(IndexToRemove);
                TransformLocalScales.RemoveAtSwapBack(IndexToRemove);
                UsingTransformPositions.RemoveAtSwapBack(IndexToRemove);

                ShrinkSizeOverrides.Remove(IDToRemove);
                ParentSizes.RemoveAtSwapBack(IndexToRemove);

                ReceivedFullEngineUpdate.RemoveAtSwapBack(IndexToRemove);

                RemoveAtSwapBackTransforms();

                ref LayoutPointer wrapper = ref AccessedLayouts.ElementAt(IndexToRemove);

                if (wrapper.ID == IDToRemove)
                {
                    wrapper.ID = DataStoreID.Invalid;

                    unsafe
                    {
                        wrapper.AutoLayout = null;
                        wrapper.Layout = null;
                    }

                    UnsafeUtility.ReleaseGCObject(wrapper.GCHandle);
                    wrapper.GCHandle = 0;

                    AccessedLayoutCount.Ref().SubSat(1, 0);
                }

                AccessedLayouts.RemoveAtSwapBack(IndexToRemove);
            }

            private void RemoveAtSwapBackTransforms()
            {
                // Get the proxy of the transform, physical or virtual, to remove
                ProxyToRemove = TransformProxies[IndexToRemove];

                // remove the proxy at the requested index and swap in the one we just updated
                TransformProxies.RemoveAtSwapBack(IndexToRemove);
                WorldToLocalMatrices.RemoveAtSwapBack(IndexToRemove);
                LocalToWorldMatrices.RemoveAtSwapBack(IndexToRemove);

                if (IndexToRemove < TransformProxies.Length) // otherwise we removed last
                {
                    TransformProxy proxySwappedFromBack = TransformProxies[IndexToRemove];

                    if (proxySwappedFromBack.IsVirtual)
                    {
                        VirtualToSharedIndexMap[proxySwappedFromBack.Index] = IndexToRemove;
                    }
                    else
                    {
                        PhysicalToSharedIndexMap[proxySwappedFromBack.Index] = IndexToRemove;
                    }
                }

                int proxyIndexToUpdate = -1;
                if (ProxyToRemove.IsVirtual)
                {
                    // remove virtual transform
                    VirtualToSharedIndexMap.RemoveAtSwapBack(ProxyToRemove.Index);

                    if (ProxyToRemove.Index < VirtualToSharedIndexMap.Length) // otherwise we removed last
                    {
                        // get the proxy index of the newly swapped in virtual transform
                        proxyIndexToUpdate = VirtualToSharedIndexMap[ProxyToRemove.Index];
                    }
                }
                else
                {
                    PhysicalTransforms.RemoveAtSwapBack(ProxyToRemove.Index);
                    PhysicalToSharedIndexMap.RemoveAtSwapBack(ProxyToRemove.Index);

                    if (ProxyToRemove.Index < PhysicalToSharedIndexMap.Length) // otherwise we removed last
                    {
                        // get the proxy index of the newly swapped in physical transform
                        proxyIndexToUpdate = PhysicalToSharedIndexMap[ProxyToRemove.Index];
                    }
                }

                if (proxyIndexToUpdate >= 0 && proxyIndexToUpdate < TransformProxies.Length)
                {
                    TransformProxy swappedTransformProxy = TransformProxies[proxyIndexToUpdate];
                    swappedTransformProxy.Index = ProxyToRemove.Index;
                    TransformProxies[proxyIndexToUpdate] = swappedTransformProxy;
                }
            }

            [BurstCompile]
            [MonoPInvokeCallback(typeof(BurstMethod))]
            public static unsafe void Run(void* jobData)
            {
                UnsafeUtility.AsRef<Unregister>(jobData).Run();
            }
        }

        [BurstCompile]
        public struct ReleaseHandles
        {
            public NativeList<LayoutPointer> Handles;

            public void Run()
            {
                int count = Handles.Length;

                for (int i = 0; i < count; i++)
                {
                    ref LayoutPointer layout = ref Handles.ElementAt(i);

                    if (layout.GCHandle == 0)
                    {
                        continue;
                    }

                    UnsafeUtility.ReleaseGCObject(layout.GCHandle);
                }
            }

            [BurstCompile]
            [MonoPInvokeCallback(typeof(BurstMethod))]
            public static unsafe void Run(void* jobData)
            {
                UnsafeUtility.AsRef<ReleaseHandles>(jobData).Run();
            }
        }

        [BurstCompile]
        public struct CopyToPointer
        {
            public DataStoreIndex IndexToCopy;
            public NativeList<LayoutPointer> AccessedLayouts;

            public NativeList<AutoLayout> AutoLayouts;
            public NativeList<Length3> Lengths;
            public NativeList<Length3.MinMax> Ranges;
            public NativeList<AutoSize3> AutoSizes;
            public NativeList<float3> Alignments;
            public NativeList<bool> UseRotations;
            public NativeList<AspectRatio> AspectRatios;

            public unsafe void Run()
            {
                LayoutAccess.Properties layout = LayoutAccess.Get(IndexToCopy, ref Lengths);

                layout.WrapMinMaxes(ref Ranges);
                layout.WrapAutoSizes(ref AutoSizes);
                layout.WrapAlignments(ref Alignments);
                layout.WrapUseRotations(ref UseRotations);
                layout.WrapAspectRatios(ref AspectRatios);

                ref LayoutPointer wrapper = ref AccessedLayouts.ElementAt(IndexToCopy);

                layout.CopyTo(ref UnsafeUtility.AsRef<Layout>(wrapper.Layout));
                UnsafeUtility.MemCpy(wrapper.AutoLayout, UnsafeUtility.AddressOf(ref AutoLayouts.ElementAt(IndexToCopy)), sizeof(AutoLayout));
            }

            [BurstCompile]
            [MonoPInvokeCallback(typeof(BurstMethod))]
            public static unsafe void Run(void* jobData)
            {
                UnsafeUtility.AsRef<CopyToPointer>(jobData).Run();
            }
        }
    }
}
