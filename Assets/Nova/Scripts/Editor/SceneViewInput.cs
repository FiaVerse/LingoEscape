// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Input;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Editor
{
    internal class SceneViewInput : System<SceneViewInput>
    {
        private static InputEngine.HitTestCache<EdgeHit> EdgeDetectionCache;
        private static HitTest<RayToEdge, StructuredRay, EdgeHit> EdgeDetectionRunner;
        private static List<HitTestResult> hitResultCache = new List<HitTestResult>();

        private static BurstedMethod<BurstMethod> edgeDetection;

        /// <summary>
        /// Tracks the selected object between start/stop selection. Will become null once an object is selected.
        /// </summary>
        private static IUIBlock currentSelection = null;

        /// <summary>
        /// The most recently selected object. Used to cycle through multiple selections.
        /// </summary>
        private static HitTestResult latestHitResult;

        /// <summary>
        /// The last time an object was selected
        /// </summary>
        private static double selectionTime = float.MinValue;

        private const double MaxSelectionCycleTime = 10;

        /// <summary>
        /// Selects the scene view object at the current mouse position
        /// </summary>
        public static void Select()
        {
            if (ShiftOrControl())
            {
                SelectHitObject(Event.current.mousePosition, mouseDown: true, autoConfirm: true);
            }
            else
            {
                UnityEditor.Selection.activeGameObject = UnityEditor.HandleUtility.PickGameObject(Event.current.mousePosition, out int _);
            }
        }

        /// <summary>
        /// Performs a hit test in the scene with specific behavior for editor only/scene view
        /// to include collisions of potentially invisible content, and populates the provided list 
        /// with all UIBlocks of type T that intersect with the provided ray. The list is sorted by
        /// distance to the ray origin, with index 0 being the closest UIBlock.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ray"></param>
        /// <param name="results"></param>
        internal static bool DetectEdges(Ray ray, List<EdgeHitResult> results, Camera sceneViewCamera, NovaHashMap<DataStoreID, bool> filterRoots, int max = int.MaxValue)
        {
            PerformEdgeDetectionSceneView(ray, sceneViewCamera, ref filterRoots, ref EdgeDetectionCache);
            GetSceneViewHitResults(ref EdgeDetectionCache.Hits, results, max);
            return results.Count > 0;
        }

        /// <summary>
        /// Performs a hit test in the scene with specific behavior for editor only/scene view
        /// to include collisions of potentially invisible content, and populates the provided list 
        /// with all UIBlocks that intersect with the provided ray. The list is sorted by
        /// distance to the ray origin, with index 0 being the closest UIBlock.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ray"></param>
        /// <param name="results"></param>
        public static bool HitTest(Ray ray, List<HitTestResult> results, int layerMask = InputEngine.AllLayers)
        {
            InputEngine.Instance.HitTestAllIncludingInvisible(ray, results, float.PositiveInfinity, FilterToScene, layerMask);
            return results.Count > 0;
        }

        public static bool HitTest(Ray ray, out HitTestResult result)
        {
            return InputEngine.Instance.HitTestIncludingInvisible(ray, out result, FilterToScene);
        }

        private static void PerformEdgeDetectionSceneView(Ray ray, Camera sceneViewCamera, ref NovaHashMap<DataStoreID, bool> filterFromRoot, ref InputEngine.HitTestCache<EdgeHit> buffers)
        {
            float4x4 worldToViewport = sceneViewCamera.projectionMatrix * sceneViewCamera.transform.worldToLocalMatrix;

            {
                EdgeDetectionRunner.CollisionTest.WorldToViewport = worldToViewport;
                EdgeDetectionRunner.CollisionTest.Ray = ray;
                EdgeDetectionRunner.CollisionTest.FilterRootIDs = filterFromRoot;

                EdgeDetectionRunner.HitIndices = buffers.Hits;
                EdgeDetectionRunner.IndicesToProcess = buffers.HitQueue;

                unsafe
                {
                    edgeDetection.Method.Invoke(UnsafeUtility.AddressOf(ref EdgeDetectionRunner));
                }
            }
        }

        private static bool FilterToScene(IUIBlock uiBlock) => uiBlock.IsVirtual ? false : SceneViewUtils.IsSelectableInSceneView(uiBlock.Transform.gameObject);

        /// <summary>
        /// Returns all hit UIBlocks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hits"></param>
        /// <param name="results"></param>
        private static void GetSceneViewHitResults(ref NativeList<EdgeHit> hits, List<EdgeHitResult> results, int maxHits = int.MaxValue)
        {
            results.Clear();

            for (int i = 0; i < hits.Length && i < maxHits; ++i)
            {
                EdgeHit hit = hits[i];
                if (!HierarchyDataStore.Instance.Elements.TryGetValue(hit.ID, out IHierarchyBlock hitBlock))
                {
                    continue;
                }

                // Could add duplicates for virtual nodes... tbd how we want to handle that
                if (hitBlock.IsVirtual)
                {
                    continue;
                }

                UIBlock block = hitBlock as UIBlock;

                if (!SceneViewUtils.IsVisibleInSceneView(block.gameObject))
                {
                    continue;
                }

                results.Add(new EdgeHitResult()
                {
                    HitBlock = block,
                    HitPoint = hit.HitPointWorldSpace,
                    ParentSpace = hit.ParentSpace,
                    HitBounds = new Bounds(hit.HitBoundsCenter, hit.HitBoundsSize),
                });
            }
        }

        private static void DoSceneViewSelection(UnityEditor.SceneView sceneView)
        {
            Event evt = Event.current;

            bool select = true;

            if (!CanPerformHitTest(sceneView, evt) || !ShouldPerformHitTest(evt))
            {
                if (UnityEditor.EditorApplication.timeSinceStartup - selectionTime > MaxSelectionCycleTime)
                {
                    latestHitResult = default;
                }
                select = false;
            }

            if (select)
            {
                SelectHitObject(evt.mousePosition, mouseDown: evt.type == EventType.MouseDown);
                ReleaseControl(evt);
            }

            if (currentSelection == null && UnityEditor.Selection.activeGameObject == null)
            {
                latestHitResult = default;
            }
        }

        public static void SelectHitObject(Vector2 mousePosition, bool mouseDown, bool autoConfirm = false)
        {
            using (new UnityEditor.Handles.DrawingScope(Matrix4x4.identity))
            {
                Ray ray = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePosition);

                if (HitTest(ray, hitResultCache))
                {
                    int index = 0;

                    if (!ShiftOrControl())
                    {
                        index = hitResultCache.IndexOf(latestHitResult) + 1;

                        if (index >= hitResultCache.Count)
                        {
                            latestHitResult = default(HitTestResult);
                            return;
                        }
                    }

                    UpdateSelection(ray, hitResultCache[index], mouseDown);

                    if (autoConfirm && mouseDown)
                    {
                        UpdateSelection(ray, hitResultCache[index], !mouseDown);
                    }
                }
                else
                {
                    latestHitResult = default(HitTestResult);
                }
            }
        }

        private static bool CanPerformHitTest(UnityEditor.SceneView sceneView, Event sceneViewEvent)
        {
            return sceneView != null && sceneView.camera != null && sceneViewEvent != null;
        }

        private static bool ShouldPerformHitTest(Event sceneViewEvent)
        {
            if (sceneViewEvent.button != 0 || sceneViewEvent.clickCount == 0)
            {
                return false;
            }

            if (sceneViewEvent.type == EventType.Repaint || sceneViewEvent.type == EventType.Layout)
            {
                return false;
            }

            bool pressedEvent = sceneViewEvent.type == EventType.MouseDown && GUIUtility.hotControl != 0;
            bool releasedEvent = (sceneViewEvent.type == EventType.MouseUp || (sceneViewEvent.type == EventType.Used && GUIUtility.hotControl == 0));

            if (pressedEvent || releasedEvent)
            {
                return true;
            }

            return false;
        }

        public static GameObject PickGameObjects(Camera cam, int layers, Vector2 position, GameObject[] ignore, GameObject[] filter, out int materialIndex)
        {
            HitTest(cam.ScreenPointToRay(position), hitResultCache, layers);

            IEnumerable<GameObject> results = hitResultCache.Select(x => x.GameObject);

            int index = 0;

            if (filter != null && filter.Where(x => x != null).FirstOrDefault() != null)
            {
                results = results.Intersect(filter);
            }

            if (ignore != null && ignore.Where(x => x != null).FirstOrDefault() != null)
            {
                results = results.Except(ignore);
            }
            else if (hitResultCache.Count > 1)
            {
                GameObject[] selection = UnityEditor.Selection.gameObjects;

                if (selection != null)
                {
                    if (selection.Length == 1)
                    {
                        List<GameObject> hits = results.ToList();

                        if (hits.Count > 0)
                        {
                            index = (hits.IndexOf(selection[0]) + 1) % hits.Count;
                        }
                    }
                    else
                    {
                        results = results.Except(selection);
                    }
                }
            }

            materialIndex = 0;

            return results.ElementAtOrDefault(index);
        }


        private static bool TryPickGameObject(out GameObject gameObject)
        {
            gameObject = UnityEditor.HandleUtility.PickGameObject(Event.current.mousePosition, out int materialIndex);

            return gameObject != null;
        }

        private static void UpdateSelection(Ray ray, HitTestResult hitResult, bool mouseDown)
        {
            if (TryPickGameObject(out GameObject picked) && picked.GetComponent<UIBlock>() == null)
            {
                bool hasNearVertex = UnityEditor.HandleUtility.FindNearestVertex(Event.current.mousePosition, new Transform[] { picked.transform }, out Vector3 vertex);
                Vector3 nearestPoint = hasNearVertex ? vertex : picked.transform.position;
                float nonUIToRayDistance = Vector3.Distance(nearestPoint, ray.origin);
                float uiToRayDistance = Vector3.Distance(hitResult.HitPoint, ray.origin);

                if (nonUIToRayDistance < uiToRayDistance)
                {
                    currentSelection = null;
                    return;
                }
            }

            if (mouseDown)
            {
                currentSelection = hitResult.HitBlock;
                return;
            }

            if (hitResult.HitBlock != currentSelection)
            {
                currentSelection = null;
                return;
            }

            bool inSelection = false;

            bool actionKey = UnityEditor.EditorGUI.actionKey; // covers control on windows, command on OSX

            if (actionKey)
            {
                inSelection = UnityEditor.Selection.Contains(hitResult.GameObject);
            }

            bool addToSelection = Event.current.shift || (actionKey && !inSelection);
            bool removeFromSelection = actionKey && inSelection;

            if (addToSelection)
            {
                Object[] selectedObjects = UnityEditor.Selection.objects;
                Object[] newSelection = new Object[selectedObjects.Length + 1];
                System.Array.Copy(selectedObjects, newSelection, selectedObjects.Length);
                newSelection[selectedObjects.Length] = hitResult.GameObject;

                UnityEditor.Selection.objects = newSelection;
            }
            else if (removeFromSelection)
            {
                Object[] selectedObjects = UnityEditor.Selection.objects;
                Object[] newSelection = new Object[selectedObjects.Length - 1];
                int index = System.Array.IndexOf(selectedObjects, hitResult.GameObject);

                System.Array.Copy(selectedObjects, newSelection, index);
                System.Array.Copy(selectedObjects, index + 1, newSelection, index, selectedObjects.Length - index - 1);

                UnityEditor.Selection.objects = newSelection;
            }
            else
            {
                UnityEditor.Selection.activeGameObject = hitResult.GameObject;
            }

            latestHitResult = hitResult;

            currentSelection = null;

            selectionTime = UnityEditor.EditorApplication.timeSinceStartup;
        }

        public static bool ShiftOrControl()
        {
            return Event.current.shift || UnityEditor.EditorGUI.actionKey;
        }

        private static void ReleaseControl(Event sceneViewEvent)
        {
            if (sceneViewEvent.type != EventType.Used)
            {
                return;
            }

            UnityEditor.EditorGUIUtility.hotControl = 0;
        }

        protected override void Init()
        {
            unsafe
            {
                edgeDetection = new BurstedMethod<BurstMethod>(HitTest<RayToEdge, StructuredRay, EdgeHit>.Run);
            }

            HierarchyDataStore hierarchy = HierarchyDataStore.Instance;
            LayoutDataStore layouts = LayoutDataStore.Instance;

            EdgeDetectionCache.Init();

            EdgeDetectionRunner = new HitTest<RayToEdge, StructuredRay, EdgeHit>()
            {
                Hierarchy = hierarchy.Hierarchy,
                HierarchyLookup = hierarchy.HierarchyLookup,
                RootIDs = hierarchy.HierarchyRootIDs,

                SpatialPartitionMasks = layouts.SpatialPartitions,

                CollisionTest = new RayToEdge()
                {
                    LengthProperties = layouts.CalculatedLengths,
                    Sizes = layouts.TotalContentSizes,
                    Offsets = layouts.TotalContentOffsets,
                    WorldToLocalMatrices = layouts.WorldToLocalMatrices,
                    LocalToWorldMatrices = layouts.LocalToWorldMatrices,
                    TransformLocalRotations = layouts.TransformLocalRotations,
                    TransformLocalPositions = layouts.TransformLocalPositions,
                    TransformLocalScales = layouts.TransformLocalScales,
                    UseRotations = layouts.UseRotations,

                    Hierarchy = hierarchy.Hierarchy,
                    HierarchyLookup = hierarchy.HierarchyLookup,
                    BatchGroupElements = hierarchy.BatchGroupTracker.BatchGroupElements,
                }
            };

            UnityEditor.HandleUtility.pickGameObjectCustomPasses += PickGameObjects;
            UnityEditor.SceneView.duringSceneGui += DoSceneViewSelection;
        }

        protected override void Dispose()
        {
            EdgeDetectionCache.Dispose();

            UnityEditor.HandleUtility.pickGameObjectCustomPasses -= PickGameObjects;
            UnityEditor.SceneView.duringSceneGui -= DoSceneViewSelection;
            UnityEditor.EditorGUIUtility.hotControl = 0;
        }
    }
}
