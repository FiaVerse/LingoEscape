// Copyright (c) Supernova Technologies LLC
//#define LOG_ACTIVATIONS
using Nova.Compat;
using Nova.Editor.GUIs;
using Nova.Editor.Serialization;
using Nova.Editor.Utilities;
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Input;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.Tools
{
    internal class FallbackToolAttribute : System.Attribute
    {
        public FallbackToolAttribute(System.Type fallbackTool)
        {
            FallbackTool = fallbackTool;
        }

        public System.Type FallbackTool { get; private set; }
    }

    internal abstract class UITool : EditorTool
    {
        [InitializeOnLoadMethod]
        private static void Init()
        {
            Selection.selectionChanged += SelectionChanged;
            ToolManager.activeToolChanged += ActiveToolChanged;

            uiBlockIDs = new NovaHashMap<DataStoreID, bool>(16, Allocator.Persistent);
            NovaApplication.EditorBeforeAssemblyReload += () =>
            {
                uiBlockIDs.Dispose();
            };
        }

        [System.NonSerialized]
        private static List<UITool> activeToolInstances = new List<UITool>();
        [System.NonSerialized]
        private static System.Type TypeOfToolToRestore;

        private static void ActiveToolChanged()
        {
            System.Type toolType = ToolManager.activeToolType;

            UIBlock selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<UIBlock>() : null;

            if (toolType.IsSubclassOf(typeof(UITool)))
            {
                TypeOfToolToRestore = toolType;
            }
            else if (selected != null)
            {
                TypeOfToolToRestore = null;
            }
        }

        private static void SelectionChanged()
        {
            bool nothingSelected = Selection.activeGameObject == null;

            if (nothingSelected)
            {
                return;
            }

            bool uiBlockEditorActive = ActiveEditorUtils.TryGetActiveEditorTargetType<UIBlock>(out System.Type targetType);

            if (uiBlockEditorActive && TypeOfToolToRestore != null)
            {
                SetActiveToolOrFallback(TypeOfToolToRestore, targetType);
            }
        }

        private static void Register(UITool tool)
        {
            activeToolInstances.Add(tool);
        }

        private static void Unregister(UITool tool)
        {
            activeToolInstances.Remove(tool);
        }

        public static readonly PrimitiveBoundsHandle.Axes Axes2D = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
        public static readonly PrimitiveBoundsHandle.Axes Axes3D = PrimitiveBoundsHandle.Axes.All;

        // Can't use static readonly initialize for these colors
        public static Color EdgeGuideAccentColor => NovaGUI.Styles.NovaRed;
        public static Color EdgeGuideColor => NovaGUI.Styles.NovaRed;
        public static Color SizeColor => NovaGUI.Styles.NovaBlue;
        public static Color MarginColor => NovaGUI.Styles.NovaCyan;
        public static Color PaddingColor => NovaGUI.Styles.NovaGreen;
        public static Color CornerColor => NovaGUI.Styles.NovaBlue;
        public static Color EdgeColor => NovaGUI.Styles.Magenta_MoreBlue;
        public static Color XAxisColor => NovaGUI.Styles.Red_ish;
        public static Color YAxisColor => NovaGUI.Styles.Yellow_ish;
        public static Color ZAxisColor => NovaGUI.Styles.Blue_ish;
        public static Color HighlightColor => Color.yellow;
        public static Color HoverColor => NovaGUI.Styles.NovaBlue;

        public static readonly Color ShadowColor = Color.black.Alpha(0.25f);

        public static readonly Color GradientColor = new Color(0.85f, 0.85f, 0.85f);
        public static readonly Color TooltipBackgroundColor = new Color(.3f, .3f, .3f);
        public static readonly Color TooltipTextColor = new Color(.85f, .85f, .85f);
        public static readonly Color HandleContrastColor = new Color(0.15f, 0.15f, 0.15f);
        public static readonly Color EdgeGuideObjectHighlight = new Color(1, 0.5f, 0);

        public const float BoldLineWidth = LayoutBoundsHandle.BoldLineWidth;

        public static UITool Instance { get; private set; } = null;
        public UIBlock UIBlock => TopLevelBlocks == null || TopLevelBlocks.Length == 0 ? null : TopLevelBlocks[0];
        protected _Layout SerializedLayoutBlock { get; private set; } = new _Layout();
        protected _Layout SerializedLayoutBlock2D { get; private set; } = new _Layout();
        protected _Layout SerializedLayoutBlock3D { get; private set; } = new _Layout();
        protected _Layout SerializedLayoutTextBlock { get; private set; } = new _Layout();

        public bool AdjustingMultipleObjects => UIBlocks.Count > 1;

        public ReadOnlyList<UIBlock> UIBlocks => uiBlocks.ToReadOnly();

        protected UIBlock[] TopLevelBlocks => topLevelBlocks;

        [System.NonSerialized]
        private UIBlock[] topLevelBlocks = null;
        private HashSet<UIBlock> topLevel = new HashSet<UIBlock>();

        protected Object[] Targets { get; private set; }
        [System.NonSerialized]
        private List<UIBlock> uiBlocks = new List<UIBlock>();
        [System.NonSerialized]
        private HashList<UIBlock> uiBlockSet = new HashList<UIBlock>(ObjectComparer<UIBlock>.Shared);
        protected HashList<UIBlock> UIBlockSet => uiBlockSet;

        public const float HandleSize = 0.15f;
        public float ScaledHandleSize => HandleSize / HandleScale;
        public float HandleScale => SelectionToWorld.ValidTRS() ? math.cmax(SelectionToWorld.lossyScale) : 1;

        [System.NonSerialized]
        private SceneView sceneView = null;

        [System.NonSerialized]
        private Camera sceneViewCamera = null;
        protected Camera Camera => sceneViewCamera;

        protected const float AnimationSpeed = 20;
        protected struct Tooltip
        {
            public Vector2 HandleGuiPoint;
            public System.Func<string> LabelGetter;
        }

        private Dictionary<int, Tooltip> controls = new Dictionary<int, Tooltip>();

        [System.NonSerialized]
        private Texture2D tooltipBackground = null;

        public bool ModifiedTarget { get; private set; }
        protected bool LayoutPropertyChanged { get; set; } = false;
        protected bool RenderingPropertyChanged { get; set; } = false;

        protected virtual bool IncludeMarginInSelection { get; } = false;

        protected Bounds SelectionBounds { get; private set; }
        protected Matrix4x4 WorldToSelection { get; private set; } = Matrix4x4.identity;
        protected Matrix4x4 SelectionToWorld { get; private set; } = Matrix4x4.identity;

        protected Bounds BlockBoundsLocalSpace => new Bounds(Vector3.zero, UIBlock.CalculatedSize.Value); // doesn't include margin. Margin is in parent space...
        protected Bounds BlockBoundsParentSpace => new Bounds(UIBlock.GetCalculatedTransformLocalPosition() - UIBlock.CalculatedMargin.Offset, UIBlock.GetScaledLayoutSize());

        private protected Matrix4x4 ParentLocalToWorld => AdjustingMultipleObjects ? Matrix4x4.identity : UIBlock.GetParentToWorldMatrix();
        private protected Matrix4x4 LocalToWorld => UIBlock.transform.localToWorldMatrix;

        public const float ValuePrecision = 0.001f;
        public const float PercentPrecision = 1e-6f;

        protected bool SelectionHovered { get; private set; }
        protected virtual bool ToolHovered => false;

        [System.NonSerialized]
        private UIBlock sceneHoveredBlock = null;


        [System.NonSerialized]
        private UIBlock hierarchyHoveredBlock = null;
        private UIBlock HierarchyHoveredBlock
        {
            get
            {
                return hierarchyHoveredBlock;
            }
            set
            {
                hierarchyHoveredBlock = value;
            }
        }

        private static NovaHashMap<DataStoreID, bool> uiBlockIDs;
        protected NovaHashMap<DataStoreID, bool> UIBlockIDs => uiBlockIDs;

        private void CleanupManagedCollections()
        {
            if (SerializedLayoutBlock.SerializedProperty != null)
            {
                SerializedLayoutBlock.SerializedProperty.serializedObject.Dispose();
            }

            if (SerializedLayoutBlock2D.SerializedProperty != null)
            {
                SerializedLayoutBlock2D.SerializedProperty.serializedObject.Dispose();
            }

            if (SerializedLayoutBlock3D.SerializedProperty != null)
            {
                SerializedLayoutBlock3D.SerializedProperty.serializedObject.Dispose();
            }

            if (SerializedLayoutTextBlock.SerializedProperty != null)
            {
                SerializedLayoutTextBlock.SerializedProperty.serializedObject.Dispose();
            }

            topLevelBlocks = null;
            topLevel = null;

            controls = null;

            Targets = null;
            uiBlocks = null;
            uiBlockSet = null;
            tooltipBackground = null;
        }

        private void Awake()
        {
            Register(this);
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
            if (this == Instance)
            {
                OnWillBeDeactivated();
            }
        }

        private void OnDestroy()
        {
            Unregister(this);


            CleanupManagedCollections();
        }

        public override void OnActivated()
        {
            Handles.lighting = false;
            HandleUtility.handleMaterial.color = Color.white;

            tooltipBackground = NovaGUI.Styles.GetTexture(TooltipBackgroundColor);

            Undo.undoRedoPerformed -= RestoreUndoneProperties;
            Undo.undoRedoPerformed += RestoreUndoneProperties;
            Selection.selectionChanged -= HandleSelectionChanged;
            Selection.selectionChanged += HandleSelectionChanged;

            TypeOfToolToRestore = GetType();

            Instance = this;

            HandleSelectionChanged();

            OnToolActivated();
        }

        protected abstract void OnToolActivated();

        private void RestoreUndoneProperties()
        {
            if (UIBlock == null)
            {
                return;
            }

            UIBlock.CopyToDataStore();
        }

        private protected void Repaint()
        {
            if (sceneView == null)
            {
                return;
            }

            sceneView.Repaint();
        }

        private void UpdateSerializedObjects()
        {
            if (SerializedLayoutBlock.SerializedProperty != null)
            {
                SerializedLayoutBlock.SerializedProperty.serializedObject.Update();
            }

            if (SerializedLayoutBlock2D.SerializedProperty != null)
            {
                SerializedLayoutBlock2D.SerializedProperty.serializedObject.Update();
            }

            if (SerializedLayoutBlock3D.SerializedProperty != null)
            {
                SerializedLayoutBlock3D.SerializedProperty.serializedObject.Update();
            }

            if (SerializedLayoutTextBlock.SerializedProperty != null)
            {
                SerializedLayoutTextBlock.SerializedProperty.serializedObject.Update();
            }
        }

        private void HandleSelectionChanged()
        {
            if (Instance != this || this == null)
            {
                return;
            }

            SelectionToWorld = Matrix4x4.identity;
            SelectionHovered = false;

            UIBlock[] allBlocks = Selection.GetFiltered<UIBlock>(UnityEditor.SelectionMode.Editable);

            if (allBlocks == null || allBlocks.Length == 0)
            {
                uiBlocks.Clear();
                uiBlockSet.Clear();

                return;
            }

            UIBlock[] blocks = allBlocks.Where(x => !(x is UIBlock2D) && !(x is UIBlock3D) && !(x is TextBlock)).ToArray();
            UIBlock2D[] blocks2D = allBlocks.Where(x => x is UIBlock2D).Cast<UIBlock2D>().ToArray();
            UIBlock3D[] blocks3D = allBlocks.Where(x => x is UIBlock3D).Cast<UIBlock3D>().ToArray();
            TextBlock[] blocksText = allBlocks.Where(x => x is TextBlock).Cast<TextBlock>().ToArray();

            bool hasUIblocks = blocks != null && blocks.Length > 0;
            bool has2DBlocks = blocks2D != null && blocks2D.Length > 0;
            bool has3DBlocks = blocks3D != null && blocks3D.Length > 0;
            bool hasTextBlocks = blocksText != null && blocksText.Length > 0;

            SerializedLayoutBlock.SerializedProperty = hasUIblocks ? new SerializedObject(blocks).FindProperty(Names.UIBlock.layout) : null;
            SerializedLayoutBlock2D.SerializedProperty = has2DBlocks ? new SerializedObject(blocks2D).FindProperty(Names.UIBlock.layout) : null;
            SerializedLayoutBlock3D.SerializedProperty = has3DBlocks ? new SerializedObject(blocks3D).FindProperty(Names.UIBlock.layout) : null;
            SerializedLayoutTextBlock.SerializedProperty = hasTextBlocks ? new SerializedObject(blocksText).FindProperty(Names.UIBlock.layout) : null;

            uiBlocks.Clear();

            allBlocks.CopyTo(uiBlocks, append: true);

            Targets = uiBlocks.Cast<Object>().ToArray();

            topLevelBlocks = Selection.GetFiltered<UIBlock>(UnityEditor.SelectionMode.TopLevel | UnityEditor.SelectionMode.Editable);
            topLevel.Clear();

            for (int i = 0; i < topLevelBlocks.Length; ++i)
            {
                topLevel.Add(topLevelBlocks[i]);
            }

            uiBlockSet.Clear();
            uiBlockIDs.Clear();

            for (int i = 0; i < UIBlocks.Count; ++i)
            {
                uiBlockSet.Add(UIBlocks[i]);
                uiBlockIDs.Add(UIBlocks[i].ID, false);
            }
        }

        public override void OnWillBeDeactivated()
        {
            topLevelBlocks = null;
            uiBlocks.Clear();

            Undo.undoRedoPerformed -= RestoreUndoneProperties;
            Selection.selectionChanged -= HandleSelectionChanged;
            Instance = null;
        }

        /// <summary>
        /// Returns the total bounds of all selected UIBlocks in a shared coordinate space. If all selected blocks are coplanar, the coordinate space will be coplanar to all elements.
        /// If any selected element is not coplanar with any other selected element, the returned bounds and output matrix will be in world space.
        /// </summary>
        /// <returns></returns>
        protected Bounds GetSelectionBounds(out Matrix4x4 boundsToWorld)
        {
            Bounds totalBounds = default(Bounds);

            UIBlock defaultBlock = UIBlock;

            bool coplanar = IncludeMarginInSelection ? defaultBlock.LayoutSize.z == 0 : defaultBlock.CalculatedSize.Value.z == 0;

            float4x4 commonToWorld = IncludeMarginInSelection ? defaultBlock.GetParentToWorldMatrix() : defaultBlock.transform.localToWorldMatrix;
            float4x4 worldToCommon = math.inverse(commonToWorld);

            if (AdjustingMultipleObjects)
            {
                for (int i = 0; i < UIBlocks.Count; ++i)
                {
                    UIBlock block = UIBlocks[i];

                    if (!block.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    float4x4 localToWorld = IncludeMarginInSelection ? block.GetParentToWorldMatrix() : block.transform.localToWorldMatrix;
                    Bounds blockBounds = IncludeMarginInSelection ? new Bounds(block.GetCalculatedTransformLocalPosition() - block.CalculatedMargin.Offset, block.GetScaledLayoutSize()) : new Bounds(Vector3.zero, block.RotatedSize);

                    if (coplanar)
                    {
                        coplanar = Math.AreCoplanar(ref worldToCommon, ref localToWorld) && blockBounds.size.z == 0;

                        if (!coplanar) // coplanarity broken, move into world space
                        {
                            totalBounds = HandleUtils.TransformBounds(totalBounds, commonToWorld);
                        }
                    }

                    float4x4 localToCommon = coplanar ? math.mul(worldToCommon, localToWorld) : localToWorld;

                    blockBounds = HandleUtils.TransformBounds(blockBounds, localToCommon);

                    if (i == 0)
                    {
                        totalBounds = blockBounds;
                    }
                    else
                    {
                        totalBounds.Encapsulate(blockBounds);
                    }
                }
            }

            if (coplanar || !AdjustingMultipleObjects)
            {
                if (AdjustingMultipleObjects)
                {
                    boundsToWorld = Matrix4x4.Rotate(defaultBlock.transform.rotation);
                    totalBounds = HandleUtils.TransformBounds(totalBounds, boundsToWorld.inverse * ((Matrix4x4)commonToWorld));
                }
                else
                {
                    totalBounds = IncludeMarginInSelection ? BlockBoundsParentSpace : BlockBoundsLocalSpace;
                    boundsToWorld = (Matrix4x4)commonToWorld;
                }
            }
            else
            {
                boundsToWorld = Matrix4x4.identity;
            }

            totalBounds.center = Math.ApproximatelyZeroToZero(totalBounds.center);
            totalBounds.size = Math.ApproximatelyZeroToZero(totalBounds.size);

            return totalBounds;
        }

        private protected bool IsTopLevel(UIBlock uiBlock)
        {
            return topLevel.Contains(uiBlock);
        }

        private protected void RegisterControlTooltip(int controlID, Tooltip tooltip)
        {
            if (controls.ContainsKey(controlID))
            {
                return;
            }

            controls.Add(controlID, tooltip);
        }

        private protected static Vector3 RoundingPrecision(Length3 lengths)
        {
            ThreeD<bool> values = lengths.Type == LengthType.Value;
            return new Vector3(values.X ? ValuePrecision : PercentPrecision,
                               values.Y ? ValuePrecision : PercentPrecision,
                               values.Z ? ValuePrecision : PercentPrecision);
        }

        private protected virtual void BeforeToolGUI() { }
        private protected virtual void AfterToolGUI() { }

        protected abstract void DoToolGUI();

        public override void OnToolGUI(EditorWindow editorWindow)
        {
            controls.Clear();

            sceneView = editorWindow as SceneView;
            sceneViewCamera = sceneView.camera;

            if (UIBlock == null || !UIBlock.gameObject.activeInHierarchy || sceneViewCamera == null)
            {
                return;
            }

            EventType currentEvent = Event.current.type;

            bool repainting = currentEvent == EventType.Repaint;

            Instance = this;

            if (!repainting)
            {
                UpdateSelectionBounds();
            }

            UpdateHighlight();

            BeforeToolGUI();

            ModifiedTarget = false;
            EditorGUI.BeginChangeCheck();
            DoToolGUI();
            ModifiedTarget = EditorGUI.EndChangeCheck();

            if (LayoutPropertyChanged || RenderingPropertyChanged)
            {
                LayoutPropertyChanged = false;
                RenderingPropertyChanged = false;

                EngineManager.Instance.Update();

                for (int i = 0; i < UIBlocks.Count; ++i)
                {
                    UIBlocks[i].CopyFromDataStore();
                }

                UpdateSerializedObjects();
            }

            AfterToolGUI();

            if (repainting)
            {
                DrawHandleTooltip();
            }

            if (ModifiedTarget || Event.current.type == EventType.MouseMove)
            {
                EditModeUtils.QueueEditorUpdateNextFrame();
            }
        }

        private void UpdateSelectionBounds()
        {
            SelectionBounds = GetSelectionBounds(out Matrix4x4 selectionToWorld);
            SelectionToWorld = selectionToWorld;
            WorldToSelection = selectionToWorld.inverse;
        }

        private void UpdateHighlight()
        {
            if (!(EditorWindow.focusedWindow is SceneView))
            {
                SelectionHovered = false;
                sceneHoveredBlock = null;
                return;
            }

            if (GUIUtility.hotControl != 0 || ToolHovered)
            {
                SelectionHovered = true;
                sceneHoveredBlock = null;

                return;
            }

            Event evt = Event.current;

            switch (evt.type)
            {
                case EventType.Repaint:
                    UpdateHighlightVisuals();
                    break;
                case EventType.Layout:
                    UpdateSceneViewHover();
                    break;
            }
        }

        private void UpdateSceneViewHover()
        {
            Event evt = Event.current;

            Ray mouse = HandleUtility.GUIPointToWorldRay(evt.mousePosition);

            float3 center = SelectionBounds.center;
            float3 size = SelectionBounds.size;

            if (!Math.ValidAndFinite(ref center) || !Math.ValidAndFinite(ref size))
            {
                return;
            }

            SelectionHovered = SelectionBounds.IntersectRay(HandleUtils.TransformRay(mouse, WorldToSelection));
            object rayHit = HandleUtility.RaySnap(mouse);

            bool hitNonUIBlock = rayHit != null && (rayHit is RaycastHit hit) && hit.transform.GetComponent<UIBlock>() == null;

            if ((SceneViewInput.ShiftOrControl() || !SelectionHovered) && !hitNonUIBlock)
            {
                bool hitUIBlock = SceneViewInput.HitTest(mouse, out HitTestResult result);
                sceneHoveredBlock = hitUIBlock ? result.HitBlock as UIBlock : null;
            }
            else
            {
                sceneHoveredBlock = null;
            }

        }

        private void UpdateHighlightVisuals()
        {
            Event evt = Event.current;

            bool highlight = (EditorGUI.actionKey || evt.shift) && AdjustingMultipleObjects;
            Color hover = highlight ? HighlightColor : HoverColor;

            UIBlock hoveredBlock = sceneHoveredBlock != null ? sceneHoveredBlock : HierarchyHoveredBlock;

            if (hoveredBlock != null)
            {
                using (new Handles.DrawingScope(hover, hoveredBlock.transform.localToWorldMatrix))
                {
                    Bounds hoverBounds = new Bounds(Vector3.zero, hoveredBlock.CalculatedSize.Value);

                    switch (hoveredBlock)
                    {
                        case UIBlock2D uiBlock2d:
                            HandleUtils.DrawRoundedCornerRectOutline(hoverBounds, uiBlock2d.CalculatedVisuals.CornerRadius.Value, BoldLineWidth);
                            break;
                        default:
                            HandleUtils.DrawWireCube(hoverBounds.center, hoverBounds.size, BoldLineWidth);
                            break;
                    }
                }
            }

            if (highlight)
            {
                for (int i = 0; i < UIBlocks.Count; ++i)
                {
                    UIBlock block = UIBlocks[i];

                    if (block == sceneHoveredBlock || block == HierarchyHoveredBlock)
                    {
                        continue;
                    }

                    using (new Handles.DrawingScope(HoverColor, block.transform.localToWorldMatrix))
                    {
                        switch (block)
                        {
                            case UIBlock2D uiBlock2d:
                                HandleUtils.DrawRoundedCornerRectOutline(new Bounds(Vector3.zero, block.CalculatedSize.Value), uiBlock2d.CalculatedVisuals.CornerRadius.Value);
                                break;
                            default:
                                HandleUtils.DrawWireCube(Vector3.zero, block.CalculatedSize.Value, BoldLineWidth);
                                break;
                        }
                    }
                }
            }
        }

        private void DrawHandleTooltip()
        {
            if (GUIUtility.hotControl == 0)
            {
                return;
            }

            if (!controls.TryGetValue(GUIUtility.hotControl, out Tooltip tooltip))
            {
                return;
            }

            using (new Handles.DrawingScope(Matrix4x4.identity))
            {
                GUIStyle style = new GUIStyle()
                {
                    fontSize = 8,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = new GUIStyleState()
                    {
                        textColor = TooltipTextColor,
                        background = tooltipBackground
                    },
                };

                GUIContent label = new GUIContent(tooltip.LabelGetter?.Invoke());

                Vector2 tooltipSize = style.CalcSize(label);

                const float pixels = 5;

                Vector2 tooltipDirection = new Vector2(-0.5f, 1);

                Vector2 offset = new Vector2(0, pixels) + (tooltipSize * 0.5f * tooltipDirection);
                Ray ray = HandleUtility.GUIPointToWorldRay(tooltip.HandleGuiPoint + offset);

                Rect guiRect = HandleUtility.WorldPointToSizedRect(ray.origin, label, style);
                Handles.BeginGUI();
                EditorGUI.DropShadowLabel(guiRect, label, style);
                Handles.EndGUI();
            }
        }

        protected float SmallHandleSizeScaledParentSpace(Vector3 position)
        {
            return HandleUtility.GetHandleSize(position) * ScaledHandleSize * 0.5f;
        }

        protected float SmallHandleSizeScaledLocalSpace(Vector3 position)
        {
            return SmallHandleSizeScaledParentSpace(position) / math.cmax(UIBlock.transform.localScale);
        }

        public static Length.Calculated GetSideLength(LengthBounds.Calculated bounds, int axis, float direction)
        {
            switch (axis)
            {
                case 1:
                    return direction < 0 ? bounds.Bottom : bounds.Top;
                case 2:
                    return direction < 0 ? bounds.Front : bounds.Back;
                default:
                    return direction < 0 ? bounds.Left : bounds.Right;
            }
        }

        protected static bool IsActionEvent(EventType eventType) => eventType != EventType.Repaint && eventType != EventType.Layout;

        private static void SetActiveToolOrFallback(System.Type toolType, System.Type targetType)
        {
            EditorToolAttribute attribute = GetEditorToolAttribute(toolType);

            if (attribute != null && attribute.targetType != null)
            {
                if (!attribute.targetType.IsAssignableFrom(targetType))
                {
                    FallbackToolAttribute fallback = GetFallbackToolAttribute(toolType);

                    if (fallback != null && fallback.FallbackTool != null)
                    {
                        SetActiveToolOrFallback(fallback.FallbackTool, targetType);
                        return;
                    }

                    ToolManager.RestorePreviousPersistentTool();

                    return;
                }
            }

            try
            {
                // It's not *awesome* that this is in a try block, but 2022 started throwing exceptions
                // when you call SetActiveTool
                ToolManager.SetActiveTool(toolType);
            }
            catch
            {
                FallbackToolAttribute fallback = GetFallbackToolAttribute(toolType);

                if (fallback != null && fallback.FallbackTool != null)
                {
                    SetActiveToolOrFallback(fallback.FallbackTool, targetType);
                }
            }
        }

        private static EditorToolAttribute GetEditorToolAttribute(System.Type type)
        {
            return type.GetCustomAttributes(typeof(EditorToolAttribute), false).FirstOrDefault() as EditorToolAttribute;
        }

        private static FallbackToolAttribute GetFallbackToolAttribute(System.Type type)
        {
            return type.GetCustomAttributes(typeof(FallbackToolAttribute), false).FirstOrDefault() as FallbackToolAttribute;
        }
    }
}
