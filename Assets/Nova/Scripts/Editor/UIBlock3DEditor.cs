// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using UnityEditor;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(UIBlock3D)), CanEditMultipleObjects]
    internal class UIBlock3DEditor : BlockEditor<UIBlock3D>
    {
        private _UIBlock3DData renderData = new _UIBlock3DData();

        protected override void OnEnable()
        {
            base.OnEnable();

            renderData.SerializedProperty = serializedObject.FindProperty(Names.UIBlock3D.visuals);
        }

        protected override void DoGui(UIBlock3D uiBlock)
        {
            NovaLayoutEditors.DrawAutoLayoutUI(autoLayout, uiBlock);

            NovaLayoutEditors.DrawPositionUI(layout, uiBlock);
            NovaLayoutEditors.DrawSizeUI(layout, uiBlock, previewSizeProperty);

            UIBlock3DData.Calculated calc = serializedObject.isEditingMultipleObjects ? default : TargetBlock.CalculatedVisuals;
            NovaRenderingEditors.DrawBodyVisualsUI(uiBlock.CalculatedSize.Value, renderData, surfaceInfo, baseRenderInfo, ref calc);

            NovaLayoutEditors.DrawPaddingMarginUI(layout, uiBlock);
        }
    }
}
