// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Rendering
{
    internal partial class RenderEngine
    {
        public GetAllDirtyBatchRootsJob GetAllDirtyBatchRootsJob;
        public ShaderDataJob ShaderDataJob;
        public WorldSpaceBoundsJob WorldBoundsJob;
        public CoplanarSpaceBoundsJob RenderBoundsJob;
        public RenderOrderJob RenderOrderJob;
        public VisualElementCountJob VisualElementCountJob;
        public RootFromBlockMatrixJob RootFromBlockMatrixJob;
        public OverlapJob OverlapJob;
        public DrawCallArbitrationJob DrawCallJob;
        public TextBlockSizeChangeDetectionJob TextBlockSizeChangeDetectionJob;
        public QuadGenerationJob QuadGenerationJob;
        public QuadProcessJob QuadProcessJob;
        public SubQuadShaderDataJob SubQuadOverlapProcessJob;
        public SubQuadVertCopyJob SubQuadVertCopyJob;
        public RenderSetFilterJob RenderSetFilterJob;
        public RenderingFinalizeJob FinalizeJob;
    }
}