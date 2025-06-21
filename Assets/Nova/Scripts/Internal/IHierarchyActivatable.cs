// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal
{
    internal interface IHierarchyActivatable
    {
        bool Activated { get; }
        bool ActiveInHierarchy { get; }
        bool ActiveSelf { get; }
        bool Deactivating { get; }
    }
}
