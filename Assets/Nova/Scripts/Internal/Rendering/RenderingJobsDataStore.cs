// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Core;

namespace Nova.Internal.Rendering
{
    internal partial class RenderingDataStore : DataStore<RenderingDataStore, IRenderBlock>
    {
        public ComputeBufferIndices ComputeBufferIndices;
        public OverlapElements OverlapElements;
        public BlockBounds Bounds;

        private void InitJobStructs()
        {
            ComputeBufferIndices = new ComputeBufferIndices()
            {
                TransformAndLighting = Common.TransformIndices,
                UIBlock2D = UIBlock2DData.ComputeBufferIndices,
                Shadow = UIBlock2DData.Shadow.Indices,
                UIBlock3D = UIBlock3DData.ComputeBufferIndices,
                Text = TextBlockData.ComputeBufferIndices,
            };

            OverlapElements = new OverlapElements()
            {
                OverlappingElements = Common.OverlappingElements,
                ShadowOverlappingElements = UIBlock2DData.Shadow.OverlappingElements,
            };

            Bounds = new BlockBounds()
            {
                Block = Common.BlockRenderBounds,
                Shadow = UIBlock2DData.Shadow.Bounds,
            };
        }
    }
}
