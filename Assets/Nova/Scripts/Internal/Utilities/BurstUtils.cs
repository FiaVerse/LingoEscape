// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;

namespace Nova.Internal.Utilities
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void BurstMethod(void* jobData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void BurstFunction(void* jobData, int functionID);

    internal unsafe struct BurstedMethod<T> where T : class
    {
        public T Method;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BurstedMethod(T method)
        {
            try
            {
                Method = BurstCompiler.CompileFunctionPointer(method).Invoke;
            }
            catch
            {
                Method = method;
            }
        }
    }
}
