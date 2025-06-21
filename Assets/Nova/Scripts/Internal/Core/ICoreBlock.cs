// Copyright (c) Supernova Technologies LLC
using System.Collections.Generic;

namespace Nova.Internal.Core
{
    internal interface ICoreBlock : IHierarchyBlock, ITransformProvider, IGameObjectActiveReceiver
    {
        bool ChildrenAreDirty { get; set; }
        bool ChildHandledHierarchyChangeForParent { get; set; }

        new ICoreBlock Parent { get; set; }
        List<DataStoreID> ChildIDs { get; }
        List<ICoreBlock> Children { get; }
    }

    internal interface IHierarchyBlock : IHierarchyActivatable, ITransformProvider
    {
        bool IsHierarchyRoot { get; }
        bool IsBatchRoot { get; }

        int SiblingPriority { get; }
        IHierarchyBlock Parent { get; }
        IHierarchyBlock Root { get; }
    }
}

