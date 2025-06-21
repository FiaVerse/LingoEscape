// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Core;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Nova.Internal.Rendering
{
    internal struct GCHandleCleanup : IDisposable
    {
        private ulong handle;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GCHandleCleanup(ulong handle)
        {
            this.handle = handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (handle != 0)
            {
                UnsafeUtility.ReleaseGCObject(handle);
                handle = 0;
            }
        }
    }

    internal unsafe struct AccessWrapper<T> where T : unmanaged
    {
        public DataStoreID DataStoreID;
        public ulong GCHandle;
        public T* Ptr;

        public ref T Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref (*Ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AccessWrapper(IDataStoreElement element, ref T dataRef)
        {
            DataStoreID = element.UniqueID;
            UnsafeUtility.PinGCObjectAndGetAddress(element, out GCHandle);
            Ptr = (T*)UnsafeUtility.AddressOf(ref dataRef);
        }
    }
}

