// Copyright (c) Supernova Technologies LLC
//#define DEBUG_GUIDES

using Nova.Editor.Utilities;
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Input;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Nova.Editor.Tools
{
    internal abstract class UIEdgeSnapTool : UITool
    {
        public const float EdgeSnapPointSize = 0.03f;
        private const float ShowEdgeGuideGUIThreshold = LayoutEdgeSnapHandle.GUISpaceSnapDistance;

        private Vector3 positionHandleDirection1 = Vector3.right;
        private Vector3 positionHandleDirection2 = Vector3.up;

        [System.NonSerialized]
        private int positionHandleControlID = 0;

        public const int MaxHitSnapPoints = 2;

        [System.NonSerialized]
        List<EdgeHitResult> hits = new List<EdgeHitResult>();
        private Dictionary<UID<EdgeHitResult>, System.Action> drawEdgeGuides = new Dictionary<UID<EdgeHitResult>, System.Action>();
        private HashList<UID<EdgeHitResult>> edgeGuideIDs = new HashList<UID<EdgeHitResult>>();

        [System.NonSerialized]
        private List<Vector3> edgeGuidePoints = new List<Vector3>();

        [System.NonSerialized]
        private List<Ray> edgeGuideRays = new List<Ray>();

        protected bool PositionToolSelected => positionHandleControlID != 0;

        protected PrimitiveBoundsHandle.Axes CameraPlaneAxes { get; private set; }

        [System.NonSerialized]
        private Vector3[] EdgeSnapRectVerts = new Vector3[4];

        protected bool DrawEdgeGuides { get; set; } = false;

        sealed private protected override void BeforeToolGUI()
        {
            EventType currentEvent = Event.current.type;

            GetCameraPlaneAxes();

            if (currentEvent == EventType.MouseMove ||
                currentEvent == EventType.MouseDrag ||
                currentEvent == EventType.MouseUp)
            {
                drawEdgeGuides.Clear();
                edgeGuideIDs.Clear();
                edgeGuideRays.Clear();
                edgeGuidePoints.Clear();
            }
        }

        sealed private protected override void AfterToolGUI()
        {
            EventType evt = Event.current.type;
            bool repainting = evt == EventType.Repaint;

            if (repainting)
            {
                for (int i = 0; i < edgeGuideIDs.Count; ++i)
                {
                    if (!drawEdgeGuides.TryGetValue(edgeGuideIDs[i], out System.Action draw))
                    {
                        Debug.LogWarning($"[{i}/{edgeGuideIDs.Count}, {drawEdgeGuides.Count}] {edgeGuideIDs[i]} not found");
                    }

                    draw.Invoke();
                }
            }
        }

        private protected void DrawGuideForHitID(UID<EdgeHitResult> hitID)
        {
            edgeGuideIDs.Add(hitID);
        }

        protected bool TryGetNearestEdgePoint(Bounds selectionBounds, Vector3 movementAxis, Ray ray, out Vector3 positionInWorldSpace, out UID<EdgeHitResult> hitID)
        {
            hitID = UID<EdgeHitResult>.Invalid;
            positionInWorldSpace = Vector3.one * float.MaxValue;
            EdgeHitResult hit = default;

            if (!NovaEditorPrefs.EdgeSnappingEnabled)
            {
                return false;
            }

            if (!SceneViewInput.DetectEdges(ray, hits, Camera, UIBlockIDs, max: UIBlocks.Count + MaxHitSnapPoints))
            {
                return false;
            }

            int index = 0;

            for (int i = 0; i < hits.Count; i++)
            {
                if (!UIBlockSet.Contains(hits[i].HitBlock as UIBlock))
                {
                    index = i;
                    break;
                }

                if (i == hits.Count - 1)
                {
                    return false;
                }
            }

            hit = hits[index];

            positionInWorldSpace = hit.HitPoint;

            hitID = UID<EdgeHitResult>.Create();

            if (TryAddEdgeGuide(positionInWorldSpace, ray))
            {
                Matrix4x4 handlesMatrix = Handles.matrix;
                drawEdgeGuides[hitID] = () => DrawEdgeGuidesInternal(ray.direction, hit);
                return true;
            }


            return false;
        }

        private void DrawDebugPoint(Vector3 worldPoint, Color color)
        {
            using (new Handles.DrawingScope(color, Matrix4x4.identity))
            {
                UnityEngine.Rendering.CompareFunction zTest = Handles.zTest;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                Handles.DrawSolidDisc(worldPoint, Vector3.right, 0.4f * ScaledHandleSize * HandleUtility.GetHandleSize(worldPoint));
                Handles.DrawSolidDisc(worldPoint, Vector3.up, 0.4f * ScaledHandleSize * HandleUtility.GetHandleSize(worldPoint));
                Handles.DrawSolidDisc(worldPoint, Vector3.forward, 0.4f * ScaledHandleSize * HandleUtility.GetHandleSize(worldPoint));
                Handles.zTest = zTest;
            }
        }

        private void DrawDebugLine(Vector3 p1, Vector3 p2, Color color)
        {
            using (new Handles.DrawingScope(color, Matrix4x4.identity))
            {
                UnityEngine.Rendering.CompareFunction zTest = Handles.zTest;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                Handles.DrawLine(p1, p2);
                Handles.color = Color.black;
                Handles.DrawSolidDisc(p1, Vector3.forward, 0.4f * ScaledHandleSize * HandleUtility.GetHandleSize(p1));
                Handles.color = Color.white;
                Handles.DrawWireDisc(p1, Vector3.forward, 0.4f * ScaledHandleSize * HandleUtility.GetHandleSize(p1));
                Handles.zTest = zTest;
            }
        }

        private bool TryAddEdgeGuide(Unity.Mathematics.float3 snappedPosition, Ray snappedRay)
        {
            Vector2 guiSpaceSnapPoint = HandleUtility.WorldToGUIPoint(snappedPosition);
            Vector2 guiSpaceRayOrigin = HandleUtility.WorldToGUIPoint(snappedRay.origin);
            float snapDistance = Vector2.Distance(guiSpaceSnapPoint, guiSpaceRayOrigin);

            if (snapDistance >= ShowEdgeGuideGUIThreshold)
            {
                return false;
            }

            Unity.Mathematics.float3 snappedDirection = snappedRay.direction;
            Unity.Mathematics.float3 oppositeSnappedDirection = -snappedRay.direction;
            Unity.Mathematics.float3 raycastPosition = snappedRay.direction;

            for (int i = 0; i < edgeGuidePoints.Count; ++i)
            {
                Unity.Mathematics.float3 guidePoint = edgeGuidePoints[i];
                Unity.Mathematics.float3 guideDirection = edgeGuideRays[i].direction;
                Unity.Mathematics.float3 guideOrigin = edgeGuideRays[i].origin;

                if (Unity.Mathematics.math.all(Math.ApproximatelyEqual3(ref guidePoint, ref snappedPosition)) &&
                    (Unity.Mathematics.math.all(Math.ApproximatelyEqual3(ref guideDirection, ref snappedDirection)) ||
                    Unity.Mathematics.math.all(Math.ApproximatelyEqual3(ref guideDirection, ref oppositeSnappedDirection))) &&
                    Unity.Mathematics.math.all(Math.ApproximatelyEqual3(ref guideOrigin, ref raycastPosition)))
                {
                    return false;
                }
            }

            edgeGuidePoints.Add(snappedPosition);
            edgeGuideRays.Add(snappedRay);

            return true;
        }

        private void DrawEdgeGuidesInternal(Vector3 mouseDirectionWorldSpace, EdgeHitResult hit)
        {
            Bounds selectionBounds = SelectionBounds;
            Matrix4x4 selectionToWorld = SelectionToWorld;

            UIBlock hitBlock = hit.HitBlock as UIBlock;
            Matrix4x4 hitToWorld = hit.ParentSpace ? hitBlock.GetParentToWorldMatrix() : hitBlock.transform.localToWorldMatrix;
            Matrix4x4 worldToHit = hitToWorld.inverse;
            Bounds hitBounds = hit.HitBounds;
            Vector3 snappedPosition = hit.HitPoint;

            bool flat = Math.ApproximatelyZero(selectionBounds.size.z);
            Vector3 positionInHitSpace = worldToHit.MultiplyPoint(selectionToWorld.MultiplyPoint(selectionBounds.center));
            Vector3 selectionForwardInWorldSpace = selectionToWorld.MultiplyVector(Vector3.forward);
            Vector3 hitForwardInWorldSpace = hitToWorld.MultiplyVector(Vector3.forward);
            float dotForward = Vector3.Dot(selectionForwardInWorldSpace, hitForwardInWorldSpace);

            bool parallelForward = Math.ApproximatelyEqual(Mathf.Abs(dotForward), 1);
            bool axisAlignedRotation = parallelForward || Math.ApproximatelyZero(dotForward);

            bool coplanar = flat && // depth is 0
                            Math.ApproximatelyEqual(positionInHitSpace.z, hitBounds.center.z) && // on same z plane
                            parallelForward; // parallel forward direction

            Vector3 snapInDirectionInHitSpace = worldToHit.MultiplyVector(mouseDirectionWorldSpace);
            Vector3 snappedPositionInHitSpace = worldToHit.MultiplyPoint(snappedPosition);

            Bounds selectionBoundsInWorldSpace = HandleUtils.TransformBounds(selectionBounds, selectionToWorld);
            Bounds hitBoundsInWorldSpace = HandleUtils.TransformBounds(hitBounds, hitToWorld);
            Vector3 closestPointOnHit = hitToWorld.MultiplyPoint(hitBounds.ClosestPoint(snappedPositionInHitSpace));

            Vector3 hitPositionInHitSpace = worldToHit.MultiplyPoint(closestPointOnHit);
            Vector3 forwardInHitSpace = coplanar ? worldToHit.MultiplyVector(mouseDirectionWorldSpace) :
                               // not coplanar
                               new Vector3(Math.ApproximatelyEqual(snappedPositionInHitSpace.x, hitPositionInHitSpace.x) ? snapInDirectionInHitSpace.x : 0,
                                                               Math.ApproximatelyEqual(snappedPositionInHitSpace.y, hitPositionInHitSpace.y) ? snapInDirectionInHitSpace.y : 0,
                                                               Math.ApproximatelyEqual(snappedPositionInHitSpace.z, hitPositionInHitSpace.z) ? snapInDirectionInHitSpace.z : 0).normalized;
            if (forwardInHitSpace == Vector3.zero)
            {
                // Don't draw guides, just highlight relevant bounds
                goto HighlightHitBounds;
            }

            using (new Handles.DrawingScope(EdgeGuideColor, Matrix4x4.identity))
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

                Vector3 snapToHit = hitPositionInHitSpace - snappedPositionInHitSpace;

                Vector3 rayDir = HandleUtils.GetAxisSnappedDirection(Vector3.Cross(Vector3.forward, snapInDirectionInHitSpace), 45);

                if (snapToHit != Vector3.zero)
                {
                    snapToHit = HandleUtils.GetAxisSnappedDirection(snapToHit.normalized, 45);
                    rayDir = Vector3.Cross(Vector3.Cross(Vector3.forward, snapToHit), Vector3.forward).normalized;
                }

                Ray intersectionRay = new Ray((closestPointOnHit + snappedPosition) * 0.5f, hitToWorld.MultiplyVector(rayDir));

                if (coplanar)
                {
                    float distanceToCamera = Vector3.Distance(Camera.transform.position, intersectionRay.origin);
                    Vector3 viewportMin = Camera.ViewportToWorldPoint(new Vector3(0, 0, distanceToCamera));
                    Vector3 viewportMax = Camera.ViewportToWorldPoint(new Vector3(1, 1, distanceToCamera));

                    float distance = 0.5f * Vector3.Distance(viewportMin, viewportMax);

                    Vector3 p1 = intersectionRay.GetPoint(distance);
                    Vector3 p2 = intersectionRay.GetPoint(-distance);

                    HandleUtils.DrawPolyLine(BoldLineWidth, false, p1, p2);

                    Vector2 p1GuiSpace = HandleUtility.WorldToGUIPoint(p1);
                    Vector2 p2GuiSpace = HandleUtility.WorldToGUIPoint(p2);

                    Vector3[] snapPoints = HandleUtils.SnapPoints2D;

                    for (int i = 0; i < snapPoints.Length; ++i)
                    {
                        Vector3 snapPoint = snapPoints[i];

                        Vector3 hitPoint = hitToWorld.MultiplyPoint(hitBounds.center + Vector3.Scale(hitBounds.extents, snapPoint));
                        Vector3 selectionPoint = selectionToWorld.MultiplyPoint(selectionBounds.center + Vector3.Scale(selectionBounds.extents, snapPoint));

                        float distanceHitToLine = HandleUtility.DistancePointToLine(HandleUtility.WorldToGUIPoint(hitPoint), p1GuiSpace, p2GuiSpace);
                        float distanceSelectionToLine = HandleUtility.DistancePointToLine(HandleUtility.WorldToGUIPoint(selectionPoint), p1GuiSpace, p2GuiSpace);

                        if (distanceHitToLine < ShowEdgeGuideGUIThreshold)
                        {
                            Handles.color = ShadowColor;
                            Handles.DrawSolidDisc(hitPoint, Camera.transform.forward, HandleUtility.GetHandleSize(hitPoint) * EdgeSnapPointSize);
                            Handles.color = EdgeGuideAccentColor;
                            HandleUtils.DrawWireDisc(hitPoint, Camera.transform.forward, HandleUtility.GetHandleSize(hitPoint) * EdgeSnapPointSize);
                        }

                        if (distanceSelectionToLine < ShowEdgeGuideGUIThreshold)
                        {
                            Handles.color = ShadowColor;
                            Handles.DrawSolidDisc(selectionPoint, Camera.transform.forward, HandleUtility.GetHandleSize(selectionPoint) * EdgeSnapPointSize);
                            Handles.color = EdgeGuideAccentColor;
                            HandleUtils.DrawWireDisc(selectionPoint, Camera.transform.forward, HandleUtility.GetHandleSize(selectionPoint) * EdgeSnapPointSize);
                        }
                    }
                }
                else
                {
                    Matrix4x4 planeToWorld = axisAlignedRotation ? hitToWorld : Matrix4x4.identity;
                    Vector3 intersectionPoint = axisAlignedRotation ? snappedPositionInHitSpace : snappedPosition;
                    Vector3 planeNormal = axisAlignedRotation ? HandleUtils.GetAxisSnappedDirection(worldToHit.MultiplyVector(mouseDirectionWorldSpace)) : HandleUtils.GetAxisSnappedDirection(mouseDirectionWorldSpace);

                    Bounds intersectionBounds = axisAlignedRotation ? HandleUtils.TransformBounds(selectionBounds, worldToHit * selectionToWorld) : selectionBoundsInWorldSpace;
                    intersectionBounds.Encapsulate(axisAlignedRotation ? hitBounds : hitBoundsInWorldSpace);

                    DrawIntersectionPlane(planeToWorld, intersectionBounds, intersectionPoint, planeNormal);
                }

                Handles.color = ShadowColor;
                Handles.DrawSolidDisc(snappedPosition, Camera.transform.forward, HandleUtility.GetHandleSize(snappedPosition) * EdgeSnapPointSize);
                Handles.color = EdgeGuideAccentColor;
                HandleUtils.DrawWireDisc(snappedPosition, Camera.transform.forward, HandleUtility.GetHandleSize(snappedPosition) * EdgeSnapPointSize);
            }

        HighlightHitBounds:

            if (hitBounds.size != hitBlock.CalculatedSize.Value)
            {
                using (new Handles.DrawingScope(EdgeGuideColor, hitBlock.transform.localToWorldMatrix))
                {
                    HandleUtils.DrawWireCube(Vector3.zero, hitBlock.CalculatedSize.Value, BoldLineWidth);
                }
            }

            using (new Handles.DrawingScope(EdgeGuideColor, hitToWorld))
            {
                HandleUtils.DrawWireCube(hitBounds.center, hitBounds.size, BoldLineWidth);
            }

            using (new Handles.DrawingScope(EdgeGuideColor, selectionToWorld))
            {
                HandleUtils.DrawWireCube(selectionBounds.center, selectionBounds.size, BoldLineWidth);
            }

            // if only adjusting one object, draw the hit bounds in local space. Selection bounds defaults to parent space
            if (!AdjustingMultipleObjects && UIBlock.RotatedSize != UIBlock.CalculatedSize.Value)
            {
                using (new Handles.DrawingScope(EdgeGuideColor, LocalToWorld))
                {
                    HandleUtils.DrawWireCube(Vector3.zero, UIBlock.CalculatedSize.Value, BoldLineWidth);
                }
            }
        }

        private void DrawIntersectionPlane(Matrix4x4 localToWorld, Bounds intersectionBounds, Vector3 intersectionPoint, Vector3 planeNormal)
        {
            for (int axis = 0; axis < 3; ++axis)
            {
                if (planeNormal[axis] == 0)
                {
                    continue;
                }

                float direction = Mathf.Sign(planeNormal[axis]);

                for (int i = 0; i < SpatialPartitionMask.OctantsPerFace; ++i)
                {
                    Vector3 oct = SpatialPartitionMask.GetOctant(i, axis, direction);
                    Vector3 vert = intersectionBounds.center + Vector3.Scale(intersectionBounds.extents, oct);
                    vert[axis] = intersectionPoint[axis];

                    EdgeSnapRectVerts[i] = vert;
                }

                using (new Handles.DrawingScope(Color.white, localToWorld))
                {
                    UnityEngine.Rendering.CompareFunction zTest = Handles.zTest;
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                    Handles.DrawSolidRectangleWithOutline(EdgeSnapRectVerts, EdgeGuideColor.Alpha(0.04f), Color.clear);
                    Handles.DrawSolidRectangleWithOutline(EdgeSnapRectVerts, Color.clear, EdgeGuideColor);
                    Handles.zTest = zTest;
                }
            }
        }

        protected float TryGetEdgeSnapPosition(Vector3 direction, Bounds localSpaceBounds, out Vector3 localSpaceSnapPosition, out UID<EdgeHitResult> hitID)
        {
            bool snapPositive = TryGetDirectedEdgeSnapPosition(direction, localSpaceBounds, out float positiveDirectionScreenSpaceDistance, out Vector3 positiveDirectionLocalPosition, out UID<EdgeHitResult> positiveHitID);
            bool snapNegative = TryGetDirectedEdgeSnapPosition(-direction, localSpaceBounds, out float negativeDirectionScreenSpaceDistance, out Vector3 negativeDirectionLocalPosition, out UID<EdgeHitResult> negativeHitID);

            if (snapPositive && snapNegative)
            {
                localSpaceSnapPosition = negativeDirectionScreenSpaceDistance < positiveDirectionScreenSpaceDistance ? negativeDirectionLocalPosition : positiveDirectionLocalPosition;
                hitID = negativeDirectionScreenSpaceDistance < positiveDirectionScreenSpaceDistance ? negativeHitID : positiveHitID;
                return Mathf.Min(positiveDirectionScreenSpaceDistance, negativeDirectionScreenSpaceDistance);
            }

            if (snapPositive)
            {
                localSpaceSnapPosition = positiveDirectionLocalPosition;
                hitID = positiveHitID;
                return positiveDirectionScreenSpaceDistance;
            }

            if (snapNegative)
            {
                localSpaceSnapPosition = negativeDirectionLocalPosition;
                hitID = negativeHitID;
                return negativeDirectionScreenSpaceDistance;
            }

            hitID = UID<EdgeHitResult>.Invalid;
            localSpaceSnapPosition = localSpaceBounds.center;
            return float.MaxValue;
        }

        protected bool TryGetDirectedEdgeSnapPosition(Vector3 direction, Bounds localSpaceBounds, out float screenSpaceDistance, out Vector3 localSpacePosition, out UID<EdgeHitResult> hitID)
        {
            Vector3 directionScalar = new Vector3(Math.Sign(direction.x), Math.Sign(direction.y), Math.Sign(direction.z));

            Vector3 directedExtents = Vector3.Scale(localSpaceBounds.extents, directionScalar);
            Vector3 origin = localSpaceBounds.center + directedExtents;

            Ray worldSpaceRay = new Ray(Handles.matrix.MultiplyPoint(origin), Handles.matrix.MultiplyVector(direction));

            bool foundCollision = TryGetNearestEdgePoint(localSpaceBounds, direction, worldSpaceRay, out Vector3 worldSpacePositiveSnapPoint, out hitID);

            Vector3 localSpaceSnapPoint = Handles.inverseMatrix.MultiplyPoint(worldSpacePositiveSnapPoint);

            Vector2 guiSpaceHandlePosition = HandleUtility.WorldToGUIPoint(origin);
            Vector2 guiSpaceSnapPosition = HandleUtility.WorldToGUIPoint(localSpaceSnapPoint);
            screenSpaceDistance = Vector2.Distance(guiSpaceSnapPosition, guiSpaceHandlePosition);

            localSpacePosition = localSpaceSnapPoint - directedExtents;

            return foundCollision && screenSpaceDistance < LayoutEdgeSnapHandle.GUISpaceSnapDistance;
        }

        protected void DoPositionTool(bool drawBounds = false)
        {
            DoPositionTool(SelectionBounds, SelectionToWorld);

            if (!drawBounds)
            {
                return;
            }

            using (new Handles.DrawingScope(IncludeMarginInSelection ? MarginColor : SizeColor, SelectionToWorld))
            {
                HandleUtils.DrawWireCube(SelectionBounds.center, SelectionBounds.size, BoldLineWidth);
            }
        }

        private EventType previousEvent = EventType.Ignore;
        protected void DoPositionTool(Bounds bounds, Matrix4x4 boundsToWorld)
        {
            Event evt = Event.current;

            positionHandleControlID = GUIUtility.GetControlID(FocusType.Passive);

            if (SelectionHovered)
            {
                // Click to cycle through overlapping scene view objects when no drag occurs
                if (IsActionEvent(evt.type) && evt.isMouse)
                {
                    switch (evt.type)
                    {
                        case EventType.MouseUp:
                            switch (previousEvent)
                            {
                                case EventType.MouseDown:
                                    previousEvent = evt.type;
                                    SceneViewInput.Select();
                                    return;
                            }
                            break;
                        default:
                            previousEvent = evt.type;
                            break;
                    }
                }

                HandleUtility.AddDefaultControl(positionHandleControlID);
            }

            Vector3 newPosition = bounds.center;

            Color handleColor = IncludeMarginInSelection ? MarginColor : SizeColor;

            EditorGUI.BeginChangeCheck();

            using (new Handles.DrawingScope(Color.white, boundsToWorld))
            {
                HandleUtils.BeginManipulationCheck();

                Handles.color = ShadowColor;

                Vector3 handleNormal = Vector3.Cross(positionHandleDirection1, positionHandleDirection2);

                float discSize = ScaledHandleSize * HandleUtility.GetHandleSize(bounds.center);

                Handles.DrawSolidDisc(bounds.center, handleNormal, 1.1f * discSize);

                Handles.color = handleColor.Alpha(0.5f);
                HandleUtils.DrawWireDisc(bounds.center, handleNormal, 1.1f * discSize, BoldLineWidth);

                Handles.color = Color.clear;

                newPosition = Handles.Slider2D(positionHandleControlID,
                                               bounds.center,
                                               handleNormal,
                                               positionHandleDirection1,
                                               positionHandleDirection2,
                                               discSize,
                                               Handles.CircleHandleCap,
                                               EditorSnapSettings.move);



                switch (HandleUtils.EndManipulationCheck()) // means position handle selected
                {
                    case HandleManipulation.Started:
                        positionHandleControlID = GUIUtility.hotControl;
                        break;
                    default:
                        if (positionHandleControlID != GUIUtility.hotControl)
                        {
                            positionHandleControlID = 0;
                        }
                        break;
                }

                if (!AdjustingMultipleObjects && positionHandleControlID != 0 && previousEvent == EventType.MouseDrag)
                {
                    RegisterControlTooltip(positionHandleControlID, new Tooltip()
                    {
                        HandleGuiPoint = HandleUtils.LowestCenterPointInGUISpace(bounds),
                        LabelGetter = GetPositionToolTip,
                    });
                }
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            UpdatePositions(bounds, newPosition, ref boundsToWorld);

            EngineManager.Instance.Update();

            Vector3 previousWorldPosition = boundsToWorld.MultiplyPoint(bounds.center);
            Bounds recalculatedBounds = GetSelectionBounds(out Matrix4x4 updatedBoundsToWorld);
            Vector3 updatedWorldPosition = updatedBoundsToWorld.MultiplyPoint(recalculatedBounds.center);

            if (updatedWorldPosition == previousWorldPosition)
            {
                return;
            }

            Vector3 recalculatedPosition = recalculatedBounds.center;

            using (new Handles.DrawingScope(Color.white, updatedBoundsToWorld))
            {
                Vector3 changeDirWorldSpace = HandleUtils.GUIToWorldDirection(HandleUtility.WorldToGUIPoint(previousWorldPosition), HandleUtility.WorldToGUIPoint(updatedWorldPosition));
                changeDirWorldSpace = Vector3.Scale(HandleUtils.GetAxisSnappedDirection(changeDirWorldSpace, 45), new Vector3(Math.Sign(changeDirWorldSpace.x), Math.Sign(changeDirWorldSpace.y), Math.Sign(changeDirWorldSpace.z)));

                Matrix4x4 worldToLocal = Handles.matrix.inverse;
                Vector3 changeDirLocalSpace = HandleUtils.AxisMasked(worldToLocal.MultiplyVector(changeDirWorldSpace), CameraPlaneAxes).normalized;

                Bounds sizeBounds = new Bounds(recalculatedPosition, recalculatedBounds.size);
                Bounds centerBounds = new Bounds(recalculatedPosition, Vector3.zero);

                for (int i = 0; i < 3; ++i)
                {
                    if (changeDirLocalSpace[i] == 0)
                    {
                        continue;
                    }

                    Vector3 direction = Vector3.zero;
                    direction[i] = 1;

                    float sizeSnapScreenSpaceDistance = TryGetEdgeSnapPosition(direction, sizeBounds, out Vector3 sizeSnapPosition, out UID<EdgeHitResult> sizeHitID);
                    float centerSnapScreenSpaceDistance = TryGetEdgeSnapPosition(direction, centerBounds, out Vector3 centerSnapPosition, out UID<EdgeHitResult> centerHitID);

                    float snapScreenSpaceDistance = Mathf.Min(sizeSnapScreenSpaceDistance, centerSnapScreenSpaceDistance);

                    if (snapScreenSpaceDistance < LayoutEdgeSnapHandle.GUISpaceSnapDistance)
                    {
                        recalculatedPosition[i] = sizeSnapScreenSpaceDistance < centerSnapScreenSpaceDistance ? sizeSnapPosition[i] : centerSnapPosition[i];

                        DrawGuideForHitID(sizeSnapScreenSpaceDistance < centerSnapScreenSpaceDistance ? sizeHitID : centerHitID);
                    }
                }

                if (recalculatedPosition != newPosition)
                {
                    UpdatePositions(recalculatedBounds, recalculatedPosition, ref updatedBoundsToWorld);
                }
            }
        }

        private void UpdatePositions(Bounds bounds, Vector3 newPosition, ref Matrix4x4 boundsToWorld)
        {
            Vector3 startPosWorldSpace = boundsToWorld.MultiplyPoint(bounds.center);
            Vector3 endPosWorldSpace = boundsToWorld.MultiplyPoint(newPosition);

            for (int i = 0; i < TopLevelBlocks.Length; i++)
            {
                UIBlock uiBlock = TopLevelBlocks[i];

                IUIBlock parentBlock = uiBlock.GetParentBlock();
                bool hasParentBlock = parentBlock != null;
                Vector3 parentSize = LayoutTransformRecorder.GetParentSize(uiBlock);
                Vector3 parentPaddingOffset = hasParentBlock ? (Vector3)parentBlock.CalculatedPadding.Offset : Vector3.zero;

                Matrix4x4 parentToWorld = uiBlock.GetParentToWorldMatrix();
                Matrix4x4 worldToParent = parentToWorld.inverse;

                PrimitiveBoundsHandle.Axes cameraPlaneAxes = AdjustingMultipleObjects ? CameraPlaneAxes : HandleUtils.GetCameraPlaneAxes(Camera, parentToWorld);

                Vector3 localPosition = uiBlock.GetCalculatedTransformLocalPosition();

                localPosition += HandleUtils.AxisMasked(worldToParent.MultiplyPoint(endPosWorldSpace) - worldToParent.MultiplyPoint(startPosWorldSpace), cameraPlaneAxes);

                localPosition = new Vector3(Math.ApproximatelyZero(localPosition.x) ? 0 : localPosition.x,
                                            Math.ApproximatelyZero(localPosition.y) ? 0 : localPosition.y,
                                            Math.ApproximatelyZero(localPosition.z) ? 0 : localPosition.z);

                if (!LayoutPropertyChanged)
                {
                    LayoutPropertyChanged = true;

                    Undo.RecordObjects(TopLevelBlocks, "Position");
                }

                Vector3 layoutOffset = LayoutUtils.LocalPositionToLayoutOffset(localPosition, uiBlock.LayoutSize, uiBlock.CalculatedMargin.Offset, parentSize, parentPaddingOffset, (Vector3)uiBlock.Alignment);
                uiBlock.Position.Raw = HandleUtils.RoundToNearest(Length3.GetRawValue(Handles.SnapValue(layoutOffset, EditorSnapSettings.move), uiBlock.Position, uiBlock.PositionMinMax, parentSize));
            }
        }

        private StringBuilder tooltipBuilder = new StringBuilder();
        private string GetPositionToolTip()
        {
            tooltipBuilder.Clear();
            for (int i = 0; i < 3; ++i)
            {
                if (!HandleUtils.AxisIsSet(CameraPlaneAxes, i))
                {
                    continue;
                }

                if (tooltipBuilder.Length > 0)
                {
                    tooltipBuilder.Append("\n");
                }

                string axisLabel = LayoutUtils.GetAlignmentLabel(i, (int)UIBlock.Alignment[i]);
                tooltipBuilder.Append($"{axisLabel}: {UIBlock.CalculatedPosition[i].Value.ToString("F2")}");
            }

            return tooltipBuilder.ToString();
        }

        private void GetCameraPlaneAxes()
        {
            CameraPlaneAxes = PrimitiveBoundsHandle.Axes.None;

            CameraPlaneAxes = HandleUtils.GetCameraPlaneAxes(Camera, SelectionToWorld);

            bool xOnPlane = (CameraPlaneAxes & PrimitiveBoundsHandle.Axes.X) != 0;
            bool yOnPlane = (CameraPlaneAxes & PrimitiveBoundsHandle.Axes.Y) != 0;

            positionHandleDirection1 = xOnPlane ? Vector3.right : Vector3.up;
            positionHandleDirection2 = xOnPlane && yOnPlane ? Vector3.up : Vector3.forward;
        }
    }
}
