// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// The data common to all blocks
    /// </summary>
    internal struct CommonData : IInitializable
    {
        public NativeList<DataStoreIndex, RenderBounds> BlockRenderBounds;
        public NativeList<DataStoreIndex, int> OrderInZLayer;
        public NativeList<DataStoreIndex, NovaList<VisualElementIndex>> OverlappingElements;
        public NativeList<DataStoreIndex, VisualModifierID> VisualModifierIDs;
        public NativeList<DataStoreIndex, ComputeBufferIndex> TransformIndices;
        public NativeList<DataStoreIndex, CoplanarSetID> CoplanarSetIDs;
        public NativeList<DataStoreIndex, RotationSetID> RotationSetIDs;
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        public NovaComputeBuffer<TransformAndLightingData, TransformAndLightingData> TransformAndLightingData;
        public NovaHashMap<DataStoreID, byte> HiddenElements;

        public NativeList<NovaList<VisualElementIndex>> OverlappingElementsPool;

        public void Init()
        {
            BaseInfos.Init(Constants.AllElementsInitialCapacity);
            TransformAndLightingData.Init(Constants.AllElementsInitialCapacity);
            BlockRenderBounds.Init(Constants.AllElementsInitialCapacity);
            TransformIndices.Init(Constants.AllElementsInitialCapacity);
            OverlappingElements.Init(Constants.AllElementsInitialCapacity);
            VisualModifierIDs.Init(Constants.AllElementsInitialCapacity);
            OrderInZLayer.Init(Constants.AllElementsInitialCapacity);
            CoplanarSetIDs.Init(Constants.AllElementsInitialCapacity);
            RotationSetIDs.Init(Constants.AllElementsInitialCapacity);
            OverlappingElementsPool.Init(Constants.SomeElementsInitialCapacity);
            HiddenElements.Init();
        }

        public void Dispose()
        {
            BaseInfos.Dispose();
            TransformAndLightingData.Dispose();
            BlockRenderBounds.Dispose();
            TransformIndices.Dispose();
            OrderInZLayer.Dispose();
            CoplanarSetIDs.Dispose();
            RotationSetIDs.Dispose();
            OverlappingElements.DisposeListAndElements();
            VisualModifierIDs.Dispose();
            OverlappingElementsPool.DisposeListAndElements();
            HiddenElements.Dispose();
        }
    }
}

