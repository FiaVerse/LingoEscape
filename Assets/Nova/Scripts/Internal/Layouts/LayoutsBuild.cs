// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Layouts
{
    [BurstCompile]
    internal partial class LayoutCore
    {
        [BurstCompile]
        internal struct Build : INovaJob
        {
            public bool PreviewSizesAvailable;
            [NativeDisableParallelForRestriction]
            public NovaHashMap<DataStoreID, PreviewSize> PreviewSizes;
            public DataStoreID ElementID;

            public NativeList<Length3> LengthConfigs;
            public NativeList<Length3.MinMax> LengthRanges;
            public NativeList<Length3.Calculated> CalculatedLengths;
            public NativeList<float3> Alignments;
            public NativeList<bool> UseRotations;
            public NativeList<bool> UsingTransformPositions;
            public NativeList<AutoSize3> AutoSizes;
            public NativeList<AspectRatio> AspectRatios;

            public NativeList<AutoLayout> AutoLayouts;
            public NativeList<Length2.Calculated> CalculatedSpacing;
            public NovaHashMap<DataStoreIndex, Axes> FormerAutoLayoutAxes;

            public NovaHashMap<DataStoreID, SizeOverride> ShrinkSizeOverrides;
            public NativeList<float3> ParentSizes;

            public NativeList<ExpandableTrack> AutoLayoutTrackCache;
            public NativeList<ExpandableRange> AutoLayoutRangeCache;

            public NativeList<float3> DirectContentSizes;
            public NativeList<float3> DirectContentOffsets;

            [ReadOnly]
            public NativeList<TransformProxy> TransformProxies;

            public NativeList<quaternion> TransformRotations;
            public NativeList<float3> TransformPositions;

            [NativeDisableContainerSafetyRestriction]
            public NativeList<HierarchyElement> Hierarchy;
            [ReadOnly]
            public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

            public NovaHashMap<DataStoreIndex, DataStoreID> AncestorBuffer;
            public NativeList<LayoutPointer> DirtyLayouts;
            public NativeList<HierarchyDependency> DirtyDependencies;

            [ReadOnly]
            public NativeList<DataStoreIndex> IndicesToProcess;

            public NativeList<bool> NeedsSecondPass;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute()
            {
                bool processingSingleElement = ElementID.IsValid;

                NativeList<DataStoreIndex> DepthLevelSortedIndices = IndicesToProcess;
                int length = DepthLevelSortedIndices.Length;

                if (length == 0)
                {
                    return;
                }

                NeedsSecondPass.Length = Hierarchy.Length;
                unsafe
                {
                    UnsafeUtility.MemSet(NeedsSecondPass.GetRawPtr(), 0, NeedsSecondPass.Length);
                }

                InitRootToLeafStruct(out BuildRootDown rootToLeaf);

                bool needsSecondPass = false;
                for (int i = 0; i < length; ++i)
                {
                    DataStoreIndex elementIndex = DepthLevelSortedIndices[i];
                    DataStoreIndex parentIndex = rootToLeaf.Execute(elementIndex, isSecondPass: false, out bool hasCycle, out _, forceRun: processingSingleElement);
                    needsSecondPass |= hasCycle;

                    if (parentIndex.IsValid)
                    {
                        NeedsSecondPass.ElementAt(parentIndex) |= hasCycle;
                    }
                }

                InitLeafToRootStruct(out BuildLeafUp leafToRoot);

                for (int i = length - 1; i >= 0; --i)
                {
                    leafToRoot.Execute(DepthLevelSortedIndices[i], isSecondPass: false, forceRun: processingSingleElement);
                }

                if (needsSecondPass)
                {
                    for (int i = 0; i < length; ++i)
                    {
                        DataStoreIndex elementIndex = DepthLevelSortedIndices[i];
                        HierarchyElement element = Hierarchy.ElementAt(elementIndex);
                        DataStoreID parentID = element.ParentID;
                        DataStoreIndex parentIndex = parentID.IsValid ? HierarchyLookup[parentID] : DataStoreIndex.Invalid;

                        bool hasRelativeSize = false;
                        if (parentIndex.IsValid && NeedsSecondPass[parentIndex])
                        {
                            _ = rootToLeaf.Execute(elementIndex, true, out _, out hasRelativeSize, forceRun: processingSingleElement);
                        }

                        ref bool secondPass = ref NeedsSecondPass.ElementAt(elementIndex);

                        secondPass |= hasRelativeSize;

                        if (secondPass)
                        {
                            leafToRoot.Execute(elementIndex, isSecondPass: true, forceRun: processingSingleElement);
                        }
                    }
                }

                if (processingSingleElement)
                {
                    InitLayoutToTransformStruct(out ConvertToTransforms convertLayoutToTransformPosition);

                    DataStoreIndex elementIndex = HierarchyLookup[ElementID];

                    // This nuance might come back, but currently the scenario that is broken
                    // is limited to virtual blocks.
                    if (!TransformProxies[elementIndex].IsVirtual)
                    {
                        convertLayoutToTransformPosition.Execute(elementIndex, forceRun: processingSingleElement);

                        InitContentBoundsStruct(out ContentBounds contentBounds);
                        contentBounds.Execute(elementIndex, calculateChildPositions: true);
                    }
                    else
                    {
                        DataStoreID parentID = Hierarchy.ElementAt(elementIndex).ParentID;
                        DataStoreIndex parentIndex = HierarchyLookup[parentID];
                        NovaList<DataStoreIndex> siblings = Hierarchy.ElementAt(parentIndex).Children;

                        length = siblings.Length;
                        for (int i = 0; i < length; ++i)
                        {
                            convertLayoutToTransformPosition.Execute(siblings[i], forceRun: processingSingleElement);
                        }
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InitDependencyStruct(out GetDependencies getDependencies)
            {
                getDependencies = default;
                getDependencies.AspectRatios = AspectRatios;
                getDependencies.AutoSizes = AutoSizes;
                getDependencies.LengthConfigs = LengthConfigs;
                getDependencies.LengthRanges = LengthRanges;
                getDependencies.UseRotations = UseRotations;
                getDependencies.TransformRotations = TransformRotations;
                getDependencies.Alignments = Alignments;
                getDependencies.AutoLayouts = AutoLayouts;

                // About to refresh changes. Dump anything that's stale
                FormerAutoLayoutAxes.Clear();

                getDependencies.FormerAutoLayoutAxes = FormerAutoLayoutAxes;
                getDependencies.Hierarchy = Hierarchy;
                getDependencies.HierarchyLookup = HierarchyLookup;

                getDependencies.Dependencies = IndicesToProcess;
                getDependencies.AncestorBuffer = AncestorBuffer;
                getDependencies.DirtyLayouts = DirtyLayouts;
                getDependencies.DirtyDependencies = DirtyDependencies;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void InitRootToLeafStruct(out BuildRootDown rootToLeaf)
            {
                rootToLeaf = default;
                rootToLeaf.PreviewSizesAvailable = PreviewSizesAvailable;
                rootToLeaf.PreviewSizes = PreviewSizes;
                rootToLeaf.LengthConfigs = LengthConfigs;
                rootToLeaf.LengthRanges = LengthRanges;
                rootToLeaf.CalculatedLengths = CalculatedLengths;
                rootToLeaf.UseRotations = UseRotations;
                rootToLeaf.Alignments = Alignments;
                rootToLeaf.AutoSizes = AutoSizes;
                rootToLeaf.ParentSizes = ParentSizes;
                rootToLeaf.AutoLayouts = AutoLayouts;
                rootToLeaf.CalculatedSpacing = CalculatedSpacing;
                rootToLeaf.AspectRatios = AspectRatios;
                rootToLeaf.Hierarchy = Hierarchy;
                rootToLeaf.HierarchyLookup = HierarchyLookup;
                rootToLeaf.TransformProxies = TransformProxies;
                rootToLeaf.DirtyDependencies = DirtyDependencies;

                rootToLeaf.LocalPositions = TransformPositions;
                rootToLeaf.LocalRotations = TransformRotations;
                rootToLeaf.UsingTransformPositions = UsingTransformPositions;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void InitLeafToRootStruct(out BuildLeafUp leafToRoot)
            {
                leafToRoot = default;
                leafToRoot.LengthConfigs = LengthConfigs;
                leafToRoot.LengthRanges = LengthRanges;
                leafToRoot.CalculatedLengths = CalculatedLengths;
                leafToRoot.UseRotations = UseRotations;
                leafToRoot.Alignments = Alignments;
                leafToRoot.AutoSizes = AutoSizes;
                leafToRoot.ParentSizes = ParentSizes;
                leafToRoot.AspectRatios = AspectRatios;
                leafToRoot.Rotations = TransformRotations;
                leafToRoot.TransformProxies = TransformProxies;
                leafToRoot.Hierarchy = Hierarchy;
                leafToRoot.HierarchyLookup = HierarchyLookup;
                leafToRoot.AutoLayouts = AutoLayouts;
                leafToRoot.CalculatedSpacing = CalculatedSpacing;
                leafToRoot.FormerAutoLayoutAxes = FormerAutoLayoutAxes;
                leafToRoot.DirtyDependencies = DirtyDependencies;
                leafToRoot.ShrinkSizeOverrides = ShrinkSizeOverrides;
                leafToRoot.AutoLayoutTrackCache = AutoLayoutTrackCache;
                leafToRoot.AutoLayoutRangeCache = AutoLayoutRangeCache;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void InitContentBoundsStruct(out ContentBounds contentBounds)
            {
                contentBounds = default;
                contentBounds.Lengths = CalculatedLengths;
                contentBounds.UseRotations = UseRotations;
                contentBounds.Alignments = Alignments;
                contentBounds.LocalRotations = TransformRotations;
                contentBounds.LocalPositions = TransformPositions;
                contentBounds.Hierarchy = Hierarchy;
                contentBounds.DirectContentOffsets = DirectContentOffsets;
                contentBounds.DirectContentSizes = DirectContentSizes;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void InitLayoutToTransformStruct(out ConvertToTransforms convertLayoutToTransformPosition)
            {
                convertLayoutToTransformPosition = default;
                convertLayoutToTransformPosition.CalculatedLengths = CalculatedLengths;
                convertLayoutToTransformPosition.MinMaxes = LengthRanges;
                convertLayoutToTransformPosition.UncalculatedLengths = LengthConfigs;
                convertLayoutToTransformPosition.UseRotations = UseRotations;
                convertLayoutToTransformPosition.Alignments = Alignments;
                convertLayoutToTransformPosition.TransformRotations = TransformRotations;
                convertLayoutToTransformPosition.TransformProxies = TransformProxies;
                convertLayoutToTransformPosition.Hierarchy = Hierarchy;
                convertLayoutToTransformPosition.HierarchyLookup = HierarchyLookup;
                convertLayoutToTransformPosition.DirtyElementIndices = IndicesToProcess;
                convertLayoutToTransformPosition.DirtyDependencies = DirtyDependencies;
                convertLayoutToTransformPosition.TransformPositions = TransformPositions;
                convertLayoutToTransformPosition.PreviewSizesAvailable = PreviewSizesAvailable;
                convertLayoutToTransformPosition.PreviewSizes = PreviewSizes;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void Rebuild(void* jobData, void* elementID, int secondPass);

        [BurstCompile]
        [MonoPInvokeCallback(typeof(Rebuild))]
        public static unsafe void GetDependenciesAndRun(void* jobData, void* elementID, int secondPass)
        {
            ref Build data = ref UnsafeUtility.AsRef<Build>(jobData);

            UnsafeUtility.CopyPtrToStructure(elementID, out DataStoreID id);

            if (secondPass == 0)
            {
                data.InitDependencyStruct(out GetDependencies getDependencies);
                getDependencies.ElementID = id;
                getDependencies.Execute();
            }

            data.ElementID = id;
            data.Execute();
            data.ElementID = DataStoreID.Invalid;
        }
    }
}
