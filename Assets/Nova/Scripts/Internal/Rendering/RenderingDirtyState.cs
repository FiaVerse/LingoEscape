// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;

namespace Nova.Internal.Rendering
{
    internal struct RenderingDirtyState : IInitializable, IClearable
    {
        public NativeDedupedList<DataStoreIndex> DirtyShaderData;
        public NativeDedupedList<DataStoreID> DirtyBatchRoots;
        public NativeDedupedList<DataStoreID> DirtyVisualModifiers;

        public NativeDedupedList<DataStoreID> DirtyBaseInfos;
        public NativeDedupedList<DataStoreID> AddedElements;

        public bool IsDirty
        {
            get => (DirtyShaderData.Length + DirtyBatchRoots.Length + DirtyBaseInfos.Length) > 0;
        }

        public void Clear()
        {
            DirtyShaderData.Clear();
            DirtyBatchRoots.Clear();
            DirtyVisualModifiers.Clear();
            DirtyBaseInfos.Clear();
            AddedElements.Clear();
        }

        public void Dispose()
        {
            DirtyShaderData.Dispose();
            DirtyBatchRoots.Dispose();
            DirtyVisualModifiers.Dispose();
            DirtyBaseInfos.Dispose();
            AddedElements.Dispose();
        }

        public void Init()
        {
            DirtyShaderData.Init(Constants.SomeElementsInitialCapacity);
            DirtyBatchRoots.Init(Constants.FewElementsInitialCapacity);
            DirtyVisualModifiers.Init(Constants.FewElementsInitialCapacity);
            DirtyBaseInfos.Init(Constants.SomeElementsInitialCapacity);
            AddedElements.Init(Constants.AllElementsInitialCapacity);
        }
    }
}

