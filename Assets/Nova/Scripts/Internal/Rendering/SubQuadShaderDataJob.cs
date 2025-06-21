// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    internal struct SubQuadData : IInitializable, IClearable
    {
        public NovaList<int> RendersUnder;
        public NovaList<SubQuadVert> Verts;
        public int SortedIndex;

        public void Clear()
        {
            RendersUnder.Clear();
            Verts.Clear();
        }

        public void Dispose()
        {
            RendersUnder.Dispose();
            Verts.Dispose();
        }

        public void Init()
        {
            RendersUnder.Init();
            Verts.Init();
        }
    }

    internal struct InProgressQuad
    {
        public RotationSpaceBounds Bounds;
        public bool EdgeSoftenDisabled;
        public float2 UVZoom;
        public float2 CenterUV;
    }

    internal struct SubQuadProcessingData : IInitializable, IClearable
    {
        public NovaList<InProgressQuad> SubQuads;
        public NovaList<float> XSplits;
        public NovaList<float> YSplits;

        public void Clear()
        {
            SubQuads.Clear();
            XSplits.Clear();
            YSplits.Clear();
        }

        public void Dispose()
        {
            SubQuads.Dispose();
            XSplits.Dispose();
            YSplits.Dispose();
        }

        public void Init()
        {
            SubQuads.Init();
            XSplits.Init();
            YSplits.Init();
        }
    }

    [BurstCompile]
    internal partial struct SubQuadShaderDataJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatches;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, RotationSetSummary> RotationSets;
        [ReadOnly]
        public NativeList<RenderIndex, ComputeBufferIndex> ComputeBufferIndices;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> LocalFromWorldMatrices;
        [ReadOnly]
        public NativeList<RenderIndex, UIBlock2DData> UIBlock2DData;
        [ReadOnly]
        public ImageDataProvider ImageDataProvider;

        [NativeDisableParallelForRestriction]
        public NativeList<RenderIndex, SubQuadData> SubQuadData;

        [NativeDisableParallelForRestriction]
        public NativeList<SubQuadProcessingData> ProcessingData;

        [NativeSetThreadIndex]
        public int threadIndex;

        private RotationSet rotationSet;
        private SubQuadProcessingData processingData;
        private ComputeBufferIndex computeBufferIndex;
        private SubQuadData subQuadData;
        private DataStoreIndex dataStoreIndex;
        private float4x4 localFromSet;

        public void Execute(int index)
        {
            if (!DirtyBatches.TryGetQuadProvider(ref RotationSets, ref index, out rotationSet, out DataStoreID batchRootID))
            {
                return;
            }

            processingData = ProcessingData.ElementAt(threadIndex);
            processingData.Clear();

            VisualElementIndex visualElementIndex = rotationSet.QuadProviders[index];
            ref VisualElement visualElement = ref VisualElements[batchRootID].ElementAt(visualElementIndex);
            dataStoreIndex = visualElement.DataStoreIndex;

            ref float4x4 localFromWorld = ref LocalFromWorldMatrices.ElementAt(dataStoreIndex);
            localFromSet = math.mul(localFromWorld, rotationSet.WorldFromSet);

            DoBlock(ref visualElement);

            ProcessingData[threadIndex] = processingData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoBlock(ref VisualElement visualElement)
        {
            subQuadData = SubQuadData.ElementAt(visualElement.RenderIndex);
            ref QuadBoundsDescriptor descriptor = ref rotationSet.BoundarySummary.Descriptors.ElementAt(subQuadData.SortedIndex);

            computeBufferIndex = ComputeBufferIndices[descriptor.RenderIndex];

            UIBlock2DData blockData = UIBlock2DData[visualElement.RenderIndex];

            InProgressQuad baseQuad = new InProgressQuad()
            {
                Bounds = descriptor.MaxRenderBounds,
                EdgeSoftenDisabled = blockData.SoftenEdges ? false : true,
            };

            if (ImageDataProvider.TryGetImageData(blockData.Image.ImageID, out ImageDescriptor imageDescriptor, out TextureDescriptor textureDescriptor))
            {
                // Nova UVs go from (-1, -1) to (1, 1), with (0, 0) in the center.
                if (blockData.Image.Adjustment.ScaleMode == ImageScaleMode.Sliced ||
                    blockData.Image.Adjustment.ScaleMode == ImageScaleMode.Tiled)
                {
                    float2 blockHalfSize = Math.float2_Half * descriptor.Bounds.Size;

                    // Add the 9 sliced quads
                    RotationSpaceBounds borderedBlockBounds = descriptor.MaxRenderBounds;

                    // The bounds of the sprite within the texture
                    RotationSpaceBounds spriteUVBounds = new RotationSpaceBounds(
                        imageDescriptor.Rect.x, imageDescriptor.Rect.y,
                        imageDescriptor.Rect.xMax, imageDescriptor.Rect.yMax
                        );
                    float2 textureDimensions = textureDescriptor.Dimensions;
                    spriteUVBounds.BL = Math.float2_Two * spriteUVBounds.BL / textureDimensions - Math.float2_One;
                    spriteUVBounds.TR = Math.float2_Two * spriteUVBounds.TR / textureDimensions - Math.float2_One;

                    SpriteBorder borderSizeAsTextureUV = Math.float4_Two * imageDescriptor.Border.vals / textureDimensions.xyxy;
                    SpriteBorder borderSizeBlockSpace = imageDescriptor.Border.vals / blockData.Image.Adjustment.PixelsPerUnitMultiplier;

                    // Adjust the size of the border if it's larger than the size of the block
                    bool2 borderOccupiesFullDimension = borderSizeBlockSpace.TotalSize > descriptor.Bounds.Size;
                    float4 adjustedBorderSize = borderSizeBlockSpace.vals / borderSizeBlockSpace.TotalSize.xyxy * descriptor.Bounds.Size.xyxy;
                    borderSizeBlockSpace.vals = math.select(borderSizeBlockSpace.vals, adjustedBorderSize, borderOccupiesFullDimension.xyxy);

                    RotationSpaceBounds centerSliceRotationSpaceBounds = new RotationSpaceBounds(
                        descriptor.Bounds.BL + borderSizeBlockSpace.BL,
                         descriptor.Bounds.TR - borderSizeBlockSpace.TR
                        );
                    //centerSliceRotationSpaceBounds.BL = math.min(centerSliceRotationSpaceBounds.BL, centerSliceRotationSpaceBounds.TR);

                    RotationSpaceBounds centerSliceBlockUVBounds = new RotationSpaceBounds(
                        Math.float2_NegativeOne + borderSizeBlockSpace.BL / blockHalfSize,
                        Math.float2_One - borderSizeBlockSpace.TR / blockHalfSize
                        );
                    RotationSpaceBounds centerSliceTextureUVBounds = new RotationSpaceBounds(
                        spriteUVBounds.BL + borderSizeAsTextureUV.BL,
                        spriteUVBounds.TR - borderSizeAsTextureUV.TR
                        );

                    // Add the corners
                    // TL
                    baseQuad.CenterUV = GetUVCenterAndScale(
                        // TextureUV
                        new RotationSpaceBounds(spriteUVBounds.LeftEdge, centerSliceTextureUVBounds.TopEdge, centerSliceTextureUVBounds.LeftEdge, spriteUVBounds.TopEdge),
                        // BlockUV
                        new RotationSpaceBounds(-1f, centerSliceBlockUVBounds.TopEdge, centerSliceBlockUVBounds.LeftEdge, 1f),
                        out baseQuad.UVZoom
                        );
                    baseQuad.Bounds = new RotationSpaceBounds(borderedBlockBounds.LeftEdge, centerSliceRotationSpaceBounds.TopEdge, centerSliceRotationSpaceBounds.LeftEdge, borderedBlockBounds.TopEdge);
                    AddSubQuad(baseQuad);

                    // TR
                    baseQuad.CenterUV = GetUVCenterAndScale(
                        // TextureUV
                        new RotationSpaceBounds(centerSliceTextureUVBounds.RightEdge, centerSliceTextureUVBounds.TopEdge, spriteUVBounds.RightEdge, spriteUVBounds.TopEdge),
                        // BlockUV
                        new RotationSpaceBounds(centerSliceBlockUVBounds.RightEdge, centerSliceBlockUVBounds.TopEdge, 1f, 1f),
                        out baseQuad.UVZoom
                        );
                    baseQuad.Bounds = new RotationSpaceBounds(centerSliceRotationSpaceBounds.RightEdge, centerSliceRotationSpaceBounds.TopEdge, borderedBlockBounds.RightEdge, borderedBlockBounds.TopEdge);
                    AddSubQuad(baseQuad);

                    // BR
                    baseQuad.CenterUV = GetUVCenterAndScale(
                        // TextureUV
                        new RotationSpaceBounds(centerSliceTextureUVBounds.RightEdge, spriteUVBounds.BottomEdge, spriteUVBounds.RightEdge, centerSliceTextureUVBounds.BottomEdge),
                        // BlockUV
                        new RotationSpaceBounds(centerSliceBlockUVBounds.RightEdge, -1f, 1f, centerSliceBlockUVBounds.BottomEdge),
                        out baseQuad.UVZoom
                        );
                    baseQuad.Bounds = new RotationSpaceBounds(centerSliceRotationSpaceBounds.RightEdge, borderedBlockBounds.BottomEdge, borderedBlockBounds.RightEdge, centerSliceRotationSpaceBounds.BottomEdge);
                    AddSubQuad(baseQuad);

                    // BL
                    baseQuad.CenterUV = GetUVCenterAndScale(
                        // TextureUV
                        new RotationSpaceBounds(spriteUVBounds.LeftEdge, spriteUVBounds.BottomEdge, centerSliceTextureUVBounds.LeftEdge, centerSliceTextureUVBounds.BottomEdge),
                        // BlockUV
                        new RotationSpaceBounds(-1f, -1f, centerSliceBlockUVBounds.LeftEdge, centerSliceBlockUVBounds.BottomEdge),
                        out baseQuad.UVZoom
                        );
                    baseQuad.Bounds = new RotationSpaceBounds(borderedBlockBounds.LeftEdge, borderedBlockBounds.BottomEdge, centerSliceRotationSpaceBounds.LeftEdge, centerSliceRotationSpaceBounds.BottomEdge);
                    AddSubQuad(baseQuad);

                    if (blockData.Image.Adjustment.ScaleMode == ImageScaleMode.Tiled)
                    {
                        float2 centerTileBlockSpaceSize = (new float2(imageDescriptor.Rect.width, imageDescriptor.Rect.height) - new float2(imageDescriptor.Border.TotalWidth, imageDescriptor.Border.TotalHeight)) / blockData.Image.Adjustment.PixelsPerUnitMultiplier;
                        float2 centerTileBlockUVSize = centerTileBlockSpaceSize / blockHalfSize;

                        // Add the tiles
                        int2 centerTileCount = (int2)math.ceil(centerSliceRotationSpaceBounds.Size / centerTileBlockSpaceSize);

                        // Ensure centerTileCount is always > 0
                        centerTileCount = math.max(centerTileCount, Math.int2_One);
                        for (int x = 0; x < centerTileCount.x; ++x)
                        {
                            for (int y = 0; y < centerTileCount.y; ++y)
                            {
                                // Add the center tile
                                float2 tileIndices = new float2(x, y);

                                float2 blockUVBL = centerSliceBlockUVBounds.BL + tileIndices * centerTileBlockUVSize;
                                RotationSpaceBounds centerTileBlockUVBounds = new RotationSpaceBounds(blockUVBL,
                                        math.min(blockUVBL + centerTileBlockUVSize, centerSliceBlockUVBounds.TR));

                                // How much of the center tile is actually used
                                float2 tileRatio = centerTileBlockUVBounds.Size / centerTileBlockUVSize;
                                RotationSpaceBounds centerTileTextureUVBounds = new RotationSpaceBounds(
                                    centerSliceTextureUVBounds.BL,
                                    centerSliceTextureUVBounds.BL + tileRatio * centerSliceTextureUVBounds.Size);
                                baseQuad.CenterUV = GetUVCenterAndScale(centerTileTextureUVBounds, centerTileBlockUVBounds, out baseQuad.UVZoom);

                                float2 quadBoundsBL = centerSliceRotationSpaceBounds.BL + tileIndices * centerTileBlockSpaceSize;
                                RotationSpaceBounds centerTileBlockBounds = new RotationSpaceBounds(quadBoundsBL, math.min(quadBoundsBL + centerTileBlockSpaceSize, centerSliceRotationSpaceBounds.TR));
                                baseQuad.Bounds = centerTileBlockBounds;
                                AddSubQuad(baseQuad);

                                if (x == 0)
                                {
                                    // Left
                                    baseQuad.CenterUV = GetUVCenterAndScale(
                                        // TextureUV
                                        new RotationSpaceBounds(
                                            spriteUVBounds.LeftEdge,
                                            centerSliceTextureUVBounds.BottomEdge,
                                            centerSliceTextureUVBounds.LeftEdge,
                                            centerTileTextureUVBounds.TopEdge),
                                        // BlockUV
                                        new RotationSpaceBounds(-1f, centerTileBlockUVBounds.BottomEdge, centerSliceBlockUVBounds.LeftEdge, centerTileBlockUVBounds.TopEdge),
                                        out baseQuad.UVZoom
                                        );
                                    baseQuad.Bounds = new RotationSpaceBounds(borderedBlockBounds.LeftEdge, centerTileBlockBounds.BottomEdge, centerSliceRotationSpaceBounds.LeftEdge, centerTileBlockBounds.TopEdge);
                                    AddSubQuad(baseQuad);

                                    // Right
                                    baseQuad.CenterUV = GetUVCenterAndScale(
                                        // TextureUV
                                        new RotationSpaceBounds(centerSliceTextureUVBounds.RightEdge, centerSliceTextureUVBounds.BottomEdge, spriteUVBounds.RightEdge, centerTileTextureUVBounds.TopEdge),
                                        // BlockUV
                                        new RotationSpaceBounds(centerSliceBlockUVBounds.RightEdge, centerTileBlockUVBounds.BottomEdge, 1f, centerTileBlockUVBounds.TopEdge),
                                        out baseQuad.UVZoom
                                        );
                                    baseQuad.Bounds = new RotationSpaceBounds(centerSliceRotationSpaceBounds.RightEdge, centerTileBlockBounds.BottomEdge, borderedBlockBounds.RightEdge, centerTileBlockBounds.TopEdge);
                                    AddSubQuad(baseQuad);
                                }

                                if (y == 0)
                                {
                                    // Top
                                    baseQuad.CenterUV = GetUVCenterAndScale(
                                        // TextureUV
                                        new RotationSpaceBounds(centerSliceTextureUVBounds.LeftEdge, centerSliceTextureUVBounds.TopEdge, centerTileTextureUVBounds.RightEdge, spriteUVBounds.TopEdge),
                                        // BlockUV
                                        new RotationSpaceBounds(centerTileBlockUVBounds.LeftEdge, centerSliceBlockUVBounds.TopEdge, centerTileBlockUVBounds.RightEdge, 1f),
                                        out baseQuad.UVZoom
                                        );
                                    baseQuad.Bounds = new RotationSpaceBounds(centerTileBlockBounds.LeftEdge, centerSliceRotationSpaceBounds.TopEdge, centerTileBlockBounds.RightEdge, borderedBlockBounds.TopEdge);
                                    AddSubQuad(baseQuad);

                                    // Bottom
                                    baseQuad.CenterUV = GetUVCenterAndScale(
                                        // TextureUV
                                        new RotationSpaceBounds(centerSliceTextureUVBounds.LeftEdge, spriteUVBounds.BottomEdge, centerTileTextureUVBounds.RightEdge, centerSliceTextureUVBounds.BottomEdge),
                                        // BlockUV
                                        new RotationSpaceBounds(centerTileBlockUVBounds.LeftEdge, -1f, centerTileBlockUVBounds.RightEdge, centerSliceBlockUVBounds.BottomEdge),
                                        out baseQuad.UVZoom
                                        );
                                    baseQuad.Bounds = new RotationSpaceBounds(centerTileBlockBounds.LeftEdge, borderedBlockBounds.BottomEdge, centerTileBlockBounds.RightEdge, centerSliceRotationSpaceBounds.BottomEdge);
                                    AddSubQuad(baseQuad);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Body
                        baseQuad.CenterUV = GetUVCenterAndScale(centerSliceTextureUVBounds, centerSliceBlockUVBounds, out baseQuad.UVZoom);
                        baseQuad.Bounds = centerSliceRotationSpaceBounds;
                        AddSubQuad(baseQuad);

                        // Left
                        baseQuad.CenterUV = GetUVCenterAndScale(
                            // TextureUV
                            new RotationSpaceBounds(spriteUVBounds.LeftEdge, centerSliceTextureUVBounds.BottomEdge, centerSliceTextureUVBounds.LeftEdge, centerSliceTextureUVBounds.TopEdge),
                            // BlockUV
                            new RotationSpaceBounds(-1f, centerSliceBlockUVBounds.BottomEdge, centerSliceBlockUVBounds.LeftEdge, centerSliceBlockUVBounds.TopEdge),
                            out baseQuad.UVZoom
                            );
                        baseQuad.Bounds = new RotationSpaceBounds(borderedBlockBounds.LeftEdge, centerSliceRotationSpaceBounds.BottomEdge, centerSliceRotationSpaceBounds.LeftEdge, centerSliceRotationSpaceBounds.TopEdge);
                        AddSubQuad(baseQuad);

                        // Top
                        baseQuad.CenterUV = GetUVCenterAndScale(
                            // TextureUV
                            new RotationSpaceBounds(centerSliceTextureUVBounds.LeftEdge, centerSliceTextureUVBounds.TopEdge, centerSliceTextureUVBounds.RightEdge, spriteUVBounds.TopEdge),
                            // BlockUV
                            new RotationSpaceBounds(centerSliceBlockUVBounds.LeftEdge, centerSliceBlockUVBounds.TopEdge, centerSliceBlockUVBounds.RightEdge, 1f),
                            out baseQuad.UVZoom
                            );
                        baseQuad.Bounds = new RotationSpaceBounds(centerSliceRotationSpaceBounds.LeftEdge, centerSliceRotationSpaceBounds.TopEdge, centerSliceRotationSpaceBounds.RightEdge, borderedBlockBounds.TopEdge);
                        AddSubQuad(baseQuad);

                        // Right
                        baseQuad.CenterUV = GetUVCenterAndScale(
                            // TextureUV
                            new RotationSpaceBounds(centerSliceTextureUVBounds.RightEdge, centerSliceTextureUVBounds.BottomEdge, spriteUVBounds.RightEdge, centerSliceTextureUVBounds.TopEdge),
                            // BlockUV
                            new RotationSpaceBounds(centerSliceBlockUVBounds.RightEdge, centerSliceBlockUVBounds.BottomEdge, 1f, centerSliceBlockUVBounds.TopEdge),
                            out baseQuad.UVZoom
                            );
                        baseQuad.Bounds = new RotationSpaceBounds(centerSliceRotationSpaceBounds.RightEdge, centerSliceRotationSpaceBounds.BottomEdge, borderedBlockBounds.RightEdge, centerSliceRotationSpaceBounds.TopEdge);
                        AddSubQuad(baseQuad);

                        // Bottom
                        baseQuad.CenterUV = GetUVCenterAndScale(
                            // TextureUV
                            new RotationSpaceBounds(centerSliceTextureUVBounds.LeftEdge, spriteUVBounds.BottomEdge, centerSliceTextureUVBounds.RightEdge, centerSliceTextureUVBounds.BottomEdge),
                            // BlockUV
                            new RotationSpaceBounds(centerSliceBlockUVBounds.LeftEdge, -1f, centerSliceBlockUVBounds.RightEdge, centerSliceBlockUVBounds.BottomEdge),
                            out baseQuad.UVZoom
                            );
                        baseQuad.Bounds = new RotationSpaceBounds(centerSliceRotationSpaceBounds.LeftEdge, borderedBlockBounds.BottomEdge, centerSliceRotationSpaceBounds.RightEdge, centerSliceRotationSpaceBounds.BottomEdge);
                        AddSubQuad(baseQuad);
                    }
                }
                else
                {
                    float2 textureDimensions = textureDescriptor.Dimensions;
                    float2 halfTextureDimensions = Math.float2_Half * textureDimensions;
                    float2 centerUV = ((float2)imageDescriptor.Rect.center - halfTextureDimensions) / halfTextureDimensions;
                    float2 zoom = textureDimensions / (float2)imageDescriptor.Rect.size;
                    float2 spriteUVCorrection = Math.float2_One / zoom;
                    baseQuad.CenterUV = centerUV + spriteUVCorrection * blockData.Image.Adjustment.CenterUV;
                    baseQuad.UVZoom = spriteUVCorrection * math.select(float2.zero, Math.float2_One / blockData.Image.Adjustment.UVScale, blockData.Image.Adjustment.UVScale != float2.zero);

                    if (blockData.Image.Adjustment.ScaleMode == ImageScaleMode.Envelope)
                    {
                        // We only do envelope here because AdjustSizeForImage handles fit
                        float nodeAspectRatio = descriptor.Bounds.Size.x / descriptor.Bounds.Size.y;
                        float relativeAspectRatio = imageDescriptor.AspectRatio / nodeAspectRatio;
                        if (relativeAspectRatio > 1)
                        {
                            baseQuad.UVZoom.x /= relativeAspectRatio;
                        }
                        else
                        {
                            baseQuad.UVZoom.y *= relativeAspectRatio;
                        }
                    }

                    AddSubQuad(baseQuad);
                }
            }
            else
            {
                // No image, just add the base quad
                AddSubQuad(baseQuad);
            }

            CheckBodyOverlaps(ref subQuadData, ref descriptor);
            CheckBorderOverlaps(ref subQuadData, ref descriptor);

            RemoveTJunctions();

            SubQuadData.ElementAt(visualElement.RenderIndex) = subQuadData;
        }

        /// <summary>
        /// Converts a provided texture UV rect and block UV rect into a UVCenter and UVZoom expected by the shader.
        /// </summary>
        private float2 GetUVCenterAndScale(RotationSpaceBounds textureUVBounds, RotationSpaceBounds blockUVBounds, out float2 uvZoom)
        {
            uvZoom = (textureUVBounds.TR - textureUVBounds.BL) / (blockUVBounds.TR - blockUVBounds.BL);
            return textureUVBounds.TR - blockUVBounds.TR * uvZoom;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveTJunctions()
        {
            if (processingData.SubQuads.Length == 0)
            {
                return;
            }

            // Add all of the corners
            for (int i = 0; i < processingData.SubQuads.Length; ++i)
            {
                ref InProgressQuad quad = ref processingData.SubQuads.ElementAt(i);

                processingData.XSplits.Add(quad.Bounds.BL.x);
                processingData.YSplits.Add(quad.Bounds.BL.y);
                processingData.XSplits.Add(quad.Bounds.TR.x);
                processingData.YSplits.Add(quad.Bounds.TR.y);
            }

            // Sort
            processingData.XSplits.Sort();
            processingData.YSplits.Sort();
            Dedupe(ref processingData.XSplits);
            Dedupe(ref processingData.YSplits);

            // Now go through the sub quads and break them up into the respective rows and columns
            for (int i = 0; i < processingData.SubQuads.Length; ++i)
            {
                ref InProgressQuad quad = ref processingData.SubQuads.ElementAt(i);

                int2 trIndices = default;
                trIndices.x = GetClosest(ref processingData.XSplits, quad.Bounds.TR.x);
                trIndices.y = GetClosest(ref processingData.YSplits, quad.Bounds.TR.y);
                AddToRowsAndColumns(ref quad, ref trIndices);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetClosest(ref NovaList<float> list, float val)
        {
            float closestDelta = Math.Abs(list[0] - val);
            float lastDelta = closestDelta;
            int closestIndex = 0;
            for (int i = 1; i < list.Length; ++i)
            {
                float delta = Math.Abs(list[i] - val);

                bool closer = delta < closestDelta;
                closestDelta = math.select(closestDelta, delta, closer);
                closestIndex = math.select(closestIndex, i, closer);

                if (delta > lastDelta)
                {
                    // Since the list is sorted, if the deltas start getting larger
                    // we don't need to check anything else
                    break;
                }
                lastDelta = delta;
            }

            return closestIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Dedupe(ref NovaList<float> list)
        {
            float lastValue = list[0];
            int writeIndex = 1;
            for (int i = 1; i < list.Length; ++i)
            {
                float currentValue = list[i];
                if (!Math.ApproximatelyEqual(currentValue, lastValue))
                {
                    list[writeIndex++] = currentValue;
                }

                lastValue = list[i];
            }

            list.Length = writeIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToRowsAndColumns(ref InProgressQuad quad, ref int2 trIndices)
        {
            SubQuadVert vert = new SubQuadVert()
            {
                BlockDataIndex = (uint)computeBufferIndex,
                EdgeSoftenMask = quad.EdgeSoftenDisabled ? 0f : 1f,
                CenterUV = quad.CenterUV,
                UVZoom = quad.UVZoom,
            };


            for (int x = trIndices.x - 1; x >= 0; --x)
            {
                float xSplit = processingData.XSplits[x];

                for (int y = trIndices.y - 1; y >= 0; --y)
                {
                    float ySplit = processingData.YSplits[y];

                    float xSplit2 = processingData.XSplits[x + 1];
                    float ySplit2 = processingData.YSplits[y + 1];

                    // TR
                    vert.Pos = math.transform(localFromSet, new float3(xSplit2, ySplit2, 0)).xy;
                    subQuadData.Verts.Add(vert);

                    // BR
                    vert.Pos = math.transform(localFromSet, new float3(xSplit2, ySplit, 0)).xy;
                    subQuadData.Verts.Add(vert);

                    // BL
                    vert.Pos = math.transform(localFromSet, new float3(xSplit, ySplit, 0)).xy;
                    subQuadData.Verts.Add(vert);

                    // TL
                    vert.Pos = math.transform(localFromSet, new float3(xSplit, ySplit2, 0)).xy;
                    subQuadData.Verts.Add(vert);

                    if (Math.ApproximatelyEqual(ySplit, quad.Bounds.BL.y))
                    {
                        break;
                    }
                }

                if (Math.ApproximatelyEqual(xSplit, quad.Bounds.BL.x))
                {
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckBodyOverlaps(ref SubQuadData subQuadData, ref QuadBoundsDescriptor descriptor)
        {
            if (!descriptor.BodyRenders)
            {
                return;
            }

            BodyOnly converter = new BodyOnly(ref descriptor);
            for (int i = 0; i < subQuadData.RendersUnder.Length; ++i)
            {
                ref QuadBoundsDescriptor overlap = ref rotationSet.BoundarySummary.Descriptors.ElementAt(subQuadData.RendersUnder[i]);

                OcclusionType occlusionType = GetOcclusionType(ref overlap);
                switch (occlusionType)
                {
                    case OcclusionType.BodyAndBorder:
                        BodyAndBorder bodyAndBorder = new BodyAndBorder(ref overlap);
                        CheckOverlap(ref converter, ref bodyAndBorder);
                        break;
                    case OcclusionType.BodyOnly:
                        BodyOnly bodyOnly = new BodyOnly(ref overlap);
                        CheckOverlap(ref converter, ref bodyOnly);
                        break;
                    case OcclusionType.BorderOnly:
                        BorderOnly borderOnly = new BorderOnly(ref overlap);
                        CheckOverlap(ref converter, ref borderOnly);
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckBorderOverlaps(ref SubQuadData subQuadData, ref QuadBoundsDescriptor descriptor)
        {
            if (!descriptor.HasBorder)
            {
                return;
            }

            BorderOnly converter = new BorderOnly(ref descriptor);
            for (int i = 0; i < subQuadData.RendersUnder.Length; ++i)
            {
                ref QuadBoundsDescriptor overlap = ref rotationSet.BoundarySummary.Descriptors.ElementAt(subQuadData.RendersUnder[i]);

                OcclusionType occlusionType = GetOcclusionType(ref overlap);
                switch (occlusionType)
                {
                    case OcclusionType.BodyAndBorder:
                        BodyAndBorder bodyAndBorder = new BodyAndBorder(ref overlap);
                        CheckOverlap(ref converter, ref bodyAndBorder);
                        break;
                    case OcclusionType.BodyOnly:
                        BodyOnly bodyOnly = new BodyOnly(ref overlap);
                        CheckOverlap(ref converter, ref bodyOnly);
                        break;
                    case OcclusionType.BorderOnly:
                        BorderOnly borderOnly = new BorderOnly(ref overlap);
                        CheckOverlap(ref converter, ref borderOnly);
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckOverlap<T, U>(ref T converter, ref U overlap)
            where T : unmanaged, IBoundsConverter
            where U : unmanaged, IBoundsConverter
        {
            ref RotationSpaceBounds bounds = ref converter.Bounds;
            ref RotationSpaceBounds overlapBounds = ref overlap.Bounds;

            bool4 equal = bounds.ApproxEqual(ref overlapBounds);
            if (!math.any(equal))
            {
                return;
            }

            RotationSpaceBounds maxCoverageBounds = overlap.MaxCoverageBounds;
            bool4 hasRemainders = GetCutoutRemainders(ref bounds, ref maxCoverageBounds, out float4 remainders);
            float4 maxCoverageRemainder = overlap.MaxCoverageRemainder;

            bool hasRoundedCorner = !Math.ApproximatelyZero(overlap.CornerRadius);

            if (hasRoundedCorner)
            {
                bool4 edgesAlign = Math.ApproximatelyEqual4(ref remainders, ref maxCoverageRemainder);
                for (int i = 0; i < 4; ++i)
                {
                    if (!edgesAlign[i])
                    {
                        continue;
                    }

                    RotationSpaceBounds edgeBounds = overlap.GetEdgeBounds(i);
                    DisableSoften(ref edgeBounds);
                }
            }
            else
            {
                // If the corner is not rounded, we process all 4 edges at once...I guess.
                // I don't remember why I did it that way, but 4 months later I'm here fixing a bug
                // and the only info I have about the aforementioned change is that it was in a commit titled
                // "Fix edge soften bug for unrounded 2d blocks". So I assume I did it for a valid reason.
                RotationSpaceBounds edgeBounds = overlap.GetEdgeBounds(0);
                DisableSoften(ref edgeBounds);
            }

            if (!hasRoundedCorner || !Math.ApproximatelyEqual(converter.CornerRadius, overlap.CornerRadius))
            {
                // Skip corner processing if corners not rounded or radii differ
                return;
            }

            // Process corners
            for (int i = 0; i < 4; ++i)
            {
                float2 corner1 = bounds.GetCorner(i);
                float2 corner2 = overlapBounds.GetCorner(i);

                if (!Math.ApproximatelyEqual(ref corner1, ref corner2))
                {
                    continue;
                }

                RotationSpaceBounds cornerBounds = converter.GetCornerBounds(i);
                DisableSoften(ref cornerBounds);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisableSoften(ref RotationSpaceBounds cutout)
        {
            float2 cutoutSize = cutout.Size;
            if (math.any(Math.ApproximatelyZero2(ref cutoutSize)))
            {
                // Don't need to check zero-sized cutouts
                return;
            }

            int quadsToCheck = processingData.SubQuads.Length;
            for (int i = 0; i < quadsToCheck; ++i)
            {
                InProgressQuad quad = processingData.SubQuads[i];
                if (!quad.Bounds.Overlap(ref cutout))
                {
                    continue;
                }

                // Remove the quad and replace it with the remainder    
                processingData.SubQuads.Swap(i, quadsToCheck - 1);
                processingData.SubQuads.RemoveAtSwapBack(quadsToCheck - 1);
                quadsToCheck -= 1;
                i -= 1;

                AddCutoutRemainders(ref quad, ref cutout);

                // Now add back in the original quad with soften disabled
                quad.Bounds = new RotationSpaceBounds(math.max(quad.Bounds.BL, cutout.BL), math.min(quad.Bounds.TR, cutout.TR));
                quad.EdgeSoftenDisabled = true;
                AddSubQuad(quad);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddCutoutRemainders(ref InProgressQuad quad, ref RotationSpaceBounds cutout)
        {
            bool4 hasRemainder = GetCutoutRemainders(ref quad.Bounds, ref cutout, out float4 remainders);

            InProgressQuad copy = quad;

            if (hasRemainder.x)
            {
                // Right
                copy.Bounds = new RotationSpaceBounds(new float2(cutout.TR.x, quad.Bounds.BL.y + remainders.w), new float2(quad.Bounds.TR.x, quad.Bounds.TR.y - remainders.y));
                AddSubQuad(copy);
            }

            if (hasRemainder.y)
            {
                // Top
                copy.Bounds = new RotationSpaceBounds(new float2(quad.Bounds.BL.x, cutout.TR.y), quad.Bounds.TR);
                AddSubQuad(copy);
            }

            if (hasRemainder.z)
            {
                // Left
                copy.Bounds = new RotationSpaceBounds(new float2(quad.Bounds.BL.x, quad.Bounds.BL.y + remainders.w), new float2(cutout.BL.x, quad.Bounds.TR.y - remainders.y));
                AddSubQuad(copy);
            }

            if (hasRemainder.w)
            {
                // Bottom
                copy.Bounds = new RotationSpaceBounds(quad.Bounds.BL, new float2(quad.Bounds.TR.x, cutout.BL.y));
                AddSubQuad(copy);
            }
        }

        /// <summary>
        /// Makes sure we don't add any zero-size quads
        /// </summary>
        /// <param name="quad"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddSubQuad(InProgressQuad quad)
        {
            float2 size = quad.Bounds.Size;
            if (math.any(Math.ApproximatelyZero2(ref size)))
            {
                return;
            }

            processingData.SubQuads.Add(quad);
        }

        /// <summary>
        /// R, T, L, B
        /// </summary>
        /// <param name="quad"></param>
        /// <param name="cutout"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool4 GetCutoutRemainders(ref RotationSpaceBounds quad, ref RotationSpaceBounds cutout, out float4 remainders)
        {
            remainders = default;
            remainders.xy = quad.TR - cutout.TR;
            remainders.zw = cutout.BL - quad.BL;
            remainders = math.max(remainders, float4.zero);
            return !Math.ApproximatelyZero4(ref remainders);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private OcclusionType GetOcclusionType(ref QuadBoundsDescriptor quadBoundsDescriptor)
        {
            if (quadBoundsDescriptor.BodyOccludes)
            {
                return quadBoundsDescriptor.BorderOccludes ? OcclusionType.BodyAndBorder : OcclusionType.BodyOnly;
            }
            else if (quadBoundsDescriptor.BorderOccludes)
            {
                return OcclusionType.BorderOnly;
            }
            else
            {
                return OcclusionType.None;
            }
        }

        private enum OcclusionType
        {
            None,
            BodyOnly,
            BorderOnly,
            BodyAndBorder,
        }
    }
}

