// Copyright (c) Supernova Technologies LLC
//#define DEBUG_VISUALS

using Nova.Compat;
using Nova.Editor.GUIs;
using Nova.Editor.Utilities;
using Nova.Internal;
using Nova.Internal.Core;
using Nova.Internal.Input;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Navigator = Nova.Internal.Navigator<Nova.UIBlock>;

namespace Nova.Editor
{
    internal class NavGraphDebugView : System<NavGraphDebugView>
    {
        const float kArrowThickness = 2.5f;
        const float kArrowHeadSize = 1.2f;
        private static readonly Vector3[] Directions = new Vector3[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1), new Vector3(0, 0, 1), new Vector3(0, 0, -1) };

        private static Dictionary<GestureRecognizer, NavNode> navigationGraph = new Dictionary<GestureRecognizer, NavNode>();
        private static GestureRecognizer[] navNodes = null;
        private static HashSet<GestureRecognizer> drawnNodes = new HashSet<GestureRecognizer>();

        protected override void Dispose()
        {
            SceneView.duringSceneGui -= DrawGizmos;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
        }

        protected override void Init()
        {
            SceneView.duringSceneGui -= DrawGizmos;
            SceneView.duringSceneGui += DrawGizmos;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        private static void PlayModeChanged(PlayModeStateChange _)
        {
            GenerateGraph();
        }

        public static UIBlock NextNavigationPoint(GestureRecognizer from, Vector3 direction)
        {
            if (TryGetNextNavigationPointFast(from, direction, out UIBlock next))
            {
                return next;
            }

            return GetNextNavigationPoint(from, direction);
        }

        public static bool TryGetNextNavigationPointFast(GestureRecognizer from, Vector3 direction, out UIBlock next)
        {
            next = null;
            if (navigationGraph.TryGetValue(from, out NavNode node))
            {
                GestureRecognizer target = node.GetLink(direction).Target;
                next = target == null ? null : target.UIBlock;
            }

            return next != null;
        }

        private static UIBlock GetNextNavigationPoint(GestureRecognizer from, Vector3 direction)
        {
            if (!TryGetNextNavigationPoint(from, direction, out Ray ray, out UIBlock toUIBlock))
            {
                return null;
            }

            UIBlock next = toUIBlock;
            while (next != null && next.TryGetComponent(out GestureRecognizer gr) && gr.OnSelect == SelectBehavior.ScopeNavigation && gr.AutoSelect)
            {
                if (Navigator.EditorOnly_TryGet(ray, from.UIBlock, gr.UIBlock, Interaction.AllLayers, filterScope: true, NavNodeFilter, out HitTestResult result, sortByTopLevelProximity: true))
                {
                    next = result.HitBlock as UIBlock;
                    toUIBlock = next;
                }
                else
                {
                    next = null;
                }
            }

            return toUIBlock;
        }

        private static bool TryGetNextNavigationPoint(GestureRecognizer from, Vector3 direction, out Ray ray, out UIBlock toUIBlock)
        {
            ray = default;
            toUIBlock = null;

            if (from == null || !from.Navigable || from.Navigation.GetLink(direction) == NavLink.Empty)
            {
                return false;
            }

            if (TryGetManualNavigation(from, direction, out toUIBlock))
            {
                return true;
            }

            ray = Navigator.CreateRay(direction, from.UIBlock);

            GestureRecognizer scope = GetScope(from.UIBlock);

            if (TryGetAutoNavigation(from, scope, ray, direction, out toUIBlock))
            {
                return true;
            }

            if (TryGetFallbackNavigation(from, scope, ray, direction, out toUIBlock))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetManualNavigation(GestureRecognizer from, Vector3 direction, out UIBlock to)
        {
            to = null;

            // Check for manual navigation but, since manual navigation can return null as
            // a valid navigation target, we only filter against non-null manual targets.
            if (!from.Navigation.TryGetNavigation(direction, out IUIBlock toUIBlock) || (toUIBlock != null && !NavNodeFilter(toUIBlock)))
            {
                return false;
            }

            to = toUIBlock as UIBlock;
            return true;
        }

        private static bool TryGetAutoNavigation(GestureRecognizer from, GestureRecognizer scopeGR, Ray ray, Vector3 direction, out UIBlock to)
        {
            to = null;

            UIBlock scope = scopeGR == null ? null : scopeGR.UIBlock;

            if (!Navigator.EditorOnly_TryGet(ray, from.UIBlock, scope, Interaction.AllLayers, filterScope: true, NavNodeFilter, out HitTestResult result))
            {
                return false;
            }

            to = result.HitBlock as UIBlock;

            if (scope != null && from.UIBlock != scope && Navigator.TryGetScopedMove(scope, from.UIBlock, direction, Interaction.AllLayers, out ScopedMove move))
            {
                to = move.ToUIBlock as UIBlock;
            }

            return true;
        }

        private static bool TryGetFallbackNavigation(GestureRecognizer from, GestureRecognizer scopeGR, Ray ray, Vector3 direction, out UIBlock to)
        {
            to = null;

            if (from.Navigation.GetLink(direction).Fallback != NavLinkFallback.NavigateOutsideScope)
            {
                return false;
            }

            UIBlock scope = scopeGR == null ? null : scopeGR.UIBlock;

            while (scope != null)
            {
                scopeGR = GetScope(scope);
                scope = scopeGR == null ? null : scopeGR.UIBlock;

                if (Navigator.EditorOnly_TryGet(ray, from.UIBlock, scope, Interaction.AllLayers, true, NavNodeFilter, out HitTestResult result))
                {
                    to = result.HitBlock as UIBlock;
                    break;
                }
            }

            return to != null;
        }

        private static void CacheLinks(GestureRecognizer recognizer)
        {
            if (!NavNodeFilter(recognizer.UIBlock))
            {
                navigationGraph.Remove(recognizer);
                return;
            }

            navigationGraph[recognizer] = new NavNode()
            {
                Left = GetTarget(GetNextNavigationPoint(recognizer, Vector3.left)),
                Right = GetTarget(GetNextNavigationPoint(recognizer, Vector3.right)),
                Up = GetTarget(GetNextNavigationPoint(recognizer, Vector3.up)),
                Down = GetTarget(GetNextNavigationPoint(recognizer, Vector3.down)),
                Forward = GetTarget(GetNextNavigationPoint(recognizer, Vector3.forward)),
                Back = GetTarget(GetNextNavigationPoint(recognizer, Vector3.back)),
            };
        }

        private static GestureRecognizer GetTarget(UIBlock uiBlock)
        {
            return uiBlock == null ? null : uiBlock.GetComponent<GestureRecognizer>();
        }

        private static GestureRecognizer GetScope(UIBlock from)
        {
            if (from == null)
            {
                return null;
            }

            for (UIBlock current = from.Parent; current != null; current = current.Parent)
            {
                if (NavNodeFilter(current, out GestureRecognizer gr) && gr.OnSelect == SelectBehavior.ScopeNavigation)
                {
                    return gr;
                }
            }

            return null;
        }


        private static bool NavNodeFilter(IUIBlock node) => NavNodeFilter(node, out _);
        private static bool NavNodeFilter(IUIBlock node, out GestureRecognizer gr)
        {
            gr = null;
            UIBlock uiBlock = node as UIBlock;

            if (uiBlock == null || !uiBlock.TryGetComponent(out gr) || !SceneViewUtils.IsVisibleInSceneView(uiBlock.gameObject))
            {
                return false;
            }

            return NovaApplication.InPlayer(gr) ? gr.IsNavigable : gr.Navigable;
        }

        private static void GenerateGraph()
        {
            navigationGraph.Clear();

            navNodes = NovaEditorPrefs.FilterNavDebugViewToSelection ?
                       Selection.GetFiltered<GestureRecognizer>(SelectionMode.Deep).Where(x => x.enabled && NavNodeFilter(x.UIBlock)).ToArray() :
                       StageUtility.GetCurrentStageHandle().FindComponentsOfType<GestureRecognizer>().Where(x => x.enabled && x.gameObject.activeInHierarchy && x.Navigable).ToArray();

            foreach (GestureRecognizer recognizer in navNodes)
            {
                CacheLinks(recognizer);
            }
        }

        private static void DrawGizmos(SceneView scene)
        {
            if (Event.current.type != EventType.Repaint || !NovaEditorPrefs.DisplayNavigationDebugView)
            {
                return;
            }

            GenerateGraph();

            drawnNodes.Clear();

            Color handleColor = Handles.color;
            Handles.color = NovaGUI.Styles.Yellow_ish;

            foreach (GestureRecognizer recognizer in navNodes)
            {
                DrawGizmos(recognizer);

                drawnNodes.Add(recognizer);
            }

            Handles.color = handleColor;
        }

        private static void DrawGizmos(GestureRecognizer recognizer)
        {
            UIBlock source = recognizer.UIBlock;

            for (int i = 0; i < Directions.Length; ++i)
            {
                Vector3 direction = Directions[i];
                UIBlock hit = NextNavigationPoint(recognizer, direction);

                if (hit == null)
                {
                    continue;
                }

                bool twoWay = false;

                if (hit.TryGetComponent(out GestureRecognizer gr))
                {
                    twoWay = TryGetNextNavigationPointFast(gr, -direction, out UIBlock next);
                    twoWay &= next == source;
                }

                if (twoWay && drawnNodes.Contains(gr))
                {
                    continue;
                }

                DrawNavigationArrow(direction, source, hit, twoWay);
            }
        }

        private static void DrawNavigationArrow(Vector3 direction, UIBlock fromObj, UIBlock toObj, bool twoWay)
        {
            if (fromObj == null || toObj == null)
            {
                return;
            }

            Transform fromTransform = fromObj.transform;
            Transform toTransform = toObj.transform;

            Vector3 sideDir = direction.z == 0 ? new Vector3(direction.y, -direction.x, 0) : new Vector3(direction.z, 0, -direction.x);
            Vector3 fromPoint = fromTransform.TransformPoint(Vector3.Scale(fromObj.CalculatedSize.Value * 0.5f, direction));
            Vector3 toPoint = toTransform.TransformPoint(Vector3.Scale(toObj.CalculatedSize.Value * 0.5f, -direction));
            float fromSize = HandleUtility.GetHandleSize(fromPoint) * 0.05f;
            float toSize = HandleUtility.GetHandleSize(toPoint) * 0.05f;

            float length = Vector3.Distance(fromPoint, toPoint);
            bool drawCurve = length > Math.Epsilon;

            if (!twoWay || !drawCurve)
            {
                fromPoint += fromTransform.TransformDirection(sideDir) * fromSize;
                toPoint += toTransform.TransformDirection(sideDir) * toSize;
            }

            UnityEngine.Rendering.CompareFunction zTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            if (drawCurve)
            {
                Vector3 fromTangent = fromTransform.rotation * direction * length * 0.3f;
                Vector3 toTangent = toTransform.rotation * -direction * length * 0.3f;

                Handles.DrawBezier(fromPoint, toPoint, fromPoint + fromTangent, toPoint + toTangent, Handles.color, null, kArrowThickness);
            }

            if (twoWay)
            {
                Vector3 fromEnd1 = fromPoint + fromTransform.rotation * (direction + sideDir) * fromSize * kArrowHeadSize;
                Vector3 fromEnd2 = fromPoint + fromTransform.rotation * (direction - sideDir) * fromSize * kArrowHeadSize;
                Handles.DrawAAPolyLine(kArrowThickness, fromEnd1, fromPoint, fromEnd2);
            }

            Vector3 toEnd1 = toPoint + toTransform.rotation * (-direction + sideDir) * toSize * kArrowHeadSize;
            Vector3 toEnd2 = toPoint + toTransform.rotation * (-direction - sideDir) * toSize * kArrowHeadSize;
            Handles.DrawAAPolyLine(kArrowThickness, toEnd1, toPoint, toEnd2);

            Handles.zTest = zTest;
        }

        private static void DrawProximityBubble(Ray navRay, UIBlock toUIBlock)
        {
            Ray localRay = HandleUtils.TransformRay(navRay, toUIBlock.transform.worldToLocalMatrix);
            UIBounds bounds = new UIBounds(toUIBlock.CalculatedSize.Value);

            float3 closestPointLocalSpace = bounds.ClosestPoint(localRay.origin);
            StructuredRay rayLocalSpace = new StructuredRay(localRay);

            float radiusLocalSpace = NavigateToBounds.GetMinProximityBubbleRadius(ref bounds, closestPointLocalSpace, ref rayLocalSpace);

            Vector3 proximityPointLocalSpace = rayLocalSpace.GetPoint(radiusLocalSpace);
            Vector3 proximityPointWorldSpace = toUIBlock.transform.TransformPoint(proximityPointLocalSpace);

            Color color = Handles.color;
            Handles.color = Color.magenta;
            Handles.DrawWireDisc(proximityPointWorldSpace, toUIBlock.transform.forward, Vector3.Magnitude(navRay.origin - proximityPointWorldSpace));
            Handles.color = color;
        }
    }
}
