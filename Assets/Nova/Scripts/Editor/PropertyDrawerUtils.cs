// Copyright (c) Supernova Technologies LLC
using Nova.Editor.GUIs;
using Nova.Internal.Utilities;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.Utilities
{
    internal static class PropertyDrawerUtils
    {
        public static readonly float SingleLineHeight = EditorGUIUtility.singleLineHeight + NovaGUI.MinSpaceBetweenFields;

        public static Rect Center(this ref Rect rect, float width)
        {
            return new Rect(rect.x + (rect.width - width) * 0.5f, rect.y, width, rect.height);
        }

        public static Rect Center(this ref Rect rect, Vector2 size)
        {
            return new Rect(rect.x + (rect.width - size.x) * 0.5f, rect.y + (rect.height - size.y) * 0.5f, size.x, size.y);
        }

        public static Rect TopRight(this ref Rect rect, float width, float height)
        {
            return new Rect(rect.xMax - width, rect.y, width, height);
        }

        public static Rect BottomRight(this ref Rect rect, float width, float height)
        {
            return new Rect(rect.xMax - width, rect.yMax - height, width, height);
        }

        public static void Split(this ref Rect rect, out Rect a, out Rect b)
        {
            float individualSize = (rect.width - NovaGUI.MinSpaceBetweenFields) * .5f;
            a = new Rect(rect.x, rect.y, individualSize, rect.height);
            b = new Rect(rect.x + a.width + NovaGUI.MinSpaceBetweenFields, rect.y, individualSize, rect.height);
        }

        public static void Split(this ref Rect rect, out Rect a, out Rect b, out Rect c)
        {
            float individualSize = (rect.width - 2f * NovaGUI.MinSpaceBetweenFields) / 3f;
            a = rect;
            a.width = individualSize;
            b = c = a;
            b.x += a.width + NovaGUI.MinSpaceBetweenFields;
            c.x = b.x + a.width + NovaGUI.MinSpaceBetweenFields;
        }

        public static void Split(this ref Rect rect, float aWidth, out Rect a, out Rect b)
        {
            a = new Rect(rect.x, rect.y, aWidth, rect.height);
            b = new Rect(a.xMax + NovaGUI.MinSpaceBetweenFields, rect.y, rect.width - aWidth - NovaGUI.MinSpaceBetweenFields, rect.height);
        }

        public static void BumpLine(this ref Rect rect)
        {
            rect.Bump(SingleLineHeight);
        }

        public static void Bump(this ref Rect rect, float height)
        {
            rect = new Rect(rect.x, rect.y + height, rect.width, rect.height);
        }

        public static void ShiftAndResize(this ref Rect rect, float val)
        {
            rect = new Rect(rect.x + val, rect.y, rect.width - val, rect.height);
        }

        public static void ShiftAndResizeLabel(this ref Rect rect) => rect.ShiftAndResize(EditorGUIUtility.labelWidth);

        /// <summary>
        /// Takes a rect, returns the top section of it, and also modifies the passed in
        /// rect to remove the part that is being used
        /// NOTE: height defaults to EditorGUIUtility.singleLineHeight
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static Rect GetTopSection(ref this Rect position, float height = -1f, float padding = Constants.SingleLineEditorPadding)
        {
            if (height == -1f)
            {
                height = EditorGUIUtility.singleLineHeight;
            }

            Rect toRet = position;
            toRet.height = height;

            float heightPlusPadding = height + padding;
            position.y += heightPlusPadding;
            position.height -= heightPlusPadding;
            return toRet;
        }
    }
}

