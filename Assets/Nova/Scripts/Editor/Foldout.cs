// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using System;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    internal struct Foldout : IDisposable
    {
        public const int ArrowIconSize = 14;
        public const int HeaderExtraControlWidth = 36;

        private bool doFoldout;
        private bool isHeaderGroup;
        private bool disableableGroup;

        private Foldout(bool doFoldout, bool isHeaderGroup, bool disableableGroup = false)
        {
            this.doFoldout = doFoldout;
            this.isHeaderGroup = isHeaderGroup;
            this.disableableGroup = disableableGroup;
        }

        public void Dispose()
        {
            if (isHeaderGroup)
            {
                NovaGUI.Layout.EndVertical();

                if (disableableGroup)
                {
                    EditorGUI.EndDisabledGroup();
                }

                if (doFoldout)
                {

                    Rect controlRect = GUILayoutUtility.GetLastRect();
                    controlRect.yMax -= 1;

                    NovaGUI.Styles.DrawSeparator(controlRect, afterControl: false);
                }
            }
        }

        public static Foldout DoHeaderGroup(bool foldout, string label, Action<Rect> dropdownMenu = null)
        {
            Rect headerRect = NovaGUI.Layout.GetControlRect();

            foldout = FoldoutHeaderField(headerRect, 0, foldout, label, dropdownMenu);

            NovaGUI.Layout.BeginVertical(foldout ? NovaGUI.Styles.InnerContent : GUIStyle.none);

            return new Foldout(foldout, isHeaderGroup: true);
        }

        public static Foldout DoHeaderGroup(bool foldout, string label, SerializedProperty toggleProperty, Action<Rect> dropdownMenu = null)
        {
            Rect headerRect = NovaGUI.Layout.GetControlRect();

            foldout = FoldoutHeaderField(headerRect, HeaderExtraControlWidth, foldout, label, dropdownMenu);

            Rect togglePosition = GUILayoutUtility.GetLastRect();
            togglePosition.width = NovaGUI.ToggleBoxSize;
            togglePosition.x = headerRect.xMax - HeaderExtraControlWidth;

            GUIContent propertyLabel = EditorGUI.BeginProperty(togglePosition, GUIContent.none, toggleProperty);
            EditorGUI.BeginChangeCheck();

            bool isOn = EditorGUI.Toggle(togglePosition, propertyLabel, toggleProperty.boolValue);

            if (EditorGUI.EndChangeCheck())
            {
                toggleProperty.boolValue = isOn;
            }
            EditorGUI.EndProperty();

            NovaGUI.Layout.BeginVertical(foldout ? NovaGUI.Styles.InnerContent : GUIStyle.none);
            EditorGUI.BeginDisabledGroup(!isOn);

            return new Foldout(foldout, isHeaderGroup: true, disableableGroup: true);
        }

        public static bool FoldoutToggle(Rect position, bool open)
        {
            Rect foldoutRect = position;
            foldoutRect.x -= ArrowIconSize;
            foldoutRect.width = ArrowIconSize;

            return GUI.Toggle(foldoutRect, open, string.Empty, NovaGUI.Styles.FoldoutToggle);
        }

        public static bool InProjectSettings = false; // Hack to make the dropdown arrows work in project settings

        private static bool FoldoutHeaderField(Rect headerRect, float additionalControlWidth, bool foldout, string label, Action<Rect> dropdownMenu)
        {
            // align rect vertically and horizontally - default values are slightly off.
            headerRect.width -= 1;
            headerRect.height += 1f;

            EventType currentEvent = Event.current.type;

            Rect menuButtonRect = headerRect;
            menuButtonRect.width = NovaGUI.IconSize;
            menuButtonRect.height = NovaGUI.IconSize;
            menuButtonRect.x = headerRect.width + 2;
            menuButtonRect.y += 0.5f;

            Rect controlRect = menuButtonRect;
            controlRect.width += additionalControlWidth;
            controlRect.height = headerRect.height;
            controlRect.x -= additionalControlWidth;

            bool repaint = Event.current.type == EventType.Repaint;
            bool fakeUsed = controlRect.Contains(Event.current.mousePosition) && !repaint;
            if (fakeUsed)
            {
                // This "tricks" the header foldout into no-oping on the mouse event. Otherwise it would
                // try to capture the event, and the foldout would expand rather than the toggle changing.
                Event.current.type = EventType.Used;
            }

            Color backgroundColor = GUI.backgroundColor;
            bool hover = headerRect.Contains(Event.current.mousePosition);
            if (repaint)
            {
                NovaGUI.Styles.SectionHeaderBackground.Draw(headerRect, false, false, false, false);
                GUI.backgroundColor = hover ? Color.white : Color.clear;
            }

            foldout = EditorGUI.BeginFoldoutHeaderGroup(headerRect, foldout, label, NovaGUI.Styles.SectionHeaderStyle);
            EditorGUI.EndFoldoutHeaderGroup();

            if (repaint)
            {
                GUI.backgroundColor = Color.white;
                if (!hover)
                {
                    Rect foldoutRect = headerRect;
                    foldoutRect.width = ArrowIconSize;
                    if (!InProjectSettings)
                    {
                        foldoutRect.x -= ArrowIconSize;
                    }
                    EditorStyles.foldout.Draw(foldoutRect, false, false, foldout, false);
                }
                GUI.backgroundColor = backgroundColor;
            }

            if (fakeUsed)
            {
                // here we undo our previous "trick" 
                Event.current.type = currentEvent;
            }

            if (dropdownMenu != null)
            {
                if (GUI.Button(menuButtonRect, NovaGUI.Styles.MenuIcon, NovaGUI.Styles.MenuIconStyle))
                {
                    dropdownMenu.Invoke(menuButtonRect);
                }
            }

            NovaGUI.Styles.DrawSeparator(headerRect);

            return foldout;
        }

        public static Foldout DoFoldout(bool foldout, string label, GUIStyle style)
        {
            foldout = EditorGUILayout.Foldout(foldout, label, true, style == null ? EditorStyles.foldout : style);
            return new Foldout(foldout, isHeaderGroup: false);
        }

        public static Foldout DoFoldout(bool foldout, string label, ref Rect position)
        {
            foldout = EditorGUI.Foldout(position.GetTopSection(), foldout, label, true);
            return new Foldout(foldout, isHeaderGroup: false, disableableGroup: false);
        }

        public static implicit operator bool(Foldout foldout) => foldout.doFoldout;
    }
}
