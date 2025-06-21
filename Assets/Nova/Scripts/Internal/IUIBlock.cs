// Copyright (c) Supernova Technologies LLC
using Nova.Events;
using Nova.Internal;
using Nova.Internal.Rendering;

namespace Nova
{
    internal interface IUIBlock : IEventTarget, IRenderBlock
    {
        IInputTarget InputTarget { get; }
    }
}
