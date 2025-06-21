// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Editor.GUIs;
using Nova.Editor.Utilities;
using Nova.Extensions;
using Nova.Internal;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.Tools
{
    internal class BlockTool : UIEdgeSnapTool
    {
        public override GUIContent toolbarIcon
        {
            get
            {
                return new GUIContent(EditorGUIUtility.IconContent($"{Labels.IconPath}/UIBlockToolIcon.png")) { tooltip = "UI Block Tool" };
            }
        }

        private List<Bounds> uiBlockStartBounds = new List<Bounds>();
        private Vector3 uiBlockStartLayoutOffset = Vector3.zero;

        private Matrix4x4 startBoundsToWorld = Matrix4x4.identity;
        private Matrix4x4 worldToStartBounds = Matrix4x4.identity;
        private Bounds totalStartBounds = default(Bounds);

        private protected LayoutEdgeSnapHandle SizeHandles { get; private set; } = new LayoutEdgeSnapHandle();
        protected override bool ToolHovered => SizeHandles.Hovering;

        protected override void OnToolActivated()
        {
            SizeHandles.SetColor(SizeColor);
            SizeHandles.DrawHandles = DrawHandle;
            SizeHandles.midpointHandleSizeFunction = SmallHandleSizeScaledParentSpace;
            SizeHandles.TryGetClosestPoint = TryGetNearestEdgePoint;
            SizeHandles.OnSnapToHitID = DrawGuideForHitID;
        }

        protected override void DoToolGUI()
        {
            DoSizeTool();

            DoPositionTool();
        }

        private void DoSizeTool()
        {
            Bounds bounds = SelectionBounds;
            Matrix4x4 boundsToWorld = SelectionToWorld;

            SizeHandles.axes = bounds.extents.z == 0 ? Axes2D : CameraPlaneAxes;
            SizeHandles.size = bounds.size;
            SizeHandles.center = bounds.center;
            SizeHandles.BoundsToDraw = bounds;

            SizeHandles.DefaultToAspectLocked = AspectRatioLocked;

            using (new Handles.DrawingScope(SizeColor, boundsToWorld))
            {
                EditorGUI.BeginChangeCheck();
                HandleUtils.BeginManipulationCheck();

                // Do we want this?
                // DrawUnrotatedBounds();

                SizeHandles.DrawHandle();

                bool handleClicked = HandleUtils.EndManipulationCheck() == HandleManipulation.Started;

                if (handleClicked)
                {
                    totalStartBounds = bounds;

                    Vector3 startingBoundsCenter = boundsToWorld.MultiplyPoint(totalStartBounds.center);
                    startBoundsToWorld = Matrix4x4.TRS(startingBoundsCenter, boundsToWorld.rotation, Vector3.one);
                    worldToStartBounds = startBoundsToWorld.inverse;

                    RefreshStartingBlockSizes(ref worldToStartBounds);
                }

                if (SizeHandles.ControlID != 0)
                {
                    RegisterControlTooltip(SizeHandles.ControlID, new Tooltip()
                    {
                        HandleGuiPoint = HandleUtils.LowestCenterPointInGUISpace(bounds),
                        LabelGetter = GetSizeTooltip
                    });
                }

                if (!EditorGUI.EndChangeCheck())
                {
                    return;
                }

                UpdateSizesAndPositions(ref boundsToWorld, new Bounds(SizeHandles.center, SizeHandles.size));
            }

            EngineManager.Instance.Update();

            Bounds recalculatedBounds = GetSelectionBounds(out Matrix4x4 recalculatedBoundsToWorld);

            using (new Handles.DrawingScope(SizeColor, recalculatedBoundsToWorld))
            {
                if (SizeHandles.TryGetEdgeSnappedBounds(recalculatedBounds, out Bounds snappedBounds))
                {
                    SizeHandles.size = snappedBounds.size;
                    SizeHandles.center = snappedBounds.center;

                    UpdateSizesAndPositions(ref recalculatedBoundsToWorld, snappedBounds);
                }
            }
        }

        private void UpdateSizesAndPositions(ref Matrix4x4 boundsToWorld, Bounds handleBounds)
        {
            Vector3 sizeScalar = Math.RoundWithinEpsilon(handleBounds.size.DividedBy(totalStartBounds.size, divideByZeroValue: 1));

            Vector3 adjustedBoundsCenter = boundsToWorld.MultiplyPoint(handleBounds.center);

            Matrix4x4 adjustedBoundsToWorld = Matrix4x4.TRS(adjustedBoundsCenter, startBoundsToWorld.rotation, Vector3.Scale(startBoundsToWorld.lossyScale, sizeScalar));

            if (!AdjustingMultipleObjects)
            {
                UpdateSizeAndPosition(UIBlocks[0], uiBlockStartBounds[0], sizeScalar, ref adjustedBoundsToWorld, uiBlockStartLayoutOffset);
            }
            else
            {
                for (int i = 0; i < UIBlocks.Count; ++i)
                {
                    UpdateSizeAndPosition(UIBlocks[i], uiBlockStartBounds[i], sizeScalar, ref adjustedBoundsToWorld);
                }
            }
        }

        private void UpdateSizeAndPosition(UIBlock uiBlock, Bounds startingBounds, Vector3 sizeScalar, ref Matrix4x4 adjustedBoundsToWorld, Vector3 startingLayoutOffset = default)
        {
            IUIBlock parentBlock = uiBlock.GetParentBlock();
            bool hasParent = parentBlock != null;
            Vector3 parentSize = LayoutTransformRecorder.GetParentSize(uiBlock);
            Vector3 parentPaddingOffset = hasParent ? (Vector3)parentBlock.CalculatedPadding.Offset : Vector3.zero;
            Matrix4x4 parentToWorld = uiBlock.GetParentToWorldMatrix();
            Matrix4x4 worldToParent = Unity.Mathematics.math.inverse(parentToWorld);

            Quaternion relativeRotation = adjustedBoundsToWorld.rotation * Quaternion.Inverse(uiBlock.transform.rotation);

            // Round because Matrix4x4.rotation appears to drift,
            // and it's causing what should identity quaternions
            // to be slightly rotated
            relativeRotation = (Unity.Mathematics.quaternion)Math.RoundWithinEpsilon(((Unity.Mathematics.quaternion)relativeRotation).value);

            sizeScalar = relativeRotation * sizeScalar;
            sizeScalar = Vector3.Max(-sizeScalar, sizeScalar);

            ThreeD<AutoSize> autosize = uiBlock.AutoSize;

            bool lockAspect = uiBlock.AspectRatioAxis != Axis.None;

            sizeScalar = new Vector3(autosize.X != AutoSize.None || (lockAspect && uiBlock.AspectRatioAxis != Axis.X) ? 1 : sizeScalar.x,
                                     autosize.Y != AutoSize.None || (lockAspect && uiBlock.AspectRatioAxis != Axis.Y) ? 1 : sizeScalar.y,
                                     autosize.Z != AutoSize.None || (lockAspect && uiBlock.AspectRatioAxis != Axis.Z) ? 1 : sizeScalar.z);

            Vector3 adjustedSize = HandleUtils.RoundToNearest(Vector3.Scale(startingBounds.size, sizeScalar));
            Vector3 snappedHandleSize = Math.RoundWithinEpsilon(uiBlock.SizeMinMax.Clamp(adjustedSize));

            if (uiBlock.CalculatedSize.Value == snappedHandleSize)
            {
                return;
            }

            if (!LayoutPropertyChanged)
            {
                Undo.RecordObjects(Targets, "UI Block Tool");
            }

            LayoutPropertyChanged = true;

            if (!hasParent)
            {
                parentSize = startingBounds.size;
            }

            uiBlock.Size.Raw = HandleUtils.RoundToNearest(Length3.GetRawValue(snappedHandleSize, uiBlock.Size, uiBlock.SizeMinMax, parentSize));

            if (!hasParent)
            {
                uiBlock.PreviewSize = snappedHandleSize;
                Internal.Layouts.LayoutDataStore.EditorOnly_QueuePreviewSizeRefresh();
                parentSize = Vector3.zero;
            }

            if (!IsTopLevel(uiBlock))
            {
                // Only modifying position of top level UIBlocks.
                // modifying nested transforms will cause the adjusted
                // selection to jitter as it resizes because we are
                // simultaneously moving/resizing blocks hierarchically
                return;
            }

            Vector3 rotatedSize = uiBlock.RotateSize ? (Vector3)LayoutUtils.RotateSize(snappedHandleSize, uiBlock.transform.localRotation) : snappedHandleSize;
            Vector3 totalSize = Vector3.Scale(rotatedSize, uiBlock.transform.localScale) + uiBlock.CalculatedMargin.Size;

            Vector3 localPosition;

            if (hasParent)
            {
                ThreeD<AutoSize> parentAutoSize = AutoSize.None;
                switch (parentBlock)
                {
                    case UIBlock parentComponent:
                        parentAutoSize = parentComponent.AutoSize;
                        break;
                    case VirtualUIBlock parentObject:
                        parentAutoSize = parentObject.AutoSize;
                        break;
                }

                if (Util.Any(parentAutoSize == AutoSize.Shrink))
                {
                    uiBlock.CalculateLayout();
                    parentSize = LayoutTransformRecorder.GetParentSize(uiBlock);
                }
            }

            if (AdjustingMultipleObjects)
            {
                Vector3 worldPosition = adjustedBoundsToWorld.MultiplyPoint(startingBounds.center);
                localPosition = worldToParent.MultiplyPoint(worldPosition);
            }
            else
            {
                Vector3 alignment = uiBlock.transform.localRotation * SizeHandles.AdjustmentHandle;
                localPosition = LayoutUtils.LayoutOffsetToLocalPosition(startingLayoutOffset, totalSize, uiBlock.CalculatedMargin.Offset, parentSize, parentPaddingOffset, -alignment);
            }

            if (hasParent)
            {
                Vector3 layoutOffset = LayoutUtils.LocalPositionToLayoutOffset(localPosition, totalSize, uiBlock.CalculatedMargin.Offset, parentSize, parentPaddingOffset, (Vector3)uiBlock.Alignment);
                uiBlock.Position.Raw = HandleUtils.RoundToNearest(Length3.GetRawValue(layoutOffset, uiBlock.Position, uiBlock.PositionMinMax, parentSize), RoundingPrecision(uiBlock.Position));
            }
            else if (!SceneViewUtils.IsInCurrentPrefabStage(uiBlock.gameObject))
            {
                Undo.RecordObject(uiBlock.transform, "UI Block Tool");
                uiBlock.transform.localPosition = HandleUtils.RoundToNearest(localPosition);
            }
        }

        private bool AspectRatioLocked
        {
            get
            {
                for (int i = 0; i < UIBlocks.Count; ++i)
                {
                    if (UIBlocks[i].AspectRatioAxis == Axis.None)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private void RefreshStartingBlockSizes(ref Matrix4x4 worldToBounds)
        {
            uiBlockStartBounds.Clear();
            for (int i = 0; i < UIBlocks.Count; ++i)
            {
                UIBlock uiBlock = UIBlocks[i];
                uiBlockStartBounds.Add(new Bounds(worldToBounds.MultiplyPoint(uiBlock.transform.position), uiBlock.CalculatedSize.Value));
            }

            if (!AdjustingMultipleObjects)
            {
                UIBlock uiBlock = UIBlocks[0];

                Vector3 alignment = uiBlock.transform.localRotation * SizeHandles.AdjustmentHandle;

                Vector3 position = uiBlock.GetCalculatedTransformLocalPosition();
                Vector3 size = uiBlock.GetScaledLayoutSize();

                IUIBlock parent = uiBlock.GetParentBlock();
                bool hasParent = parent != null;

                Vector3 parentSize = hasParent ? parent.PaddedSize : Vector3.zero;
                Vector3 paddingOffset = hasParent ? (Vector3)parent.CalculatedPadding.Offset : Vector3.zero;

                Vector3 layoutOffset = LayoutUtils.LocalPositionToLayoutOffset(position, size, uiBlock.CalculatedMargin.Offset, parentSize, paddingOffset, -alignment);
                uiBlockStartLayoutOffset = layoutOffset;
            }
        }

        private void DrawHandle(Vector3 position, Quaternion rotation, float size, bool active)
        {
            Color faceColor = active ? HighlightColor : Color.white;
            Color outlineColor = active ? HighlightColor : Handles.color;
            Color shadowColor = active ? HighlightColor : ShadowColor;

            HandleUtils.OutlinedRectHandleCap(position, rotation, size, faceColor, outlineColor, shadowColor);
        }

        private string GetSizeTooltip()
        {
            Vector3 size = UIBlock.CalculatedSize.Value;

            return size.z == 0 ? $"{size.x.ToString("F2")} x {size.y.ToString("F2")}" : $"{size.x.ToString("F2")} x {size.y.ToString("F2")} x {size.z.ToString("F2")}";
        }
    }
}
