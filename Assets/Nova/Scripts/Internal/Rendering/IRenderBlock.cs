// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Rendering
{
    internal interface IRenderBlock<T> : IRenderBlock
    {
        ref T RenderData { get; }
    }

    internal interface IRenderBlock : IUIBlockBase
    {
        ref BaseRenderInfo BaseRenderInfo { get; }
        ref Surface Surface { get; }
        bool Visible { get; set; }
    }
}
