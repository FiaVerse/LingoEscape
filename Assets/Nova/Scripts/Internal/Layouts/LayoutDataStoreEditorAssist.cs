// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Layouts
{
    internal struct PreviewSize
    {
        public float3 Size;
        public bool Available;
        public bool Dirty;

        public void DiffAndDirty(ref PreviewSize other)
        {
            Dirty = !Size.Equals(other.Size) || Available != other.Available;
        }
    }

    internal partial class LayoutDataStore
    {
        public static void EditorOnly_QueuePreviewSizeRefresh()
        {
            Instance.Previews.EditorOnly_QueueRefresh();
        }

        internal class PreviewSizeManager : IDisposable
        {
            public NovaHashMap<DataStoreID, PreviewSize> PreviewSizes;
            private bool previewSizesNeedRefreshing = false;
            private bool previewSizesNeedCleaning = false;

            public void EditorOnly_QueueRefresh()
            {
                previewSizesNeedRefreshing = true;
            }

            public void EditorOnly_TryRefresh()
            {
                if (!previewSizesNeedRefreshing)
                {
                    return;
                }

                RefreshPreviewSizes();

                previewSizesNeedRefreshing = false;
            }

            public void EditorOnly_ClearDirtyState()
            {
                if (!previewSizesNeedCleaning)
                {
                    return;
                }

                NativeArray <DataStoreID> previewSizeIDs = PreviewSizes.GetKeyArray(Allocator.Temp);

                int rootCount = previewSizeIDs.Length;
                for (int i = 0; i < rootCount; ++i)
                {
                    DataStoreID rootID = previewSizeIDs[i];
                    if (PreviewSizes.TryGetValue(rootID, out PreviewSize preview))
                    {
                        preview.Dirty = false;
                        PreviewSizes[rootID] = preview;
                    }
                }

                previewSizeIDs.Dispose();
                previewSizesNeedCleaning = false;
            }

            private void RefreshPreviewSizes()
            {
                NativeList<DataStoreID> rootIDs = HierarchyDataStore.Instance.HierarchyRootIDs;

                PooledDictionary<DataStoreID, PreviewSize> oldPreviewSizes = DictionaryPool<DataStoreID, PreviewSize>.Get();
                NativeArray<DataStoreID> previewSizeIDs = PreviewSizes.GetKeyArray(Allocator.Temp);

                for (int i = 0; i < previewSizeIDs.Length; ++i)
                {
                    DataStoreID rootID = previewSizeIDs[i];
                    oldPreviewSizes.Add(rootID, PreviewSizes[rootID]);
                }

                previewSizeIDs.Dispose();

                PreviewSizes.Clear();

                int previewCount = 0;
                for (int i = 0; i < rootIDs.Length; ++i)
                {
                    DataStoreID rootID = rootIDs[i];
                    
                    IUIBlock root = HierarchyDataStore.Instance.Elements[rootID] as IUIBlock;

                    UnityEngine.GameObject rootObject = root.Transform.gameObject;

                    if (!SceneViewUtils.IsInCurrentPrefabStage(rootObject))
                    {
                        // only hierarchy roots in prefab view are allowed to display/use their preview size
                        continue;
                    }

                    ref Layout rootLayout = ref root.SerializedLayout;

                    AutoSize3 autoSize = rootLayout.AutoSize;
                    Length3 rootSize = rootLayout.Size;

                    root.PreviewSize = rootLayout.SizeMinMax.Clamp(math.select(rootSize.Value, root.PreviewSize, rootSize.IsRelative));
                    PreviewSize preview = new PreviewSize() { Size = root.PreviewSize, Available = SceneViewUtils.IsInCurrentStage(rootObject) };

                    if (!oldPreviewSizes.TryGetValue(rootID, out PreviewSize oldPreview))
                    {
                        oldPreview = default;
                    }

                    preview.DiffAndDirty(ref oldPreview);

                    previewSizesNeedCleaning |= preview.Dirty;
                    previewCount++;

                    PreviewSizes.Add(rootID, preview);
                }

                DictionaryPool<DataStoreID, PreviewSize>.Release(oldPreviewSizes);
            }

            public void Init(ref LayoutCore.DiffAndDirty diffAndDirty)
            {
                PreviewSizes = new NovaHashMap<DataStoreID, PreviewSize>(NovaApplication.IsEditor ? 4 : 0, Allocator.Persistent);
                previewSizesNeedRefreshing = NovaApplication.IsEditor ? true : false;

                diffAndDirty.PreviewSizes = PreviewSizes;
            }

            public void Dispose()
            {
                PreviewSizes.Dispose();
            }
        }
    }
}
