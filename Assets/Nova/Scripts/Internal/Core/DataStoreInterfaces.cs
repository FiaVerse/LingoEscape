// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Common;

namespace Nova.Internal.Core
{
    internal interface IDataStoreElement : INamedElement
    {
        DataStoreID UniqueID { get; }
        DataStoreIndex Index { get; }
        void SetIndex(DataStoreIndex index);
        bool IsRegistered { get; }
        void Register();
        void Unregister();
    }

    internal interface INamedElement
    {
        string Name { get; }
    }

    internal interface IDataStore : IInitializable
    {
        bool IsRegistered(DataStoreID id);
    }
}
