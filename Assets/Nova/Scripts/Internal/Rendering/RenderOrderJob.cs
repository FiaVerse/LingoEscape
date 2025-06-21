// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    internal struct RenderHierarchyElement
    {
        public DataStoreIndex DataStoreIndex;
        public DataStoreIndex ParentIndex;

        public RenderHierarchyElement(DataStoreIndex index, DataStoreIndex parent)
        {
            DataStoreIndex = index;
            ParentIndex = parent;
        }
    }

    /// <summary>
    /// Sorts the elements of each batch group by render order
    /// </summary>
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    internal struct RenderOrderJob : INovaJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> BatchesToProcess;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<HierarchyElement> Hierarchy;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<BatchGroupElement> BatchGroupElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [NativeDisableParallelForRestriction]
        public NativeList<RenderIndex, UIBlock2DData> UIBlock2DData;
        [ReadOnly]
        public NativeList<RenderIndex, UIBlock3DData> UIBlock3DData;
        [ReadOnly]
        public TexturePackDataProvider PackDataProvider;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<RenderIndex, TextBlockData> TextNodeData;
        [ReadOnly]
        public NativeList<Length3.Calculated> LayoutProperties;
        [ReadOnly]
        public NativeList<DataStoreIndex, Surface> SurfaceData;
        [NativeDisableParallelForRestriction]
        public NativeList<DataStoreIndex, VisualModifierID> VisualModifierIDs;
        [ReadOnly]
        public NovaHashMap<DataStoreID, CoplanarSetID> CoplanarSetRoots;
        [ReadOnly]
        public NovaHashMap<DataStoreID, RotationSetID> RotationSetRoots;
        [ReadOnly]
        public ImageDataProvider ImageDataProvider;
        [ReadOnly]
        public NovaHashMap<DataStoreID, byte> HiddenElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, VisualModifierID> BlockToVisualModifierID;

        [NativeDisableParallelForRestriction]
        public NovaHashMap<VisualModifierID, AABB> VisualModifierClipBounds;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, BatchZLayers> ZLayers;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, ZLayerCounts> ZLayerCounts;
        [NativeDisableParallelForRestriction]
        public NativeList<DataStoreIndex, int> OrderInZLayer;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, NovaList<RenderHierarchyElement>> SortingProcessQueues;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, DrawCallSummary> DrawCallSummaries;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, NovaList<VisualElementIndex, VisualElement>> VisualElements;
        [NativeDisableParallelForRestriction]
        public NativeList<DataStoreIndex, CoplanarSetID> CoplanarSetIDs;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, RotationSetSummary> RotationSets;
        [NativeDisableParallelForRestriction]
        public NativeList<DataStoreIndex, RotationSetID> RotationSetIDs;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, NovaList<DataStoreID>> ContainedSortGroups;
        [NativeDisableParallelForRestriction]
        public NativeList<VisualModifierID, VisualModifierID> ParentVisualModifier;
        [NativeDisableParallelForRestriction]
        public NovaHashMap<DataStoreID, NovaList<VisualModifierID>> ContainedVisualModifers;

        private DataStoreID batchRootID;
        private DrawCallSummary drawCallSummary;
        private BatchZLayers zLayers;
        private NovaList<VisualElementIndex, VisualElement> visualElements;
        private RotationSetSummary rotationSetSummary;
        private NovaList<VisualModifierID> containedVisualModifers;

        private VisualElementIndex CurrentVisualElementIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => visualElements.Length;
        }

        public void Execute(int index)
        {
            batchRootID = BatchesToProcess[index];

            NovaList<RenderHierarchyElement> processQueue = SortingProcessQueues.GetAndClear(batchRootID);

            zLayers = ZLayers.GetAndClear(batchRootID);
            visualElements = VisualElements.GetAndClear(batchRootID);
            drawCallSummary = DrawCallSummaries.GetAndClear(batchRootID);
            containedVisualModifers = ContainedVisualModifers.GetAndClear(batchRootID);

            rotationSetSummary = RotationSets[batchRootID];
            ZLayerCounts zLayerCounts = ZLayerCounts.GetAndClear(batchRootID);

            NovaList<DataStoreID> containedSortGroups = ContainedSortGroups.GetAndClear(batchRootID);

            // First, get all of the nodes order in their respective layers
            DataStoreIndex batchRootDataStoreIndex = DataStoreIDToDataStoreIndex[batchRootID];
            processQueue.Add(new RenderHierarchyElement(batchRootDataStoreIndex, DataStoreIndex.Invalid));
            while (processQueue.TryPopBack(out RenderHierarchyElement currentElement))
            {
                ref HierarchyElement hierarchyElement = ref Hierarchy.ElementAt(currentElement.DataStoreIndex);
                ref BatchGroupElement batchGroupElement = ref BatchGroupElements.ElementAt(currentElement.DataStoreIndex);

                if (batchGroupElement.BatchRootID != batchRootID)
                {
                    // Belongs to a different batch root
                    // Mark the child sort group as contained
                    containedSortGroups.Add(batchGroupElement.BatchRootID);
                    continue;
                }

                ref RenderElement<BaseRenderInfo> baseInfo = ref BaseInfos.ElementAt(currentElement.DataStoreIndex);
                OrderInZLayer[currentElement.DataStoreIndex] = zLayerCounts.IncrementCount(baseInfo.Val.ZIndex);

                VisualModifierID visualModifierID = SetVisualModifier(ref currentElement.DataStoreIndex, ref hierarchyElement);
                SetCoplanarSetRoot(hierarchyElement.ID, ref currentElement);
                SetRotationSetRoot(hierarchyElement.ID, ref currentElement);

                // Add the children
                AddChildren(ref processQueue, ref currentElement.DataStoreIndex, ref hierarchyElement);

                if (!baseInfo.Val.Visible || (NovaApplication.ConstIsEditor && HiddenElements.ContainsKey(hierarchyElement.ID)))
                {
                    continue;
                }

                switch (baseInfo.Val.BlockType)
                {
                    case BlockType.UIBlock2D:
                        AddUIBlock2D(ref currentElement.DataStoreIndex, ref visualModifierID, ref baseInfo);
                        break;
                    case BlockType.Text:
                        AddTextBlock(ref currentElement.DataStoreIndex, ref visualModifierID, ref baseInfo);
                        break;
                    case BlockType.UIBlock3D:
                        AddUIBlock3D(ref currentElement.DataStoreIndex, ref visualModifierID, ref baseInfo);
                        break;
                }
            }

            ContainedSortGroups[batchRootID] = containedSortGroups;
            zLayers.SortLayers();
            zLayerCounts.SortLayers();
            ZLayers[batchRootID] = zLayers;
            ZLayerCounts[batchRootID] = zLayerCounts;
            VisualElements[batchRootID] = visualElements;
            DrawCallSummaries[batchRootID] = drawCallSummary;
            SortingProcessQueues[batchRootID] = processQueue;
            rotationSetSummary.ResizeDescriptorArray();
            RotationSets[batchRootID] = rotationSetSummary;
            ContainedVisualModifers[batchRootID] = containedVisualModifers;
        }

        private void SetRotationSetRoot(DataStoreID dataStoreID, ref RenderHierarchyElement hierarchyElement)
        {
            if (RotationSetRoots.TryGetValue(dataStoreID, out RotationSetID rootID))
            {
                // Element is a coplanar set root
                RotationSetIDs[hierarchyElement.DataStoreIndex] = rootID;
                return;
            }
            else
            {
                // Use parent's coplanar set
                RotationSetIDs[hierarchyElement.DataStoreIndex] = RotationSetIDs[hierarchyElement.ParentIndex];
            }
        }

        private void SetCoplanarSetRoot(DataStoreID dataStoreID, ref RenderHierarchyElement hierarchyElement)
        {
            if (CoplanarSetRoots.TryGetValue(dataStoreID, out CoplanarSetID rootID))
            {
                // Element is a coplanar set root
                CoplanarSetIDs[hierarchyElement.DataStoreIndex] = rootID;
                return;
            }
            else
            {
                // Use parent's coplanar set
                CoplanarSetIDs[hierarchyElement.DataStoreIndex] = CoplanarSetIDs[hierarchyElement.ParentIndex];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddChildren(ref NovaList<RenderHierarchyElement> processQueue, ref DataStoreIndex dataStoreIndex, ref HierarchyElement hierarchyElement)
        {
            for (int i = hierarchyElement.Children.Length - 1; i >= 0; --i)
            {
                processQueue.Add(new RenderHierarchyElement(hierarchyElement.Children[i], dataStoreIndex));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VisualModifierID SetVisualModifier(ref DataStoreIndex dataStoreIndex, ref HierarchyElement hierarchyElement)
        {
            DataStoreIndex parentIndex = DataStoreIndex.Invalid;
            bool hasValidParentInRoot = hierarchyElement.ParentID.IsValid &&
                hierarchyElement.ID != batchRootID &&
                DataStoreIDToDataStoreIndex.TryGetValue(hierarchyElement.ParentID, out parentIndex);

            ref VisualModifierID modifierID = ref VisualModifierIDs.ElementAt(dataStoreIndex);
            if (BlockToVisualModifierID.TryGetValue(hierarchyElement.ID, out VisualModifierID visualModifierID))
            {
                // Element itself is a visual modifier
                modifierID = visualModifierID;
                containedVisualModifers.Add(modifierID);

                // Register the parents visual modifier as the parent of this one
                ParentVisualModifier[visualModifierID] = hasValidParentInRoot ? VisualModifierIDs[parentIndex] : VisualModifierID.Invalid;

                LayoutAccess.Calculated layoutProperties = LayoutAccess.Get(dataStoreIndex, ref LayoutProperties);
                float3 size = layoutProperties.Size.Value;
                float3 halfSize = Math.float3_Half * size;

                // Set the bounds if we are a clip type
                if (VisualModifierClipBounds.ContainsKey(visualModifierID))
                {
                    // Dont clamp z
                    halfSize.z = float.MaxValue;
                    VisualModifierClipBounds[modifierID] = new AABB(-halfSize, halfSize);
                }
            }
            else if (hasValidParentInRoot)
            {
                // Use parents modifier
                modifierID = VisualModifierIDs[parentIndex];
            }
            else
            {
                // Root
                modifierID = VisualModifierID.Invalid;
            }

            return modifierID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddUIBlock3D(ref DataStoreIndex dataStoreIndex, ref VisualModifierID visualModifierID, ref RenderElement<BaseRenderInfo> baseInfo)
        {
            DrawCallDescriptor drawCallDescriptor = DrawCallDescriptor.Default;
            drawCallDescriptor.DrawCallType = VisualType.UIBlock3D;
            drawCallDescriptor.Surface = SurfaceData[dataStoreIndex];
            drawCallDescriptor.VisualModifierID = visualModifierID;
            drawCallDescriptor.UIBlock3D.PassType = UIBlock3DData[baseInfo.RenderIndex].Color.ToPassType();

            AddElement(ref dataStoreIndex, ref baseInfo, ref drawCallDescriptor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddTextBlock(ref DataStoreIndex dataStoreIndex, ref VisualModifierID visualModifierID, ref RenderElement<BaseRenderInfo> baseInfo)
        {
            TextBlockData textNodeData = TextNodeData[baseInfo.RenderIndex];

            if (textNodeData.QuadCount == 0)
            {
                return;
            }

            DrawCallDescriptor drawCallDescriptor = DrawCallDescriptor.Default;
            drawCallDescriptor.DrawCallType = VisualType.TextBlock;
            drawCallDescriptor.Surface = SurfaceData[dataStoreIndex];
            drawCallDescriptor.VisualModifierID = visualModifierID;

            if (NovaSettings.Config.SuperSampleText)
            {
                drawCallDescriptor.MaterialModifiers |= MaterialModifier.SuperSample;
            }

            for (int i = 0; i < textNodeData.MeshData.Length; ++i)
            {
                ref TextBlockMeshData meshData = ref textNodeData.MeshData.ElementAt(i);
                if (meshData.CharacterCount == 0)
                {
                    continue;
                }

                drawCallDescriptor.DrawCallType = i > 0 ? VisualType.TextSubmesh : VisualType.TextBlock;

                drawCallDescriptor.Text.MaterialID = meshData.MaterialID;
                AddElement(ref dataStoreIndex, ref baseInfo, ref drawCallDescriptor);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddUIBlock2D(ref DataStoreIndex dataStoreIndex, ref VisualModifierID visualModifierID, ref RenderElement<BaseRenderInfo> baseInfo)
        {
            ref UIBlock2DData data = ref UIBlock2DData.ElementAt(baseInfo.RenderIndex);
            Surface surface = SurfaceData[dataStoreIndex];

            if (data.Shadow.HasOuterShadow)
            {
                DrawCallDescriptor shadowDescriptor = DrawCallDescriptor.Default;
                shadowDescriptor.DrawCallType = VisualType.DropShadow;
                shadowDescriptor.Surface = surface;
                shadowDescriptor.VisualModifierID = visualModifierID;

                if (data.RadialFill.EnabledAndNot360)
                {
                    shadowDescriptor.MaterialModifiers |= MaterialModifier.RadialFill;
                }

                AddElement(ref dataStoreIndex, ref baseInfo, ref shadowDescriptor);
            }

            if (!(data.FillEnabled || data.Shadow.HasInnerShadow || data.Border.Enabled))
            {
                return;
            }

            DrawCallDescriptor drawCallDescriptor = DrawCallDescriptor.Default;
            drawCallDescriptor.DrawCallType = VisualType.UIBlock2D;
            drawCallDescriptor.Surface = surface;
            drawCallDescriptor.VisualModifierID = visualModifierID;

            if (data.Shadow.HasInnerShadow)
            {
                drawCallDescriptor.MaterialModifiers |= MaterialModifier.InnerShadow;
            }

            if (data.RadialFill.EnabledAndNot360)
            {
                drawCallDescriptor.MaterialModifiers |= MaterialModifier.RadialFill;
            }

            if (data.Border.Enabled)
            {
                switch (data.Border.Direction)
                {
                    case BorderDirection.In:
                        drawCallDescriptor.MaterialModifiers |= MaterialModifier.InnerBorder;
                        break;
                    case BorderDirection.Center:
                        drawCallDescriptor.MaterialModifiers |= MaterialModifier.CenterBorder;
                        break;
                    case BorderDirection.Out:
                        drawCallDescriptor.MaterialModifiers |= MaterialModifier.OuterBorder;
                        break;
                }
            }

            if (data.FillEnabled && data.Image.HasImage &&
                ImageDataProvider.TryGetImageData(data.Image.ImageID, out ImageDescriptor imageDescriptor))
            {
                if (data.Image.Mode == ImagePackMode.Packed &&
                    PackDataProvider.TryGetPackID(imageDescriptor.TextureID, out TexturePackID texturePackID) &&
                    PackDataProvider.ShouldUsePack(texturePackID))
                {
                    drawCallDescriptor.MaterialModifiers |= MaterialModifier.StaticImage;
                    drawCallDescriptor.UIBlock2D.TexturePackID = texturePackID;
                }
                else if (ImageDataProvider.TryGetTextureID(data.Image.ImageID, out TextureID textureID))
                {
                    drawCallDescriptor.MaterialModifiers |= MaterialModifier.DynamicImage;
                    drawCallDescriptor.UIBlock2D.TextureID = textureID;
                }
            }

            rotationSetSummary.RegisterQuadProvider(RotationSetIDs[dataStoreIndex], CurrentVisualElementIndex);

            AddElement(ref dataStoreIndex, ref baseInfo, ref drawCallDescriptor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddElement(ref DataStoreIndex dataStoreIndex, ref RenderElement<BaseRenderInfo> baseInfo, ref DrawCallDescriptor drawCallDescriptor)
        {
            drawCallDescriptor.GameObjectLayer = baseInfo.Val.GameObjectLayer;
            DrawCallDescriptorID drawCallDescriptorID = drawCallSummary.EnsureDescriptor(ref drawCallDescriptor);
            visualElements.Add(new VisualElement(ref dataStoreIndex, ref baseInfo.RenderIndex, ref drawCallDescriptorID, drawCallDescriptor.DrawCallType));
            zLayers.AddElement(visualElements.Length - 1, baseInfo.Val.ZIndex);
        }
    }
}

