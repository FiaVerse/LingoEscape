// Copyright (c) Supernova Technologies LLC
using Nova.Internal;
using Nova.Internal.Core;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.Utilities
{
    /// <summary>
    /// When transform positions are modified manually in editor, the layout positions
    /// can get out of sync without a system in place to ensure the synchronization of 
    /// both the scene view values and what's serialized. This handles most of that
    /// synchronization. Prefabs must be handled uniquely because they get disabled/enabled
    /// during undo/redo operations, which breaks our ability to compare a value we read off
    /// the transform to the last value we saw/calculated in the data store.
    /// </summary>
    internal static class LayoutTransformRecorder
    {
        private static int undoGroup = -1;
        private static UIBlock[] selectedPrefabRoots = null;
        private static Dictionary<UIBlock, Vector3> selectedPrefabPositions = new Dictionary<UIBlock, Vector3>();

        [InitializeOnLoadMethod]
        private static void Init()
        {
            // Ensure these all start in an expected state 
            undoGroup = -1;
            selectedPrefabRoots = null;
            selectedPrefabPositions.Clear();

            Selection.selectionChanged -= RefreshSelectedPrefabs;
            Selection.selectionChanged += RefreshSelectedPrefabs;

            EngineManager.EditorOnly_OnBeforeEngineUpdate -= PreEngineUpdate;
            EngineManager.EditorOnly_OnBeforeEngineUpdate += PreEngineUpdate;

            EngineManager.EditorOnly_OnAfterEngineUpdate -= PostEngineUpdate;
            EngineManager.EditorOnly_OnAfterEngineUpdate += PostEngineUpdate;

            RefreshSelectedPrefabs();
        }

        private static void PostEngineUpdate()
        {
            if (selectedPrefabRoots == null || !LayoutDataStore.Instance.TransformsWereModified)
            {
                return;
            }

            for (int i = 0; i < selectedPrefabRoots.Length; ++i)
            {
                UIBlock prefabRoot = selectedPrefabRoots[i];
                ILayoutBlock layoutBlock = prefabRoot;

                DataStoreIndex index = layoutBlock.Index;

                if (!index.IsValid || !LayoutDataStore.Instance.UsingTransformPositions[index])
                {
                    continue;
                }

                Vector3 prevRaw = selectedPrefabPositions[prefabRoot];

                // Since this runs before Engine.PostUpdate, we read from the engine 
                // output directly because the DataStore state hasn't been reset yet
                Vector3 newRaw = LayoutAccess.Get(index, ref LayoutDataStore.Instance.LengthConfigs).Position.Raw;

                if (newRaw != prevRaw)
                {
                    SerializedObject so = new SerializedObject(prefabRoot);
                    _Length3 position = new _Length3() { SerializedProperty = so.FindProperty("layout.Position") };
                    position.X.Raw = newRaw.x;
                    position.Y.Raw = newRaw.y;
                    position.Z.Raw = newRaw.z;
                    so.ApplyModifiedProperties();
                }
            }
        }

        private static void PreEngineUpdate()
        {
            int currentGroup = Undo.GetCurrentGroup();

            if (undoGroup == currentGroup)
            {
                // only update when the group number changes,
                // otherwise we get "recordings" of temporary
                // values as they are being typed into the inspector,
                // rather than the final value
                return;
            }

            undoGroup = currentGroup;

            foreach (UIBlock uiBlock in selectedPrefabRoots)
            {
                selectedPrefabPositions[uiBlock] = uiBlock.Position.Raw;
            }
        }

        private static void RefreshSelectedPrefabs()
        {
            selectedPrefabRoots = Selection.GetFiltered<UIBlock>(SelectionMode.TopLevel).Where(x => x.gameObject.activeInHierarchy && PrefabUtility.IsAnyPrefabInstanceRoot(x.gameObject)).ToArray();
            selectedPrefabPositions.Clear();
        }

        /// <summary>
        /// Synchronize the UIBlock position with its Transform position, and ensure the layout position gets tracked as a dirty property which must be saved to the scene.
        /// </summary>
        /// <param name="uiBlock"></param>
        public static void CopyFromTransform(UIBlock uiBlock)
        {
            Vector3 transformPosition = uiBlock.transform.localPosition;
            Vector3 storePosition = uiBlock.GetCalculatedTransformLocalPosition();
            bool positionChanged = transformPosition != storePosition;

            if (positionChanged)
            {
                Vector3 parentSize = GetParentSize(uiBlock);

                IUIBlock parent = uiBlock.GetParentBlock();
                Vector3 paddingOffset = parent != null ? (Vector3)parent.CalculatedPadding.Offset : Vector3.zero;

                // if position changed from what we're tracking, we need to convert transform -> layout position first
                Vector3 offset = LayoutUtils.LocalPositionToLayoutOffset(transformPosition, uiBlock.LayoutSize, uiBlock.CalculatedMargin.Offset, parentSize, paddingOffset, (Vector3)uiBlock.Alignment);
                uiBlock.Position.Raw = Length3.GetRawValue(offset, uiBlock.Position, uiBlock.PositionMinMax, parentSize);
            }

            if (positionChanged || LayoutDataStore.Instance.TransformsWereModified)
            {
                // Whether or not the datastore is up-to-date, we need to copy out the datastore value into the struct stored on the UIBlock directly

                // Tried using Undo.Record and setting the SerializedLayout.Position.Raw directly ... it didn't work reliably
                // It worked 99.99% of the time, but it randomly did not work even when I could see the values changing in the debugger.
                // They just wouldn't save to the scene. Sick, Unity.

                // Unfortunately, creating a serialized object and setting the serialized
                // properties directly is the only thing that has proven itself reliable.

                SerializedObject so = new SerializedObject(uiBlock);
                _Layout layout = new _Layout() { SerializedProperty = so.FindProperty("layout") };
                _Length3 position = layout.Position;

                Vector3 raw = uiBlock.Position.Raw;
                position.X.Raw = raw.x;
                position.Y.Raw = raw.y;
                position.Z.Raw = raw.z;

                so.ApplyModifiedProperties();
            }
        }

        public static bool IsTrackedTransform(Transform transform) => LayoutDataStore.Instance.TransformTracker.Tracking(transform);

        /// <summary>
        /// Get the size of this UIBlock's parent, accounting for roots and preview sizes
        /// </summary>
        /// <param name="uiBlock"></param>
        /// <returns></returns>
        public static Vector3 GetParentSize(UIBlock uiBlock)
        {
            IUIBlock parentBlock = uiBlock.GetParentBlock();
            bool hasParentBlock = parentBlock != null;

            if (!hasParentBlock)
            {
                return Vector3.zero;
            }

            return parentBlock.PaddedSize;
        }
    }
}
