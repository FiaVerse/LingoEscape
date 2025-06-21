// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Unity.Collections;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// Helper for turning an index over all major dirty elements into a DataStoreIndex and matching Batch Root id
    /// </summary>
    internal struct VisualElementIndexHelper
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatches;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;

        public bool TryGetIndex(int index, out DataStoreID batchRootID, out VisualElementIndex visualElementIndex)
        {
            for (int i = 0; i < DirtyBatches.Length; ++i)
            {
                batchRootID = DirtyBatches[i];
                NovaList<VisualElementIndex, VisualElement> elements = VisualElements[batchRootID];
                if (index >= elements.Length)
                {
                    index -= elements.Length;
                    continue;
                }

                visualElementIndex = index;
                return true;
            }

            Debug.LogError($"Failed to get VisualIndex for ${index}");
            visualElementIndex = VisualElementIndex.Invalid;
            batchRootID = DataStoreID.Invalid;
            return false;
        }
    }
}

