// Copyright (c) Supernova Technologies LLC
using Nova.Editor.GUIs;
using Nova.Editor.Utilities;
using Nova.Internal;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Text;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Nova.Editor.Tools
{
#if UNITY_2021_2_OR_NEWER
    [EditorTool(displayName: "Spacing Tool", componentToolTarget: typeof(UIBlock))]
#else
    [EditorTool(displayName: "Spacing Tool", targetType = typeof(UIBlock))]
#endif
    internal class SpacingTool : UIEdgeSnapTool
    {
        public static SpacingTool TypedInstance = null;

        private void OnEnable()
        {
            TypedInstance = this;
        }
        
        public override GUIContent toolbarIcon
        {
            get
            {
                return new GUIContent(EditorGUIUtility.IconContent($"{Labels.IconPath}/SpacingToolIcon.png")) { tooltip = "Padding/Margin Tool" };
            }
        }

        private const float GUIPixelOffset = 20;

        private BoundsAnim paddingDrawingBounds = null;
        private BoundsAnim marginDrawingBounds = null;

        private protected LayoutEdgeSnapHandle PaddingHandles { get; private set; } = new LayoutEdgeSnapHandle();
        private protected LayoutEdgeSnapHandle MarginHandles { get; private set; } = new LayoutEdgeSnapHandle();

        private bool ShowAssistedPaddingHandleBounds => !MarginHandles.Hovering && PaddingHandles.Hovering && !PaddingHandles.IsSelected && !PositionToolSelected;
        private bool ShowAssistedMarginHandleBounds => !PaddingHandles.Hovering && MarginHandles.Hovering && !MarginHandles.IsSelected && !PositionToolSelected;

        protected override bool ToolHovered => MarginHandles.Hovering || PaddingHandles.Hovering;
        protected override bool IncludeMarginInSelection => true;

        protected override void OnToolActivated()
        {
            MarginHandles.SetColor(MarginColor);
            MarginHandles.DrawHandles = DrawHandle;
            MarginHandles.midpointHandleSizeFunction = SmallHandleSizeScaledParentSpace;
            MarginHandles.TryGetClosestPoint = TryGetNearestEdgePoint;
            MarginHandles.OnSnapToHitID = DrawGuideForHitID;

            PaddingHandles.SetColor(PaddingColor);
            PaddingHandles.DrawHandles = DrawHandle;
            PaddingHandles.midpointHandleSizeFunction = SmallHandleSizeScaledLocalSpace;
            PaddingHandles.TryGetClosestPoint = TryGetNearestEdgePoint;
            PaddingHandles.OnSnapToHitID = DrawGuideForHitID;
        }

        protected override void DoToolGUI()
        {
            if (AdjustingMultipleObjects)
            {
                DoPositionTool(drawBounds: true);
                return;
            }

            DoPositionTool();

            bool paddingWasHovering = PaddingHandles.Hovering;
            bool marginWasHovering = MarginHandles.Hovering;

            DoPaddingTool();

            DoMarginTool();

            if ((paddingWasHovering || marginWasHovering || MarginHandles.Hovering || PaddingHandles.Hovering) &&
               (paddingWasHovering == MarginHandles.Hovering || marginWasHovering == PaddingHandles.Hovering))
            {
                HandleUtility.Repaint();
            }
        }

        private void DoPaddingTool()
        {
            EditorGUI.BeginChangeCheck();
            using (new Handles.DrawingScope(PaddingColor, LocalToWorld))
            {
                Bounds blockBounds = new Bounds(Vector3.zero, UIBlock.CalculatedSize.Value);
                Bounds paddingBounds = new Bounds(UIBlock.CalculatedPadding.Offset, UIBlock.PaddedSize);
                Bounds handleBounds = GetAssistedHandleBounds(paddingBounds, blockBounds, GUIPixelOffset);

                if (paddingDrawingBounds == null)
                {
                    paddingDrawingBounds = new BoundsAnim(paddingBounds, LocalToWorld, Repaint);
                    paddingDrawingBounds.speed = AnimationSpeed;
                }

                Bounds drawingBounds = paddingBounds;
                if (ShowAssistedPaddingHandleBounds)
                {
                    if (!paddingDrawingBounds.isAnimating)
                    {
                        Vector3 adjustment = handleBounds.center + Vector3.Scale(handleBounds.extents, PaddingHandles.AdjustmentHandle);
                        Vector3 boundsMax = drawingBounds.max;
                        Vector3 boundsMin = drawingBounds.min;

                        int cameraFacingAxis = blockBounds.size.z == 0 ? 2 : !HandleUtils.AxisIsSet(CameraPlaneAxes, 0) ? 0 : !HandleUtils.AxisIsSet(CameraPlaneAxes, 1) ? 1 : 2;

                        for (int i = 0; i < 3; ++i)
                        {
                            float sign = Math.Sign(PaddingHandles.AdjustmentHandle[i]);

                            if (sign < 0)
                            {
                                boundsMin[i] = adjustment[i];
                            }
                            else if (sign > 0)
                            {
                                boundsMax[i] = adjustment[i];
                            }
                        }

                        boundsMin[cameraFacingAxis] = drawingBounds.min[cameraFacingAxis];
                        boundsMax[cameraFacingAxis] = drawingBounds.max[cameraFacingAxis];

                        drawingBounds.SetMinMax(boundsMin, boundsMax);

                        paddingDrawingBounds.target = drawingBounds;
                        paddingDrawingBounds.StartMatrix = LocalToWorld;
                        paddingDrawingBounds.TargetMatrix = paddingDrawingBounds.StartMatrix;
                    }
                }
                else
                {
                    paddingDrawingBounds.value = paddingBounds;
                }

                PaddingHandles.axes = CameraPlaneAxes;
                PaddingHandles.size = paddingBounds.size;
                PaddingHandles.center = paddingBounds.center;
                PaddingHandles.BoundsToDraw = paddingDrawingBounds.value;
                PaddingHandles.InteractiveBounds = handleBounds;
                PaddingHandles.Interactive = !MarginHandles.Hovering;

                PaddingHandles.DrawHandle();

                if (PaddingHandles.IsSelected)
                {
                    RegisterControlTooltip(PaddingHandles.ControlID, new Tooltip()
                    {
                        HandleGuiPoint = HandleUtils.LowestCenterPointInGUISpace(blockBounds),
                        LabelGetter = () => GetPaddingTooltip(PaddingHandles.AdjustmentHandle),
                    });
                }
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            UpdatePadding(new Bounds(PaddingHandles.center, PaddingHandles.size));

            UIBlock.CalculateLayout();

            using (new Handles.DrawingScope(PaddingColor, LocalToWorld))
            {
                if (PaddingHandles.TryGetEdgeSnappedBounds(new Bounds(UIBlock.CalculatedPadding.Offset, UIBlock.PaddedSize), out Bounds snappedBounds))
                {
                    UpdatePadding(snappedBounds);
                }
            }
        }

        private void UpdatePadding(Bounds handleBounds)
        {
            Vector3 sizeChange = UIBlock.CalculatedSize.Value - handleBounds.size - UIBlock.CalculatedPadding.Size;
            Vector3 min = UIBlock.CalculatedPadding.GetMinEdges().Value;
            Vector3 max = UIBlock.CalculatedPadding.GetMaxEdges().Value;

            for (int i = 0; i < 3; ++i)
            {
                if (PaddingHandles.AdjustmentHandle[i] < 0)
                {
                    min[i] += sizeChange[i];
                }

                if (PaddingHandles.AdjustmentHandle[i] > 0)
                {
                    max[i] += sizeChange[i];
                }
            }

            min = HandleUtils.RoundToNearest(UIBlock.PaddingMinMax.GetMinEdges().Clamp(min));
            max = HandleUtils.RoundToNearest(UIBlock.PaddingMinMax.GetMaxEdges().Clamp(max));

            if (min != UIBlock.CalculatedPadding.GetMinEdges().Value || max != UIBlock.CalculatedPadding.GetMaxEdges().Value)
            {
                if (!LayoutPropertyChanged)
                {
                    Undo.RecordObject(UIBlock, "Padding");
                }

                LayoutPropertyChanged = true;

                for (int i = 0; i < 3; ++i)
                {
                    Length2 padding = UIBlock.Padding[i];
                    Vector2 raw = Length2.GetRawValue(new Vector2(min[i], max[i]), padding, UIBlock.PaddingMinMax[i], Vector2.one * UIBlock.CalculatedSize[i].Value);
                    padding.Raw = HandleUtils.RoundToNearest(raw);
                    UIBlock.Padding[i] = padding;
                }
            }
        }

        private void DoMarginTool()
        {
            IUIBlock parentBlock = UIBlock.GetParentBlock();
            bool hasParent = parentBlock != null;
            Vector3 parentSize = LayoutTransformRecorder.GetParentSize(UIBlock);
            Vector3 parentPaddingOffset = hasParent ? (Vector3)parentBlock.CalculatedPadding.Offset : Vector3.zero;

            EditorGUI.BeginChangeCheck();

            using (new Handles.DrawingScope(MarginColor, ParentLocalToWorld))
            {
                Vector3 localPosition = UIBlock.GetCalculatedTransformLocalPosition();
                Bounds blockBounds = new Bounds(localPosition, Vector3.Scale(UIBlock.RotatedSize, UIBlock.transform.localScale));

                Bounds marginBounds = new Bounds(localPosition - UIBlock.CalculatedMargin.Offset, blockBounds.size + UIBlock.CalculatedMargin.Size);

                Bounds handleBounds = GetAssistedHandleBounds(marginBounds, blockBounds, -GUIPixelOffset);

                if (marginDrawingBounds == null)
                {
                    marginDrawingBounds = new BoundsAnim(marginBounds, ParentLocalToWorld, Repaint);
                    marginDrawingBounds.speed = AnimationSpeed;
                }

                Bounds drawingBounds = marginBounds;
                if (ShowAssistedMarginHandleBounds)
                {
                    if (!marginDrawingBounds.isAnimating)
                    {
                        drawingBounds.Encapsulate(handleBounds.center + Vector3.Scale(handleBounds.extents, MarginHandles.AdjustmentHandle));

                        marginDrawingBounds.target = drawingBounds;
                        marginDrawingBounds.StartMatrix = ParentLocalToWorld;
                        marginDrawingBounds.TargetMatrix = marginDrawingBounds.StartMatrix;
                    }
                }
                else
                {
                    marginDrawingBounds.value = marginBounds;
                }

                MarginHandles.axes = CameraPlaneAxes;
                MarginHandles.size = marginBounds.size;
                MarginHandles.center = marginBounds.center;
                MarginHandles.BoundsToDraw = marginDrawingBounds.value;
                MarginHandles.InteractiveBounds = handleBounds;
                MarginHandles.Interactive = !PaddingHandles.Hovering;

                MarginHandles.DrawHandle();

                if (MarginHandles.IsSelected)
                {
                    RegisterControlTooltip(MarginHandles.ControlID, new Tooltip()
                    {
                        HandleGuiPoint = HandleUtils.LowestCenterPointInGUISpace(blockBounds),
                        LabelGetter = () => GetMarginTooltip(MarginHandles.AdjustmentHandle)
                    });
                }
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            UpdateMargin(parentSize, parentPaddingOffset, new Bounds(MarginHandles.center, MarginHandles.size));
            UIBlock.CalculateLayout();

            using (new Handles.DrawingScope(MarginColor, ParentLocalToWorld))
            {
                if (MarginHandles.TryGetEdgeSnappedBounds(BlockBoundsParentSpace, out Bounds snappedBounds))
                {
                    UpdateMargin(parentSize, parentPaddingOffset, snappedBounds);
                }
            }
        }

        private void UpdateMargin(Vector3 parentSize, Vector3 parentPaddingOffset, Bounds handleBounds)
        {
            Vector3 sizeChange = handleBounds.size - UIBlock.GetScaledLayoutSize();
            Vector3 min = UIBlock.CalculatedMargin.GetMinEdges().Value;
            Vector3 max = UIBlock.CalculatedMargin.GetMaxEdges().Value;

            for (int i = 0; i < 3; ++i)
            {
                if (MarginHandles.AdjustmentHandle[i] < 0)
                {
                    min[i] += sizeChange[i];
                }

                if (MarginHandles.AdjustmentHandle[i] > 0)
                {
                    max[i] += sizeChange[i];
                }
            }

            min = HandleUtils.RoundToNearest(UIBlock.MarginMinMax.GetMinEdges().Clamp(min));
            max = HandleUtils.RoundToNearest(UIBlock.MarginMinMax.GetMaxEdges().Clamp(max));

            if (min != UIBlock.CalculatedMargin.GetMinEdges().Value || max != UIBlock.CalculatedMargin.GetMaxEdges().Value)
            {
                if (!LayoutPropertyChanged)
                {
                    Undo.RecordObject(UIBlock, "Margin");
                }

                LayoutPropertyChanged = true;

                for (int i = 0; i < 3; ++i)
                {
                    Length2 margin = UIBlock.Margin[i];
                    Vector2 raw = Length2.GetRawValue(new Vector2(min[i], max[i]), margin, UIBlock.MarginMinMax[i], Vector2.one * parentSize[i]);
                    margin.Raw = HandleUtils.RoundToNearest(raw);
                    UIBlock.Margin[i] = margin;
                }

                Vector3 localPosition = UIBlock.GetCalculatedTransformLocalPosition();

                Vector3 rotatedSize = Vector3.Scale(UIBlock.RotatedSize, UIBlock.transform.localScale);
                Vector3 layoutOffset = LayoutUtils.LocalPositionToLayoutOffset(localPosition, max + min + rotatedSize, 0.5f * (min - max), parentSize, parentPaddingOffset, (Vector3)UIBlock.Alignment);
                UIBlock.Position.Raw = HandleUtils.RoundToNearest(Length3.GetRawValue(layoutOffset, UIBlock.Position, UIBlock.PositionMinMax, parentSize), RoundingPrecision(UIBlock.Position));
            }
        }

        private Bounds GetAssistedHandleBounds(Bounds boundsToOffset, Bounds referenceBounds, float guiOffset)
        {
            bool expand = Math.Sign(guiOffset) < 0;

            Vector3 min = expand ? boundsToOffset.min : Vector3.one * float.MaxValue;
            Vector3 max = expand ? boundsToOffset.max : Vector3.one * float.MinValue;

            Vector3[] corners = HandleUtils.Corners2D;

            PrimitiveBoundsHandle.Axes axes = referenceBounds.size.z == 0 ? Axes2D : CameraPlaneAxes;

            Vector3 centerToCamera = Camera.current.transform.position - boundsToOffset.center;

            int cameraFacingAxis = !HandleUtils.AxisIsSet(axes, 0) ? 0 : !HandleUtils.AxisIsSet(axes, 1) ? 1 : 2;
            float cameraDirection = Math.Sign(centerToCamera[cameraFacingAxis]);

            for (int i = 0; i < corners.Length; ++i)
            {
                Vector3 corner = corners[i];
                Vector3 cornerOnBounds = boundsToOffset.center + Vector3.Scale(boundsToOffset.extents, HandleUtils.Vector2ToAdjustmentAxesVector3(corner, axes, cameraDirection));

                Vector3 position = HandleUtils.WorldPositionFromBoundsPixelOffset(cornerOnBounds, axes, Math.Sign(guiOffset), referenceBounds, Mathf.Abs(guiOffset));

                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }

            min[cameraFacingAxis] = boundsToOffset.min[cameraFacingAxis];
            max[cameraFacingAxis] = boundsToOffset.max[cameraFacingAxis];

            boundsToOffset.SetMinMax(min, max);

            return boundsToOffset;
        }

        private StringBuilder tooltipBuilder = new StringBuilder();
        private string GetMarginTooltip(Vector3 handle)
        {
            tooltipBuilder.Clear();

            tooltipBuilder.Append("Margin");

            for (int axis = 0; axis < 3; ++axis)
            {
                float direction = Math.Sign(handle[axis]);

                if (direction == 0 || !HandleUtils.AxisIsSet(CameraPlaneAxes, axis))
                {
                    continue;
                }

                tooltipBuilder.Append($"\n{LayoutUtils.GetAlignmentLabel(axis, (int)direction)}: {GetSideLength(UIBlock.CalculatedMargin, axis, direction).Value.ToString("F2")}");
            }

            return tooltipBuilder.ToString();
        }

        private string GetPaddingTooltip(Vector3 handle)
        {
            tooltipBuilder.Clear();

            tooltipBuilder.Append("Padding");

            for (int axis = 0; axis < 3; ++axis)
            {
                float direction = Math.Sign(handle[axis]);

                if (direction == 0 || !HandleUtils.AxisIsSet(CameraPlaneAxes, axis))
                {
                    continue;
                }

                tooltipBuilder.Append($"\n{LayoutUtils.GetAlignmentLabel(axis, (int)direction)}: {GetSideLength(UIBlock.CalculatedPadding, axis, direction).Value.ToString("F2")}");
            }

            return tooltipBuilder.ToString();
        }

        private void DrawHandle(Vector3 position, Quaternion rotation, float size, bool active)
        {
            Color faceColor = active ? HighlightColor : Color.white;
            Color outlineColor = active ? HighlightColor : Handles.color;
            Color shadowColor = active ? HighlightColor : ShadowColor;

            HandleUtils.OutlinedRectHandleCap(position, rotation, size, faceColor, outlineColor, shadowColor);
        }
    }

    internal static class BoundsExtensions
    {
        public static MinMax3 GetMinEdges(ref this MinMaxBounds bounds)
        {
            return new MinMax3(bounds.Left, bounds.Bottom, bounds.Front);
        }

        public static MinMax3 GetMaxEdges(ref this MinMaxBounds bounds)
        {
            return new MinMax3(bounds.Right, bounds.Top, bounds.Back);
        }

        public static Length3.Calculated GetMinEdges(in this LengthBounds.Calculated bounds)
        {
            return new Length3.Calculated(bounds.Left, bounds.Bottom, bounds.Front);
        }

        public static Length3.Calculated GetMaxEdges(in this LengthBounds.Calculated bounds)
        {
            return new Length3.Calculated(bounds.Right, bounds.Top, bounds.Back);
        }
    }
}
