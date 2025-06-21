// Copyright (c) Supernova Technologies LLC
using System.Runtime.InteropServices;

namespace Nova.Internal.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ValuePair<T1,T2> where T1 : unmanaged where T2 : unmanaged
    {
        public T1 Item1;
        public T2 Item2;

        public ValuePair(T1 t1, T2 t2)
        {
            Item1 = t1;
            Item2 = t2;
        }
    }
}

