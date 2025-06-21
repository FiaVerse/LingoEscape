// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using Nova.Internal.Core;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(SortGroup))]
    [CanEditMultipleObjects]
    internal class SortGroupEditor : NovaEditor<SortGroup>
    {
        private _SortGroupInfo info = new _SortGroupInfo();

        protected override void OnEnable()
        {
            base.OnEnable();
            info.SerializedProperty = serializedObject.FindProperty(Names.SortGroup.info);

            Undo.undoRedoPerformed += RestoreUndoneRedoneProperties;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RestoreUndoneRedoneProperties;
        }

        private void RestoreUndoneRedoneProperties()
        {
            MarkDirty();
        }

        /// <summary>
        /// Returns whether or not the sort group inherits the renderqueue property from the hierarchy root
        /// </summary>
        /// <param name="sortGroup"></param>
        /// <returns></returns>
        private bool InheritsRootsProperties(SortGroup sortGroup, out SortGroupInfo hierarchyRootInfo)
        {
            hierarchyRootInfo = default;
            if (RenderEngine.Instance == null)
            {
                return false;
            }

            DataStoreID dataStoreID = sortGroup.UIBlock.ID;
            if (!dataStoreID.IsValid)
            {
                return false;
            }

            if (!RenderEngine.Instance.SortGroupHierarchyInfo.TryGetValue(dataStoreID, out SortGroupHierarchyInfo info) ||
                // Don't include the root itself
                info.HierarchyRoot == dataStoreID ||
                !RenderingDataStore.Instance.RootDataStore.ScreenSpaceCameraTargets.TryGetValue(info.HierarchyRoot, out _))
            {
                return false;
            }

            // The hierarchy root is a screen space root, so return its' info
            if (RenderingDataStore.Instance.RootDataStore.SortGroupInfos.TryGetValue(info.HierarchyRoot, out Internal.SortGroupInfo storedHierarchyRootInfo))
            {
                hierarchyRootInfo = UnsafeUtility.As<Internal.SortGroupInfo, SortGroupInfo>(ref storedHierarchyRootInfo);
            }
            else
            {
                // If it doesn't have any info (which might happen if the sort group is disabled),
                // just return the default screen space info
                hierarchyRootInfo = new SortGroupInfo()
                {
                    SortingOrder = 0,
                    RenderQueue = ConditionalConstants.DefaultOverlayRenderQueue,
                    RenderOverOpaqueGeometry = true,
                };
            }

            return true;
        }

        private bool AnyTargetInheritsProperties(out SortGroupInfo rootInfo)
        {
            rootInfo = default;
            for (int i = 0; i < targetComponents.Count; ++i)
            {
                if (InheritsRootsProperties(targetComponents[i], out rootInfo))
                {
                    return true;
                }
            }

            return false;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            NovaGUI.IntFieldClamped(Labels.SortGroup.SortingOrder, info.SortingOrderProp, Int16.MinValue, Int16.MaxValue);

            bool anyInheritsProps = AnyTargetInheritsProperties(out SortGroupInfo rootInfo);
            if (anyInheritsProps)
            {
                EditorGUI.BeginDisabledGroup(true);

                Rect fieldRect = NovaGUI.Layout.GetControlRect(NovaGUI.Layout.MinFloatFieldWidthOption);
                GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, Labels.SortGroup.RenderQueue_Overridden, info.RenderQueueProp);
                EditorGUI.IntField(fieldRect, propertyLabel, rootInfo.RenderQueue);
                EditorGUI.EndProperty();

                Rect rect = NovaGUI.Layout.GetControlRect();
                GUIContent labelContent = EditorGUI.BeginProperty(rect, Labels.SortGroup.RenderOverOpaqueGeometry_Overridden, info.RenderOverOpaqueGeometryProp);
                EditorGUI.Toggle(rect, labelContent, rootInfo.RenderOverOpaqueGeometry);

                EditorGUI.EndDisabledGroup();
            }
            else
            {
                NovaGUI.IntFieldClamped(Labels.SortGroup.RenderQueue, info.RenderQueueProp, 0, 5000);
                NovaGUI.ToggleField(Labels.SortGroup.RenderOverOpaqueGeometry, info.RenderOverOpaqueGeometryProp);
            }

            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
        }

        private void MarkDirty()
        {
            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targetComponents.Count; ++i)
            {
                targetComponents[i].RegisterOrUpdate();
            }
        }
    }
}

