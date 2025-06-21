// Copyright (c) Supernova Technologies LLC
using Nova.Editor.GUIs;
using UnityEditor;

namespace Nova.Editor.Layouts
{
    [CustomEditor(typeof(UIBlock)), CanEditMultipleObjects]
    internal class UIBlockEditor : BlockEditor<UIBlock>
    {
        protected override void DoGui(UIBlock uiBlock)
        {
            NovaLayoutEditors.DrawAutoLayoutUI(autoLayout, uiBlock);
            NovaLayoutEditors.DrawPositionUI(layout, uiBlock);
            NovaLayoutEditors.DrawSizeUI(layout, uiBlock, previewSizeProperty);
            NovaLayoutEditors.DrawPaddingMarginUI(layout, uiBlock);
        }
    }
}
