// Copyright (c) Supernova Technologies LLC
using Nova.Editor.GUIs;
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.Tools
{
    internal static class UIBlockGizmos
    {
        [DrawGizmo(GizmoType.Pickable | GizmoType.Active | GizmoType.InSelectionHierarchy | GizmoType.NonSelected | GizmoType.NotInSelectionHierarchy, typeof(UIBlock))]
        public static void DrawGizmos(UIBlock uiBlock, GizmoType gizmoType)
        {
            if (!NovaEditorPrefs.HierarchyGizmosEnabled)
            {
                return;
            }

            bool selected = (gizmoType & GizmoType.Selected) != 0;
            bool inSelectionHierarchy = (gizmoType & GizmoType.InSelectionHierarchy) != 0;

            bool inSelection = selected || inSelectionHierarchy;

            if (!inSelection)
            {
                return;
            }

            Color selectedColor = SceneView.selectedOutlineColor;
            selectedColor.a = 1;

            OnDrawGizmosInSelection(uiBlock, selected ? selectedColor : NovaGUI.Styles.SceneViewInSelectionHiearchyColor);
        }

        private static void OnDrawGizmosInSelection(UIBlock uiBlock, Color color)
        {
            using (new Handles.DrawingScope(color, uiBlock.transform.localToWorldMatrix))
            {
                HandleUtils.DrawWireCube(Vector3.zero, uiBlock.CalculatedSize.Value, 2, withShadow: false);
            }
        }
    }
}
