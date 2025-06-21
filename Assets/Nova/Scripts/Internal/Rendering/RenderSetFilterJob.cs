// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct RenderSetFilterJob : INovaJob
    {
        [ReadOnly]
        public NativeList<DataStoreIndex> PotentialCoplanarSetRoots;
        [ReadOnly]
        public NativeList<DataStoreIndex> PotentialRotationSetRoots;
        [ReadOnly]
        public NativeList<DataStoreID> AllBatchRootIDs;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<float4x4> LocalFromWorldMatrices;
        [NativeDisableParallelForRestriction]
        public NativeList<float4x4> WorldFromLocalMatrices;
        [ReadOnly]
        public NovaHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<BatchGroupElement> BatchGroupElements;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<HierarchyElement> Hierarchy;
        [ReadOnly]
        public NativeList<DataStoreID> DirtyRoots;

        public NovaHashMap<DataStoreID, CoplanarSetID> CoplanarSetRoots;
        public NovaHashMap<DataStoreID, RotationSetID> RotationSetRoots;
        public NovaHashMap<DataStoreID, NovaList<CoplanarSetID, CoplanarSet>> CoplanarSets;
        public NovaHashMap<DataStoreID, RotationSetSummary> RotationSets;

        public void Execute()
        {
            CoplanarSetRoots.Clear();
            RotationSetRoots.Clear();

            AddBatchRoots();
            ProcessPotentialCoplanarRoots();
            ProcessPotentialRotationRoots();
        }

        private void ProcessPotentialRotationRoots()
        {
            for (int i = 0; i < PotentialRotationSetRoots.Length; ++i)
            {
                DataStoreIndex dataStoreIndex = PotentialRotationSetRoots[i];
                DataStoreID dataStoreID = Hierarchy.ElementAt(dataStoreIndex).ID;

                if (RotationSetRoots.ContainsKey(dataStoreID))
                {
                    // Already a root
                    continue;
                }

                ref BatchGroupElement batchGroupElement = ref BatchGroupElements.ElementAt(dataStoreIndex);
                RotationSetSummary rotationSetSummary = RotationSets[batchGroupElement.BatchRootID];

                ref float4x4 localFromWorld = ref LocalFromWorldMatrices.ElementAt(dataStoreIndex);

                // Check if can be combined with any 
                bool assignedSet = false;
                for (int j = 0; j < rotationSetSummary.SetCount; ++j)
                {
                    ref RotationSet rotationSet = ref rotationSetSummary.Sets.ElementAt(j);

                    if (!Math.AreCoplanar(ref localFromWorld, ref rotationSet.WorldFromSet))
                    {
                        continue;
                    }

                    quaternion worldFromSetRotation = new quaternion(rotationSet.WorldFromSet);
                    quaternion localFromWorldRotation = new quaternion(localFromWorld);
                    quaternion combinedRotation = math.mul(localFromWorldRotation, worldFromSetRotation);

                    if (!Math.ApproximatelyIdentity(ref combinedRotation))
                    {
                        continue;
                    }

                    RotationSetRoots[dataStoreID] = j;
                    assignedSet = true;
                    break;
                }

                if (assignedSet)
                {
                    continue;
                }

                CreateNewRotationSet(ref rotationSetSummary, ref localFromWorld, ref WorldFromLocalMatrices.ElementAt(dataStoreIndex), dataStoreID, batchGroupElement.BatchRootID);
            }
        }

        private void ProcessPotentialCoplanarRoots()
        {
            for (int i = 0; i < PotentialCoplanarSetRoots.Length; ++i)
            {
                DataStoreIndex dataStoreIndex = PotentialCoplanarSetRoots[i];
                DataStoreID dataStoreID = Hierarchy.ElementAt(dataStoreIndex).ID;

                if (CoplanarSetRoots.ContainsKey(dataStoreID))
                {
                    // Already a root
                    continue;
                }

                ref BatchGroupElement batchGroupElement = ref BatchGroupElements.ElementAt(dataStoreIndex);
                NovaList<CoplanarSetID, CoplanarSet> coplanarSets = CoplanarSets[batchGroupElement.BatchRootID];

                ref float4x4 localFromWorld = ref LocalFromWorldMatrices.ElementAt(dataStoreIndex);

                // Need to check if we can be combined with any of the existing coplanar sets that exist in this batch group
                bool assignedSet = false;
                for (int j = 0; j < coplanarSets.Length; ++j)
                {
                    ref CoplanarSet setToCheck = ref coplanarSets.ElementAt(j);
                    DataStoreIndex setToCheckRootIndex = DataStoreIDToDataStoreIndex[setToCheck.RootID];
                    ref float4x4 worldFromSetToCheck = ref WorldFromLocalMatrices.ElementAt(setToCheckRootIndex);
                    if (!Math.AreCoplanar(ref localFromWorld, ref worldFromSetToCheck))
                    {
                        continue;
                    }

                    // It is coplanar with this set
                    CoplanarSetRoots[dataStoreID] = j;
                    assignedSet = true;
                    break;
                }

                if (assignedSet)
                {
                    continue;
                }

                // Create a new coplanar set
                CreateNewCoplanarSet(ref coplanarSets, dataStoreID, batchGroupElement.BatchRootID);
            }
        }

        private void AddBatchRoots()
        {
            for (int i = 0; i < AllBatchRootIDs.Length; ++i)
            {
                DataStoreID batchRootID = AllBatchRootIDs[i];

                if (!DirtyRoots.AsArray().Contains(batchRootID))
                {
                    continue;
                }

                DataStoreIndex dataStoreIndex = DataStoreIDToDataStoreIndex[batchRootID];

                ref float4x4 localFromWorld = ref LocalFromWorldMatrices.ElementAt(dataStoreIndex);
                ref float4x4 worldFromLocal = ref WorldFromLocalMatrices.ElementAt(dataStoreIndex);

                // Clear
                NovaList<CoplanarSetID, CoplanarSet> coplanarSets = CoplanarSets.GetAndClear(batchRootID);
                CreateNewCoplanarSet(ref coplanarSets, batchRootID, batchRootID);

                RotationSetSummary rotationSetSummary = RotationSets.GetAndClear(batchRootID);
                CreateNewRotationSet(ref rotationSetSummary, ref localFromWorld, ref worldFromLocal, batchRootID, batchRootID);
            }
        }

        private void CreateNewRotationSet(ref RotationSetSummary rotationSetSummary, ref float4x4 localFromWorld, ref float4x4 worldFromLocal, DataStoreID dataStoreID, DataStoreID batchRootID)
        {
            RotationSetRoots.Add(dataStoreID, rotationSetSummary.SetCount);
            rotationSetSummary.CreateSet(ref localFromWorld, ref worldFromLocal);
            RotationSets[batchRootID] = rotationSetSummary;
        }

        private void CreateNewCoplanarSet(ref NovaList<CoplanarSetID, CoplanarSet> coplanarSets, DataStoreID rootID, DataStoreID batchRootID)
        {
            CoplanarSetRoots.Add(rootID, coplanarSets.Length);
            coplanarSets.Add(new CoplanarSet(rootID));
            CoplanarSets[batchRootID] = coplanarSets;
        }
    }
}

