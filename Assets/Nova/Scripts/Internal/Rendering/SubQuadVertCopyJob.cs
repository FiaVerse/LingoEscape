// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities.Extensions;
using Unity.Burst;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct SubQuadVertCopyJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatches;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [NativeDisableParallelForRestriction]
        public NativeList<RenderIndex, SubQuadData> SubQuadData;

        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, DrawCallSummary> DrawCallSummaries;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, NovaList<SubQuadVert>> SubQuadBuffers;

        private NovaList<SubQuadVert> shaderData;

        public void Execute(int index)
        {
            DataStoreID batchRootID = DirtyBatches[index];
            shaderData = SubQuadBuffers.GetAndClear(batchRootID);
            NovaList<VisualElementIndex, VisualElement> visualElements = VisualElements[batchRootID];

            DrawCallSummary drawCallSummary = DrawCallSummaries[batchRootID];
            for (int i = 0; i < drawCallSummary.DrawCalls.Length; ++i)
            {
                ref DrawCall drawCall = ref drawCallSummary.DrawCalls.ElementAt(i);
                ref DrawCallDescriptor descriptor = ref drawCallSummary.DrawCallDescriptors.ElementAt(drawCall.DescriptorID);

                if (descriptor.DrawCallType != VisualType.UIBlock2D)
                {
                    continue;
                }

                ref NovaList<VisualElementIndex> orderedBlocks = ref drawCallSummary.NonIndexedElements.ElementAt(drawCall.ID);
                ref ShaderIndexBounds indexBounds = ref drawCallSummary.IndexBounds.ElementAt(drawCall.ID);
                indexBounds.InstanceStart = shaderData.Length;

                for (int j = 0; j < orderedBlocks.Length; ++j)
                {
                    ref VisualElement visualElement = ref visualElements.ElementAt(orderedBlocks[j]);
                    shaderData.AddRange(ref SubQuadData.ElementAt(visualElement.RenderIndex).Verts);
                }

                indexBounds.InstanceCount = shaderData.Length - indexBounds.InstanceStart;
            }

            SubQuadBuffers[batchRootID] = shaderData;
            DrawCallSummaries[batchRootID] = drawCallSummary;
        }
    }
}
