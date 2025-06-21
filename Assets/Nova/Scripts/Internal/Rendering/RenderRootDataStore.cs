// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal enum RenderRootType
    {
        Hierarchy,
        SortGroup,
    }

    internal struct RenderRootDataStore : IInitializable
    {
        public NovaHashMap<DataStoreID, RenderRootType> Roots;
        public NovaHashMap<DataStoreID, SortGroupInfo> SortGroupInfos;
        public NovaHashMap<DataStoreID, int> ScreenSpaceCameraTargets;
        public NovaHashMap<DataStoreID, NovaList<int>> ScreenSpaceAdditionalCameras;

        private NativeList<NovaList<int>> additionalCameraPool;

        public void AddScreenSpaceRoot(DataStoreID dataStoreID, IScreenSpace screenSpace)
        {
            ScreenSpaceCameraTargets[dataStoreID] = screenSpace.CameraID;

            if (!ScreenSpaceAdditionalCameras.TryGetValue(dataStoreID, out NovaList<int> additionalCameras))
            {
                additionalCameras = additionalCameraPool.GetFromPoolOrInit();
            }

            additionalCameras.Clear();
            for (int i =0; i < screenSpace.AdditionalCameras.Count; ++i)
            {
                Camera cam = screenSpace.AdditionalCameras[i];
                if (cam == null)
                {
                    continue;
                }

                additionalCameras.Add(cam.GetInstanceID());
            }

            ScreenSpaceAdditionalCameras[dataStoreID] = additionalCameras;
        }

        public void RemoveScreenSpaceRoot(DataStoreID dataStoreID)
        {
            ScreenSpaceCameraTargets.Remove(dataStoreID);
            if (ScreenSpaceAdditionalCameras.TryGetValue(dataStoreID, out NovaList<int> additionalCameras))
            {
                additionalCameraPool.ReturnToPool(ref additionalCameras);
                ScreenSpaceAdditionalCameras.Remove(dataStoreID);
            }
        }

        public void AddHierarchyRoot(DataStoreID dataStoreID)
        {
            Roots[dataStoreID] = RenderRootType.Hierarchy;
        }

        public void RemoveHierarchyRoot(DataStoreID dataStoreID)
        {
            Roots.Remove(dataStoreID);
        }

        public void AddSortGroup(DataStoreID dataStoreID, ref SortGroupInfo sortGroupInfo)
        {
            Roots[dataStoreID] = RenderRootType.SortGroup;
            SortGroupInfos[dataStoreID] = sortGroupInfo;
        }

        public void RemoveSortGroup(DataStoreID dataStoreID)
        {
            Roots.Remove(dataStoreID);
            SortGroupInfos.Remove(dataStoreID);
        }

        public void Init()
        {
            Roots.Init(Constants.SomeElementsInitialCapacity);
            SortGroupInfos.Init(Constants.FewElementsInitialCapacity);
            ScreenSpaceCameraTargets.Init(Constants.FewElementsInitialCapacity);
            ScreenSpaceAdditionalCameras.Init(Constants.FewElementsInitialCapacity);

            additionalCameraPool.Init(Constants.FewElementsInitialCapacity);
        }

        public void Dispose()
        {
            Roots.Dispose();
            SortGroupInfos.Dispose();
            ScreenSpaceCameraTargets.Dispose();
            ScreenSpaceAdditionalCameras.Dispose();

            additionalCameraPool.DisposeListAndElements();
        }
    }
}

