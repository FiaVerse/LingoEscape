// Copyright (c) Supernova Technologies LLC
//#define DEBUG_RECTS
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    internal static class NovaGUI
    {
        public static bool LengthToggleShortcutEnabled = false;
        public static bool EditingSingleObject = true;
        public static bool SwapLengths => LengthToggleShortcutEnabled && EditingSingleObject;

        public const float SingleCharacterGUIWidth = 10;
        public const float WideCharacterGUIWidth = 12.5f;
        public const float ToggleBoxSize = 15;
        public const float IconSize = 15;
        public const float MiniLabelWidth = 45;

        /// <summary>
        /// The width of the inspector when it's in its most collapsed horiztonal state, inclusive of space for a label
        /// </summary>
        public const float MinimumTotalViewWidth = 220;

        /// <summary>
        /// The width of the inspector when it's in its most collapsed horiztonal state, inclusive of space for a label
        /// </summary>
        public const float MinimumExternalWindowWidth = 250;

        /// <summary>
        /// The width of the inspector when it's in its most collapsed horiztonal state, exluding space for a label
        /// </summary>
        public const float MinimumFieldWidth = 190;

        public const float MinSpaceBetweenFields = 2;
        public const float IndentSize = 16;
        public const float ToggleToolbarFieldWidth = 4 * SingleCharacterGUIWidth;
        public const float MinFloatFieldWidth = 3 * SingleCharacterGUIWidth;
        public const float CompactInspectorWidth = 504;

        public static bool CompactViewMode => ViewWidth < CompactInspectorWidth;
        public static float PrefixLabelWidth => Mathf.Min(EditorGUIUtility.labelWidth, ViewWidth - MinimumTotalViewWidth) + MinSpaceBetweenFields;
        public static float FieldWidth => Mathf.Max(ViewWidth * 0.575f, MinimumFieldWidth);

        [ClutchShortcut("Nova/Length Type Swap", KeyCode.L, ShortcutModifiers.Action)]
        private static void LengthSwapToggled()
        {
            LengthToggleShortcutEnabled = !LengthToggleShortcutEnabled;
            InternalEditorUtility.RepaintAllViews();
        }

        public static float LabelWidth
        {
            get
            {
                return EditorGUIUtility.labelWidth;
            }
            set
            {
                EditorGUIUtility.labelWidth = value;
            }
        }

        public static float ViewWidth
        {
            get
            {
                return Mathf.Min(2.25f * EditorGUIUtility.labelWidth, EditorGUIUtility.currentViewWidth);
            }
        }

        public static bool ShowZAxisValues(UIBlock uiBlock) => uiBlock is UIBlock3D ? NovaEditorPrefs.UIBlock3DShowAllZAxis : NovaEditorPrefs.UIBlockShowAllZAxis;

        public static class Styles
        {
            private static ColorSpace playerColorSpace;

            [InitializeOnLoadMethod]
            private static void Init()
            {
                AssemblyReloadEvents.beforeAssemblyReload += CleanupTextures;
                EditorApplication.playModeStateChanged += (_) => CleanupTextures();

                playerColorSpace = PlayerSettings.colorSpace;
                NovaEditorEventManager.PlayerSettingsChanged += HandlePlayerSettingsChanged;

                SceneView.beforeSceneGui += (SceneView sceneView) =>
                {
                    if (!NovaEditorPrefs.HierarchyGizmosEnabled || !sceneView.drawGizmos)
                    {
                        return;
                    }

                    Color selectedColor = SceneView.selectedOutlineColor;
                    selectedColor.a = 1;

                    // Cache this so we don't need to re-parse per UIBlock
                    SceneViewInSelectionHiearchyColor = Utils.GetUnityEditorPrefColor("Scene/Selected Children Outline", selectedColor);
                };
            }


            public static Color SceneViewInSelectionHiearchyColor { get; private set; }

            public static readonly Color DarkThemeOverlay = new Color(1, 1, 1, 0.05f);
            public static readonly Color DarkThemeHelpBox = new Color(0, 0, 0, 0.075f);
            public static readonly Color LightThemeOverlay = new Color(0, 0, 0, 0.075f);
            public static readonly Color LightThemeHelpBox = new Color(1, 1, 1, 0.05f);

            private static Dictionary<Color, Texture2D> solidTextures = new Dictionary<Color, Texture2D>();

            private static void HandlePlayerSettingsChanged()
            {
                if (PlayerSettings.colorSpace == playerColorSpace)
                {
                    return;
                }

                playerColorSpace = PlayerSettings.colorSpace;
                CleanupTextures();
            }

            private static void CleanupTextures()
            {
                foreach (var texture in solidTextures)
                {
                    if (texture.Value == null)
                    {
                        continue;
                    }

                    DestroyUtils.SafeDestroy(texture.Value);
                }

                solidTextures.Clear();

                // clears stale cache state so it will get recreated
                SectionHeaderBackground = null;
            }

            public static Color OverlayColor => EditorGUIUtility.isProSkin ? DarkThemeOverlay : LightThemeOverlay;

            private static GUIStyle _innerContentStyle = null;
            public static GUIStyle InnerContent
            {
                get
                {
                    if (_innerContentStyle == null || _innerContentStyle.normal.background == null)
                    {
                        _innerContentStyle = new GUIStyle(Box);

                        _innerContentStyle.overflow = new RectOffset(18, 4, 0, 0);
                        _innerContentStyle.padding.left -= 18;
                        _innerContentStyle.margin.left -= 18;
                        _innerContentStyle.padding.top += 3;
                        _innerContentStyle.padding.bottom += 3;
                    }

                    return _innerContentStyle;
                }
            }

            private static GUIStyle _boxStyle = null;
            public static GUIStyle Box
            {
                get
                {
                    if (_boxStyle == null || _boxStyle.normal.background == null)
                    {
                        _boxStyle = new GUIStyle();

                        _boxStyle.normal = new GUIStyleState()
                        {
                            background = GetTexture(OverlayColor)
                        };
                    }

                    return _boxStyle;
                }
            }


            private static GUIStyle _helpBox = null;
            public static GUIStyle HelpBox
            {
                get
                {
                    if (_helpBox == null || _helpBox.normal.background == null)
                    {
                        _helpBox = new GUIStyle(GUIStyle.none);
                        _helpBox.padding = EditorStyles.helpBox.padding;
                        _helpBox.margin = EditorStyles.helpBox.margin;
                        _helpBox.overflow = EditorStyles.helpBox.overflow;
                        _helpBox.padding.right += 2;
                        _helpBox.padding.bottom += 2;

                        _helpBox.border = new RectOffset(-1, 0, 0, -1);
                    }

                    return _helpBox;
                }
            }

            private static GUIStyle _toolbarButtonLeft = null;
            public static GUIStyle ToolbarButtonLeft
            {
                get
                {
                    if (_toolbarButtonLeft == null)
                    {
                        _toolbarButtonLeft = new GUIStyle(EditorStyles.miniButtonLeft);
                        _toolbarButtonLeft.fontSize = 10;
                        _toolbarButtonLeft.alignment = TextAnchor.MiddleCenter;
                    }
                    return _toolbarButtonLeft;
                }
            }

            private static GUIStyle _miniButton = null;
            public static GUIStyle MiniButton
            {
                get
                {
                    if (_miniButton == null)
                    {
                        _miniButton = new GUIStyle(GUI.skin.button)
                        {
                            fontSize = 8,
                            fontStyle = FontStyle.Bold
                        };
                    }

                    return _miniButton;
                }
            }

            private static GUIStyle _toolbarButtonMid = null;
            public static GUIStyle ToolbarButtonMid
            {
                get
                {
                    if (_toolbarButtonMid == null)
                    {
                        _toolbarButtonMid = new GUIStyle(EditorStyles.miniButtonMid);
                        _toolbarButtonMid.fontSize = 10;
                        _toolbarButtonMid.alignment = TextAnchor.MiddleCenter;
                    }
                    return _toolbarButtonMid;
                }
            }

            private static GUIStyle _toolbarButtonRight = null;
            public static GUIStyle ToolbarButtonRight
            {
                get
                {
                    if (_toolbarButtonRight == null)
                    {
                        _toolbarButtonRight = new GUIStyle(EditorStyles.miniButtonRight);
                        _toolbarButtonRight.fontSize = 10;
                        _toolbarButtonRight.alignment = TextAnchor.MiddleCenter;
                    }
                    return _toolbarButtonRight;
                }
            }

            private static GUIStyle _yellowTextNumberField = null;
            public static GUIStyle YellowTextNumberField
            {
                get
                {
                    if (_yellowTextNumberField == null)
                    {
                        _yellowTextNumberField = new GUIStyle(EditorStyles.numberField);
                        _yellowTextNumberField.fontStyle = FontStyle.Bold;
                        _yellowTextNumberField.normal.textColor = Yellow_ish;
                        _yellowTextNumberField.active.textColor = Yellow_ish;
                        _yellowTextNumberField.hover.textColor = Yellow_ish;
                        _yellowTextNumberField.focused.textColor = Yellow_ish;

                        _yellowTextNumberField.onNormal.textColor = Yellow_ish;
                        _yellowTextNumberField.onActive.textColor = Yellow_ish;
                        _yellowTextNumberField.onHover.textColor = Yellow_ish;
                        _yellowTextNumberField.onFocused.textColor = Yellow_ish;
                    }

                    return _yellowTextNumberField;
                }
            }

            private static GUIStyle _blueTextNumberField = null;
            public static GUIStyle BlueTextNumberField
            {
                get
                {
                    if (_blueTextNumberField == null)
                    {
                        _blueTextNumberField = new GUIStyle(EditorStyles.numberField);
                        _blueTextNumberField.fontStyle = FontStyle.Bold;
                        _blueTextNumberField.normal.textColor = Blue_ish;
                        _blueTextNumberField.active.textColor = Blue_ish;
                        _blueTextNumberField.hover.textColor = Blue_ish;
                        _blueTextNumberField.focused.textColor = Blue_ish;

                        _blueTextNumberField.onNormal.textColor = Blue_ish;
                        _blueTextNumberField.onActive.textColor = Blue_ish;
                        _blueTextNumberField.onHover.textColor = Blue_ish;
                        _blueTextNumberField.onFocused.textColor = Blue_ish;
                    }

                    return _blueTextNumberField;
                }
            }

            private static GUIStyle _foldoutToggle = null;
            public static GUIStyle FoldoutToggle
            {
                get
                {
                    if (_foldoutToggle == null)
                    {
                        _foldoutToggle = new GUIStyle(EditorStyles.foldout);
                        _foldoutToggle.alignment = TextAnchor.MiddleLeft;
                    }

                    return _foldoutToggle;
                }
            }

            private static GUIStyle _redTextNumberField = null;
            public static GUIStyle RedTextNumberField
            {
                get
                {
                    if (_redTextNumberField == null)
                    {
                        _redTextNumberField = new GUIStyle(EditorStyles.numberField);
                        _redTextNumberField.fontStyle = FontStyle.Bold;
                        _redTextNumberField.normal.textColor = Red_ish;
                        _redTextNumberField.active.textColor = Red_ish;
                        _redTextNumberField.hover.textColor = Red_ish;
                        _redTextNumberField.focused.textColor = Red_ish;

                        _redTextNumberField.onNormal.textColor = Red_ish;
                        _redTextNumberField.onActive.textColor = Red_ish;
                        _redTextNumberField.onHover.textColor = Red_ish;
                        _redTextNumberField.onFocused.textColor = Red_ish;
                    }

                    return _redTextNumberField;
                }
            }


            /// <summary>
            /// This is a total hack, but it's somehow consistent with what Unity does internally.
            /// https://github.com/Unity-Technologies/UnityCsReference/blob/806e5e64bd16362aca5188fa99f3493a86457c4d/Editor/Mono/Settings/Providers/AssetSettingsProvider.cs#L135
            /// For those of you who are wondering, yes, GUI/EditorGUI is an *amazing* API
            /// </summary>
            public static GUIStyle MenuIconStyle => "IconButton";
            public static GUIContent MenuIcon => new GUIContent() { image = EditorStyles.foldoutHeaderIcon.normal.scaledBackgrounds[0] };

            // More GUI Style nonsense
            public static GUIStyle PadlockButtonStyle => "IN LockButton";
            public static GUIStyle FloatFieldLinkStyle => "FloatFieldLinkButton";

            private static GUIStyle _sectionHeaderStyle = null;
            public static GUIStyle SectionHeaderStyle
            {
                get
                {
                    if (_sectionHeaderStyle == null)
                    {
                        _sectionHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader);
                        _sectionHeaderStyle.margin = new RectOffset(-32, 0, 1, 2);
                    }
                    return _sectionHeaderStyle;
                }
            }

            private static readonly Color DarkThemeHeaderColor = new Color(0.18f, 0.18f, 0.18f);
            private static readonly Color LightThemeHeaderColor = new Color(0.65f, 0.65f, 0.65f);

            private static GUIStyle _sectionHeaderBackground = null;
            public static GUIStyle SectionHeaderBackground
            {
                get
                {
                    if (_sectionHeaderBackground == null || _sectionHeaderBackground.normal == null || _sectionHeaderBackground.normal.background == null)
                    {
                        _sectionHeaderBackground = new GUIStyle(GUIStyle.none);
                        _sectionHeaderBackground.overflow = new RectOffset(20, 8, 1, 1);
                        _sectionHeaderBackground.normal.background = GetTexture(EditorGUIUtility.isProSkin ? DarkThemeHeaderColor : LightThemeHeaderColor);
                    }
                    return _sectionHeaderBackground;
                }
                private set
                {
                    if (value != null)
                    {
                        Debug.LogWarning($"Invalid assingment of SectionHeaderBackground. Only can be set to null as a means of clearing.");

                        return;
                    }

                    _sectionHeaderBackground = null;
                }
            }

            private static GUIStyle[] _separatorStyles = null;
            private static GUIStyle[] SeparatorStyles
            {
                get
                {
                    if (_separatorStyles == null || _separatorStyles[0].normal.background == null)
                    {
                        _separatorStyles = new GUIStyle[2];
                        Color c1 = new Color(0, 0, 0, 0.2f);
                        Color c2 = new Color(0, 0, 0, 0.1f);

                        _separatorStyles[0] = new GUIStyle()
                        {
                            normal = new GUIStyleState()
                            {
                                background = GetTexture(c1),
                            }
                        };

                        _separatorStyles[1] = new GUIStyle()
                        {
                            normal = new GUIStyleState()
                            {
                                background = GetTexture(c2),
                            }
                        };
                    }

                    return _separatorStyles;
                }
            }

            private static GUIStyle colorRect = null;
            public static GUIStyle ColorRect
            {
                get
                {
                    if (colorRect == null)
                    {
                        colorRect = new GUIStyle()
                        {
                            normal = new GUIStyleState()
                        };
                    }

                    return colorRect;
                }
            }

            private static GUIStyle largeLink = null;
            public static GUIStyle LargeLink
            {
                get
                {
                    if (largeLink == null)
                    {
                        largeLink = new GUIStyle(EditorStyles.linkLabel);
                        largeLink.fontSize += 2;
                        largeLink.hover = new GUIStyleState()
                        {
                            textColor = EditorStyles.label.normal.textColor
                        };
                    }

                    return largeLink;
                }
            }

            private static GUIStyle paragraphLabel = null;
            public static GUIStyle ParagraphLabel
            {
                get
                {
                    if (paragraphLabel == null)
                    {
                        paragraphLabel = new GUIStyle(EditorStyles.label);

                        paragraphLabel.fontSize += 2;
                        paragraphLabel.wordWrap = true;
                        paragraphLabel.padding = new RectOffset(36, 36, 18, 18);
                    }

                    return paragraphLabel;
                }
            }

            private static GUIStyle largeHeader = null;
            public static GUIStyle LargeHeader
            {
                get
                {
                    if (largeHeader == null)
                    {
                        largeHeader = new GUIStyle(EditorStyles.boldLabel);

                        largeHeader.fontSize += 2;
                    }

                    return largeHeader;
                }
            }

            public static void Draw(Rect position, Color color)
            {
                if (Event.current == null || Event.current.type != EventType.Repaint)
                {
                    return;
                }

                ColorRect.normal.background = GetTexture(color);

                ColorRect.Draw(position, false, false, false, false);
            }

            public static void DrawSeparator(Rect controlRect, float separatorHeight = 1.5f, bool afterControl = true, bool useControlWidth = false)
            {
                if (Event.current.type != EventType.Repaint)
                {
                    return;
                }

                Rect line = controlRect;
                line.y = line.yMax;

                if (!useControlWidth)
                {
                    line.x = 0;
                    line.width = EditorGUIUtility.currentViewWidth;
                }

                int topIndex = afterControl ? 0 : 1;
                int bottomIndex = afterControl ? 1 : 0;

                float topLineHeight = afterControl ? separatorHeight * (1 / 3f) : separatorHeight * (2 / 3f);
                float bottomLineHeight = afterControl ? separatorHeight * (2 / 3f) : separatorHeight * (1 / 3f);

                line.height = topLineHeight;
                SeparatorStyles[topIndex].Draw(line, false, false, false, false);
                line.y += topLineHeight;
                line.height = bottomLineHeight;
                SeparatorStyles[bottomIndex].Draw(line, false, false, false, false);
            }

            public static Color Yellow_ish
            {
                get
                {
                    float saturation = EditorGUIUtility.isProSkin ? 0.5f : 1f;
                    float brightness = EditorGUIUtility.isProSkin ? 0.8f : 0.6f;

                    Color.RGBToHSV(Color.yellow, out float h, out float s, out float v);

                    return Color.HSVToRGB(h, saturation, brightness);
                }
            }

            public static Color Blue_ish
            {
                get
                {
                    const float saturation = 0.6f;
                    float brightness = EditorGUIUtility.isProSkin ? 1f : 0.8f;

                    Color.RGBToHSV(new Color(0.3f, 0.533f, 1), out float h, out float s, out float v);

                    return Color.HSVToRGB(h, saturation, brightness);
                }
            }

            public static Color Red_ish
            {
                get
                {
                    const float saturation = 0.7f;
                    float brightness = EditorGUIUtility.isProSkin ? 0.9f : 0.7f;

                    Color.RGBToHSV(new Color(1, 0.3f, 0.533f), out float h, out float s, out float v);

                    return Color.HSVToRGB(h, saturation, brightness);
                }
            }

            public static Color Green_ish
            {
                get
                {
                    const float saturation = 0.8f;
                    float brightness = 0.7f;

                    Color.RGBToHSV(new Color(0.3f, 1, 0.533f), out float h, out float s, out float v);

                    return Color.HSVToRGB(h, saturation, brightness);
                }
            }

            public static readonly Color NovaRed = new Color(0.8f, 0, 0.35f);
            public static readonly Color NovaBlue = new Color(0, 0.33f, 1);
            public static readonly Color NovaGreen = new Color(0, .45f, 0.25f);
            public static readonly Color NovaCyan = new Color(0, 0.4f, 0.4f);
            public static readonly Color Cyan_MoreBlue = new Color(0, 0.8f, 0.9f);
            public static readonly Color Cyan_MoreGreen = new Color(0, 0.9f, 0.8f);
            public static readonly Color Magenta_MoreBlue = new Color(0.6f, 0.4f, 0.9f);
            public static readonly Color Magenta_MoreWhite = new Color(1, 0.4f, 1);

            public static Texture2D GetTexture(Color color)
            {
                bool createTexture = !solidTextures.TryGetValue(color, out Texture2D texture) || texture == null;

                if (createTexture)
                {
                    texture = CreateTexture(color);
                    texture.name = $"NovaGUI.SolidTexture.{ColorUtility.ToHtmlStringRGBA(color)}";
                    solidTextures[color] = texture;
                }

                return texture;
            }

            private static Texture2D CreateTexture(Color color)
            {
                Texture2D texture = new Texture2D(2, 2);

                for (int y = 0; y < texture.height; ++y)
                {
                    for (int x = 0; x < texture.width; ++x)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }

                texture.Apply();

                return texture;
            }
        }

        public static class Layout
        {
            // The value to pass into Space(count) to adjust for foldout arrows
            public const float FoldoutArrowIndentSpace = 1.125f;

            public static float PrefixWidth => PrefixLabelWidth + MinSpaceBetweenFields;

            public static GUILayoutOption PrefixLabelWidthOption
            {
                get
                {
                    return GUILayout.Width(PrefixWidth);
                }
            }

            private static GUILayoutOption _foldoutArrowWidthOption = null;
            public static GUILayoutOption FoldoutArrowWidthOption
            {
                get
                {
                    if (_foldoutArrowWidthOption == null)
                    {
                        _foldoutArrowWidthOption = GUILayout.Width(Foldout.ArrowIconSize);
                    }

                    return _foldoutArrowWidthOption;
                }
            }

            private static GUILayoutOption __minFloatFieldWidthOption = null;
            public static GUILayoutOption MinFloatFieldWidthOption
            {
                get
                {
                    if (__minFloatFieldWidthOption == null)
                    {
                        __minFloatFieldWidthOption = GUILayout.MinWidth(MinFloatFieldWidth);
                    }

                    return __minFloatFieldWidthOption;
                }
            }


            public static Rect GetControlRect(params GUILayoutOption[] options)
            {
                Rect field = GetControlRect(hasLabel: false, EditorGUIUtility.singleLineHeight, options);
                return field;
            }

            public static Rect GetControlRect(bool hasLabel, float height, params GUILayoutOption[] options)
            {
                if (Event.current.type == EventType.Used)
                {
                    return new Rect(Vector2.zero, Vector2.one);
                }

                Rect field = EditorGUILayout.GetControlRect(hasLabel, height, options);

                return field;
            }

            public static void GetXYZFieldRects(bool includeZ, out Rect xRect, out Rect yRect, out Rect zRect)
            {
                Rect fieldRect = GetControlRect();

                float percentPerItem = includeZ ? 1f / 3f : 0.5f;
                float width = fieldRect.width * percentPerItem;

                xRect = fieldRect;
                xRect.width = width;

                yRect = xRect;
                yRect.x += width + MinSpaceBetweenFields;

                zRect = default;

                if (includeZ)
                {
                    zRect = yRect;
                    zRect.x += width + MinSpaceBetweenFields;
                }
            }

            public static Rect BeginHorizontal()
            {
                return EditorGUILayout.BeginHorizontal();
            }

            public static Rect BeginHorizontal(GUIStyle style)
            {
                return EditorGUILayout.BeginHorizontal(style);
            }

            public static void EndHorizontal()
            {
                EditorGUILayout.EndHorizontal();
            }

            public static Rect BeginVertical()
            {
                return EditorGUILayout.BeginVertical();
            }

            public static Rect BeginVertical(GUIStyle style)
            {
                return EditorGUILayout.BeginVertical(style);
            }

            public static void EndVertical()
            {
                EditorGUILayout.EndVertical();
            }
        }

        public static class Utils
        {
            public static Color GetUnityEditorPrefColor(string path, Color defaultColor)
            {
                string[] colorPaths = EditorPrefs.GetString(path, string.Empty).Split(';');

                if (colorPaths != null &&
                    colorPaths.Length == 5 &&
                    float.TryParse(colorPaths[1], out float r) &&
                    float.TryParse(colorPaths[2], out float g) &&
                    float.TryParse(colorPaths[3], out float b))
                {
                    return new Color(r, g, b);
                }

                return defaultColor;
            }
        }

        public static void WarningIcon(string tooltip)
        {
            // Request value with size 0, so we have the lightest impact on shifting all the other objects.
            // Still moves things around a bit, which is why we offset a negative amount below.
            Rect position = NovaGUI.Layout.GetControlRect(GUILayout.Width(0), GUILayout.Height(0));

            WarningIcon(position, tooltip);

            EditorGUILayout.Space(-3);
        }

        public static void WarningIcon(Rect position, string tooltip)
        {
            position.x -= NovaGUI.IconSize;
            position.y += NovaGUI.MinSpaceBetweenFields;
            position.width = NovaGUI.IconSize;
            position.height = NovaGUI.IconSize;

            EditorGUI.LabelField(position, new GUIContent(Labels.WarningIcon) { tooltip = tooltip });
        }

        public static void LinkLabel(Rect position, GUIContent label, string url, bool largeLink = true)
        {
            if (LinkButton(position, label, largeLink))
            {
                System.Diagnostics.Process.Start(url);
            }
        }

        public static void LinkLabel(Rect position, string label, string url, bool largeLink = true)
        {
            LinkLabel(position, EditorGUIUtility.TrTempContent(label), url, largeLink);
        }

        public static bool LinkButton(Rect position, string label, bool largeLink = true)
        {
            return LinkButton(position, EditorGUIUtility.TrTempContent(label), largeLink);
        }

        public static bool LinkButton(Rect position, GUIContent label, bool largeLink = true)
        {
            EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

            GUIStyle labelStyle = largeLink ? Styles.LargeLink : EditorStyles.linkLabel;

            Vector2 size = labelStyle.CalcSize(label);
            position.width = size.x;
            position.height = size.y;

            Handles.color = labelStyle.normal.textColor;
            Handles.DrawLine(new Vector3(position.xMin + (float)labelStyle.padding.left, position.yMax), new Vector3(position.xMax - (float)labelStyle.padding.right, position.yMax));
            Handles.color = Color.white;

            return GUI.Button(position, label, labelStyle);
        }

        public static void PrefixLabel(GUIContent label)
        {
            Rect fieldRect = Layout.GetControlRect(Layout.PrefixLabelWidthOption);
            EditorGUI.LabelField(fieldRect, label);
        }

        public static void PrefixLabel(GUIContent label, SerializedProperty property)
        {
            Rect fieldRect = Layout.GetControlRect(Layout.PrefixLabelWidthOption);
            GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, property);
            EditorGUI.LabelField(fieldRect, propertyLabel);
            EditorGUI.EndProperty();
        }

        public static bool PrefixFoldoutLabel(GUIContent label, bool foldout, SerializedProperty property)
        {
            Rect fieldRect = Layout.GetControlRect(Layout.PrefixLabelWidthOption);
            EditorGUI.BeginDisabledGroup(false);
            foldout = Foldout.FoldoutToggle(fieldRect, foldout);
            GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, property);
            EditorGUI.LabelField(fieldRect, propertyLabel);
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndProperty();

            return foldout;
        }

        public static bool PrefixFoldout(bool foldout)
        {
            // Grab a rect for the foldout arrow and label the label
            // is offset by default, so they won't draw on top of eachother
            Rect fieldRect = Layout.GetControlRect(Layout.FoldoutArrowWidthOption);
            EditorGUI.BeginDisabledGroup(false);
            foldout = Foldout.FoldoutToggle(fieldRect, foldout);
            EditorGUI.EndDisabledGroup();

            return foldout;
        }

        public static Foldout EditorPrefFoldoutHeader(string labelKey, System.Action<Rect> dropdownMenu = null, string displayName = null)
        {
            string prefKey = NovaEditorPrefs.GetFullEditorPrefPath(labelKey);
            bool currentVal = EditorPrefs.GetBool(prefKey, false);
            Foldout foldout = Foldout.DoHeaderGroup(currentVal, string.IsNullOrEmpty(displayName) ? labelKey : displayName, dropdownMenu);

            if (foldout != currentVal)
            {
                EditorPrefs.SetBool(prefKey, foldout);
            }
            return foldout;
        }

        public static Foldout EditorPrefFoldoutHeader(string label, SerializedProperty enabledProperty)
        {
            string prefKey = NovaEditorPrefs.GetFullEditorPrefPath(label);
            bool currentVal = EditorPrefs.GetBool(prefKey, false);
            Foldout foldout = Foldout.DoHeaderGroup(currentVal, label, enabledProperty);

            if (foldout != currentVal)
            {
                EditorPrefs.SetBool(prefKey, foldout);
            }
            return foldout;
        }

        public static void ColorField(GUIContent label, SerializedProperty prop, bool showAlpha = true)
        {
            ColorField(Layout.GetControlRect(), label, prop, showAlpha);
        }

        public static void ColorField(Rect fieldRect, GUIContent label, SerializedProperty prop, bool showAlpha = true)
        {
            GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, prop);

            EditorGUI.BeginChangeCheck();
            Color color = ColorField(fieldRect, propertyLabel, prop.colorValue, prop.hasMultipleDifferentValues, showAlpha);
            if (EditorGUI.EndChangeCheck())
            {
                prop.colorValue = color;
            }

            EditorGUI.EndProperty();
        }

        public static Color ColorField(Rect fieldRect, GUIContent label, Color color, bool showMixed, bool showAlpha = true)
        {
            float labelWidth = LabelWidth;

            Rect labelRect = fieldRect;

            float opacityLabelWidth = showMixed ? -MinSpaceBetweenFields : EditorStyles.label.CalcSize(Labels.Opacity).x;

            if (!string.IsNullOrWhiteSpace(label.text))
            {
                labelRect.width = LabelWidth - opacityLabelWidth;
                fieldRect.xMin = labelRect.xMax;
            }

            EditorGUI.LabelField(labelRect, label);

            Rect alphaField = fieldRect;
            alphaField.width = 0;

            Rect colorRect = fieldRect;

            if (!showMixed)
            {   
                LabelWidth = opacityLabelWidth;
                alphaField.width = 2 * WideCharacterGUIWidth + EditorStyles.numberField.padding.horizontal + opacityLabelWidth;
                colorRect.xMin = alphaField.xMax - MinSpaceBetweenFields;
            }

            // For some reason, conditionally drawing this IntField only when showMixed == false
            // can put the ColorPicker into a bad state on the frame showMixed changes from
            // true to false. Don't know why, but Unity just stops reporting color changes from the
            // ColorPicker in that bad state. So instead of not drawing it, we put it in a disabled
            // scope and the alphaField rect will have a width of 0. If the rect has a non-zero
            // width, Unity will still change the mouse cursor when the mouse hovers over the
            // color field (making it seem like you're interacting with a hidden IntField)
            // - despite this being in a disabled scope.
            EditorGUI.BeginDisabledGroup(showMixed);
            color.a = Mathf.Clamp01(EditorGUI.IntField(alphaField, showMixed ? GUIContent.none : Labels.Opacity, Mathf.RoundToInt(100 * color.a)) / 100f);
            EditorGUI.EndDisabledGroup();

            bool wasMixed = EditorGUI.showMixedValue;

            EditorGUI.showMixedValue = showMixed;

            color = EditorGUI.ColorField(colorRect, GUIContent.none, color, showEyedropper: true, showAlpha: showAlpha, hdr: false);

            EditorGUI.showMixedValue = wasMixed;

            LabelWidth = labelWidth;

            return color;
        }

        public static void SliderField(GUIContent label, SerializedProperty property, float min = 0, float max = 1)
        {
            Rect sliderField = Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            GUIContent sliderLabel = EditorGUI.BeginProperty(sliderField, label, property);
            float sliderValue = EditorGUI.Slider(sliderField, sliderLabel, property.floatValue, min, max);
            EditorGUI.EndProperty();

            if (EditorGUI.EndChangeCheck())
            {
                property.floatValue = sliderValue;
            }
        }

        public static void Toggle3DField(SerializedProperty threeDBoolProp, GUIContent label)
        {
            bool wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            Rect fieldRect = Layout.GetControlRect();
            GUIContent fieldLabel = EditorGUI.BeginProperty(fieldRect, label, threeDBoolProp);
            EditorGUI.MultiPropertyField(fieldRect, Labels.XYZ, threeDBoolProp.FindPropertyRelative("X"), fieldLabel);
            EditorGUI.EndProperty();
            EditorGUIUtility.wideMode = wideMode;
        }

        public static void ToggleField(GUIContent label, SerializedProperty boolField)
        {
            ToggleField(Layout.GetControlRect(), label, boolField);
        }

        public static void ToggleField(Rect rect, GUIContent label, SerializedProperty boolField)
        {
            EditorGUI.BeginChangeCheck();
            GUIContent labelContent = EditorGUI.BeginProperty(rect, label, boolField);
            bool newValue = EditorGUI.Toggle(rect, labelContent, boolField.boolValue);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                boolField.boolValue = newValue;
            }
        }

        public static void Space(float count = 1)
        {
            GUILayout.Space(count * IndentSize);
        }

        public static void FloatField(GUIContent label, SerializedProperty property)
        {
            Rect fieldRect = Layout.GetControlRect(Layout.MinFloatFieldWidthOption);
            GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, property);
            EditorGUI.BeginChangeCheck();
            float value = EditorGUI.FloatField(fieldRect, propertyLabel, property.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.floatValue = value;
            }
            EditorGUI.EndProperty();
        }

        public static void FloatFieldClamped(GUIContent label, SerializedProperty property, float min, float max)
        {
            Rect fieldRect = Layout.GetControlRect(Layout.MinFloatFieldWidthOption);
            GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, property);
            EditorGUI.BeginChangeCheck();
            float value = EditorGUI.FloatField(fieldRect, propertyLabel, property.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.floatValue = Mathf.Clamp(value, min, max);
            }
            EditorGUI.EndProperty();
        }

        public static void IntFieldClamped(GUIContent label, SerializedProperty property, int min, int max)
        {
            Rect fieldRect = Layout.GetControlRect(Layout.MinFloatFieldWidthOption);
            GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, property);
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.IntField(fieldRect, propertyLabel, property.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = Mathf.Clamp(value, min, max);
            }
            EditorGUI.EndProperty();
        }

        public static void IntSlider(GUIContent label, SerializedProperty serializedProperty, int min, int max)
        {
            EditorGUI.BeginChangeCheck();
            Rect rect = NovaGUI.Layout.GetControlRect();
            GUIContent labelContent = EditorGUI.BeginProperty(rect, label, serializedProperty);
            int newVal = EditorGUI.IntSlider(rect, labelContent, serializedProperty.intValue, min, max);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                serializedProperty.intValue = newVal;
            }
        }

        public static void Vector3Field(GUIContent label, SerializedProperty property, ThreeD<bool> disabled = default)
        {
            float labelWidth = LabelWidth;

            Layout.BeginHorizontal();
            PrefixLabel(label, property);

            LabelWidth = SingleCharacterGUIWidth;

            EditorGUI.BeginDisabledGroup(disabled.X);
            FloatField(Labels.X, property.FindPropertyRelative("x"));
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(disabled.Y);
            FloatField(Labels.Y, property.FindPropertyRelative("y"));
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(disabled.Z);
            FloatField(Labels.Z, property.FindPropertyRelative("z"));
            EditorGUI.EndDisabledGroup();

            Layout.EndHorizontal();

            LabelWidth = labelWidth;
        }

        public static void Vector2Field(GUIContent label, SerializedProperty property, TwoD<bool> disabled = default)
        {
            float labelWidth = LabelWidth;

            Layout.BeginHorizontal();
            PrefixLabel(label, property);

            LabelWidth = SingleCharacterGUIWidth;

            EditorGUI.BeginDisabledGroup(disabled.X);
            FloatField(Labels.X, property.FindPropertyRelative("x"));
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(disabled.Y);
            FloatField(Labels.Y, property.FindPropertyRelative("y"));
            EditorGUI.EndDisabledGroup();

            Layout.EndHorizontal();

            LabelWidth = labelWidth;
        }

        public static void Length2Field(GUIContent label, _Length2 length, Length2.Calculated calc, Vector2 min, Vector2 max)
        {
            NovaGUI.Layout.BeginHorizontal();
            PrefixLabel(label);

            NovaGUI.Layout.GetXYZFieldRects(false, out Rect x, out Rect y, out Rect _);
            float labelWidth = LabelWidth;
            LabelWidth = SingleCharacterGUIWidth;
            LengthField(x, Labels.X, length.X, calc.X, min: min.x, max: max.x);
            LengthField(y, Labels.Y, length.Y, calc.Y, min: min.y, max: max.y);
            LabelWidth = labelWidth;
            NovaGUI.Layout.EndHorizontal();
        }

        public static bool Length3Field(GUIContent label, _Length3 lengths, _MinMax3 minMax, Length3.Calculated calc, ThreeD<bool> disabled, bool zField, bool showRange)
        {
            float labelWidth = LabelWidth;

            Layout.BeginHorizontal();

            showRange = PrefixFoldoutLabel(label, showRange, lengths.SerializedProperty);

            Layout.GetXYZFieldRects(zField, out Rect x, out Rect y, out Rect z);

            float lengthTypeFieldWidth = zField && CompactViewMode ? MinFloatFieldWidth : ToggleToolbarFieldWidth;

            LabelWidth = SingleCharacterGUIWidth;
            EditorGUI.BeginDisabledGroup(disabled.X);
            LengthField(x, Labels.X, lengths.X, calc.X, minMax.X.Min, minMax.X.Max, lengthTypeFieldWidth);
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(disabled.Y);
            LengthField(y, Labels.Y, lengths.Y, calc.Y, minMax.Y.Min, minMax.Y.Max, lengthTypeFieldWidth);
            EditorGUI.EndDisabledGroup();

            if (zField)
            {
                EditorGUI.BeginDisabledGroup(disabled.Z);
                LengthField(z, Labels.Z, lengths.Z, calc.Z, minMax.Z.Min, minMax.Z.Max, lengthTypeFieldWidth);
                EditorGUI.EndDisabledGroup();
            }
            Layout.EndHorizontal();

            if (showRange)
            {
                EditorGUILayout.Space(1);
                Length3RangeField(lengths, minMax, calc, zField);
            }

            LabelWidth = labelWidth;

            return showRange;
        }

        public static void Length3RangeField(_Length3 lengths, _MinMax3 minMax, Length3.Calculated calc, bool zField)
        {
            Styles.DrawSeparator(GUILayoutUtility.GetLastRect());

            float labelWidth = LabelWidth;

            GUIStyle style = new GUIStyle(Styles.Box);
            style.overflow.left = Mathf.RoundToInt(EditorGUIUtility.currentViewWidth);
            style.overflow.right = 8;

            Layout.BeginHorizontal(style);
            LabelWidth = SingleCharacterGUIWidth;
            LengthRangeField(Labels.X, lengths.X, minMax.X, calc.X, lengths.X.Type == LengthType.Value);
            LengthRangeField(Labels.Y, lengths.Y, minMax.Y, calc.Y, lengths.Y.Type == LengthType.Value);

            if (zField)
            {
                LengthRangeField(Labels.Z, lengths.Z, minMax.Z, calc.Z, lengths.Z.Type == LengthType.Value);
            }

            LabelWidth = labelWidth;

            Layout.EndHorizontal();
        }

        public static void EnumFlagsField<T>(GUIContent label, SerializedProperty serializedProperty, T current) where T : System.Enum
        {
            EditorGUI.BeginChangeCheck();
            Rect rect = NovaGUI.Layout.GetControlRect();
            GUIContent labelContent = EditorGUI.BeginProperty(rect, label, serializedProperty);
            System.Enum newVal = EditorGUI.EnumFlagsField(rect, labelContent, current);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                serializedProperty.intValue = System.Convert.ToInt32(newVal);
            }
        }

        public static void EnumField<T>(GUIContent label, SerializedProperty serializedProperty, T current) where T : System.Enum
        {
            EditorGUI.BeginChangeCheck();
            Rect rect = NovaGUI.Layout.GetControlRect();
            GUIContent labelContent = EditorGUI.BeginProperty(rect, label, serializedProperty);
            System.Enum newVal = EditorGUI.EnumPopup(rect, labelContent, current);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                serializedProperty.intValue = System.Convert.ToInt32(newVal);
            }
        }

        public static (bool, bool) LengthBoundsField(GUIContent label, _LengthBounds lengths, _MinMaxBounds minMax, LengthBounds.Calculated calc, bool zField, bool showSides, bool showRange)
        {
            LengthBounds bounds = new LengthBounds()
            {
                Left = new Length(lengths.Left.Raw, lengths.Left.Type),
                Right = new Length(lengths.Right.Raw, lengths.Right.Type),
                Top = new Length(lengths.Top.Raw, lengths.Top.Type),
                Bottom = new Length(lengths.Bottom.Raw, lengths.Bottom.Type),
                Front = new Length(lengths.Front.Raw, lengths.Front.Type),
                Back = new Length(lengths.Back.Raw, lengths.Back.Type),
            };

            MinMaxBounds range = new MinMaxBounds()
            {
                Left = new MinMax(minMax.Left.Min, minMax.Left.Max),
                Right = new MinMax(minMax.Right.Min, minMax.Right.Max),
                Top = new MinMax(minMax.Top.Min, minMax.Top.Max),
                Bottom = new MinMax(minMax.Bottom.Min, minMax.Bottom.Max),
                Front = new MinMax(minMax.Front.Min, minMax.Front.Max),
                Back = new MinMax(minMax.Back.Min, minMax.Back.Max),
            };

            bool showMixed = zField ? bounds.HasAsymmeticalSides() : bounds.XY.HasAsymmetricalSides();

            Layout.BeginVertical();
            Rect fieldRect = Layout.GetControlRect();
            showSides = Foldout.FoldoutToggle(fieldRect, showSides);

            EditorGUI.BeginChangeCheck();
            GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, lengths.SerializedProperty);
            Length all = LengthFieldAlternate(fieldRect, propertyLabel, bounds.Left, range.Left, calc.Left, showMixed, showClampIndicators: false);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                _Length length = lengths.Left;

                length.Raw = all.Raw;
                length.Type = all.Type;

                length = lengths.Right;

                length.Raw = all.Raw;
                length.Type = all.Type;

                length = lengths.Top;

                length.Raw = all.Raw;
                length.Type = all.Type;

                length = lengths.Bottom;

                length.Raw = all.Raw;
                length.Type = all.Type;

                if (zField)
                {
                    length = lengths.Front;

                    length.Raw = all.Raw;
                    length.Type = all.Type;

                    length = lengths.Back;

                    length.Raw = all.Raw;
                    length.Type = all.Type;
                }
            }

            if (showSides)
            {
                float lengthTypeFieldWidth = CompactViewMode ? MinFloatFieldWidth : ToggleToolbarFieldWidth;
                float labelWidth = LabelWidth;

                EditorGUILayout.Space(1);

                Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                Layout.BeginVertical(Styles.InnerContent);
                Layout.BeginHorizontal();
                Space(0.5f);
                using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                {
                    Layout.BeginHorizontal();
                    Layout.BeginVertical();
                    Layout.GetXYZFieldRects(zField, out Rect left, out Rect top, out Rect front);
                    Layout.GetXYZFieldRects(zField, out Rect right, out Rect bottom, out Rect back);
                    Layout.EndVertical();
                    Layout.EndHorizontal();

                    LabelWidth = MiniLabelWidth - 10;
                    LengthField(left, Labels.Left, lengths.Left, calc.Left, minMax.Left.Min, minMax.Left.Max, lengthTypeFieldWidth);
                    LengthField(right, Labels.Right, lengths.Right, calc.Right, minMax.Right.Min, minMax.Right.Max, lengthTypeFieldWidth);
                    LabelWidth = MiniLabelWidth;
                    LengthField(top, Labels.Top, lengths.Top, calc.Top, minMax.Top.Min, minMax.Top.Max, lengthTypeFieldWidth);
                    LengthField(bottom, Labels.Bottom, lengths.Bottom, calc.Bottom, minMax.Bottom.Min, minMax.Bottom.Max, lengthTypeFieldWidth);

                    if (zField)
                    {
                        LabelWidth = MiniLabelWidth - 10;
                        LengthField(front, Labels.Front, lengths.Front, calc.Front, minMax.Front.Min, minMax.Front.Max, lengthTypeFieldWidth);
                        LengthField(back, Labels.Back, lengths.Back, calc.Back, minMax.Back.Min, minMax.Back.Max, lengthTypeFieldWidth);
                    }

                    LabelWidth = labelWidth;
                }
                Layout.EndHorizontal();

                EditorGUILayout.Space(1);

                Layout.BeginHorizontal();
                Space(1.25f);
                fieldRect = Layout.GetControlRect(GUILayout.Width(PrefixLabelWidth + 2 * ToggleBoxSize));

                EditorGUI.BeginChangeCheck();
                GUIContent minMaxLabel = EditorGUI.BeginProperty(fieldRect, Labels.MinMax, minMax.SerializedProperty);
                showRange = Foldout.FoldoutToggle(fieldRect, showRange);
                EditorGUI.LabelField(fieldRect, minMaxLabel);

                float4 xyMin = new float4(range.Left.Min, range.Right.Min, range.Top.Min, range.Bottom.Min);
                float2 zMin = new float2(range.Front.Min, range.Back.Min);

                float4 xyMax = new float4(range.Left.Max, range.Right.Max, range.Top.Max, range.Bottom.Max);
                float2 zMax = new float2(range.Front.Max, range.Back.Max);

                bool mixedClampMin = zField ? math.any(math.isfinite(xyMin) | math.isfinite(zMin).xxyy) && math.any(math.isinf(xyMin) | math.isinf(zMin).xxyy) : math.any(math.isfinite(xyMin)) && math.any(math.isinf(xyMin));
                bool mixedClampMax = zField ? math.any(math.isfinite(xyMax) | math.isfinite(zMax).xxyy) && math.any(math.isinf(xyMax) | math.isinf(zMin).xxyy) : math.any(math.isfinite(xyMax)) && math.any(math.isinf(xyMax));
                bool mixedMin = zField ? range.HasAsymmetricalMin() : range.XY.HasAsymmetricalMin();
                bool mixedMax = zField ? range.HasAsymmetricalMax() : range.XY.HasAsymmetricalMax();
                bool4 mixed = new bool4(mixedClampMin, mixedMin, mixedClampMax, mixedMax);

                MinMax rangeAll = LengthRangeField(GUIContent.none, bounds.Left, range.Left, calc.Left, false, horizontal: true, mixed);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    _MinMax lengthRange = minMax.Left;
                    _Length length = lengths.Left;
                    lengthRange.Min = rangeAll.Min;
                    lengthRange.Max = rangeAll.Max;

                    lengthRange = minMax.Right;
                    length = lengths.Right;
                    lengthRange.Min = rangeAll.Min;
                    lengthRange.Max = rangeAll.Max;

                    lengthRange = minMax.Top;
                    length = lengths.Top;
                    lengthRange.Min = rangeAll.Min;
                    lengthRange.Max = rangeAll.Max;

                    lengthRange = minMax.Bottom;
                    length = lengths.Bottom;
                    lengthRange.Min = rangeAll.Min;
                    lengthRange.Max = rangeAll.Max;

                    if (zField)
                    {
                        lengthRange = minMax.Front;
                        length = lengths.Front;
                        lengthRange.Min = rangeAll.Min;
                        lengthRange.Max = rangeAll.Max;

                        lengthRange = minMax.Back;
                        length = lengths.Back;
                        lengthRange.Min = rangeAll.Min;
                        lengthRange.Max = rangeAll.Max;
                    }
                }

                Layout.EndHorizontal();

                if (showRange)
                {
                    Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                    Layout.BeginHorizontal(Styles.InnerContent);
                    Space(3);
                    LengthBoundsRangeField(lengths, minMax, calc, zField);
                    Layout.EndHorizontal();
                }

                Layout.EndVertical();
            }

            Layout.EndVertical();

            return (showSides, showRange);
        }

        public static void LengthBoundsRangeField(_LengthBounds lengths, _MinMaxBounds minMax, LengthBounds.Calculated calc, bool zField)
        {
            float labelWidth = LabelWidth;

            Layout.BeginHorizontal();
            Layout.BeginVertical();
            LabelWidth = MiniLabelWidth - 10;
            LengthRangeField(Labels.Left, lengths.Left, minMax.Left, calc.Left, lengths.Left.Type == LengthType.Value);
            LengthRangeField(Labels.Right, lengths.Right, minMax.Right, calc.Right, lengths.Right.Type == LengthType.Value);
            Layout.EndVertical();
            Layout.BeginVertical();
            LabelWidth = MiniLabelWidth;
            LengthRangeField(Labels.Top, lengths.Top, minMax.Top, calc.Top, lengths.Top.Type == LengthType.Value);

            Rect separator = GUILayoutUtility.GetLastRect();
            separator.y += separator.height + MinSpaceBetweenFields;
            separator.height = 1;
            separator.width = EditorGUIUtility.currentViewWidth;
            separator.x = 0;

            Styles.Draw(separator, Styles.OverlayColor);

            LengthRangeField(Labels.Bottom, lengths.Bottom, minMax.Bottom, calc.Bottom, lengths.Bottom.Type == LengthType.Value);
            Layout.EndVertical();
            if (zField)
            {
                Layout.BeginVertical();
                LabelWidth = MiniLabelWidth - 10;
                LengthRangeField(Labels.Front, lengths.Front, minMax.Front, calc.Front, lengths.Front.Type == LengthType.Value);
                LengthRangeField(Labels.Back, lengths.Back, minMax.Back, calc.Back, lengths.Back.Type == LengthType.Value);
                Layout.EndVertical();
            }
            Layout.EndHorizontal();

            LabelWidth = labelWidth;
        }

        public static void AspectRatioField(_Layout layout, UIBlock uiBlock, GUIContent[] labels)
        {
            float fieldWidth = FieldWidth;

            Layout.BeginHorizontal();
            Rect lockAspectRect = Layout.GetControlRect();
            lockAspectRect.width *= 0.5f;
            lockAspectRect.x += (0.5f * lockAspectRect.width) - (ToggleBoxSize + MinSpaceBetweenFields) * 0.5f;

            Rect propertyRect = lockAspectRect;
            propertyRect.x = 0;
            propertyRect.width = fieldWidth;

            EditorGUI.BeginProperty(propertyRect, GUIContent.none, layout.AspectRatioAxisProp);
            EditorGUI.BeginChangeCheck();
            Rect lockToggle = lockAspectRect;
            lockToggle.width = ToggleBoxSize + MinSpaceBetweenFields;
            bool locked = layout.AspectRatioAxis != Axis.None;
            bool aspectRatioLocked = GUI.Toggle(lockToggle, locked, new GUIContent() { tooltip = locked ? "Unlock Aspect Ratio" : "Lock Aspect Ratio" }, Styles.FloatFieldLinkStyle);
            if (EditorGUI.EndChangeCheck())
            {
                layout.AspectRatioAxis = !aspectRatioLocked ? Axis.None : Axis.X;

                if (layout.AspectRatioAxis != Axis.None)
                {
                    // hard coded to axis index 0 because we just set axis to X
                    layout.AspectRatio = uiBlock.CalculatedSize[0].Value == 0 ? Vector3.zero : uiBlock.CalculatedSize.Value / uiBlock.CalculatedSize[0].Value;
                }
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(!aspectRatioLocked);
            lockAspectRect.width -= lockToggle.width;
            lockAspectRect.x += lockToggle.width;
            float buttonWidth = ShowZAxisValues(uiBlock) ? lockAspectRect.width / 3f : lockAspectRect.width / 2f;
            int axis = Toolbar(lockAspectRect, layout.AspectRatioAxis.Index(), labels, buttonWidth);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                layout.AspectRatioAxis = !aspectRatioLocked ? Axis.None : (Axis)(axis + 1);

                if (layout.AspectRatioAxis != Axis.None)
                {
                    layout.AspectRatio = uiBlock.CalculatedSize.Value / uiBlock.CalculatedSize[axis].Value;
                }
            }
            EditorGUI.EndProperty();
            Layout.EndHorizontal();
        }

        public static Length LengthFieldAlternate(Rect position, GUIContent label, Length length, MinMax minMax, Length.Calculated calc, bool showMixedValue, bool showClampIndicators)
        {
            bool wasShowingMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = showMixedValue;

            bool clampedBoth = minMax.Min == minMax.Max;
            bool clampedMin = length.Raw == minMax.Min;
            bool clampedMax = length.Raw == minMax.Max;

            GUIStyle floatFieldStyle = showClampIndicators ?
                                       clampedBoth ? Styles.RedTextNumberField :
                                       clampedMin ? Styles.YellowTextNumberField :
                                       clampedMax ? Styles.BlueTextNumberField :
                                       EditorStyles.numberField : EditorStyles.numberField;

            float typeFieldWidth = ViewWidth < CompactInspectorWidth ? MinFloatFieldWidth : ToggleToolbarFieldWidth;

            Rect floatField = position;
            floatField.width = Mathf.Max(MinFloatFieldWidth, floatField.width - typeFieldWidth) - MinSpaceBetweenFields;
            EditorGUI.BeginChangeCheck();
            float raw = length.Raw;
            float fieldValue = float.IsNaN(raw) ? 0 : length.Type == LengthType.Value ? raw : raw * 100;
            fieldValue = EditorGUI.FloatField(floatField, label, fieldValue, floatFieldStyle);
            raw = fieldValue / (length.Type == LengthType.Value ? 1 : 100);
            bool rawValueChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            Rect lengthTypeField = floatField;
            lengthTypeField.width = typeFieldWidth;
            lengthTypeField.x += floatField.width + MinSpaceBetweenFields;
            LengthType newType = LengthTypeField(lengthTypeField, length.Type);
            bool typeChanged = EditorGUI.EndChangeCheck();

            EditorGUI.showMixedValue = wasShowingMixed;

            if (typeChanged)
            {
                length.Type = newType;
                length.Raw = length.Type == LengthType.Value ? calc.Value : calc.Percent;
            }

            if (rawValueChanged)
            {
                length.Raw = raw;
            }

            if (length.Type == LengthType.Value)
            {
                length.Raw = minMax.Clamp(length.Raw);
            }

            return length;
        }

        public static void LengthField(Rect position, GUIContent label, _Length length, Length.Calculated calculated, float min = float.NegativeInfinity, float max = float.PositiveInfinity, float typeFieldWidth = ToggleToolbarFieldWidth)
        {
            EditorGUI.BeginDisabledGroup(SwapLengths);
            bool clampedBoth = min == max;
            bool clampedMin = length.Raw <= min;
            bool clampedMax = length.Raw >= max;

            GUIStyle floatFieldStyle = length.Type == LengthType.Value ?
                                       clampedBoth ? Styles.RedTextNumberField :
                                       clampedMin ? Styles.YellowTextNumberField :
                                       clampedMax ? Styles.BlueTextNumberField :
                                       EditorStyles.numberField : EditorStyles.numberField;

            GUIContent propertyLabel = EditorGUI.BeginProperty(position, label, length.SerializedProperty);
            Rect floatField = position;
            floatField.width = Mathf.Max(MinFloatFieldWidth, floatField.width - typeFieldWidth) - MinSpaceBetweenFields;
            EditorGUI.BeginChangeCheck();
            float raw = length.Raw;

            float fieldValue = 0;
            if (!float.IsNaN(raw))
            {
                if (SwapLengths)
                {
                    fieldValue = length.Type == LengthType.Value ? calculated.Percent * 100f : calculated.Value;
                }
                else
                {
                    fieldValue = length.Type == LengthType.Value ? raw : raw * 100;
                }
            }

            fieldValue = EditorGUI.FloatField(floatField, propertyLabel, fieldValue, floatFieldStyle);
            raw = fieldValue / (length.Type == LengthType.Value ? 1 : 100);
            bool rawValueChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            Rect lengthTypeField = floatField;
            lengthTypeField.width = typeFieldWidth;
            lengthTypeField.x += floatField.width + MinSpaceBetweenFields;
            
            // yuck
            LengthType newType = LengthTypeField(lengthTypeField, !SwapLengths ? length.TypeProp.hasMultipleDifferentValues ? (LengthType)(-1) : length.Type : (length.Type == LengthType.Value ? LengthType.Percent : LengthType.Value));
            
            bool typeChanged = EditorGUI.EndChangeCheck();
            EditorGUI.EndProperty();

            if (typeChanged)
            {
                SetLengthType(length, newType);
            }

            if (rawValueChanged)
            {
                length.Raw = raw;
            }

            EditorGUI.EndDisabledGroup();
        }

        public static void SetLengthType(_Length length, LengthType type)
        {
            Object[] targets = length.SerializedProperty.serializedObject.targetObjects;

            for (int i = 0; i < targets.Length; ++i)
            {
                float rawLength = CalculatedLengthFromPropertyPath(targets[i] as UIBlock, length.SerializedProperty.propertyPath, type);
                SetRawLength(targets[i], length.RawProp.propertyPath, rawLength);
            }

            // The rendering properties get the calculated values on the fly, so we need to update the
            // length type *after* the raw value is updated. But we need to call UpdateIfRequiredOrScript
            // so that we don't overwrite the float value which is set above
            length.SerializedProperty.serializedObject.UpdateIfRequiredOrScript();
            length.Type = type;
            length.SerializedProperty.serializedObject.ApplyModifiedProperties();
        }

        public static void SetRawLength(Object target, string rawPropertyPath, float rawValue)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty rawProperty = so.FindProperty($"{rawPropertyPath}");

            rawProperty.floatValue = rawValue;

            rawProperty.serializedObject.ApplyModifiedProperties();
        }

        public static T Toolbar<T>(Rect position, T selected, GUIContent[] labels, float buttonWidth = 2 * SingleCharacterGUIWidth) where T : struct, System.Enum
        {
            Rect rect = position;
            rect.width = buttonWidth;
            rect.height = EditorGUIUtility.singleLineHeight;

            string[] values = System.Enum.GetNames(typeof(T));
            T selectedValue = selected;
            for (int i = 0; i < values.Length; ++i)
            {
                if (i >= labels.Length)
                {
                    break;
                }

                if (!System.Enum.TryParse(values[i], out T value))
                {
                    continue;
                }

                GUIStyle style = i == 0 ? Styles.ToolbarButtonLeft : i == labels.Length - 1 ? Styles.ToolbarButtonRight : Styles.ToolbarButtonMid;
                bool isSelected = value.Equals(selected);
                bool on = GUI.Toggle(rect, isSelected, labels[i], style: style);

                rect.x += buttonWidth;

                if (on && !isSelected)
                {
                    selectedValue = value;

                    if (EditorGUIUtility.editingTextField)
                    {
                        EditorGUI.FocusTextInControl(null);
                    }
                }
            }

            return selectedValue;
        }

        public static int Toolbar(Rect position, int selectedIndex, GUIContent[] labels, float buttonWidth, bool toggleToDeselect = false, int disabledIndex = -1)
        {
            Rect rect = position;
            rect.width = buttonWidth;
            rect.height = EditorGUIUtility.singleLineHeight;

            int selectedValue = selectedIndex;
            for (int i = 0; i < labels.Length; ++i)
            {
                GUIStyle style = i == 0 ? Styles.ToolbarButtonLeft : i == labels.Length - 1 ? Styles.ToolbarButtonRight : Styles.ToolbarButtonMid;
                bool isSelected = i == selectedIndex;
                EditorGUI.BeginDisabledGroup(i == disabledIndex);
                bool on = GUI.Toggle(rect, isSelected, labels[i], style: style);
                EditorGUI.EndDisabledGroup();

                rect.x += buttonWidth;

                if (on && !isSelected)
                {
                    selectedValue = i;

                    if (EditorGUIUtility.editingTextField)
                    {
                        EditorGUI.FocusTextInControl(null);
                    }
                }
                else if (toggleToDeselect && !on && isSelected)
                {
                    selectedValue = -1;

                    if (EditorGUIUtility.editingTextField)
                    {
                        EditorGUI.FocusTextInControl(null);
                    }
                }
            }

            return selectedValue;
        }

        public static int Toolbar(Rect position, int selectedIndex, GUIContent[] labels)
        {
            return Toolbar(position, selectedIndex, labels, position.width / labels.Length);
        }

        public static LengthType LengthTypeField(Rect position, LengthType type)
        {
            // cast to int to avoid Enum.TryParse
            return (LengthType)Toolbar(position, (int)type, Labels.LengthType, position.width * 0.5f);
        }

        public static MinMax LengthRangeField(GUIContent label, Length length, MinMax minMax, Length.Calculated calc, bool showClampIndicators, bool horizontal = false, bool4 mixedProperties = default)
        {
            float labelWidth = LabelWidth;

            using var indentScope = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

            Layout.BeginVertical(EditorStyles.inspectorFullWidthMargins);

            bool hasLabel = !string.IsNullOrEmpty(label.text);

            Rect fieldRect = Layout.BeginHorizontal();
            if (hasLabel)
            {
                Rect labelRect = Layout.GetControlRect(GUILayout.Width(LabelWidth), GUILayout.Height(horizontal ? EditorGUIUtility.singleLineHeight : 2 * EditorGUIUtility.singleLineHeight));
                EditorGUI.LabelField(labelRect, label);
            }

            GUIStyle minStyle = showClampIndicators && length.Raw == minMax.Min ? Styles.YellowTextNumberField : EditorStyles.numberField;
            GUIStyle maxStyle = showClampIndicators && length.Raw == minMax.Max ? Styles.BlueTextNumberField : EditorStyles.numberField;

            if (horizontal)
            {
                Layout.BeginHorizontal();
            }
            else
            {
                Layout.BeginVertical();
            }

            LabelWidth = 3f * SingleCharacterGUIWidth;
            EditorGUI.BeginChangeCheck();
            minMax.Min = ToggleEnabledFloatField(Labels.Min, minMax.Min, calc.Value, float.NegativeInfinity, minStyle, mixedProperties.xy);
            minMax.Max = ToggleEnabledFloatField(Labels.Max, minMax.Max, calc.Value, float.PositiveInfinity, maxStyle, mixedProperties.zw);
            bool rangeChanged = EditorGUI.EndChangeCheck();

            if (horizontal)
            {
                Layout.EndHorizontal();
            }
            else
            {
                Layout.EndVertical();
            }

            indentScope.Dispose();

            Layout.EndHorizontal();
            Layout.EndVertical();

            LabelWidth = labelWidth;

            float min = Mathf.Min(minMax.Min, minMax.Max);
            float max = Mathf.Max(minMax.Min, minMax.Max);

            minMax.Min = min;
            minMax.Max = max;

            return minMax;
        }

        public static void LengthRangeField(GUIContent label, _Length length, _MinMax minMax, Length.Calculated calc, bool showClampIndicators)
        {
            float labelWidth = LabelWidth;

            bool hasLabel = !string.IsNullOrEmpty(label.text);

            if (hasLabel)
            {
                Layout.BeginVertical(Styles.HelpBox);
                Rect fieldRect = Layout.GetControlRect(GUILayout.Width(LabelWidth));
                GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, minMax.SerializedProperty);
                EditorGUI.LabelField(fieldRect, propertyLabel);
            }
            else
            {
                Layout.BeginHorizontal(Styles.HelpBox);

                // Allocate an empty spacer
                Layout.GetControlRect(GUILayout.Width(LabelWidth));
            }

            using var indentScope = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            GUIStyle minStyle = showClampIndicators && length.Raw == minMax.Min ? Styles.YellowTextNumberField : EditorStyles.numberField;
            GUIStyle maxStyle = showClampIndicators && length.Raw == minMax.Max ? Styles.BlueTextNumberField : EditorStyles.numberField;

            Layout.BeginVertical();
            LabelWidth = 3f * SingleCharacterGUIWidth;
            EditorGUI.BeginChangeCheck();
            ToggleEnabledFloatField(Labels.Min, minMax.MinProp, calc.Value, float.NegativeInfinity, minStyle);
            ToggleEnabledFloatField(Labels.Max, minMax.MaxProp, calc.Value, float.PositiveInfinity, maxStyle, minMax.Min);
            bool rangeChanged = EditorGUI.EndChangeCheck();
            Layout.EndVertical();
            indentScope.Dispose();

            if (hasLabel)
            {
                EditorGUI.EndProperty();
                Layout.EndVertical();
            }
            else
            {
                Layout.EndHorizontal();
            }

            LabelWidth = labelWidth;

            if (rangeChanged)
            {
                float min = Mathf.Min(minMax.Min, minMax.Max);
                float max = Mathf.Max(minMax.Min, minMax.Max);

                minMax.Min = min;
                minMax.Max = max;
            }
        }

        public static float ToggleEnabledFloatField(GUIContent label, float value, float defaultValue, float invalidValue, GUIStyle floatFieldStyle, bool2 mixedProperties = default)
        {
            bool showMixed = EditorGUI.showMixedValue;
            Layout.BeginHorizontal();
            bool wasValid = value != invalidValue;

            EditorGUI.showMixedValue = mixedProperties.x; // mixed here means the mixed properties are mixed between valid/invalid
            Rect toggleRect = Layout.GetControlRect(GUILayout.Width(ToggleBoxSize));
            bool isValid = EditorGUI.Toggle(toggleRect, wasValid);
            EditorGUI.BeginDisabledGroup(!isValid);

            int fontSize = floatFieldStyle.fontSize;
            TextAnchor alignment = floatFieldStyle.alignment;
            floatFieldStyle.fontSize = value == invalidValue ? 10 : 12;
            floatFieldStyle.alignment = TextAnchor.MiddleLeft;

            EditorGUI.showMixedValue = mixedProperties.y; // mixed here means the property values are mixed
            Rect fieldRect = Layout.GetControlRect(Layout.MinFloatFieldWidthOption);
            value = EditorGUI.FloatField(fieldRect, label, value, floatFieldStyle);

            floatFieldStyle.fontSize = fontSize;
            floatFieldStyle.alignment = alignment;

            EditorGUI.EndDisabledGroup();

            EditorGUI.showMixedValue = showMixed;

            Layout.EndHorizontal();

            if (wasValid && !isValid)
            {
                return invalidValue;
            }

            if (!wasValid && isValid)
            {
                return defaultValue;
            }

            return value;
        }

        public static void ToggleEnabledFloatField(GUIContent label, SerializedProperty property, float defaultValue, float invalidValue, GUIStyle floatFieldStyle, float? minValue = null)
        {
            Layout.BeginHorizontal();

            Rect fieldRect = Layout.GetControlRect(GUILayout.MinWidth(MinFloatFieldWidth + ToggleBoxSize));
            Rect toggleRect = fieldRect;
            toggleRect.width = ToggleBoxSize;

            Rect floatFieldRect = fieldRect;
            floatFieldRect.width -= ToggleBoxSize;
            floatFieldRect.x += ToggleBoxSize + MinSpaceBetweenFields;

            EditorGUI.BeginChangeCheck();
            GUIContent propertyLabel = EditorGUI.BeginProperty(fieldRect, label, property);
            bool wasValid = property.floatValue != invalidValue;
            bool isValid = EditorGUI.Toggle(toggleRect, wasValid);
            EditorGUI.BeginDisabledGroup(!isValid);

            int fontSize = floatFieldStyle.fontSize;
            TextAnchor alignment = floatFieldStyle.alignment;
            floatFieldStyle.fontSize = !isValid ? 10 : 12;
            floatFieldStyle.alignment = TextAnchor.MiddleLeft;

            float value = EditorGUI.FloatField(floatFieldRect, propertyLabel, property.floatValue, floatFieldStyle);

            floatFieldStyle.fontSize = fontSize;
            floatFieldStyle.alignment = alignment;

            EditorGUI.EndDisabledGroup();
            EditorGUI.EndProperty();
            bool changed = EditorGUI.EndChangeCheck();
            Layout.EndHorizontal();

            if (changed)
            {
                if (minValue.HasValue && !float.IsInfinity(minValue.Value))
                {
                    // Clamp it to the min so that if a user deletes the max field to start typing in
                    // a new value, it doesn't set the min value to zero
                    value = Mathf.Max(minValue.Value, value);
                }

                if (wasValid && !isValid)
                {
                    property.floatValue = invalidValue;
                }

                else if (!wasValid && isValid)
                {
                    property.floatValue = defaultValue;
                }
                else
                {
                    property.floatValue = value;
                }
            }
        }

        #region Calculated Length Reflection
        /// <summary>
        /// A bit of a gross hack, but here we use reflection to retrieve a Length.Calculated value given a serialized
        /// property path. Since the calculated values are not serialized themselves, internally we expose some utilities
        /// to retrieve or calculate on-the-fly some length value of the given target UIBlock.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="lengthPropertyPath"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static float CalculatedLengthFromPropertyPath(UIBlock target, string lengthPropertyPath, LengthType type)
        {
            string[] path = lengthPropertyPath.Split('.');

            if (path[0].Equals(nameof(UIBlock.AutoLayout), System.StringComparison.InvariantCultureIgnoreCase))
            {
                Length.Calculated spacing = target.CalculatedSpacing;
                return type == LengthType.Value ? spacing.Value : spacing.Percent;
            }

            if (path[0].Equals(nameof(UIBlock.Layout), System.StringComparison.InvariantCultureIgnoreCase))
            {
                return CalculatedLayoutLengthFromPropertyPath(target, path, type);
            }

            switch (target)
            {
                case UIBlock2D uiBlock2D:
                    return CalculatedRenderLengthFromPropertyPath(uiBlock2D, path, type);
                case UIBlock3D uiblock3D:
                    return CalculatedRenderLengthFromPropertyPath(uiblock3D, path, type);
            }

            Debug.LogError("Matching calculated property path not found.");

            return float.NaN;
        }

        /// <summary>
        /// Retrieves a calculated length within any of the size, position, padding, or margin structs
        /// </summary>
        /// <param name="target"></param>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static float CalculatedLayoutLengthFromPropertyPath(UIBlock target, string[] path, LengthType type)
        {
            PropertyInfo calculatedLayout = typeof(UIBlock).GetProperty(nameof(UIBlock.CalculatedLayout), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            FieldInfo calculatedLengthProp = calculatedLayout.PropertyType.GetField($"{path[1]}", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo calculatedLengthField = calculatedLengthProp.FieldType.GetField(path[2], BindingFlags.Public | BindingFlags.Instance);

            Length.Calculated length = (Length.Calculated)calculatedLengthField.GetValue(calculatedLengthProp.GetValue(calculatedLayout.GetValue(target)));

            return type == LengthType.Value ? length.Value : length.Percent;
        }

        /// <summary>
        /// Retrieves a calculated corner radius on a UIBlock3D
        /// </summary>
        /// <param name="target"></param>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static float CalculatedRenderLengthFromPropertyPath(UIBlock3D target, string[] path, LengthType type)
        {
            PropertyInfo calculatedVisuals = typeof(UIBlock3D).GetProperty(nameof(UIBlock3D.CalculatedVisuals), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            FieldInfo calculatedLengthProp = calculatedVisuals.PropertyType.GetField($"{path[1]}", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Length.Calculated length = (Length.Calculated)calculatedLengthProp.GetValue(calculatedVisuals.GetValue(target));
            return type == LengthType.Value ? length.Value : length.Percent;
        }

        /// <summary>
        /// Retrieves a calculated length within any of the border, gradient, corner radius, or shadow structs on a UIBlock2D
        /// </summary>
        /// <param name="target"></param>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static float CalculatedRenderLengthFromPropertyPath(UIBlock2D target, string[] path, LengthType type)
        {
            PropertyInfo calculatedVisuals = typeof(UIBlock2D).GetProperty(nameof(UIBlock2D.CalculatedVisuals), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            FieldInfo calculatedLengthProp = calculatedVisuals.PropertyType.GetField(path[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Length.Calculated length = default;

            if (path.Length == 2)
            {
                length = (Length.Calculated)calculatedLengthProp.GetValue(calculatedVisuals.GetValue(target));
            }
            else
            {
                FieldInfo calculatedLengthsField = calculatedLengthProp.FieldType.GetField(path[2], BindingFlags.Public | BindingFlags.Instance);

                if (path.Length == 3)
                {
                    length = (Length.Calculated)calculatedLengthsField.GetValue(calculatedLengthProp.GetValue(calculatedVisuals.GetValue(target)));
                }
                else
                {
                    FieldInfo calculatedSingleLengthField = calculatedLengthsField.FieldType.GetField(path[3], BindingFlags.Public | BindingFlags.Instance);

                    length = (Length.Calculated)calculatedSingleLengthField.GetValue(calculatedLengthsField.GetValue(calculatedLengthProp.GetValue(calculatedVisuals.GetValue(target))));
                }
            }

            return type == LengthType.Value ? length.Value : length.Percent;
        }
        #endregion
    }
}

