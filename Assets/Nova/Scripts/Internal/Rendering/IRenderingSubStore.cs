// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    internal struct RenderingPreUpdateData
    {
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [ReadOnly]
        public NativeList<BatchGroupElement> AllBatchGroupElements;
        [ReadOnly]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;

        public RenderingDirtyState DirtyState;
    }


    internal interface IRenderingSubStore<T,TIndex> : IInitializable where TIndex : IIndex<TIndex>
    {
        bool RemoveAtSwapBack(DataStoreID dataStoreID, TIndex index, out DataStoreIndex swappedForwardIndex);
        void ClearDirtyState();
    }
}
