// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Rendering;
using UnityEditor;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    internal static class NovaSettingsEditors
    {
        // This is the longest label in the settings menu right now, don't need to overengineer and loop through to check them.
        private static readonly float MaxLabelWidth = EditorStyles.label.CalcSize(Labels.Settings.UIBlock3DCornerDivisions).x + NovaGUI.MinSpaceBetweenFields;

        public static void DrawGeneral(_SettingsConfig config)
        {
            using Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("General");

            if (!foldout)
            {
                return;
            }

            NovaGUI.Layout.BeginHorizontal();

            // This is an indent
            NovaGUI.Space(NovaGUI.Layout.FoldoutArrowIndentSpace);

            float labelWidth = NovaGUI.LabelWidth;

            NovaGUI.LabelWidth = MaxLabelWidth;

            NovaGUI.EnumFlagsField(Labels.Settings.LogFlags, config.LogFlagsProp, NovaSettings.LogFlags);

            NovaGUI.LabelWidth = labelWidth;

            NovaGUI.Layout.EndHorizontal();
        }

        public static void DrawRendering(_SettingsConfig config)
        {
            using Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Rendering");

            if (!foldout)
            {
                return;
            }


            NovaGUI.Layout.BeginHorizontal();

            // This is an indent
            NovaGUI.Space(NovaGUI.Layout.FoldoutArrowIndentSpace);

            NovaGUI.Layout.BeginVertical();

            float labelWidth = NovaGUI.LabelWidth;

            NovaGUI.LabelWidth = MaxLabelWidth;

            NovaGUI.ToggleField(Labels.Settings.PackedImages, config.PackedImagesEnabledProp);
            NovaGUI.ToggleField(Labels.Settings.SuperSampleText, config.SuperSampleTextProp);
            NovaGUI.SliderField(Labels.Settings.EdgeSoftenWidth, config.EdgeSoftenWidthProp, 1f, 3f);

            NovaGUI.IntSlider(Labels.Settings.UIBlock3DCornerDivisions, config.UIBlock3DCornerDivisionsProp, 0, 20);
            NovaGUI.IntSlider(Labels.Settings.UIBlock3DEdgeDivisions, config.UIBlock3DEdgeDivisionsProp, 0, 20);

            NovaGUI.EnumField(Labels.Settings.PackedImageCopyMode, config.PackedImageCopyModeProp, NovaSettings.PackedImageCopyMode);

            if (SystemSettings.UsingScriptableRenderPipeline)
            {
                NovaGUI.WarningIcon(Labels.Surface.DisabledSurfaceSRPWarning);
            }

            EditorGUI.BeginDisabledGroup(SystemSettings.UsingScriptableRenderPipeline);

            NovaGUI.PrefixLabel(Labels.Settings.LightingModelsToBuild);

            EditorGUI.indentLevel++;
            NovaGUI.EnumFlagsField(Labels.Settings.UIBlock2DLightingModels, config.UIBlock2DLightingModelsProp, NovaSettings.UIBlock2DLightingModels);
            NovaGUI.EnumFlagsField(Labels.Settings.TextBlockLightingModels, config.TextBlockLightingModelsProp, NovaSettings.TextBlockLightingModels);
            NovaGUI.EnumFlagsField(Labels.Settings.UIBlock3DLightingModels, config.UIBlock3DLightingModelsProp, NovaSettings.UIBlock3DLightingModels);
            EditorGUI.indentLevel--;

            EditorGUI.EndDisabledGroup();

            NovaGUI.LabelWidth = labelWidth;
            NovaGUI.Layout.EndVertical();

            NovaGUI.Layout.EndHorizontal();
        }

        public static void DrawInput(_SettingsConfig config)
        {
            using Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Input");

            if (!foldout)
            {
                return;
            }

            NovaGUI.Layout.BeginHorizontal();

            // This is an indent
            NovaGUI.Space(NovaGUI.Layout.FoldoutArrowIndentSpace);

            NovaGUI.Layout.BeginVertical();

            float labelWidth = NovaGUI.LabelWidth;
            NovaGUI.LabelWidth = MaxLabelWidth;


            NovaGUI.IntSlider(Labels.Settings.ClickThreshold, config.ClickFrameDeltaThresholdProp, 0, 5);

            NovaGUI.LabelWidth = labelWidth;
            NovaGUI.Layout.EndVertical();

            NovaGUI.Layout.EndHorizontal();

        }

        public static void DrawEditor(SerializedObject serializedObject)
        {
            using Foldout foldout = NovaGUI.EditorPrefFoldoutHeader("Editor");

            if (!foldout)
            {
                return;
            }

            float labelWidth = NovaGUI.LabelWidth;

            NovaGUI.LabelWidth = MaxLabelWidth;

            NovaGUI.Layout.BeginHorizontal();

            // This is an indent
            NovaGUI.Space(NovaGUI.Layout.FoldoutArrowIndentSpace);

            EditorGUI.BeginChangeCheck();
            bool edgeSnappingEnabled = EditorGUILayout.Toggle(Labels.Settings.EdgeSnapping, NovaEditorPrefs.EdgeSnappingEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                NovaEditorPrefs.EdgeSnappingEnabled = edgeSnappingEnabled;
            }

            NovaGUI.Layout.EndHorizontal();

            NovaGUI.Layout.BeginHorizontal();

            // This is an indent
            NovaGUI.Space(NovaGUI.Layout.FoldoutArrowIndentSpace);

            EditorGUI.BeginChangeCheck();
            bool hierarchyGizmos = EditorGUILayout.Toggle(Labels.Settings.HierarchyGizmos, NovaEditorPrefs.HierarchyGizmosEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                NovaEditorPrefs.HierarchyGizmosEnabled = hierarchyGizmos;
            }

            NovaGUI.Layout.EndHorizontal();

            EditorGUILayout.Space();

            NovaGUI.Layout.BeginHorizontal();

            // This is an indent
            NovaGUI.Space(NovaGUI.Layout.FoldoutArrowIndentSpace);

            NovaGUI.Layout.BeginVertical();

            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(NovaSettings.ButtonPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(NovaSettings.TogglePrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(NovaSettings.SliderPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(NovaSettings.DropdownPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(NovaSettings.TextFieldPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(NovaSettings.ScrollViewPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(NovaSettings.UIRootPrefab)));

            NovaGUI.Layout.EndVertical();

            NovaGUI.Layout.EndHorizontal();

            NovaGUI.LabelWidth = labelWidth;
        }
    }
}

