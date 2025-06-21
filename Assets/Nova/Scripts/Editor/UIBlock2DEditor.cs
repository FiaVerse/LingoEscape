// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    internal enum ImageSelectionType
    {
        Texture,
        Sprite
    }

    [CustomEditor(typeof(UIBlock2D)), CanEditMultipleObjects]
    internal class UIBlock2DEditor : BlockEditor<UIBlock2D>
    {
        _UIBlock2DData renderData = new _UIBlock2DData();
        private ImageSelectionType imageMode = ImageSelectionType.Texture;

        protected override void OnEnable()
        {
            base.OnEnable();

            renderData.SerializedProperty = serializedObject.FindProperty(Names.UIBlock2D.visuals);

            if (serializedObject.FindProperty(Names.UIBlock2D.sprite).objectReferenceValue != null)
            {
                imageMode = ImageSelectionType.Sprite;
            }
        }

        protected override void DoGui(UIBlock2D uiBlock)
        {
            Vector3 size = uiBlock.CalculatedSize.Value;
            float minHalfSize = .5f * Mathf.Min(size.x, size.y);

            UIBlock2DData.Calculated calc = serializedObject.isEditingMultipleObjects ? default : TargetBlock.CalculatedVisuals;

            NovaLayoutEditors.DrawAutoLayoutUI(autoLayout, uiBlock);
            NovaLayoutEditors.DrawPositionUI(layout, uiBlock);
            NovaLayoutEditors.DrawSizeUI(layout, uiBlock, previewSizeProperty);
            NovaRenderingEditors.DrawBodyVisualsUI(minHalfSize, renderData, surfaceInfo, baseRenderInfo, ref imageMode, ref calc);
            NovaRenderingEditors.DrawBorderUI(renderData.Border, calc.Border);

            NovaRenderingEditors.DrawShadowUI(renderData, calc.Shadow);
            NovaLayoutEditors.DrawPaddingMarginUI(layout, uiBlock);
        }
    }
}

