// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal struct SortGroupHierarchyInfo
    {
        public DataStoreID HierarchyRoot;
        /// <summary>
        /// The order within the hierarchy root of this batch group
        /// </summary>
        public int Order;
    }

    /// <summary>
    /// Sorts sort groups hierarchically, updates visual modifier shader data, and assigns materials
    /// </summary>
    [BurstCompile]
    internal struct RenderingFinalizeJob : INovaJob
    {
        public int CurrentMaterialCount;
        public int CurrentShaderCount;

        [ReadOnly]
        public NativeList<DataStoreID> HierarchyRootIDs;
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatchRoots;
        [ReadOnly]
        public NativeList<HierarchyElement> Hierarchy;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<DataStoreID>> ContainedSortGroups;
        [NativeDisableContainerSafetyRestriction]
        public NovaHashMap<DataStoreID, DrawCallSummary> DrawCallSummaries;
        [ReadOnly]
        public NativeList<VisualModifierID, ClipMaskInfo> VisualModifierData;
        [ReadOnly]
        public NativeList<float4x4> LocalFromWorldMatrices;
        [ReadOnly]
        public NativeList<float4x4> WorldFromLocalMatrices;
        [ReadOnly]
        public NativeList<VisualModifierID, DataStoreID> ModifierToBlockID;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [ReadOnly]
        public NativeList<Length3.Calculated> LayoutProperties;
        [ReadOnly]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        [ReadOnly]
        public NativeList<RenderIndex, UIBlock2DData> UIBlock2DData;
        [ReadOnly]
        public NovaHashMap<DataStoreID, SortGroupInfo> SortGroupInfos;
        [ReadOnly]
        public NovaHashMap<MaterialDescriptor, MaterialCacheIndex> CachedMaterials;
        [ReadOnly]
        public NovaHashMap<ShaderDescriptor, ShaderCacheIndex> CachedShaders;
        [ReadOnly]
        public NovaHashMap<DataStoreID, int> ScreenSpaceCameraTargets;
        [ReadOnly]
        public NativeList<DataStoreIndex, VisualModifierID> VisualModifierIDs;
        [ReadOnly]
        public NativeList<BatchGroupElement> BatchGroupElements;
        [ReadOnly]
        public NovaHashMap<DataStoreID, NovaList<VisualModifierID>> ContainedVisualModifers;

        public NovaHashMap<DataStoreID, SortGroupHierarchyInfo> SortGroupHierarchyInfo;
        public NativeList<DataStoreID> ProcessingQueue;
        public NativeList<VisualModifierID, VisualModifierRenderData> VisualModifierRenderData;
        public NativeList<VisualModifierID, VisualModifierShaderData> VisualModifierShaderData;
        public NativeList<VisualModifierID> UpdatedVisualModifiers;
        public NovaHashMap<DataStoreID, NovaList<DrawCallDescriptorID, MaterialCacheIndex>> MaterialAssignments;
        public NativeList<ValuePair<ShaderCacheIndex, MaterialDescriptor>> MaterialsToAdd;
        public NativeList<ShaderDescriptor> ShadersToAdd;
        public NovaHashMap<DataStoreID, SortGroupInfo> ProcessedSortGroupInfos;
        public NativeList<VisualModifierID, VisualModifierID> ParentVisualModifier;
        public NovaHashMap<DataStoreID, VisualModifierShaderData> RootVisualModifierOverride;

        private MaterialCacheIndex NextMaterialIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CurrentMaterialCount + MaterialsToAdd.Length;
        }

        private ShaderCacheIndex NextShaderIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CurrentShaderCount + ShadersToAdd.Length;
        }

        public void Execute()
        {
            SortSortGroups();
            UpdateVisualModifierShaderData();
            UpdateMaterials();
        }

        private void UpdateMaterials()
        {
            for (int i = 0; i < DirtyBatchRoots.Length; i++)
            {
                AssignMaterials(DirtyBatchRoots[i]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignMaterials(DataStoreID batchRootID)
        {
            DrawCallSummary drawCallSummary = DrawCallSummaries[batchRootID];
            NovaList<DrawCallDescriptorID, MaterialCacheIndex> materialAssignments = MaterialAssignments.GetAndClear(batchRootID);

            for (int i = 0; i < drawCallSummary.DrawCallDescriptors.Length; i++)
            {
                materialAssignments.Add(GetMaterialCacheIndex(batchRootID, ref drawCallSummary.DrawCallDescriptors.ElementAt(i)));
            }

            MaterialAssignments[batchRootID] = materialAssignments;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MaterialCacheIndex GetMaterialCacheIndex(DataStoreID batchRootID, ref DrawCallDescriptor drawCallDescriptor)
        {
            MaterialDescriptor materialDescriptor = new MaterialDescriptor()
            {
                MaterialModifiers = drawCallDescriptor.MaterialModifiers,
                ShaderDescriptor = new ShaderDescriptor()
                {
                    LightingModel = drawCallDescriptor.Surface.Model,
                    PassType = drawCallDescriptor.PassType,
                    VisualType = drawCallDescriptor.DrawCallType,
                },
            };

            bool hasSortGroup = ProcessedSortGroupInfos.TryGetValue(batchRootID, out SortGroupInfo sortGroupInfo);

            if (hasSortGroup && sortGroupInfo.RenderOverOpaqueGeometry)
            {
                materialDescriptor.DisableZTest = true;
            }

            if (drawCallDescriptor.PassType == PassType.Transparent)
            {
                materialDescriptor.RenderQueue = hasSortGroup ? sortGroupInfo.RenderQueue : Constants.DefaultTransparentRenderQueue;
            }
            else
            {
                materialDescriptor.RenderQueue = Constants.DefaultOpaqueRenderQueue;
            }

            if (drawCallDescriptor.HasVisualModifier)
            {
                VisualModifierShaderData shaderData = VisualModifierShaderData[drawCallDescriptor.VisualModifierID];
                materialDescriptor.MaterialModifiers |= shaderData.ClipMaskIndex != -1 ? MaterialModifier.ClipMask : MaterialModifier.ClipRect;
            }
            else
            {
                VisualModifierShaderData overrideModifierData = RootVisualModifierOverride[batchRootID];
                if (overrideModifierData.Count > 0)
                {
                    materialDescriptor.MaterialModifiers |= overrideModifierData.ClipMaskIndex != -1 ? MaterialModifier.ClipMask : MaterialModifier.ClipRect;
                }
            }

            if (materialDescriptor.ShaderDescriptor.IsText)
            {
                materialDescriptor.TextMaterialID = drawCallDescriptor.Text.MaterialID;
            }
            else
            {
                materialDescriptor.TextMaterialID = 0;
            }

            if (CachedMaterials.TryGetValue(materialDescriptor, out MaterialCacheIndex toRet))
            {
                return toRet;
            }

            // Material isn't cached
            if (TryGetMaterialToAdd(ref materialDescriptor, out int index))
            {
                // But something else has requested it
                return CurrentMaterialCount + index;
            }

            toRet = NextMaterialIndex;
            MaterialsToAdd.Add(new ValuePair<ShaderCacheIndex, MaterialDescriptor>(GetShaderIndex(ref materialDescriptor), materialDescriptor));
            return toRet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetMaterialToAdd(ref MaterialDescriptor materialDescriptor, out int index)
        {
            for (int i = 0; i < MaterialsToAdd.Length; i++)
            {
                if (!MaterialsToAdd[i].Item2.Equals(materialDescriptor))
                {
                    continue;
                }

                index = i;
                return true;
            }

            index = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ShaderCacheIndex GetShaderIndex(ref MaterialDescriptor materialDescriptor)
        {
            if (CachedShaders.TryGetValue(materialDescriptor.ShaderDescriptor, out ShaderCacheIndex toRet))
            {
                return toRet;
            }

            // Not cached
            if (ShadersToAdd.TryGetIndexOf(materialDescriptor.ShaderDescriptor, out int index))
            {
                // Something else has already requested to add it
                return CurrentShaderCount + index;
            }

            // Not cached and nothing else has requested it yet
            toRet = NextShaderIndex;
            ShadersToAdd.Add(materialDescriptor.ShaderDescriptor);
            return toRet;
        }

        /// <summary>
        /// Gets the lowest visual modifier that contains the batch root, if there is one, otherwise invalid
        /// </summary>
        private VisualModifierID GetContainingModifier(DataStoreID batchRootID)
        {
            DataStoreIndex rootIndex = DataStoreIDToDataStoreIndex[batchRootID];

            HierarchyElement rootHierarchyElement = Hierarchy[rootIndex];

            if (!rootHierarchyElement.ParentID.IsValid ||
                !DataStoreIDToDataStoreIndex.TryGetValue(rootHierarchyElement.ParentID, out DataStoreIndex parentIndex))
            {
                // This is a hierarchy root
                return VisualModifierID.Invalid;
            }

            // This batch root has a valid parent, so use its modifier if it exists
            VisualModifierID containingModifierID = VisualModifierIDs[parentIndex];
            if (containingModifierID.IsValid)
            {
                return containingModifierID;
            }

            // The parent of this root didn't have a visual modifier, check if there is another root up the chain
            return GetContainingModifier(BatchGroupElements[parentIndex].BatchRootID);
        }

        /// <summary>
        /// Updates the shader data for all visual modifiers, including handling nesting
        /// </summary>
        private void UpdateVisualModifierShaderData()
        {
            UpdatedVisualModifiers.Clear();

            // Set the data for each individual visual modifier
            for (int i = 0; i < DirtyBatchRoots.Length; ++i)
            {
                DataStoreID batchRootID = DirtyBatchRoots[i];

                // Check if this batch root is contained in another visual modifier that is outside of the batch root
                VisualModifierID containingModifierID = GetContainingModifier(batchRootID);
                if (containingModifierID.IsValid)
                {
                    // Mark this batch root as having an override
                    VisualModifierShaderData overrideData = default;
                    VisualModifierRenderData dummyData = default;
                    overrideData.Append(containingModifierID, ref dummyData);
                    RootVisualModifierOverride[batchRootID] = overrideData;
                }
                else
                {
                    // Mark this batch root as not having an override
                    RootVisualModifierOverride[batchRootID] = default;
                }

                // Update the rendering data for all contained visual modifiers
                NovaList<VisualModifierID> containedModifiers = ContainedVisualModifers[batchRootID];
                for (int j = 0; j < containedModifiers.Length; ++j)
                {
                    VisualModifierID visualModifierID = containedModifiers[j];
                    UpdateVisualModifierRenderData(batchRootID, visualModifierID);
                    UpdatedVisualModifiers.Add(visualModifierID);

                    // If the parent visual modifier is currently invalid, but there is a containing modifier,
                    // set that as the parent. If the parent visual modifier *is* currently valid, then that means
                    // that this visual modifier is already nested within another modifier
                    if (!ParentVisualModifier[visualModifierID].IsValid && containingModifierID.IsValid)
                    {
                        ParentVisualModifier[visualModifierID] = containingModifierID;
                    }
                }
            }

            // Fill in the shader data for the visual modifiers, based on how they are nested within other clip masks
            // NOTE: At this point, ParentVisualModifier *will* be valid, even across batchgroup boundaries
            for (int j = 0; j < UpdatedVisualModifiers.Length; ++j)
            {
                VisualModifierID visualModifierID = UpdatedVisualModifiers[j];
                VisualModifierRenderData renderData = VisualModifierRenderData[visualModifierID];

                ref VisualModifierShaderData shaderData = ref VisualModifierShaderData.ElementAt(visualModifierID);
                shaderData.Clear();

                DataStoreID modifierDataStoreID = ModifierToBlockID[visualModifierID];
                DataStoreIndex visualModifierIndex = DataStoreIDToDataStoreIndex[modifierDataStoreID];
                BatchGroupElement rootBatchGroupElement = BatchGroupElements[visualModifierIndex];
                DataStoreIndex rootIndex = DataStoreIDToDataStoreIndex[rootBatchGroupElement.BatchRootID];

                while (true)
                {
                    shaderData.Append(visualModifierID, ref renderData);

                    visualModifierID = ParentVisualModifier[visualModifierID];
                    if (!visualModifierID.IsValid)
                    {
                        // End of the clipmask nesting
                        break;
                    }

                    renderData = VisualModifierRenderData[visualModifierID];

                    // Adjust the matrix if they belong to different batch groups
                    DataStoreID parentModifierID = ModifierToBlockID[visualModifierID];
                    DataStoreIndex parentModifierIndex = DataStoreIDToDataStoreIndex[parentModifierID];
                    BatchGroupElement parentRootBatchGroupElement = BatchGroupElements[parentModifierIndex];

                    if (rootBatchGroupElement.BatchRootID == parentRootBatchGroupElement.BatchRootID)
                    {
                        // They belong to the same batch root, nothing to do
                        continue;
                    }

                    DataStoreIndex parentRootIndex = DataStoreIDToDataStoreIndex[parentRootBatchGroupElement.BatchRootID];

                    // They belong to different batch roots, so we need to add the extra transformation to the matrix
                    float4x4 parentRootFromRoot = math.mul(LocalFromWorldMatrices[parentRootIndex], WorldFromLocalMatrices[rootIndex]);
                    renderData.ModifierFromRoot = math.mul(renderData.ModifierFromRoot, parentRootFromRoot);
                }
            }

            // Go through all of the batch roots, and if they have a root override, fill in the info
            for (int i = 0; i < DirtyBatchRoots.Length; ++i)
            {
                DataStoreID batchRootID = DirtyBatchRoots[i];
                VisualModifierShaderData overrideModifierShaderData = RootVisualModifierOverride[batchRootID];

                if (overrideModifierShaderData.Count == 0)
                {
                    // Doesn't have an override
                    continue;
                }

                DataStoreIndex rootIndex = DataStoreIDToDataStoreIndex[batchRootID];
                float4x4 worldFromRoot = WorldFromLocalMatrices[rootIndex];

                // The data for the override modifier was filled with dummy data, so first fill in the data properly
                VisualModifierID visualModifierID = overrideModifierShaderData.ModifierIDs[0];
                VisualModifierRenderData renderData = VisualModifierRenderData[visualModifierID];

                DataStoreID parentModifierID = ModifierToBlockID[visualModifierID];
                DataStoreIndex parentModifierIndex = DataStoreIDToDataStoreIndex[parentModifierID];
                BatchGroupElement parentRootBatchGroupElement = BatchGroupElements[parentModifierIndex];
                DataStoreIndex parentRootIndex = DataStoreIDToDataStoreIndex[parentRootBatchGroupElement.BatchRootID];
                float4x4 parentRootFromRoot = math.mul(LocalFromWorldMatrices[parentRootIndex], worldFromRoot);
                renderData.ModifierFromRoot = math.mul(renderData.ModifierFromRoot, parentRootFromRoot);

                overrideModifierShaderData.Set(0, visualModifierID, ref renderData);

                // Continue up the chain
                visualModifierID = ParentVisualModifier[visualModifierID];
                while (visualModifierID.IsValid)
                {
                    renderData = VisualModifierRenderData[visualModifierID];

                    parentModifierID = ModifierToBlockID[visualModifierID];
                    parentModifierIndex = DataStoreIDToDataStoreIndex[parentModifierID];
                    parentRootBatchGroupElement = BatchGroupElements[parentModifierIndex];
                    parentRootIndex = DataStoreIDToDataStoreIndex[parentRootBatchGroupElement.BatchRootID];
                    parentRootFromRoot = math.mul(LocalFromWorldMatrices[parentRootIndex], worldFromRoot);
                    renderData.ModifierFromRoot = math.mul(renderData.ModifierFromRoot, parentRootFromRoot);

                    overrideModifierShaderData.Append(visualModifierID, ref renderData);

                    visualModifierID = ParentVisualModifier[visualModifierID];
                }

                RootVisualModifierOverride[batchRootID] = overrideModifierShaderData;
            }
        }

        /// <summary>
        /// Updates the render data for a single visual modifier
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateVisualModifierRenderData(DataStoreID batchRootID, VisualModifierID id)
        {
            ClipMaskInfo info = VisualModifierData[id];
            VisualModifierRenderData renderData = default;

            renderData.ColorModifier = (Vector4)info.Color;
            renderData.IsMask = info.IsClipMask;

            DataStoreID dataStoreID = ModifierToBlockID[id];
            DataStoreIndex dataStoreIndex = DataStoreIDToDataStoreIndex[dataStoreID];
            float4x4 worldFromRoot = WorldFromLocalMatrices[DataStoreIDToDataStoreIndex[batchRootID]];
            float4x4 modifierFromWorld = LocalFromWorldMatrices[dataStoreIndex];
            renderData.ModifierFromRoot = math.mul(modifierFromWorld, worldFromRoot);

            if (info.Clip)
            {
                // It clips, so properly fill out the size
                LayoutAccess.Calculated layoutProperties = LayoutAccess.Get(dataStoreIndex, ref LayoutProperties);
                float3 size = layoutProperties.Size.Value;
                float3 halfSize = Math.float3_Half * size;

                float radius = 0f;
                var baseInfo = BaseInfos[dataStoreIndex];
                if (baseInfo.Val.BlockType == BlockType.UIBlock2D)
                {
                    float minHalfNodeSize = math.cmin(halfSize.xy);
                    UIBlock2DData data = UIBlock2DData[baseInfo.RenderIndex];
                    switch (data.CornerRadius.Type)
                    {
                        case LengthType.Value:
                            radius = math.clamp(data.CornerRadius.Raw, 0, minHalfNodeSize);
                            break;
                        case LengthType.Percent:
                            radius = data.CornerRadius.Raw * minHalfNodeSize;
                            break;
                    }
                }

                float maxHalfDim = math.cmax(halfSize.xy);
                float nFactor = maxHalfDim > Math.Epsilon ? 1.0f / maxHalfDim : 0f;
                float2 nHalfSize = halfSize.xy * nFactor;
                float nRadius = radius * nFactor;
                renderData.ClipRectInfo = new Vector4(nHalfSize.x, nHalfSize.y, nFactor, nRadius);
            }
            else
            {
                // Doesn't clip, so just make the size very large
                renderData.ClipRectInfo = new Vector4(1f, 1f, 0f, 0f);
            }

            VisualModifierRenderData[id] = renderData;
        }

        private void SortSortGroups()
        {
            SortGroupHierarchyInfo.Clear();
            ProcessedSortGroupInfos.Clear();

            for (int i = 0; i < HierarchyRootIDs.Length; i++)
            {
                DataStoreID hierarchyRoot = HierarchyRootIDs[i];
                ProcessRoot(ref hierarchyRoot);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessRoot(ref DataStoreID hierarchyRoot)
        {
            ProcessingQueue.Clear();
            ProcessingQueue.Add(hierarchyRoot);

            int currentCount = 0;
            while (ProcessingQueue.TryPopBack(out DataStoreID sortGroupRoot))
            {
                SortGroupHierarchyInfo.Add(sortGroupRoot, new SortGroupHierarchyInfo()
                {
                    HierarchyRoot = hierarchyRoot,
                    Order = currentCount++,
                });

                NovaList<DataStoreID> contained = ContainedSortGroups[sortGroupRoot];
                ProcessingQueue.AddRangeReverse(ref contained);

                // Now process the sort group infos, inheriting the hierarchy root's if it is a 
                // screen space root
                if (!SortGroupInfos.TryGetValue(sortGroupRoot, out SortGroupInfo sortGroupInfo))
                {
                    continue;
                }

                if (ScreenSpaceCameraTargets.ContainsKey(hierarchyRoot) &&
                    SortGroupInfos.TryGetValue(hierarchyRoot, out SortGroupInfo hierarchyRootInfo))
                {
                    sortGroupInfo.RenderQueue = hierarchyRootInfo.RenderQueue;
                    sortGroupInfo.RenderOverOpaqueGeometry = hierarchyRootInfo.RenderOverOpaqueGeometry;
                }

                ProcessedSortGroupInfos[sortGroupRoot] = sortGroupInfo;
            }
        }
    }
}

