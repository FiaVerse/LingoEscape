// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Common;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Input
{
    internal struct HitTestResult : System.IEquatable<HitTestResult>
    {
        public IUIBlock HitBlock;
        public float3 HitPoint;
        public float3 Normal;

        public GameObject GameObject => HitBlock.IsVirtual ? null : HitBlock.Transform.gameObject;

        public bool Equals(HitTestResult other)
        {
            return HitBlock == other.HitBlock;
        }

        public override int GetHashCode()
        {
            return HitBlock.GetHashCode();
        }
    }

    internal struct EdgeHitResult
    {
        public IUIBlock HitBlock;
        public Bounds HitBounds;
        public Vector3 HitPoint;
        public bool ParentSpace;
    }

    internal class InputEngine : System<InputEngine>
    {
        public const int AllLayers = Constants.PhysicsAllLayers;

        public static event System.Action OnPreUpdate = null;
        public static void PreUpdate()
        {
            OnPreUpdate?.Invoke();
        }

        public struct HitTestCache<T> : IInitializable where T : unmanaged, IComparer<T>
        {
            public NativeList<T> Hits;
            public NativeList<DataStoreIndex> HitQueue;

            public void Dispose()
            {
                Hits.Dispose();
                HitQueue.Dispose();
            }

            public void Init()
            {
                Hits = new NativeList<T>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);
                HitQueue = new NativeList<DataStoreIndex>(Constants.AllElementsInitialCapacity, Allocator.Persistent);
            }
        }

        private struct HitFilter
        {
            public int LayerMask;
            public float MaxDistance;
            public int MaxHits;
            public System.Func<IUIBlock, bool> Filter;
            public bool FilterToPlayer;

            public static readonly HitFilter InfiniteDistance = new HitFilter() { MaxHits = int.MaxValue, MaxDistance = float.PositiveInfinity, LayerMask = AllLayers };

            public bool CanHit(IUIBlock uiBlock)
            {
                return IsOnHittableLayer(uiBlock) && (Filter == null || Filter.Invoke(uiBlock));
            }

            private bool IsOnHittableLayer(IUIBlock uiBlock)
            {
                MonoBehaviour monoBlock = uiBlock as MonoBehaviour;

                if (monoBlock == null)
                {
                    return false;
                }

#pragma warning disable CS0162 // Unreachable code detected
                if (NovaApplication.ConstIsEditor && FilterToPlayer && NovaApplication.IsPlaying)
                {
                    if (!Application.IsPlaying(monoBlock.gameObject))
                    {
                        return false;
                    }
                }
#pragma warning restore CS0162 // Unreachable code detected

                return (LayerMask & 1 << monoBlock.gameObject.layer) != 0;
            }
        }

        private static BurstedMethod<BurstMethod> navigation;
        private static BurstedMethod<BurstMethod> navigableDescendantQuery;
        private static BurstedMethod<BurstMethod> navAncestorScopesQuery;
        private NativeList<DataStoreID> navigationScopeRootIDs;
        private NativeList<DataStoreID> navigationScopeIDCache;
        private NovaHashMap<DataStoreID, bool> navigationScopeFlags;
        private NovaHashMap<DataStoreID, bool> navigationNodeFlags;
        private NovaHashMap<DataStoreID, float> topLevelProximityCache;
        private HitTestCache<NavigationHit> NavigationCache;
        [FixedAddressValueType]
        private HitTest<NavigateToBounds, StructuredRay, NavigationHit> NavigationRunner;
        [FixedAddressValueType]
        private FirstNavigableDescendant NavDescendantsRunner;
        [FixedAddressValueType]
        private AncestorScopeQuery NavAncestorScopeQueryRunner;

        private HitTestCache<PointerHit> PointerCache;
        [FixedAddressValueType]
        private HitTest<RayToBounds, StructuredRay, PointerHit> HitTestRunner;
        [FixedAddressValueType]
        private HitTest<SphereToBounds, StructuredSphere, PointerHit> CollisionTestRunner;

        private List<HitTestResult> hitResultCache = new List<HitTestResult>();

        private static BurstedMethod<BurstMethod> hitTest;
        private static BurstedMethod<BurstMethod> collisionTest;


        /// <summary>
        /// Performs a hit test in the scene and populates the provided list with all UIBlocks that intersect 
        /// with the provided ray. The list is sorted by distance to the ray origin, with index
        /// 0 being the closest UIBlock.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ray"></param>
        /// <param name="results"></param>
        public void HitTestAll(Ray ray, List<HitTestResult> results, float maxDistance, int layerMask)
        {
            PerformHitTest(ray, layerMask, ref PointerCache);
            HitFilter filter = new HitFilter() { MaxHits = int.MaxValue, MaxDistance = maxDistance, LayerMask = layerMask, FilterToPlayer = true };
            GetHitResults(ref PointerCache.Hits, results, ref filter);
        }

        public void HitTestAllIncludingInvisible(Ray ray, List<HitTestResult> results, float maxDistance, System.Func<IUIBlock, bool> filter, int layerMask = AllLayers)
        {
            PerformHitTest(ray, layerMask, ref PointerCache, true);
            HitFilter hitFilter = new HitFilter() { MaxHits = int.MaxValue, LayerMask = layerMask, MaxDistance = maxDistance, Filter = filter };
            GetHitResults(ref PointerCache.Hits, results, ref hitFilter);
        }

        public bool HitTestIncludingInvisible(Ray ray, out HitTestResult result, System.Func<IUIBlock, bool> filter)
        {
            PerformHitTest(ray, AllLayers, ref PointerCache, true);
            HitFilter hitFilter = new HitFilter() { MaxHits = int.MaxValue, LayerMask = AllLayers, Filter = filter };
            result.HitBlock = GetHitBlock(ref PointerCache.Hits, ref hitFilter, out float hitDistance, out float3 hitNormal);
            result.HitPoint = ray.GetPoint(hitDistance);
            result.Normal = hitNormal;

            return result.HitBlock != null;
        }

        /// <summary>
        /// Performs a hit test in the scene and populates the provided list with all UIBlocks that intersect 
        /// with the provided sphere. The list is sorted by distance to the sphere origin, with index
        /// 0 being the closest UIBlock.
        /// </summary>
        /// <param name="sphere"></param>
        /// <param name="results"></param>
        /// <param name="layerMask"></param>
        public void HitTestAll(StructuredSphere sphere, List<HitTestResult> results, int layerMask)
        {
            PerformCollisionTest(sphere, layerMask, ref PointerCache);
            HitFilter filter = new HitFilter() { MaxHits = int.MaxValue, MaxDistance = sphere.Radius, LayerMask = layerMask, FilterToPlayer = true };
            GetHitResults(ref PointerCache.Hits, results, ref filter);
        }

        /// <summary>
        /// Performs a hit test in the scene and returns the nearest UIBlock that intersects
        /// with the provided ray and the distance from ray origin to the interesection point.
        /// </summary>
        /// <param name="sphere"></param>
        /// <param name="result"></param>
        /// <param name="layerMask"></param>
        public bool HitTest(StructuredSphere sphere, out HitTestResult result, int layerMask)
        {
            PerformCollisionTest(sphere, layerMask, ref PointerCache);
            HitFilter filter = new HitFilter() { MaxHits = 1, MaxDistance = sphere.Radius, LayerMask = layerMask, FilterToPlayer = true };

            GetHitResults(ref PointerCache.Hits, hitResultCache, ref filter);

            if (hitResultCache.Count > 0)
            {
                result = hitResultCache[0];
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Performs a hit test in the scene and returns the nearest UIBlock that intersects
        /// with the provided ray and the distance from ray origin to the interesection point.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ray"></param>
        /// <param name="blockHit"></param>
        /// <returns></returns>
        public bool HitTest(Ray ray, out HitTestResult result, float maxDistance, int layerMask)
        {
            PerformHitTest(ray, layerMask, ref PointerCache);
            HitFilter filter = new HitFilter() { MaxHits = 1, MaxDistance = maxDistance, LayerMask = layerMask, FilterToPlayer = true };
            GetHitResults(ref PointerCache.Hits, hitResultCache, ref filter);

            if (hitResultCache.Count > 0)
            {
                result = hitResultCache[0];
                return true;
            }

            result = default;
            return false;
        }

        public void RegisterNavigationNode(DataStoreID nodeID)
        {
            navigationNodeFlags.Add(nodeID, true);
        }

        public void UnregisterNavigationNode(DataStoreID nodeID)
        {
            navigationNodeFlags.Remove(nodeID);
        }

        public void RegisterNavigationScope(DataStoreID scopeID, bool autoSelect)
        {
            navigationScopeFlags[scopeID] = autoSelect;
        }

        public void UnregisterNavigationScope(DataStoreID scopeID)
        {
            navigationScopeFlags.Remove(scopeID);
        }

        public bool TryGetFirstNavigableDescendant(DataStoreIndex rootIndex, out IUIBlock navigableDescendant)
        {
            NavDescendantsRunner.RootIndex = rootIndex;

            unsafe
            {
                navigableDescendantQuery.Method.Invoke(UnsafeUtility.AddressOf(ref NavDescendantsRunner));
            }

            navigableDescendant = null;
            DataStoreID descendantID = NavDescendantsRunner.OUT_NavigableDescendantID;

            if (!HierarchyDataStore.Instance.Elements.TryGetValue(descendantID, out IHierarchyBlock descendant))
            {
                return false;
            }

            navigableDescendant = descendant as IUIBlock;

            return true;
        }

        /// <summary>
        /// Index 0 of autoscopes is the scope root. The last item in autoscopes is closest to the given descendant.
        /// </summary>
        public void GetScopes<T>(DataStoreIndex descendantIndex, DataStoreID scopeRootID, int layerMask, List<T> autoscopes) where T : class, IUIBlock
        {
            NavAncestorScopeQueryRunner.DescendantIndex = descendantIndex;
            NavAncestorScopeQueryRunner.RootID = scopeRootID;
            NavAncestorScopeQueryRunner.LayerMask = layerMask;

            unsafe
            {
                navAncestorScopesQuery.Method.Invoke(UnsafeUtility.AddressOf(ref NavAncestorScopeQueryRunner));
            }

            autoscopes.Clear();

            NativeList<DataStoreID> ancestorScopes = NavAncestorScopeQueryRunner.Scopes;
            int count = ancestorScopes.Length;

            Dictionary<DataStoreID, IHierarchyBlock> elements = HierarchyDataStore.Instance.Elements;

            for (int i = count - 1; i >= 0; --i)
            {
                autoscopes.Add(elements[ancestorScopes[i]] as T);
            }
        }

        /// <summary>
        /// Performs a navigation hit test in the scene and returns the nearest UIBlock that intersects
        /// with the provided ray and the distance from ray origin to the interesection point.
        /// </summary>
        /// <returns></returns>
        public void Navigate(Ray ray, DataStoreID rootID, DataStoreID excludeID, bool excludeRoot, bool excludeAllClippedContent, List<HitTestResult> results, bool filterToNavNodes = true, int layerMask = AllLayers, System.Func<IUIBlock, bool> filter = null, int maxHits = 1)
        {
            PerformNavigation(ray, rootID, excludeID, excludeRoot, topLevelProximities: false, excludeAllClippedContent, filterToNavNodes, layerMask, ref NavigationCache);

            HitFilter hitFilter = HitFilter.InfiniteDistance;
            hitFilter.LayerMask = layerMask;
            hitFilter.Filter = filter;
            hitFilter.MaxHits = maxHits;

            GetHitResults(ref NavigationCache.Hits, results, ref hitFilter);
        }

        public void NavigateTopLevel(Ray ray, DataStoreID rootID, DataStoreID excludeID, bool excludeAllClippedContent, List<HitTestResult> results, bool filterToNavNodes = true, int layerMask = AllLayers, System.Func<IUIBlock, bool> filter = null, int maxHits = 1)
        {
            PerformNavigation(ray, rootID, excludeID, excludeRoot: true, topLevelProximities: true, excludeAllClippedContent, filterToNavNodes, layerMask, ref NavigationCache);

            HitFilter hitFilter = HitFilter.InfiniteDistance;
            hitFilter.LayerMask = layerMask;
            hitFilter.Filter = filter;
            hitFilter.MaxHits = maxHits;

            GetHitResults(ref NavigationCache.Hits, results, ref hitFilter);
        }

        private void PerformNavigation(Ray ray, DataStoreID rootID, DataStoreID excludeID, bool excludeRoot, bool topLevelProximities, bool excludeAllClippedContent, bool filterToNavNodes, int layerMask, ref HitTestCache<NavigationHit> buffers)
        {
            if (!EngineManager.Instance.HaveUpdated)
            {
                return;
            }

                NavigationRunner.CollisionTest.Ray = ray;
                NavigationRunner.CollisionTest.LayerMask = layerMask;

                if (rootID.IsValid)
                {
                    NavigationRunner.RootIDs = navigationScopeRootIDs;
                    NavigationRunner.RootIDs[0] = rootID;
                    NavigationRunner.CollisionTest.ScopeID = rootID;
                    NavigationRunner.CollisionTest.ExcludeScope = excludeRoot;
                }
                else
                {
                    NavigationRunner.RootIDs = HierarchyDataStore.Instance.HierarchyRootIDs;
                    NavigationRunner.CollisionTest.ScopeID = DataStoreID.Invalid;
                    NavigationRunner.CollisionTest.ExcludeScope = false;
                }

                NavigationRunner.CollisionTest.IgnoreID = excludeID;
                NavigationRunner.CollisionTest.IgnoreAllClippedContent = excludeAllClippedContent;
                NavigationRunner.CollisionTest.FilterToNavNodes = filterToNavNodes;
                NavigationRunner.CollisionTest.UseTopLevelProximities = topLevelProximities;

                NavigationRunner.HitIndices = buffers.Hits;
                NavigationRunner.IndicesToProcess = buffers.HitQueue;

                unsafe
                {
                    navigation.Method.Invoke(UnsafeUtility.AddressOf(ref NavigationRunner));
                }
        }


        private void PerformHitTest(Ray ray, int layerMask, ref HitTestCache<PointerHit> buffers, bool includeInvisible = false)
        {
            if (!EngineManager.Instance.HaveUpdated)
            {
                return;
            }

                // because we reuse the struct, just ensure this is false
                // unless we explicitly call the scene view methods
                HitTestRunner.CollisionTest.IncludeInvisibleContent = NovaApplication.IsEditor ? includeInvisible : false;
                HitTestRunner.CollisionTest.Ray = ray;
                HitTestRunner.CollisionTest.LayerMask = layerMask;
                HitTestRunner.HitIndices = buffers.Hits;
                HitTestRunner.IndicesToProcess = buffers.HitQueue;

                unsafe
                {
                    hitTest.Method.Invoke(UnsafeUtility.AddressOf(ref HitTestRunner));
                }
        }

        private void PerformCollisionTest(StructuredSphere sphere, int layerMask, ref HitTestCache<PointerHit> buffers)
        {
            if (!EngineManager.Instance.HaveUpdated)
            {
                return;
            }

                CollisionTestRunner.CollisionTest.Sphere = sphere;
                CollisionTestRunner.CollisionTest.LayerMask = layerMask;
                CollisionTestRunner.HitIndices = buffers.Hits;
                CollisionTestRunner.IndicesToProcess = buffers.HitQueue;

                unsafe
                {
                    collisionTest.Method.Invoke(UnsafeUtility.AddressOf(ref CollisionTestRunner));
                }
        }

        /// <summary>
        /// Returns all hit UIBlocks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hits"></param>
        /// <param name="results"></param>
        private void GetHitResults<T>(ref NativeList<T> hits, List<HitTestResult> results, ref HitFilter filter) where T : unmanaged, IHit
        {
            results.Clear();

            Dictionary<DataStoreID, IHierarchyBlock> elements = HierarchyDataStore.Instance.Elements;

            int hitCount = hits.Length;

            for (int i = 0; i < hitCount && results.Count < filter.MaxHits; ++i)
            {
                T hit = hits[i];

                if (hit.Proximity > filter.MaxDistance)
                {
                    break;
                }

                if (!elements.TryGetValue(hit.ID, out IHierarchyBlock hitBlock))
                {
                    continue;
                }

                // Could add duplicates for virtual nodes... tbd how we want to handle that
                if (hitBlock.IsVirtual)
                {
                    continue;
                }

                IUIBlock hitUIBlock = hitBlock as IUIBlock;

                if (hitUIBlock.IsVirtual || !filter.CanHit(hitUIBlock))
                {
                    continue;
                }

                results.Add(new HitTestResult()
                {
                    HitBlock = hitUIBlock,
                    HitPoint = hit.HitPoint,
                    Normal = hit.Normal
                });
            }
        }

        /// <summary>
        /// Returns the first hit UIBlock
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hitResults"></param>
        /// <returns></returns>
        private IUIBlock GetHitBlock(ref NativeList<PointerHit> hitResults, ref HitFilter filter, out float hitDistance, out float3 hitNormal)
        {
            hitDistance = float.NaN;
            hitNormal = Math.float3_NaN;
            IUIBlock hit = null;

            Dictionary<DataStoreID, IHierarchyBlock> elements = HierarchyDataStore.Instance.Elements;

            for (int i = 0; i < hitResults.Length; ++i)
            {
                PointerHit hitResult = hitResults[i];
                if (elements.TryGetValue(hitResult.ID, out IHierarchyBlock hitBlock))
                {
                    // Could add duplicates for virtual nodes... tbd how we want to handle that

                    IUIBlock uiBlock = hitBlock as IUIBlock;

                    if (uiBlock.IsVirtual || !filter.CanHit(uiBlock))
                    {
                        continue;
                    }

                    hitDistance = hitResult.Distance;
                    hitNormal = hitResult.Normal;
                    hit = uiBlock;
                    break;
                }
            }

            return hit;
        }

        protected override void Init()
        {
            unsafe
            {
                navigation = new BurstedMethod<BurstMethod>(HitTest<NavigateToBounds, StructuredRay, NavigationHit>.Run);
                navigableDescendantQuery = new BurstedMethod<BurstMethod>(FirstNavigableDescendant.Run);
                navAncestorScopesQuery = new BurstedMethod<BurstMethod>(AncestorScopeQuery.Run);
                hitTest = new BurstedMethod<BurstMethod>(HitTest<RayToBounds, StructuredRay, PointerHit>.Run);
                collisionTest = new BurstedMethod<BurstMethod>(HitTest<SphereToBounds, StructuredSphere, PointerHit>.Run);
            }

            NavigationCache.Init();
            navigationScopeRootIDs = new NativeList<DataStoreID>(1, Allocator.Persistent);
            navigationScopeIDCache = new NativeList<DataStoreID>(Constants.FewElementsInitialCapacity, Allocator.Persistent);
            navigationScopeFlags = new NovaHashMap<DataStoreID, bool>(Constants.FewElementsInitialCapacity, Allocator.Persistent);
            navigationNodeFlags = new NovaHashMap<DataStoreID, bool>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);
            topLevelProximityCache = new NovaHashMap<DataStoreID, float>(Constants.SomeElementsInitialCapacity, Allocator.Persistent);
            navigationScopeRootIDs.Length = 1;

            PointerCache.Init();

            HierarchyDataStore hierarchy = HierarchyDataStore.Instance;
            LayoutDataStore layouts = LayoutDataStore.Instance;

            HitTestRunner = new HitTest<RayToBounds, StructuredRay, PointerHit>()
            {
                Hierarchy = hierarchy.Hierarchy,
                HierarchyLookup = hierarchy.HierarchyLookup,
                RootIDs = hierarchy.HierarchyRootIDs,

                SpatialPartitionMasks = layouts.SpatialPartitions,

                CollisionTest = new RayToBounds()
                {
                    RenderOrderCalculator = RenderEngine.Instance.HierarchyRenderOrderCalculator,
                    VisualModifiers = new VisualModifiers()
                    {
                        VisualModifierIDs = RenderingDataStore.Instance.Common.VisualModifierIDs,
                        ModifierToBlockID = RenderingDataStore.Instance.VisualModifierTracker.ModifierToBlockID,
                        ShaderData = RenderingDataStore.Instance.VisualModifierTracker.ShaderData,
                        ClipInfo = RenderingDataStore.Instance.VisualModifierTracker.Data,
                    },

                    HierarchyLookup = hierarchy.HierarchyLookup,

                    LengthProperties = layouts.CalculatedLengths,
                    Sizes = layouts.TotalContentSizes,
                    Offsets = layouts.TotalContentOffsets,
                    WorldToLocalMatrices = layouts.WorldToLocalMatrices,
                    LocalToWorldMatrices = layouts.LocalToWorldMatrices,
                }
            };

            CollisionTestRunner = new HitTest<SphereToBounds, StructuredSphere, PointerHit>()
            {
                Hierarchy = hierarchy.Hierarchy,
                HierarchyLookup = hierarchy.HierarchyLookup,
                RootIDs = hierarchy.HierarchyRootIDs,

                SpatialPartitionMasks = layouts.SpatialPartitions,

                CollisionTest = new SphereToBounds()
                {
                    RenderOrderCalculator = RenderEngine.Instance.HierarchyRenderOrderCalculator,

                    VisualModifiers = new VisualModifiers()
                    {
                        VisualModifierIDs = RenderingDataStore.Instance.Common.VisualModifierIDs,
                        ModifierToBlockID = RenderingDataStore.Instance.VisualModifierTracker.ModifierToBlockID,
                        ShaderData = RenderingDataStore.Instance.VisualModifierTracker.ShaderData,
                        ClipInfo = RenderingDataStore.Instance.VisualModifierTracker.Data,
                    },

                    HierarchyLookup = hierarchy.HierarchyLookup,

                    LengthProperties = layouts.CalculatedLengths,
                    Sizes = layouts.TotalContentSizes,
                    Offsets = layouts.TotalContentOffsets,
                    WorldToLocalMatrices = layouts.WorldToLocalMatrices,
                    LocalToWorldMatrices = layouts.LocalToWorldMatrices,
                }
            };

            NavigationRunner = new HitTest<NavigateToBounds, StructuredRay, NavigationHit>()
            {
                Hierarchy = hierarchy.Hierarchy,
                HierarchyLookup = hierarchy.HierarchyLookup,
                RootIDs = navigationScopeRootIDs,

                SpatialPartitionMasks = layouts.SpatialPartitions,

                CollisionTest = new NavigateToBounds()
                {
                    RenderOrderCalculator = RenderEngine.Instance.HierarchyRenderOrderCalculator,
                    VisualModifiers = new VisualModifiers()
                    {
                        VisualModifierIDs = RenderingDataStore.Instance.Common.VisualModifierIDs,
                        ModifierToBlockID = RenderingDataStore.Instance.VisualModifierTracker.ModifierToBlockID,
                        ShaderData = RenderingDataStore.Instance.VisualModifierTracker.ShaderData,
                        ClipInfo = RenderingDataStore.Instance.VisualModifierTracker.Data,
                    },

                    HierarchyLookup = hierarchy.HierarchyLookup,
                    Hierarchy = hierarchy.Hierarchy,

                    NavigationScopeFlags = navigationScopeFlags,
                    NavigationNodeFlags = navigationNodeFlags,

                    LengthProperties = layouts.CalculatedLengths,
                    Sizes = layouts.TotalContentSizes,
                    Offsets = layouts.TotalContentOffsets,
                    WorldToLocalMatrices = layouts.WorldToLocalMatrices,
                    LocalToWorldMatrices = layouts.LocalToWorldMatrices,

                    TopLevelProximities = topLevelProximityCache,
                }
            };

            NavDescendantsRunner = new FirstNavigableDescendant()
            {
                NavNodes = navigationNodeFlags,
                Descendants = NavigationCache.HitQueue,

                Hierarchy = hierarchy.Hierarchy,
                HierarchyLookup = hierarchy.HierarchyLookup,

                LengthProperties = layouts.CalculatedLengths,

                WorldToLocalMatrices = layouts.WorldToLocalMatrices,
                LocalToWorldMatrices = layouts.LocalToWorldMatrices,

                VisualModifiers = new VisualModifiers()
                {
                    VisualModifierIDs = RenderingDataStore.Instance.Common.VisualModifierIDs,
                    ModifierToBlockID = RenderingDataStore.Instance.VisualModifierTracker.ModifierToBlockID,
                    ShaderData = RenderingDataStore.Instance.VisualModifierTracker.ShaderData,
                    ClipInfo = RenderingDataStore.Instance.VisualModifierTracker.Data,
                },
            };

            NavAncestorScopeQueryRunner = new AncestorScopeQuery()
            {
                NavScopes = navigationScopeFlags,
                BaseInfos = RenderingDataStore.Instance.Common.BaseInfos,
                Scopes = navigationScopeIDCache,
                Hierarchy = hierarchy.Hierarchy,
                HierarchyLookup = hierarchy.HierarchyLookup,
            };
        }

        protected override void Dispose()
        {
            NavigationCache.Dispose();
            navigationScopeRootIDs.Dispose();
            navigationScopeIDCache.Dispose();
            navigationScopeFlags.Dispose();
            navigationNodeFlags.Dispose();
            topLevelProximityCache.Dispose();

            PointerCache.Dispose();
        }
    }
}
