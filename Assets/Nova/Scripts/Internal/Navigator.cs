// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Input;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Collections.Generic;
using UnityEngine;


namespace Nova.Internal
{
    internal enum MoveResult
    {
        None,
        Found,
        WillLoad
    }

    internal struct ScopedMove
    {
        public IUIBlock ToUIBlock;
        public IUIBlock FromDirectChild;
        public IUIBlock ToDirectChild;
    }

    internal static class Navigator<T> where T : MonoBehaviour, IUIBlock
    {
        private static NavigationStack<T> navigationScopes = new NavigationStack<T>();
        private static NavigationStack<T> navigationPaths = new NavigationStack<T>();
        private static List<HitTestResult> hitResultCache = new List<HitTestResult>();
        private static List<T> scopeCache = new List<T>();
        private static ScopedSet scopedSets = new ScopedSet();

        public static event System.Action<uint, T, T, int> OnCurrentPathUntracked = null;
        public static void RegisterNavigationScope(T scope, bool autoSelect)
        {
            InputEngine.Instance.RegisterNavigationScope(scope.UniqueID, autoSelect);
        }

        public static void Track(T node)
        {
            InputEngine.Instance.RegisterNavigationNode(node.UniqueID);
        }

        public static void Untrack(T node)
        {
            InputEngine.Instance.UnregisterNavigationNode(node.UniqueID);

            scopedSets.RemoveElement(node);

            ReadOnlyList<NavigationStack<T>.Key> controls = navigationPaths.GetControls(node);

            for (int i = controls.Count - 1; i >= 0; --i)
            {
                NavigationStack<T>.Key control = controls[i];
                bool wasCurrentPath = Current(control.Value) == node;
                RemovePath(control.Value, node);

                if (wasCurrentPath)
                {
                    OnCurrentPathUntracked(control.Value, node, Current(control.Value), control.Layers);
                }
            }
        }

        public static void UnregisterNavigationScope(T scope)
        {
            InputEngine.Instance.UnregisterNavigationScope(scope.UniqueID);

            ReadOnlyList<NavigationStack<T>.Key> controls = navigationScopes.GetControls(scope);

            for (int i = controls.Count - 1; i >= 0; --i)
            {
                NavigationStack<T>.Key control = controls[i];
                navigationScopes.Remove(control.Value, scope);
            }

            if (!scope.ActiveInHierarchy)
            {
                ReadOnlyList<T> elements = scopedSets.GetScopeElements(scope);
                for (int i = elements.Count - 1; i >= 0; --i)
                {
                    Untrack(elements[i]);
                }
            }

            scopedSets.UntrackScope(scope);
        }

        private static void RemovePath(uint controlID, T path) => navigationPaths.Remove(controlID, path);

        public static T Current(uint controlID) => navigationPaths.Top(controlID);

        public static void MoveTo(uint controlID, T path, int layerMask)
        {
            if (path == null)
            {
                Clear(controlID);
                return;
            }

            navigationPaths.Push(controlID, path, layerMask);

            T scope = navigationScopes.Top(controlID);

            if (scope != null)
            {
                scopedSets.SetElementScope(path, scope);
            }
        }

        public static T GetScope(uint controlID) => navigationScopes.Top(controlID);

        public static T GetScope(uint controlID, int offsetFromTop) => navigationScopes.Top(controlID, offsetFromTop);

        public static int ScopeCount(uint controlID) => navigationScopes.GetCount(controlID);

        public static void PushScope(uint controlID, T scope, int layerMask)
        {
            navigationScopes.Push(controlID, scope, layerMask);
            scopedSets.TrackScope(scope);
        }

        public static void Clear(uint controlID)
        {
            navigationScopes.Clear(controlID);
            navigationPaths.Clear(controlID);
        }

        public static T PopScope(uint controlID)
        {
            T top = navigationScopes.Pop(controlID);

            if (top != null)
            {
                T current = navigationPaths.Top(controlID);

                while (current != null && current != top && current.transform.IsChildOf(top.transform))
                {
                    current = navigationPaths.Pop(controlID);
                }
            }

            return top;
        }

        public static bool IsInScope(uint controlID, T path)
        {
            T scope = navigationScopes.Top(controlID);

            return scope == null || (path != null && path.transform.IsChildOf(scope.transform));
        }

        /// <summary>
        /// Does a <see cref="TryGetNext(T, Vector3, out IUIBlock)"/>, but may page in more elements, depending on the scope
        /// </summary>
        /// <returns></returns>
        public static MoveResult TryLoadNext(T current, T scope, Vector3 direction, int layerMask, out IUIBlock result)
        {
            result = null;

            if (current != scope)
            {
                if (TryGetNext(current, direction, out result))
                {
                    return MoveResult.Found;
                }
            }

            if (scope != null && scope.InputTarget.Nav != null)
            {
                bool hasNext = TryGetScopedMove(scope, current, direction, layerMask, out ScopedMove move);

                // Always try handle even if !hasNext
                bool willLoad = scope.InputTarget.Nav.TryHandleScopedMove(move.FromDirectChild, move.ToDirectChild, direction);

                if (hasNext)
                {
                    result = move.ToUIBlock;
                    return MoveResult.Found;
                }

                if (willLoad)
                {
                    return MoveResult.WillLoad;
                }
            }

            return MoveResult.None;
        }

        public static void EnsureInView(IUIBlock current, uint controlID, int currentScopeOffset)
        {
            int i = currentScopeOffset + 1;

            current.CalculateLayout();

            for (T scope = GetScope(controlID, i); scope != null; scope = GetScope(controlID, ++i))
            {
                scope.InputTarget.Nav.EnsureInView(current);
            }
        }

        public static bool TryGetNext(T current, Vector3 direction, out IUIBlock result)
        {
            result = null;
            return current != null && current.InputTarget.Nav != null && current.InputTarget.Nav.TryGetNext(direction, out result);
        }

        public static bool TryGetInDirection(T current, IUIBlock scope, Vector3 direction, int layerMask, bool filterScope, out HitTestResult result) =>
            TryGet(CreateRay(direction, current, current == scope), current, scope, layerMask, filterScope, out result, excludeAllClippedContent: false);

        public static bool TryGet(Ray ray, T current, IUIBlock scope, int layerMask, bool filterScope, out HitTestResult result, bool excludeAllClippedContent = true, bool sortByTopLevelProximity = false)
        {
            result = default;

            DataStoreID scopeID = scope == null || !scope.Activated ? DataStoreID.Invalid : scope.UniqueID;
            DataStoreID currentID = current == null || !current.Activated ? DataStoreID.Invalid : current.UniqueID;

            if (sortByTopLevelProximity)
            {
                InputEngine.Instance.NavigateTopLevel(ray, scopeID, currentID, excludeAllClippedContent, hitResultCache, layerMask: layerMask);
            }
            else
            {
                InputEngine.Instance.Navigate(ray, scopeID, currentID, filterScope, excludeAllClippedContent, hitResultCache, layerMask: layerMask);
            }

            for (int i = 0; i < hitResultCache.Count; ++i)
            {
                HitTestResult hitResult = hitResultCache[i];

                if (hitResult.HitBlock.InputTarget.Nav != null)
                {
                    result = hitResult;
                    return true;
                }
            }

            return false;
        }

        public static bool EditorOnly_TryGet(Ray ray, T current, IUIBlock scope, int layerMask, bool filterScope, System.Func<IUIBlock, bool> filter, out HitTestResult result, bool sortByTopLevelProximity = false)
        {
            result = default;

            DataStoreID scopeID = scope == null || !scope.Activated ? DataStoreID.Invalid : scope.UniqueID;
            DataStoreID currentID = current == null || !current.Activated ? DataStoreID.Invalid : current.UniqueID;

            bool filterNav = NovaApplication.InPlayer(current);

            if (sortByTopLevelProximity)
            {
                InputEngine.Instance.NavigateTopLevel(ray, scopeID, currentID, excludeAllClippedContent: false, hitResultCache, filterNav, layerMask: layerMask, filter: filter);
            }
            else
            {
                InputEngine.Instance.Navigate(ray, scopeID, currentID, filterScope, excludeAllClippedContent: false, hitResultCache, filterNav, layerMask: layerMask, filter: filter);
            }

            for (int i = 0; i < hitResultCache.Count; ++i)
            {
                HitTestResult hitResult = hitResultCache[i];

                if (filter(hitResult.HitBlock))
                {
                    result = hitResult;
                    return true;
                }
            }

            return false;
        }

        public static bool Raycast(Ray ray, int layerMask, DataStoreID rootID, DataStoreID excludeID, bool filterToNavNodes, out HitTestResult result)
        {
            hitResultCache.Clear();
            InputEngine.Instance.Navigate(ray, rootID, excludeID, false, true, hitResultCache, layerMask: layerMask, filterToNavNodes: filterToNavNodes);

            result = hitResultCache.Count > 0 ? hitResultCache[0] : default;
            return result.HitBlock as T != null;
        }

        public static ReadOnlyList<HitTestResult> RaycastAll(Ray ray, int layerMask, DataStoreID rootID, DataStoreID excludeID, bool filterToNavNodes)
        {
            hitResultCache.Clear();
            InputEngine.Instance.Navigate(ray, rootID, excludeID, false, true, hitResultCache, layerMask: layerMask, maxHits: int.MaxValue, filterToNavNodes: filterToNavNodes);
            return hitResultCache.ToReadOnly();
        }

        public static Ray CreateRay(Vector3 direction, T from, bool castInside = false)
        {
            Vector3 edge = castInside ? -direction : direction;

            float scalar = Mathf.Abs(1 / Math.MaxComponentAbs(edge));
            scalar = float.IsNaN(scalar) || float.IsInfinity(scalar) ? 0 : scalar;

            Vector3 localPosition = Vector3.Scale(from.CalculatedSize.Value, edge * scalar) * 0.5f;

            return new Ray(from.transform.TransformPoint(localPosition), from.transform.TransformDirection(direction));
        }

        public static bool TryGetFirstNavigableDescendant(T root, out Vector3 localDirection, out Ray worldRay, int layerMask, out T descendant)
        {
            localDirection = default;
            worldRay = default;
            descendant = null;

            if (root == null)
            {
                return false;
            }

            if (!InputEngine.Instance.TryGetFirstNavigableDescendant(root.Index, out IUIBlock navigable))
            {
                return false;
            }

            Vector3 worldPos = navigable.Transform.position;
            Vector3 localPos = root.Transform.InverseTransformPoint(worldPos);

            UIBounds bounds = new UIBounds(root.CalculatedSize.Value);

            Vector3 closestPoint = bounds.ClosestPointOnSurface(localPos);
            Vector3 direction = Vector3.Normalize(localPos - closestPoint);

            localDirection = direction == Vector3.zero ? Vector3.down : direction;
            worldRay = new Ray(root.Transform.TransformPoint(closestPoint), root.Transform.TransformDirection(localDirection));
            descendant = navigable as T;

            return true;
        }

        public static ReadOnlyList<T> GetAncestorScopes(T descendant, T scopeRoot, int layerMask)
        {
            if (descendant == null)
            {
                return ReadOnlyList<T>.Empty;
            }

            DataStoreID rootID = scopeRoot == null ? DataStoreID.Invalid : scopeRoot.UniqueID;
            InputEngine.Instance.GetScopes(descendant.Index, rootID, layerMask, scopeCache);

            return scopeCache.ToReadOnly();
        }

        // Assumes fromUIBlock is either equal to scope or a descendant of scope.
        // Attempts to filter a navigation move to a descendant of an adjacent child
        // along the scope's autolayout axis
        public static bool TryGetScopedMove(IUIBlock scope, IUIBlock fromUIBlock, Vector3 direction, int layerMask, out ScopedMove move)
        {
            move = default;

            if (!LayoutUtils.TryGetAxisDirection(scope, direction, out int _, out int axisDirection))
            {
                return false;
            }

            if (fromUIBlock == scope)
            {
                // Children not navigable
                return false;
            }

            // Check if there's anything to navigate to in the given direction
            if (!TryGetInDirection(fromUIBlock as T, scope, direction, layerMask, filterScope: true, out HitTestResult result))
            {
                return false;
            }

            IUIBlock nextDescendant = result.HitBlock;
            AutoLayout autoLayout = scope.SerializedAutoLayout;

            IUIBlock fromDirectChild = scope.GetDirectChild(fromUIBlock);
            int fromChildIndex = scope.GetChildIndex(fromDirectChild);
            int nextChildIndex = Mathf.Clamp(fromChildIndex + autoLayout.ContentDirection * axisDirection, 0, scope.GetChildCount() - 1);
            IUIBlock nextDirectChild = scope.GetChildAtIndex(nextChildIndex);

            if (nextDescendant.IsDescendantOf(nextDirectChild))
            {
                // Don't need to change anything.
                // This is the "assumed" result.
            }
            else if (nextDescendant.IsDescendantOf(fromDirectChild))
            {
                nextDirectChild = fromDirectChild;
            }
            else if (TryGetInDirection(fromUIBlock as T, nextDirectChild, direction, layerMask, filterScope: false, out result))
            {
                // We know we can navigate to something. Now scope the nav query to what we expect is the next element in the list.
                nextDescendant = result.HitBlock;
            }
            else
            {
                nextDirectChild = scope.GetDirectChild(nextDescendant);
            }

            move = new ScopedMove()
            {
                ToUIBlock = nextDescendant,
                FromDirectChild = fromDirectChild,
                ToDirectChild = nextDirectChild
            };

            return true;
        }

        private class ScopedSet
        {
            private Dictionary<T, HashList<T>> scopedSets = new Dictionary<T, HashList<T>>();
            private Dictionary<T, T> elementToScope = new Dictionary<T, T>();

            public void TrackScope(T scope)
            {
                if (scopedSets.ContainsKey(scope))
                {
                    return;
                }

                scopedSets.Add(scope, CollectionPool<HashList<T>, T>.Get());
            }

            public ReadOnlyList<T> GetScopeElements(T scope)
            {
                if (scopedSets.TryGetValue(scope, out HashList<T> elements))
                {
                    return elements.List;
                }

                return ReadOnlyList<T>.Empty;
            }

            public void UntrackScope(T scope)
            {
                if (!scopedSets.TryGetValue(scope, out HashList<T> elements))
                {
                    return;
                }

                ReadOnlyList<T> elementsReadOnly = elements.List;

                for (int i = 0; i < elementsReadOnly.Count; i++)
                {
                    elementToScope.Remove(elementsReadOnly[i]);
                }

                scopedSets.Remove(scope);
                CollectionPool<HashList<T>, T>.Release(elements);
            }

            public void SetElementScope(T element, T scope)
            {
                // Remove stale values if element has been reparented
                RemoveElement(element);

                elementToScope[element] = scope;
                scopedSets[scope].Add(element);
            }

            public void RemoveElement(T element)
            {
                if (!elementToScope.TryGetValue(element, out T scope))
                {
                    return;
                }

                elementToScope.Remove(element);
                scopedSets[scope].Remove(element);
            }
        }
    }
}
