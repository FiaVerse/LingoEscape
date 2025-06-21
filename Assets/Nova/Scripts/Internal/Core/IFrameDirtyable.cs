// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Core
{
    /// <summary>
    /// Something that can be dirtied frame to frame
    /// </summary>
    internal interface IFrameDirtyable
    {
        bool IsDirty { get; }
        void ClearDirtyState();
    }
}

