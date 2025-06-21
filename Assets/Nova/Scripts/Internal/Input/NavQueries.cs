// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Input
{
    [BurstCompile]
    internal struct FirstNavigableDescendant
    {
        [ReadOnly]
        public NovaHashMap<DataStoreID, bool> NavNodes;
        public NativeList<DataStoreIndex> Descendants;

        public NativeList<HierarchyElement> Hierarchy;
        public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;

        public NativeList<Length3.Calculated> LengthProperties;

        [ReadOnly]
        public NativeList<float4x4> WorldToLocalMatrices;
        [ReadOnly]
        public NativeList<float4x4> LocalToWorldMatrices;

        public VisualModifiers VisualModifiers;

        public DataStoreIndex RootIndex;

        public DataStoreID OUT_NavigableDescendantID;

        private unsafe void Execute()
        {
            OUT_NavigableDescendantID = DataStoreID.Invalid;

            Descendants.Clear();

            if (!RootIndex.IsValid)
            {
                return;
            }

            Descendants.AddRange(ref Hierarchy.ElementAt(RootIndex).Children);

            for (int i = 0; i < Descendants.Length; ++i)
            {
                DataStoreIndex descendantIndex = Descendants.ElementAt(i);
                ref HierarchyElement descendant = ref Hierarchy.ElementAt(descendantIndex);

                if (NavNodes.ContainsKey(descendant.ID) && VisibleWithinMask(descendantIndex))
                {
                    OUT_NavigableDescendantID = descendant.ID;
                    break;
                }

                Descendants.InsertRange(i + 1, descendant.Children.Ptr, descendant.ChildCount);
            }
        }

        private bool VisibleWithinMask(DataStoreIndex descendantIndex)
        {
            VisualModifierID baseModifier = VisualModifiers.VisualModifierIDs[descendantIndex];

            if (!baseModifier.IsValid)
            {
                return true;
            }

            VisualModifierShaderData shaderData = VisualModifiers.ShaderData[baseModifier];

            for (int i = 0; i < shaderData.Count; ++i)
            {
                VisualModifierID modifierID = shaderData.ModifierIDs[i];

                if (!VisualModifiers.ClipInfo[modifierID].Clip)
                {
                    continue;
                }

                DataStoreIndex modifierIndex = HierarchyLookup[VisualModifiers.ModifierToBlockID[modifierID]];

                float4x4 modifierWorldToLocal = WorldToLocalMatrices[modifierIndex];
                float4x4 descendantLocalToWorld = LocalToWorldMatrices[descendantIndex];

                float3 descendantExtents = LayoutAccess.Get(descendantIndex, ref LengthProperties).Size.Value * Math.float3_Half;
                float2 modifierExtents = LayoutAccess.Get(modifierIndex, ref LengthProperties).Size.Value.xy * Math.float2_Half;

                float2 minInModifierSpace = Math.Transform(ref modifierWorldToLocal, ref descendantLocalToWorld, -descendantExtents).xy;
                float2 maxInModifierSpace = Math.Transform(ref modifierWorldToLocal, ref descendantLocalToWorld, descendantExtents).xy;

                if (!RayToBounds.WithinBounds(minInModifierSpace, modifierExtents) || !RayToBounds.WithinBounds(maxInModifierSpace, modifierExtents))
                {
                    return false;
                }
            }

            return true;
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void Run(void* jobData)
        {
            UnsafeUtility.AsRef<FirstNavigableDescendant>(jobData).Execute();
        }
    }

    [BurstCompile]
    internal struct AncestorScopeQuery
    {
        [ReadOnly]
        public NovaHashMap<DataStoreID, bool> NavScopes;
        [ReadOnly]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;

        public NativeList<HierarchyElement> Hierarchy;
        public NovaHashMap<DataStoreID, DataStoreIndex> HierarchyLookup;
        public int LayerMask;
        public DataStoreIndex DescendantIndex;
        public DataStoreID RootID;

        public NativeList<DataStoreID> Scopes;

        private void Execute()
        {
            Scopes.Clear();

            if (!DescendantIndex.IsValid)
            {
                return;
            }

            HierarchyElement descendant = Hierarchy.ElementAt(DescendantIndex);

            if (RootID == descendant.ID)
            {
                return;
            }

            while (descendant.ParentID.IsValid && descendant.ParentID != RootID)
            {
                DataStoreIndex descendantIndex = HierarchyLookup[descendant.ParentID];
                descendant = Hierarchy.ElementAt(descendantIndex);

                if (NavScopes.TryGetValue(descendant.ID, out bool autoScope))
                {
                    int objectLayer = BaseInfos[descendantIndex].Val.GameObjectLayer;

                    if (((1 << objectLayer) & LayerMask) == 0)
                    {
                        continue;
                    }

                    Scopes.Add(descendant.ID);
                }
            }
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstMethod))]
        public static unsafe void Run(void* jobData)
        {
            UnsafeUtility.AsRef<AncestorScopeQuery>(jobData).Execute();
        }
    }
}
