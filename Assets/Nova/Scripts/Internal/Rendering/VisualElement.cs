// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Nova.Internal.Rendering
{
    [Flags]
    internal enum VisualType : ushort
    {
        Invalid = 0,
        UIBlock2D = 1,
        UIBlock3D = 2 * UIBlock2D,
        TextBlock = 2 * UIBlock3D,
        DropShadow = 2 * TextBlock,
        TextSubmesh = 2 * DropShadow,

        TEXT_MASK = TextBlock | TextSubmesh
    }

    /// <summary>
    /// Since the elemenets that are rendered don't always have a 1:1 mapping to blocks,
    /// (for example, drop shadows), this represents a single item that is rendered
    /// </summary>
    internal struct VisualElement
    {
        public DataStoreIndex DataStoreIndex;
        public RenderIndex RenderIndex;
        public DrawCallDescriptorID DrawCallDescriptorID;
        public VisualType Type;
        public bool SkipRendering;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VisualElement(ref DataStoreIndex dataStoreIndex, ref RenderIndex renderIndex, ref DrawCallDescriptorID id, VisualType visualType)
        {
            DataStoreIndex = dataStoreIndex;
            RenderIndex = renderIndex;
            DrawCallDescriptorID = id;
            Type = visualType;
            SkipRendering = false;
        }
    }

    internal static class VisualElementUtilities
    {
        public static bool TryGetIndex(int index, ref NativeList<DataStoreID> dirtyBatches, ref NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> visualElements, out DataStoreID batchRootID, out int indexOut)
        {
            for (int i = 0; i < dirtyBatches.Length; ++i)
            {
                batchRootID = dirtyBatches[i];
                NovaList<VisualElementIndex, VisualElement> elements = visualElements[batchRootID];
                if (index >= elements.Length)
                {
                    index -= elements.Length;
                    continue;
                }

                indexOut = index;
                return true;
            }

            batchRootID = DataStoreID.Invalid;
            indexOut = -1;
            return false;
        }

        public static bool Is2D(this ref VisualType visualType)
        {
            return (visualType & VisualType.UIBlock3D) == 0;
        }
    }
}