// Copyright (c) Supernova Technologies LLC
using Unity.Collections;
using static Unity.Collections.AllocatorManager;

namespace Nova.Internal.Collections
{
    internal abstract class NovaAllocator
    {
        public readonly static AllocatorHandle Handle = Allocator.Persistent;

        // Just a hack to make our tools compile
        public static Dummy handle = default;
        public struct Dummy
        {
            public AllocatorHandle Data { set { } }
        }
    }
}
