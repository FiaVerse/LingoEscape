// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.TMP;
using TMPro;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    /// <summary>
    /// Specifies if the TMPs have differing values when multiselecting
    /// </summary>
    internal class TMPProperties
    {
        public bool TextDiffer = false;
        public bool HorizontalAlignmentDiffer = false;
        public bool VerticalAlignmentDiffer = false;
        public bool ColorDiffer = false;
        public bool FontDiffer = false;
        public bool FontSizeDiffer = false;

        public string Text
        {
            get => Components[0].text;
            set
            {
                RecordUndo();
                for (int i = 0; i < Components.Length; i++)
                {
                    Components[i].text = value;
                }
            }
        }

        public HorizontalAlignmentOptions HorizontalAlignment
        {
            get => Components[0].horizontalAlignment;
            set
            {
                RecordUndo();
                for (int i = 0; i < Components.Length; i++)
                {
                    Components[i].horizontalAlignment = value;
                }
            }
        }

        public VerticalAlignmentOptions VerticalAlignment
        {
            get => Components[0].verticalAlignment;
            set
            {
                RecordUndo();
                for (int i = 0; i < Components.Length; i++)
                {
                    Components[i].verticalAlignment = value;
                }
            }
        }

        public Color Color
        {
            get => Components[0].color;
            set
            {
                RecordUndo();
                for (int i = 0; i < Components.Length; i++)
                {
                    Components[i].color = value;
                }
            }
        }

        public TMP_FontAsset Font
        {
            get => Components[0].font;
            set
            {
                RecordUndo();
                for (int i = 0; i < Components.Length; i++)
                {
                    Components[i].font = value;
                }
            }
        }

        public float FontSize
        {
            get => Components[0].fontSize;
            set
            {
                RecordUndo();
                for (int i = 0; i < Components.Length; i++)
                {
                    Components[i].fontSize = value;
                }
            }
        }

        public TextMeshPro[] Components = null;
        public SerializedObject SerializedObject = null;

        private void RecordUndo()
        {
            for (int i = 0; i < Components.Length; i++)
            {
                Undo.RecordObject(Components[i], "Inspector");
            }
        }

        public void UpdateDifferState()
        {
            TextDiffer = HorizontalAlignmentDiffer = VerticalAlignmentDiffer = ColorDiffer = FontDiffer = FontSizeDiffer = false;

            TextMeshPro reference = Components[0];
            string text = reference.text;
            var hor = reference.horizontalAlignment;
            var ver = reference.verticalAlignment;
            var col = reference.color;
            var font = reference.font;
            var fontSize = reference.fontSize;
            for (int i = 1; i < Components.Length; i++)
            {
                TextMeshPro compare = Components[i];
                if (compare.text != text)
                {
                    TextDiffer = true;
                }

                if (!compare.horizontalAlignment.Equals(hor))
                {
                    HorizontalAlignmentDiffer = true;
                }

                if (!compare.verticalAlignment.Equals(ver))
                {
                    VerticalAlignmentDiffer = true;
                }

                if (!compare.color.Equals(col))
                {
                    ColorDiffer = true;
                }

                if (!compare.font.Equals(font))
                {
                    FontDiffer = true;
                }

                if (!compare.fontSize.Equals(fontSize))
                {
                    FontSizeDiffer = true;
                }
            }
        }
    }

    [CustomEditor(typeof(TextBlock)), CanEditMultipleObjects]
    internal class TextBlockEditor : BlockEditor<TextBlock>
    {
        private TMPProperties tmp = new TMPProperties();

        protected override void OnEnable()
        {
            base.OnEnable();

            tmp.Components = new TextMeshPro[targetComponents.Count];
            for (int i = 0; i < targetComponents.Count; i++)
            {
                tmp.Components[i] = targetComponents[i].TMP;
            }

            tmp.SerializedObject = new SerializedObject(tmp.Components);
        }

        protected override void EnsureComponentOrder()
        {
            for (int i = 0; i < targetComponents.Count; i++)
            {
                components.Clear();
                targetComponents[i].GetComponents(components);

                int tmpIndex = -1;
                int blockIndex = -1;
                for (int j = 0; j < components.Count; ++j)
                {
                    if (components[j] is TextMeshProTextBlock)
                    {
                        tmpIndex = j;
                    }
                    else if (components[j] is TextBlock)
                    {
                        blockIndex = j;
                    }

                    if (blockIndex >= 0 && tmpIndex >= 0)
                    {
                        break;
                    }
                }

                while (blockIndex >= tmpIndex && ComponentUtility.MoveComponentUp(targetComponents[i]))
                {
                    blockIndex--;
                }
            }

            base.EnsureComponentOrder();
        }

        private _AutoSize3 autoSize = new _AutoSize3();
        protected override void DoGui(TextBlock uiBlock)
        {
            autoSize.SerializedProperty = layout.AutoSizeProp;
            bool wasHuggingX = autoSize.X == Internal.AutoSize.Shrink;

            tmp.SerializedObject.UpdateIfRequiredOrScript();
            tmp.UpdateDifferState();
            NovaLayoutEditors.DrawAutoLayoutUI(autoLayout, uiBlock);
            NovaLayoutEditors.DrawPositionUI(layout, uiBlock);
            NovaLayoutEditors.DrawSizeUI(layout, uiBlock, previewSizeProperty);
            NovaRenderingEditors.DrawBodyVisualsUI(baseRenderInfo, surfaceInfo, tmp);
            NovaLayoutEditors.DrawPaddingMarginUI(layout, uiBlock);

            bool isHuggingX = autoSize.X == Internal.AutoSize.Shrink;

            if (isHuggingX && !wasHuggingX && float.IsInfinity(layout.SizeMinMax.X.Max))
            {
                for (int i = 0; i < targetComponents.Count; i++)
                {
                    targetComponents[i].TMP.DisableWordWrap();
                }
            }
        }
    }
}

