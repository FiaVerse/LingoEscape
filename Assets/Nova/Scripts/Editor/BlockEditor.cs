// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Editor.Serialization;
using Nova.Editor.Tools;
using Nova.Extensions;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditorInternal;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CanEditMultipleObjects]
    internal abstract class BlockEditor<TBlock> : NovaEditor<TBlock>
        where TBlock : UIBlock
    {
        protected abstract void DoGui(TBlock uiBlock);

        protected SerializedProperty previewSizeProperty = null;
        protected _Layout layout = new _Layout();
        protected _AutoLayout autoLayout = new _AutoLayout();

        protected _BaseRenderInfo baseRenderInfo = new _BaseRenderInfo();
        protected _Surface surfaceInfo = new _Surface();

        protected TBlock TargetBlock => serializedObject.isEditingMultipleObjects ? null : target as TBlock;

        private int knownBlockDirtiedCount = 0;
        private int knownGameObjectDirtiedCount = 0;

        protected override void OnEnable()
        {
            base.OnEnable();

            EnsureComponentOrder();

            previewSizeProperty = serializedObject.FindProperty(Names.UIBlock.PreviewSize);

            TBlock uiBlock = target as TBlock;
            if (ShouldShowPreviewSize(uiBlock, previewSizeProperty))
            {
                Internal.Layouts.LayoutDataStore.EditorOnly_QueuePreviewSizeRefresh();
                uiBlock.CalculateLayout();
            }

            baseRenderInfo.SerializedProperty = serializedObject.FindProperty(Names.UIBlock.visibility);
            surfaceInfo.SerializedProperty = serializedObject.FindProperty(Names.UIBlock.surface);
            layout.SerializedProperty = serializedObject.FindProperty(Names.UIBlock.layout);
            autoLayout.SerializedProperty = serializedObject.FindProperty(Names.UIBlock.autoLayout);

            Undo.undoRedoPerformed += RestoreUndoneRedoneProperties;

            // Set to 0, lets us catch properties modified in debug mode
            knownBlockDirtiedCount = 0;
            knownGameObjectDirtiedCount = 0;
        }


        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RestoreUndoneRedoneProperties;
        }

        private void RestoreUndoneRedoneProperties()
        {
            UpdateUnityObjects(triggeredByUser: true);
        }

        public override void OnInspectorGUI()
        {
            TBlock uiBlock = target as TBlock;

            int blockDirtied = EditorUtility.GetDirtyCount(uiBlock);
            int gameObjectDirtied = EditorUtility.GetDirtyCount(uiBlock.gameObject);

            if (knownBlockDirtiedCount != blockDirtied || knownGameObjectDirtiedCount != gameObjectDirtied)
            {
                if (!Application.IsPlaying(target))
                {
                    // Updated outside this particular editor UI
                    UpdateUnityObjects(triggeredByUser: true);
                }

                // assign here in case we exit early below
                knownBlockDirtiedCount = blockDirtied;
                knownGameObjectDirtiedCount = gameObjectDirtied;
            }

            if (!InternalEditorUtility.GetIsInspectorExpanded(target))
            {
                return;
            }

            UpdateSerializedObjects();

            NovaGUI.EditingSingleObject = !serializedObject.isEditingMultipleObjects;

            EditorGUI.BeginChangeCheck();

            DrawToolbarUI(uiBlock, previewSizeProperty);

            DoGui(uiBlock);

            if (EditorGUI.EndChangeCheck())
            {
                UpdateUnityObjects();
                EditModeUtils.QueueEditorUpdateNextFrame();
            }

            knownBlockDirtiedCount = EditorUtility.GetDirtyCount(uiBlock);
            knownGameObjectDirtiedCount = EditorUtility.GetDirtyCount(uiBlock.gameObject);

        }

        private static List<EditorTool> tools = new List<EditorTool>();

        /// <summary>
        /// We need to do this manually because unity has a bug where if you change the size of a
        /// UIBlock with the tool and then undo it, it removes all but one of the tools from the toolbar.
        /// </summary>
        private static List<EditorTool> GetEditorTools(TBlock uiBlock)
        {
            tools.Clear();

            tools.Add(UIBlockTool.TypedInstance);
            tools.Add(SpacingTool.TypedInstance);
            if (uiBlock is UIBlock2D)
            {
                tools.Add(GradientTool.TypedInstance);
            }
            return tools;
        }

        private static void DrawToolbarUI(TBlock uiBlock, SerializedProperty previewSize)
        {
            using (Foldout foldout = NovaGUI.EditorPrefFoldoutHeader(NovaEditorPrefs.UIBlockToolsKey, displayName: "Tools"))
            {
                if (!foldout)
                {
                    return;
                }

                NovaGUI.Layout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EditorToolbar(GetEditorTools(uiBlock));

                Rect toolbarRect = GUILayoutUtility.GetLastRect();

                GUILayout.FlexibleSpace();

                bool show3D = NovaGUI.ShowZAxisValues(uiBlock);

                if (toolbarRect.height > 0)
                {
                    float width = 2 * NovaGUI.SingleCharacterGUIWidth;
                    float height = EditorGUIUtility.singleLineHeight;
                    float y = toolbarRect.y + (0.5f * (toolbarRect.height - height));
                    float x = (2 * toolbarRect.x) + toolbarRect.width - (1.75f * width);
                    Rect toggleRect = new Rect(x, y, width, height);

                    EditorGUI.BeginChangeCheck();
                    show3D = GUI.Toggle(toggleRect, show3D, Labels.Tools.ThreeDToggle, NovaGUI.Styles.ToolbarButtonMid);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (uiBlock is UIBlock3D)
                        {
                            NovaEditorPrefs.UIBlock3DShowAllZAxis = show3D;
                        }
                        else
                        {
                            NovaEditorPrefs.UIBlockShowAllZAxis = show3D;
                        }
                    }
                }
                NovaGUI.Layout.EndHorizontal();

                if (ShouldShowPreviewSize(uiBlock, previewSize))
                {
                    DrawPreviewSizeUI(uiBlock, previewSize, show3D);
                }
            }
        }

        private static void DrawPreviewSizeUI(TBlock uiBlock, SerializedProperty previewSize, bool zField)
        {
            if (!SceneViewUtils.IsInCurrentPrefabStage(uiBlock.gameObject))
            {
                if (Util.Any(uiBlock.Size.Type == LengthType.Percent))
                {
                    EditorGUILayout.HelpBox(new GUIContent("Preview Size on root UI Blocks is only available in Prefab View.", EditorGUIUtility.IconContent("d_console.infoicon.sml").image), wide: true);
                }

                return;
            }

            NovaGUI.Space();

            EditorGUI.BeginChangeCheck();

            ThreeD<bool> disabled = uiBlock.Size.Type == LengthType.Value;

            if (zField)
            {
                NovaGUI.Vector3Field(Labels.Tools.PreviewSize, previewSize, disabled);
            }
            else
            {
                NovaGUI.Vector2Field(Labels.Tools.PreviewSize, previewSize, disabled.XY);
            }

            if (EditorGUI.EndChangeCheck())
            {
                previewSize.vector3Value = Vector3.Max(previewSize.vector3Value, Vector3.zero);
            }
        }

        private static bool ShouldShowPreviewSize(TBlock uiBlock, SerializedProperty previewSize)
        {
            if (previewSize.serializedObject.targetObjects != null &&
                previewSize.serializedObject.targetObjects.Length > 1)
            {
                return false;
            }

            if (uiBlock.Parent != null || !SceneViewUtils.IsVisibleInSceneView(uiBlock.gameObject))
            {
                return false;
            }

            return true;
        }

        protected List<MonoBehaviour> components = new List<MonoBehaviour>();
        protected virtual void EnsureComponentOrder()
        {
            for (int i = 0; i < targetComponents.Count; i++)
            {
                components.Clear();
                targetComponents[i].GetComponents(components);

                int blockIndex = -1;
                int activatorIndex = -1;
                for (int j = 0; j < components.Count; ++j)
                {
                    if (components[j] is UIBlock)
                    {
                        blockIndex = j;
                    }
                    else if (components[j] is UIBlockActivator)
                    {
                        activatorIndex = j;
                    }

                    if (blockIndex >= 0 && activatorIndex >= 0)
                    {
                        break;
                    }
                }

                // keep activator above UIBlock
                UIBlockActivator activator = components[activatorIndex] as UIBlockActivator;
                activator.hideFlags ^= HideFlags.NotEditable;

                while (activatorIndex >= blockIndex && ComponentUtility.MoveComponentUp(activator))
                {
                    activatorIndex--;
                }

                activator.hideFlags |= HideFlags.NotEditable;
            }
        }

        [Obfuscation]
        public bool HasFrameBounds()
        {
            TBlock uiBlock = target as TBlock;
            return uiBlock.gameObject.activeInHierarchy;
        }

        [Obfuscation]
        public Bounds OnGetFrameBounds()
        {
            TBlock uiBlock = target as TBlock;

            Bounds total = new Bounds(uiBlock.transform.position, uiBlock.GetWorldSize(includeMargin: false));

            for (int i = 1; i < targetComponents.Count; ++i)
            {
                total.Encapsulate(new Bounds(targetComponents[i].transform.position, targetComponents[i].GetWorldSize(includeMargin: false)));
            }

            return total;
        }

        protected void UpdateUnityObjects(bool triggeredByUser = false)
        {
            TBlock uiBlock = target as TBlock;
            Vector3 objectPreviewSize = uiBlock.PreviewSize;

            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targetComponents.Count; ++i)
            {
                targetComponents[i].EditorOnly_MarkDirty();
            }

            if (ShouldShowPreviewSize(uiBlock, previewSizeProperty))
            {
                if (triggeredByUser || objectPreviewSize != uiBlock.PreviewSize)
                {
                    Internal.Layouts.LayoutDataStore.EditorOnly_QueuePreviewSizeRefresh();
                }
            }
        }

        protected override void UpdateSerializedObjects()
        {
            for (int i = 0; i < targetComponents.Count; ++i)
            {
                targetComponents[i].CopyFromDataStore();
            }
            serializedObject.UpdateIfRequiredOrScript();
        }
    }
}

