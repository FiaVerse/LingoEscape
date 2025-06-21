// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct TextBlockSizeChangeDetectionJob : INovaJob
    {
        [ReadOnly]
        public NativeList<RenderIndex, DataStoreIndex> RenderIndexToDataStoreIndex;
        [ReadOnly]
        public NativeList<HierarchyElement> Hierarchy;
        [ReadOnly]
        public NativeList<RenderIndex, TextBlockData> BlockData;

        public NativeList<Length3.Calculated> CalculatedLayoutLengths;
        public NativeList<Length3.MinMax> LengthMinMaxes;
        public NativeList<Length3> LayoutLengths;
        public NativeList<AutoSize3> AutoSizes;

        public NativeList<RenderIndex, TextMargin> Margins;
        public NativeList<ValuePair<DataStoreID, TextMargin>> DirtiedMargins;
        /// <summary>
        /// See comment on <see cref="TextBlockDataStore.ShrinkMask"/>
        /// </summary>
        public NativeList<RenderIndex, bool2> ShrinkMask;
        /// <summary>
        /// See comment on <see cref="TextBlockDataStore.ShrinkMask"/>
        /// </summary>
        public NativeList<ValuePair<DataStoreID, float2>> ForceSizeOverrideCall;


        public void Execute()
        {
            DirtiedMargins.Clear();

            for (int i = 0; i < Margins.Length; ++i)
            {
                RenderIndex renderIndex = i;
                DataStoreIndex dataStoreIndex = RenderIndexToDataStoreIndex[renderIndex];

                AppendIfChanged(dataStoreIndex, renderIndex);
            }
        }

        public void AppendIfChanged(DataStoreIndex dataStoreIndex, RenderIndex renderIndex)
        {
            LayoutAccess.Properties layoutProperties = LayoutAccess.Get(dataStoreIndex, ref LayoutLengths, ref CalculatedLayoutLengths);
            layoutProperties.WrapMinMaxes(ref LengthMinMaxes);
            layoutProperties.WrapAutoSizes(ref AutoSizes);

            bool2 shrinkMask = layoutProperties.AutoSize.Shrink.xy;
            TextMargin newMargin = TextMargin.GetMargin(layoutProperties.CalculatedSize.Value.xy, layoutProperties.SizeMinMax.Max.xy, shrinkMask);
            ref TextMargin currentMargin = ref Margins.ElementAt(renderIndex);

            ref bool2 oldShrinkMask = ref ShrinkMask.ElementAt(renderIndex);

            if (!shrinkMask.Equals(oldShrinkMask))
            {
                bool2 forceSizeOverride = shrinkMask & !oldShrinkMask & layoutProperties.CalculatedSize.Value.xy == float2.zero;

                // If we started shrinking on an axis where the size was zero, force a size override call.
                // See comment on <see cref="TextBlockDataStore.ShrinkMask"/>
                if (math.any(forceSizeOverride))
                {
                    ForceSizeOverrideCall.Add(new ValuePair<DataStoreID, float2>(Hierarchy[dataStoreIndex].ID, BlockData[renderIndex].TextBounds.GetSize().xy));
                }

                oldShrinkMask = shrinkMask;
            }

            if (newMargin == currentMargin)
            {
                // Margin didn't change
                return;
            }

            currentMargin = newMargin;

            DirtiedMargins.Add(new ValuePair<DataStoreID, TextMargin>(Hierarchy[dataStoreIndex].ID, newMargin));
        }

        public struct DirtyElements
        {
            public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
            public NativeList<DataStoreIndex> DirtyIndices;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void DetectChanges(void* jobData, void* dirtyElements);


        [BurstCompile]
        [MonoPInvokeCallback(typeof(DetectChanges))]
        public static unsafe void Run(void* jobData, void* dirtyElements)
        {
            UnsafeUtility.CopyPtrToStructure(jobData, out TextBlockSizeChangeDetectionJob job);
            UnsafeUtility.CopyPtrToStructure(dirtyElements, out DirtyElements elements);

            job.DirtiedMargins.Clear();

            int count = elements.DirtyIndices.Length;

            for (int i = 0; i < count; ++i)
            {
                DataStoreIndex dataStoreIndex = elements.DirtyIndices[i];
                ref RenderElement<BaseRenderInfo> baseInfo = ref elements.BaseInfos.ElementAt(dataStoreIndex);

                if (baseInfo.Val.BlockType != BlockType.Text)
                {
                    continue;
                }

                job.AppendIfChanged(dataStoreIndex, baseInfo.RenderIndex);
            }
        }
    }
}

