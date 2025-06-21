// Copyright (c) Supernova Technologies LLC
using System;

namespace Nova.Internal.Common
{
    internal interface IInitializable : IDisposable
    {
        void Init();
    }

    internal interface ICapacityInitializable : IDisposable
    {
        void Init(int capacity = 0);
    }
}
