// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Nova.Editor
{
    internal static class VisibilityManager
    {
        private class HiddenSet
        {
            public List<DataStoreID> Roots = new List<DataStoreID>();
            public List<DataStoreID> Elements = new List<DataStoreID>();

            public bool IsEmpty => Roots.Count == 0;

            public void MarkDirty()
            {
                for (int i = 0; i < Roots.Count; i++)
                {
                    RenderingDataStore.Instance.DirtyState.DirtyBatchRoots.Add(Roots[i]);
                }

                for (int i = 0; i < Elements.Count; i++)
                {
                    RenderingDataStore.Instance.DirtyState.DirtyBaseInfos.Add(Elements[i]);
                }
            }

            public void Clear()
            {
                Roots.Clear();
                Elements.Clear();
            }
        }

        private static HiddenSet previous = new HiddenSet();
        private static HiddenSet current = new HiddenSet();

        public static void ReprocessAll()
        {
            if (RenderingDataStore.Instance == null)
            {
                // This sometimes happens on first import if the burst compiler causes the engine
                // to fail to initialize
                return;
            }

            current.Clear();
            RenderingDataStore.Instance.Common.HiddenElements.Clear();
            if (!SceneVisibilityManager.instance.AreAnyDescendantsHidden(SceneManager.GetActiveScene())
                // Need to have this even though it's an editor file because the obfuscator needs it
#if UNITY_EDITOR
                && (!PrefabStageUtilsImpl.TryGetPrefabScene(out Scene prefabScene) || !SceneVisibilityManager.instance.AreAnyDescendantsHidden(prefabScene)))
#else
)
#endif
            {
                if (!previous.IsEmpty)
                {
                    previous.MarkDirty();
                    previous.Clear();
                    EditModeUtils.QueueEditorUpdateNextFrame();
                }
                return;
            }

            ref NativeList<HierarchyElement> elements = ref HierarchyDataStore.Instance.Hierarchy;
            for (int i = 0; i < elements.Length; i++)
            {
                ref HierarchyElement element = ref elements.ElementAt(i);

                if (!HierarchyDataStore.Instance.Elements.TryGetValue(element.ID, out IHierarchyBlock block) ||
                    block.Transform == null ||
                    !SceneVisibilityManager.instance.IsHidden(block.Transform.gameObject))
                {
                    continue;
                }

                DataStoreID batchRootID = HierarchyDataStore.Instance.BatchGroupTracker.BatchGroupElements[i].BatchRootID;
                current.Elements.Add(element.ID);
                current.Roots.Add(batchRootID);
                RenderingDataStore.Instance.Common.HiddenElements.Add(element.ID, default);
            }

            if (!current.IsEmpty)
            {
                current.MarkDirty();

                if (!previous.IsEmpty)
                {
                    // Dirty roots which used to have hidden elements, but no longer do
                    previous.MarkDirty();
                    previous.Clear();
                }

                EditModeUtils.QueueEditorUpdateNextFrame();

                // Swap the lists
                var temp = previous;
                previous = current;
                current = temp;
            }
            else if (!previous.IsEmpty)
            {
                // No hidden elements, but there used to be
                previous.MarkDirty();
                previous.Clear();
                EditModeUtils.QueueEditorUpdateNextFrame();
            }
        }
    }
}

