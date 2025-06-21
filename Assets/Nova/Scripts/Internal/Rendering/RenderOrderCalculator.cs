// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    internal struct RenderOrder : System.IComparable<RenderOrder>
    {
        public SortGroupInfo SortGroupInfo;
        public int OrderInSortGroup;

        public int RenderQueue => SortGroupInfo.RenderQueue;

        public int CompareTo(RenderOrder other)
        {
            if (RenderQueue != other.RenderQueue)
            {
                return RenderQueue.CompareTo(other.RenderQueue);
            }

            if (SortGroupInfo.SortingOrder != other.SortGroupInfo.SortingOrder)
            {
                return SortGroupInfo.SortingOrder.CompareTo(other.SortGroupInfo.SortingOrder);
            }

            return OrderInSortGroup.CompareTo(other.OrderInSortGroup);
        }

        public static readonly RenderOrder RenderUnderEverything = new RenderOrder()
        {
            SortGroupInfo = new SortGroupInfo()
            {
                SortingOrder = int.MinValue,
                RenderQueue = int.MinValue,
                RenderOverOpaqueGeometry = false,
            },
            OrderInSortGroup = int.MinValue,
        };
    }

    internal struct RenderOrderCalculator
    {
        [ReadOnly]
        public NativeList<BatchGroupElement> BatchGroupElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, ZLayerCounts> ZLayerCounts;
        [ReadOnly]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        [ReadOnly]
        public NativeList<DataStoreIndex, int> OrderInZLayer;
        [ReadOnly]
        public NovaHashMap<DataStoreID, SortGroupInfo> SortGroupInfos;

        public RenderOrder GetRenderOrder(DataStoreIndex dataStoreIndex)
        {
            BatchGroupElement batchGroupElement = BatchGroupElements[dataStoreIndex];
            if (!ZLayerCounts.TryGetValue(batchGroupElement.BatchRootID, out ZLayerCounts zlayerCounts))
            {
                return RenderOrder.RenderUnderEverything;
            }

            RenderOrder toRet = new RenderOrder();
            if (!SortGroupInfos.TryGetValue(batchGroupElement.BatchRootID, out toRet.SortGroupInfo))
            {
                toRet.SortGroupInfo = SortGroupInfo.Default;
            }

            short zlayer = BaseInfos[dataStoreIndex].Val.ZIndex;
            int offset = zlayerCounts.GetRenderOrderOffset(zlayer);
            toRet.OrderInSortGroup = OrderInZLayer[dataStoreIndex] + offset;
            return toRet;
        }
    }
}

