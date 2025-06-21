// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Rendering
{
    internal struct RenderElement<T> where T : struct
    {
        public T Val;
        public RenderIndex RenderIndex;

        public RenderElement(ref T val)
        {
            Val = val;
            RenderIndex = -1;
        }
    }
}
