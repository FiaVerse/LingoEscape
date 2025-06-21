// Copyright (c) Supernova Technologies LLC
using System;

namespace Nova.Events
{
    internal interface IEventTarget { }

    internal interface IEventTargetProvider
    {
        public bool TryGetTarget(IEventTarget eventReceiver, Type eventType, out IEventTarget eventTarget);

        public Type BaseTargetableType { get; }
    }
}
