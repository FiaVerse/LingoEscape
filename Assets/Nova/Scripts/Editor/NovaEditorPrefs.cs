// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using UnityEditor;

namespace Nova.Editor
{
    internal static class NovaEditorPrefs
    {
        public static string GetFullEditorPrefPath(string label) => $"{Constants.ProjectName}.{label}";

        public const string UIBlockToolsKey = "UIBlockToolsKey";
        public static bool DisplayTools
        {
            get
            {
                return GetEditorPrefBool(UIBlockToolsKey, true);
            }
            set
            {
                SetEditorPrefBool(UIBlockToolsKey, value);
            }
        }

        public const string UIBlockShowAllZAxisKey = "UIBlockShowAllZAxisKey";
        public static bool UIBlockShowAllZAxis
        {
            get
            {
                return GetEditorPrefBool(UIBlockShowAllZAxisKey, false);
            }
            set
            {
                SetEditorPrefBool(UIBlockShowAllZAxisKey, value);
            }
        }

        public const string UIBlock3DShowAllZAxisKey = "UIBlock3DShowAllZAxisKey";
        public static bool UIBlock3DShowAllZAxis
        {
            get
            {
                return GetEditorPrefBool(UIBlock3DShowAllZAxisKey, true);
            }
            set
            {
                SetEditorPrefBool(UIBlock3DShowAllZAxisKey, value);
            }
        }


        private const string MinMaxSizeKey = "MinMaxSizeKey";
        public static bool DisplayMinMaxSize
        {
            get
            {
                return GetEditorPrefBool(MinMaxSizeKey, false);
            }
            set
            {
                SetEditorPrefBool(MinMaxSizeKey, value);
            }
        }

        private const string MinMaxPositionKey = "MinMaxPositionKey";
        public static bool DisplayMinMaxPosition
        {
            get
            {
                return GetEditorPrefBool(MinMaxPositionKey, false);
            }
            set
            {
                SetEditorPrefBool(MinMaxPositionKey, value);
            }
        }

        private const string MinMaxPaddingKey = "MinMaxPaddingKey";
        public static bool DisplayMinMaxPadding
        {
            get
            {
                return GetEditorPrefBool(MinMaxPaddingKey, false);
            }
            set
            {
                SetEditorPrefBool(MinMaxPaddingKey, value);
            }
        }

        private const string SidesPaddingKey = "SidesPaddingKey";
        public static bool DisplaySidesPadding
        {
            get
            {
                return GetEditorPrefBool(SidesPaddingKey, false);
            }
            set
            {
                SetEditorPrefBool(SidesPaddingKey, value);
            }
        }

        private const string MinMaxMarginKey = "MinMaxMarginKey";
        public static bool DisplayMinMaxMargin
        {
            get
            {
                return GetEditorPrefBool(MinMaxMarginKey, false);
            }
            set
            {
                SetEditorPrefBool(MinMaxMarginKey, value);
            }
        }

        private const string SidesMarginKey = "SidesMarginKey";
        public static bool DisplaySidesMargin
        {
            get
            {
                return GetEditorPrefBool(SidesMarginKey, false);
            }
            set
            {
                SetEditorPrefBool(SidesMarginKey, value);
            }
        }

        private const string MinMaxAutoLayoutKey = "MinMaxAutoLayoutKey";
        public static bool DisplayMinMaxAutoLayout
        {
            get
            {
                return GetEditorPrefBool(MinMaxAutoLayoutKey, false);
            }
            set
            {
                SetEditorPrefBool(MinMaxAutoLayoutKey, value);
            }
        }

        private const string MinMaxWrapLayoutKey = "MinMaxWrapLayoutKey";
        public static bool DisplayMinMaxCrossLayout
        {
            get
            {
                return GetEditorPrefBool(MinMaxWrapLayoutKey, false);
            }
            set
            {
                SetEditorPrefBool(MinMaxWrapLayoutKey, value);
            }
        }

        private const string ExpandedColorKey = "ExpandedColorKey";
        public static bool DisplayExpandedColor
        {
            get
            {
                return GetEditorPrefBool(ExpandedColorKey, true);
            }
            set
            {
                SetEditorPrefBool(ExpandedColorKey, value);
            }
        }

        private const string ExpandedSurfaceKey = "ExpandedSurfaceKey";
        public static bool DisplayExpandedSurface
        {
            get
            {
                return GetEditorPrefBool(ExpandedSurfaceKey, true);
            }
            set
            {
                SetEditorPrefBool(ExpandedSurfaceKey, value);
            }
        }

        private const string ExpandedImageKey = "ExpandedImageKey";
        public static bool DisplayExpandedImage
        {
            get
            {
                return GetEditorPrefBool(ExpandedImageKey, true);
            }
            set
            {
                SetEditorPrefBool(ExpandedImageKey, value);
            }
        }

        private const string ExpandedRadialFillKey = "ExpandedRadialFillKey";
        public static bool DisplayExpandedRadialFill
        {
            get
            {
                return GetEditorPrefBool(ExpandedRadialFillKey, true);
            }
            set
            {
                SetEditorPrefBool(ExpandedRadialFillKey, value);
            }
        }

        private const string ExpandedGradientKey = "ExpandedGradientKey";
        public static bool DisplayExpandedGradient
        {
            get
            {
                return GetEditorPrefBool(ExpandedGradientKey, true);
            }
            set
            {
                SetEditorPrefBool(ExpandedGradientKey, value);
            }
        }

        private const string ExpandedTextKey = "ExpandedTextKey";
        public static bool DisplayExpandedText
        {
            get
            {
                return GetEditorPrefBool(ExpandedTextKey, true);
            }
            set
            {
                SetEditorPrefBool(ExpandedTextKey, value);
            }
        }

        private const string SidesBorderKey = "SidesBorderKey";
        public static bool DisplaySidesBorder
        {
            get
            {
                return GetEditorPrefBool(SidesBorderKey, false);
            }
            set
            {
                SetEditorPrefBool(SidesBorderKey, value);
            }
        }


        public const string NavigationShowZAxisKey = "NavigationShowZAxisKey";
        public static bool DisplayNavigationZAxis
        {
            get
            {
                return GetEditorPrefBool(NavigationShowZAxisKey, false);
            }
            set
            {
                SetEditorPrefBool(NavigationShowZAxisKey, value);
            }
        }

        public const string NavigationDebugViewKey = "NavigationDebugViewKey";
        public static bool DisplayNavigationDebugView
        {
            get
            {
                return GetEditorPrefBool(NavigationDebugViewKey, false);
            }
            set
            {
                SetEditorPrefBool(NavigationDebugViewKey, value);
            }
        }

        public const string NavigationDebugViewSelectedOnlyKey = "NavigationDebugViewSelectedOnlyKey";
        public static bool FilterNavDebugViewToSelection
        {
            get
            {
                return GetEditorPrefBool(NavigationDebugViewSelectedOnlyKey, false);
            }
            set
            {
                SetEditorPrefBool(NavigationDebugViewSelectedOnlyKey, value);
            }
        }

        private static bool? edgeSnappingEnabled;
        private const string EdgeSnapSettingsSubpath = "EdgeSnapping";
        public static bool EdgeSnappingEnabled
        {
            get
            {
                if (!edgeSnappingEnabled.HasValue)
                {
                    edgeSnappingEnabled = GetEditorPrefBool(EdgeSnapSettingsSubpath, true);
                }

                return edgeSnappingEnabled.Value;
            }
            set
            {
                edgeSnappingEnabled = value;
                SetEditorPrefBool(EdgeSnapSettingsSubpath, value);
            }
        }

        private static bool? hierarchyGizmos;
        private const string HierarchyGizmosSubPath = "HierarchyGizmos";
        public static bool HierarchyGizmosEnabled
        {
            get
            {
                if (!hierarchyGizmos.HasValue)
                {
                    hierarchyGizmos = GetEditorPrefBool(HierarchyGizmosSubPath, true);
                }

                return hierarchyGizmos.Value;
            }
            set
            {
                hierarchyGizmos = value;
                SetEditorPrefBool(HierarchyGizmosSubPath, value);
            }
        }

        private const string HelpDialogPresentedKey = "HelpDialogShown";
        public static bool HelpDialogPresented
        {
            get => GetEditorPrefBool(HelpDialogPresentedKey, false);
            set => SetEditorPrefBool(HelpDialogPresentedKey, value);
        }

        public static bool GetEditorPrefBool(string label, bool defaultValue)
        {
            return EditorPrefs.GetBool(GetFullEditorPrefPath(label), defaultValue);
        }

        public static void SetEditorPrefBool(string label, bool newValue)
        {
            EditorPrefs.SetBool(GetFullEditorPrefPath(label), newValue);
        }
    }
}
