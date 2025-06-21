// Copyright (c) Supernova Technologies LLC
using AOT;
using Nova.Compat;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Hierarchy
{
    internal partial class Hierarchy
    {
        private enum Function
        {
            SetParent,
            Unparent,
            AddToHierarchy,
            RemoveFromHierarchy,
            AddBatchRoot,
            RemoveBatchRoot,
            AddHierarchyRoot,
            RemoveHierarchyRoot,
            AddVirtualProxy,
            UpdateVirtualProxyIndex,
            RemoveVirtualProxy,
            AppendAndRedistributeChildren,
            SetSiblingIndex,
            SetChildOrder,
            GetChildrenOrSiblings,
            GetSubHierarchySorted,
            GetDirtyBatchElements,
            GetRootID,
            IsDescendantOf,
        }


        [BurstCompile]
        [MonoPInvokeCallback(typeof(BurstFunction))]
        private static unsafe void Run(void* jobData, int function)
        {
            Function method = (Function)function;

            switch (method)
            {
                case Function.SetParent:
                    UnsafeUtility.AsRef<SetParentJob>(jobData).Execute();
                    break;
                case Function.Unparent:
                    UnsafeUtility.AsRef<UnparentJob>(jobData).Execute();
                    break;
                case Function.AddToHierarchy:
                    UnsafeUtility.AsRef<AddToHierarchyJob>(jobData).Execute();
                    break;
                case Function.RemoveFromHierarchy:
                    UnsafeUtility.AsRef<RemoveFromHierarchyJob>(jobData).Execute();
                    break;
                case Function.AddBatchRoot:
                    UnsafeUtility.AsRef<AddBatchRootJob>(jobData).Execute();
                    break;
                case Function.RemoveBatchRoot:
                    UnsafeUtility.AsRef<RemoveBatchRootJob>(jobData).Execute();
                    break;
                case Function.AddHierarchyRoot:
                    UnsafeUtility.AsRef<AddHierarchyRootJob>(jobData).Execute();
                    break;
                case Function.RemoveHierarchyRoot:
                    UnsafeUtility.AsRef<RemoveHierarchyRootJob>(jobData).Execute();
                    break;
                case Function.AddVirtualProxy:
                    UnsafeUtility.AsRef<AddVirtualProxyJob>(jobData).Execute();
                    break;
                case Function.UpdateVirtualProxyIndex:
                    UnsafeUtility.AsRef<UpdateVirtualProxyIndexJob>(jobData).Execute();
                    break;
                case Function.RemoveVirtualProxy:
                    UnsafeUtility.AsRef<RemoveVirtualProxyJob>(jobData).Execute();
                    break;
                case Function.AppendAndRedistributeChildren:
                    UnsafeUtility.AsRef<AppendAndRedistributeChildrenJob>(jobData).Execute();
                    break;
                case Function.SetChildOrder:
                    UnsafeUtility.AsRef<SetChildOrderJob>(jobData).Execute();
                    break;
                case Function.SetSiblingIndex:
                    UnsafeUtility.AsRef<SetSiblingIndexJob>(jobData).Execute();
                    break;
                case Function.GetChildrenOrSiblings:
                    UnsafeUtility.AsRef<GetChildrenOrSiblingsJob>(jobData).Execute();
                    break;
                case Function.GetSubHierarchySorted:
                    UnsafeUtility.AsRef<CombineSubtrees>(jobData).Execute();
                    break;
                case Function.GetDirtyBatchElements:
                    UnsafeUtility.AsRef<GetDirtyElements>(jobData).Execute();
                    break;
                case Function.GetRootID:
                    UnsafeUtility.AsRef<GetRootID>(jobData).Execute();
                    break;
                case Function.IsDescendantOf:
                    UnsafeUtility.AsRef<IsDescendantOfJob>(jobData).Execute();
                    break;
            }
        }

        /// <summary>
        /// Registers with parent at the provided sibling index
        /// </summary>
        private struct SetParentJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public NativeList<DataStoreIndex> ChildCache;
            public DataStoreIndex ChildIndex;
            public DataStoreID ParentID;
            public int SiblingPriority_In;
            public int SiblingIndex_Out;

            public void Execute()
            {
                SiblingIndex_Out = SetParent(ChildIndex, ParentID, SiblingPriority_In, ref Hierarchy, ref ChildCache);
            }
        }

        /// <summary>
        /// Removes the element from its parent's hierarchy
        /// </summary>
        /// <param name="childID"></param>
        /// <param name="parentID"></param>
        /// <returns></returns>
        private struct UnparentJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreID ChildID;

            public void Execute()
            {
                Unparent(ChildID, ref Hierarchy);
            }
        }

        private struct AddToHierarchyJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreID IDToAdd;
            public int SiblingPriority;

            public NativeList<DataStoreIndex> ChildCache;
            public DataStoreID ParentID;
            public bool IsVirtual;

            public void Execute()
            {
                DataStoreIndex childIndex = Hierarchy.Elements.Length;
                Hierarchy.Lookup.Add(IDToAdd, childIndex);

                HierarchyElement element = HierarchyElement.Create(IDToAdd);
                element.Children = Hierarchy.GetIndexList();

                Hierarchy.Elements.Add(element);
                Hierarchy.BatchGroupTracker.AddEmpty();

                Hierarchy.SiblingPriorities.Add(SiblingPriority);

                if (ParentID.IsValid)
                {
                    _ = SetParent(childIndex, ParentID, SiblingPriority, ref Hierarchy, ref ChildCache);
                }
                else if (!IsVirtual)
                {
                    AddHierarchyRootJob.AddHierarchyRoot(IDToAdd, ref Hierarchy);
                }
            }

            [BurstCompile]
            [MonoPInvokeCallback(typeof(BurstMethod))]
            public static unsafe void Run(void* jobData)
            {
                UnsafeUtility.AsRef<UnparentJob>(jobData).Execute();
            }
        }

        private struct RemoveFromHierarchyJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreID IDToRemove;

            public void Execute()
            {
                RemoveFromHierarchy(IDToRemove, ref Hierarchy);
            }
        }

        private struct RemoveHierarchyRootJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreID RootIDToRemove;
            public bool IsBatchRoot;

            public void Execute()
            {
                if (!Hierarchy.RootIDs.Remove(RootIDToRemove) || IsBatchRoot)
                {
                    return;
                }

                RemoveBatchRoot(RootIDToRemove, ref Hierarchy);
            }
        }

        private struct AddHierarchyRootJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreID RootIDToAdd;

            public void Execute()
            {
                AddHierarchyRoot(RootIDToAdd, ref Hierarchy);
            }

            public static void AddHierarchyRoot(DataStoreID rootIDToAdd, ref NativeHierarchy hierarchy)
            {
                if (!hierarchy.RootIDs.Add(rootIDToAdd))
                {
                    return;
                }

                AddBatchRoot(rootIDToAdd, ref hierarchy);
            }
        }

        private struct RemoveBatchRootJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreID RootIDToRemove;

            public void Execute()
            {
                RemoveBatchRoot(RootIDToRemove, ref Hierarchy);
            }
        }

        private struct AddBatchRootJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreID RootIDToAdd;

            public void Execute()
            {
                AddBatchRoot(RootIDToAdd, ref Hierarchy);
            }
        }

        /// <summary>
        /// Adds a hierarchical proxy to the given parent element
        /// </summary>
        /// <param name="VirtualProxyID"></param>
        /// <param name="ParentElement"></param>
        /// <returns></returns>
        private struct AddVirtualProxyJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public NativeList<DataStoreIndex> ChildCache;
            public HierarchyElement ParentElement;
            public DataStoreID VirtualProxyID;
            public int ChildrenPerProxy;
            public bool FirstSibling;

            public void Execute()
            {
                AddVirtualProxy(VirtualProxyID, ChildrenPerProxy, FirstSibling, ref ParentElement, ref ChildCache, ref Hierarchy);
            }
        }

        /// <summary>
        /// Updates the sibling index of the virtual proxy
        /// </summary>
        /// <param name="VirtualProxyID"></param>
        /// <param name="ParentElement"></param>
        /// <returns></returns>
        private struct UpdateVirtualProxyIndexJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public NativeList<DataStoreIndex> ChildCache;
            public HierarchyElement ParentElement;
            public DataStoreID VirtualProxyID;
            public int NewProxyIndex;

            public void Execute()
            {
                UpdateVirtualProxyIndex(VirtualProxyID, NewProxyIndex, ref ParentElement, ref ChildCache, ref Hierarchy);
            }
        }

        /// <summary>
        /// Removes the element from being a hierarchical proxy
        /// </summary>
        /// <param name="VirtualProxyID"></param>
        /// <param name="ParentElement"></param>
        /// <returns></returns>
        private struct RemoveVirtualProxyJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public NativeList<DataStoreIndex> ChildCache;
            public HierarchyElement ParentElement;
            public DataStoreID VirtualProxyID;

            public void Execute()
            {
                RemoveVirtualProxy(VirtualProxyID, ref ParentElement, ref ChildCache, ref Hierarchy);
            }
        }

        /// <summary>
        /// Appends all children of the given list of parents to the given list of children
        /// and then redistributes that set of children evenly amongst the set of parents.
        /// </summary>
        private struct AppendAndRedistributeChildrenJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public NativeList<DataStoreIndex> Children;
            public ProxyContainer ProxyContainer;

            public void Execute()
            {
                int activeChildCount = AppendChildrenToCache(ref ProxyContainer.ProxyIDs, ref Children, ref Hierarchy);
                RedistributeChildren(ref ProxyContainer, ref Children, activeChildCount, ref Hierarchy);
            }
        }

        private struct SetChildOrderJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreIndex ParentIndex;
            public DataStoreID ParentID;
            public NativeList<DataStoreIndex> ChildIndexCache;
            public NativeList<int> ChildPriorityCache;

            public void Execute()
            {
                int count = ChildIndexCache.Length;
                for (int i = 0; i < count; ++i)
                {
                    Hierarchy.SiblingPriorities[ChildIndexCache[i]] = ChildPriorityCache[i];
                }

                // if the parent has hierarchical proxies, the children
                // need to be redistributed among them in the new order
                if (Hierarchy.VirtualProxies.TryGetValue(ParentID, out ProxyContainer proxyContainer))
                {
                    RedistributeChildren(ref proxyContainer, ref ChildIndexCache, count, ref Hierarchy);
                }
                else // otherwise just copy the new list
                {
                    HierarchyElement parentElement = Hierarchy.Elements[ParentIndex];

                    if (parentElement.ChildCount != count) // maybe can remove this...?
                    {
                        Debug.LogError($"Called UpdateChildOrder with {ChildIndexCache.Length} children while hierarchy is tracking {parentElement.ChildCount}.");
                        return;
                    }

                    NovaList<DataStoreIndex> children = parentElement.Children;
                    children.Clear();

                    unsafe
                    {
                        children.AddRangeNoResize(ChildIndexCache.GetRawReadonlyPtr(), count);
                    }

                    parentElement.Children = children;

                    Hierarchy.Elements[ParentIndex] = parentElement;
                    Hierarchy.BatchGroupTracker.MarkContainingBatchDirty(ParentIndex);
                }
            }
        }

        /// <summary>
        /// Allows a fast path for set first/last sibling index -- arbitrary index not currently supported
        /// </summary>
        private struct SetSiblingIndexJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public DataStoreID ParentID;
            public DataStoreID ChildID;
            public NativeList<DataStoreIndex> ChildIndexCache;

            // true, set last. false, set first.
            public bool LastSibling;

            public int NewSiblingPriority;

            public void Execute()
            {
                SetSiblingIndex(ChildID, ParentID, LastSibling, NewSiblingPriority, ref ChildIndexCache, ref Hierarchy);
            }
        }

        /// <summary>
        /// Gets all children for the given parent. If the parent has proxy elements (virtual nodes), this will get all of their children instead.
        /// </summary>
        private struct GetChildrenOrSiblingsJob : IJob
        {
            public NativeHierarchy Hierarchy;
            public NativeList<DataStoreIndex> Children;
            public DataStoreID ParentID;

            public DataStoreID ChildID;
            public int SiblingIndex_Out;

            public void Execute()
            {
                SiblingIndex_Out = -1;
                DataStoreIndex childIndex = DataStoreIndex.Invalid;

                if (!ParentID.IsValid)
                {
                    if (!ChildID.IsValid)
                    {
                        Debug.LogError("Unable to retrieve children or siblings of invalid elements.");
                        return;
                    }

                    childIndex = Hierarchy.Lookup[ChildID];
                    ParentID = Hierarchy.Elements[childIndex].ParentID;
                }

                GetChildren(ParentID, ref Children, ref Hierarchy);

                if (childIndex.IsValid)
                {
                    _ = Children.TryGetIndexOf(childIndex, out SiblingIndex_Out);
                }
            }
        }

        [BurstCompile]
        private struct CombineSubtrees : INovaJob
        {
            public NativeHierarchy.ReadOnly Hierarchy;

            [ReadOnly]
            public NativeList<DataStoreID> SubRootIDs;

            public NativeList<DataStoreIndex> DepthSortedHierarchyToPopulate;

            public void Execute()
            {
                Run(ref Hierarchy, ref SubRootIDs, ref DepthSortedHierarchyToPopulate);
            }

            public static void Run(ref NativeHierarchy.ReadOnly hierarchy, ref NativeList<DataStoreID> subRootIDs, ref NativeList<DataStoreIndex> depthSortedHierarchyToPopulate)
            {
                int rootCount = subRootIDs.Length;
                depthSortedHierarchyToPopulate.Clear();

                if (rootCount == 0)
                {
                    return;
                }

                for (int i = 0; i < rootCount; ++i)
                {
                    DataStoreIndex rootIndex = hierarchy.Lookup[subRootIDs[i]];

                    if (hierarchy.Elements[rootIndex].ParentID.IsValid)
                    {
                        bool willBeAddedByOtherSubroot = false;
                        for (int j = 0; j < rootCount; ++j)
                        {
                            if (j == i)
                            {
                                continue;
                            }

                            if (hierarchy.IsDescendantOf(rootIndex, subRootIDs[j], out _))
                            {
                                willBeAddedByOtherSubroot = true;
                                break;
                            }
                        }

                        if (willBeAddedByOtherSubroot)
                        {
                            continue;
                        }
                    }

                    depthSortedHierarchyToPopulate.Add(rootIndex);

                    unsafe
                    {
                        int endOfHierarchy = depthSortedHierarchyToPopulate.Length;
                        for (int j = endOfHierarchy - 1; j < endOfHierarchy; ++j)
                        {
                            NovaList<DataStoreIndex> grandChildren = hierarchy.Elements[depthSortedHierarchyToPopulate[j]].Children;

                            int grandchildCount = grandChildren.Length;

                            if (grandchildCount > 0)
                            {
                                depthSortedHierarchyToPopulate.AddRange(grandChildren.Ptr, grandchildCount);
                                endOfHierarchy += grandchildCount;
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private struct IsDescendantOfJob
        {
            public NativeHierarchy.ReadOnly Hierarchy;
            public DataStoreID AncestorID;
            public DataStoreIndex DescendantIndex;

            public bool OUT_Result;
            public DataStoreID OUT_DirectChildID;

            public void Execute()
            {
                OUT_Result = Hierarchy.IsDescendantOf(DescendantIndex, AncestorID, out OUT_DirectChildID);
            }
        }

        [BurstCompile]
        private struct GetDirtyElements : INovaJob
        {
            public NativeHierarchy.ReadOnly Hierarchy;
            public NativeList<DataStoreID> RootsToUpdate;
            public NativeList<DataStoreIndex> ElementsToUpdate;

            public void Execute()
            {
                Hierarchy.BatchGroupTracker.PopulateWithDirtyRoots(ref RootsToUpdate);
                CombineSubtrees.Run(ref Hierarchy, ref RootsToUpdate, ref ElementsToUpdate);
            }
        }

        private struct GetRootID : IJob
        {
            public NativeHierarchy.ReadOnly Hierarchy;
            public DataStoreID DescendentID;
            public NativeList<DataStoreID> RootIDOutput;

            public void Execute()
            {
                RootIDOutput.Clear();

                DataStoreID rootID = DescendentID;
                DataStoreID parentID = DescendentID;

                while (Hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex rootIndex))
                {
                    rootID = parentID;
                    parentID = Hierarchy.Elements[rootIndex].ParentID;
                }

                RootIDOutput.Add(rootID);
            }
        }

        private static int SetParent(DataStoreIndex childIndex, DataStoreID parentID, int childSiblingPriority, ref NativeHierarchy Hierarchy, ref NativeList<DataStoreIndex> childIndexCache)
        {
            int siblingIndex = -1;

            if (!Hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
            {
                Debug.LogError("Called SetParent when parent was not found.");
                return siblingIndex;
            }

            DataStoreID formerParentID = Hierarchy.GetParentID(childIndex);

            if (formerParentID == parentID)
            {
                _ = GetChildren(parentID, ref childIndexCache, ref Hierarchy);

                // Nothing to do, the child is already registered to the parent
                _ = childIndexCache.TryGetIndexOf(childIndex, out siblingIndex);
                return siblingIndex;
            }

            int activeChildCount = GetChildren(parentID, ref childIndexCache, ref Hierarchy);

            int childCount = childIndexCache.Length;
            bool frontToBack = childSiblingPriority < childCount * 0.5f;
            siblingIndex = frontToBack ? 0 : childCount;

            if (frontToBack)
            {
                while (siblingIndex < childCount && Hierarchy.SiblingPriorities[childIndexCache[siblingIndex]] <= childSiblingPriority)
                {
                    siblingIndex++;
                }
            }
            else
            {
                while (siblingIndex > 0 && Hierarchy.SiblingPriorities[childIndexCache[siblingIndex - 1]] >= childSiblingPriority)
                {
                    siblingIndex--;
                }
            }

            // Ensure sibling index is up to date, since SetParent might get called
            // in sequence, before the parent has had a chance to call UpdateChildOrder.
            Hierarchy.SiblingPriorities[childIndex] = childSiblingPriority;

            if (Hierarchy.VirtualProxies.TryGetValue(parentID, out ProxyContainer proxyContainer))
            {
                activeChildCount++;

                childIndexCache.Insert(siblingIndex, childIndex);

                RedistributeChildren(ref proxyContainer, ref childIndexCache, activeChildCount, ref Hierarchy);
            }
            else
            {
                HierarchyElement parent = Hierarchy.Elements[parentIndex];
                SetParentInternal(childIndex, ref parent, siblingIndex, ref Hierarchy);
            }

            return siblingIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetParentInternal(DataStoreIndex childIndex, ref HierarchyElement parentElement, int siblingIndex, ref NativeHierarchy hierarchy)
        {
            HierarchyElement childElement = hierarchy.Elements[childIndex];

            if (childElement.ParentID.IsValid)
            {
                Unparent(childElement.ID, ref hierarchy);

                // Kind of funky but make sure we get the most up to date info
                childElement = hierarchy.Elements[childIndex];
            }

            childElement.ParentID = parentElement.ID;
            hierarchy.Elements[childIndex] = childElement;

            NovaList<DataStoreIndex> childIDs = parentElement.Children;
            childIDs.Insert(siblingIndex, childIndex);
            parentElement.Children = childIDs;

            DataStoreID parentID = parentElement.ID;
            DataStoreIndex parentIndex = hierarchy.Lookup[parentID];
            hierarchy.Elements[parentIndex] = parentElement;

            hierarchy.BatchGroupTracker.MarkChildDirty(childIndex, parentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unparent(DataStoreID childID, ref NativeHierarchy hierarchy)
        {
            if (!hierarchy.Lookup.TryGetValue(childID, out DataStoreIndex childIndex))
            {
                Debug.LogError("Called Unparent when child was not found.");
                return;
            }

            HierarchyElement childElement = hierarchy.Elements[childIndex];

            if (!childElement.ParentID.IsValid)
            {
                return;
            }

            if (!hierarchy.Lookup.TryGetValue(childElement.ParentID, out DataStoreIndex parentIndex))
            {
                Debug.LogError($"Called Unparent when parent was not found.");
                return;
            }

            HierarchyElement parentElement = hierarchy.Elements[parentIndex];

            RemoveFromParentHierarchy(childIndex, ref parentElement, ref hierarchy);
        }

        /// <summary>
        /// Removes the given element from its parent hierarchy
        /// </summary>
        /// <param name="childIndex"></param>
        /// <param name="parentElement"></param>
        /// <param name="hierarchy"></param>
        /// <param name="adjustDepthLevelsRecursive"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveFromParentHierarchy(DataStoreIndex childIndex, ref HierarchyElement parentElement, ref NativeHierarchy hierarchy)
        {
            if (!parentElement.Children.TryGetIndexOf(childIndex, out int siblingIndex))
            {
                Debug.LogError($"ChildIndex [{(int)childIndex}] not found in parent [0x{parentElement.ID:X8}] list. Something is broken");
            }

            NovaList<DataStoreIndex> children = parentElement.Children;
            children.RemoveAt(siblingIndex);
            parentElement.Children = children;

            HierarchyElement childElement = hierarchy.Elements[childIndex];
            childElement.ParentID = DataStoreID.Invalid;
            hierarchy.Elements[childIndex] = childElement;

            DataStoreID parentID = parentElement.ID;
            DataStoreIndex parentIndex = hierarchy.Lookup[parentID];
            hierarchy.Elements[parentIndex] = parentElement;

            hierarchy.BatchGroupTracker.MarkChildDirty(childIndex, parentIndex);
        }

        /// <summary>
        /// Reparents all children of Former Parent to the given New Parent.
        /// 
        /// ***cache is not cleared before reparenting, so existing elements in the cache 
        /// will also be reparented. Cache is cleared when the operation completes.
        /// 
        /// </summary>
        /// <param name="formerParent"></param>
        /// <param name="newParentID"></param>
        /// <param name="childCache"></param>
        /// <param name="container"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void ReparentChildren(ref HierarchyElement formerParent, DataStoreID newParentID, ref NativeList<DataStoreIndex> childCache, ref NativeHierarchy container)
        {
            DataStoreIndex newParentIndex = container.Lookup[newParentID];

            childCache.AddRange(formerParent.Children.Ptr, formerParent.Children.Length);

            for (int i = 0; i < childCache.Length; ++i)
            {
                // properties might have changed, so just get per loop iteration
                HierarchyElement newParent = container.Elements[newParentIndex];
                SetParentInternal(childCache[i], ref newParent, i, ref container);
            }

            childCache.Clear();
        }

        /// <summary>
        /// Appends all children of parentIDs to the child cache and 
        /// returns the number of appended children which are active
        /// </summary>
        /// <param name="parentIDs"></param>
        /// <param name="childIndexCache"></param>
        /// <param name="hierarchy"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int AppendChildrenToCache(ref NovaList<DataStoreID> parentIDs, ref NativeList<DataStoreIndex> childIndexCache, ref NativeHierarchy hierarchy)
        {
            int appendedActiveChildCount = 0;

            for (int i = 0; i < parentIDs.Length; ++i)
            {
                DataStoreIndex parentIndex = hierarchy.Lookup[parentIDs[i]];
                HierarchyElement parentElement = hierarchy.Elements[parentIndex];

                NovaList<DataStoreIndex> children = parentElement.Children;
                int childCount = children.Length;

                childIndexCache.AddRange(children.Ptr, childCount);

                appendedActiveChildCount += childCount;
            }

            return appendedActiveChildCount;
        }

        /// <summary>
        /// Reparents all children in the provided cache (meaning the IDs in the cache when this method
        /// is called is the set being redistributed) evenly amongst the set of provided parent IDs.
        /// The cache is then cleared when the operation completes.
        /// </summary>
        /// <param name="parents"></param>
        /// <param name="children"></param>
        /// <param name="hierarchy"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void RedistributeChildren(ref ProxyContainer proxyContainer, ref NativeList<DataStoreIndex> children, int activeChildCountInCache, ref NativeHierarchy hierarchy)
        {
            NovaList<DataStoreID> proxies = proxyContainer.ProxyIDs;

            int activeChildrenPerProxy = activeChildCountInCache / proxies.Length;
            activeChildrenPerProxy = math.select(activeChildrenPerProxy + 1, activeChildrenPerProxy, activeChildCountInCache % proxies.Length == 0);

            activeChildrenPerProxy = math.max(activeChildrenPerProxy, proxyContainer.DesiredChildrenPerProxy);

            int processParentIndex = 0;
            int processChildIndex = 0;

            while (processChildIndex < children.Length && processParentIndex < proxies.Length)
            {
                DataStoreID parentID = proxies[processParentIndex++];
                processChildIndex = ReparentChildRange(processChildIndex, activeChildrenPerProxy, parentID, ref children, ref hierarchy);
            }

            children.Clear();
        }

        /// <summary>
        /// Reparents up to activeChildCountToMove elements, starting from the children[startIndex], to the given parent element
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReparentChildRange(int startIndex, int activeChildCountToMove, DataStoreID parentID, ref NativeList<DataStoreIndex> children, ref NativeHierarchy hierarchy)
        {
            int index = startIndex;

            int activeChildCount = 0;
            int newSiblingIndex = 0;

            DataStoreIndex parentIndex = hierarchy.Lookup[parentID];
            while (activeChildCount < activeChildCountToMove && index < children.Length)
            {
                if (index >= children.Length)
                {
                    return index;
                }

                DataStoreIndex childIndex = children[index];

                // properties might have changed, so just get per loop iteration
                HierarchyElement parentElement = hierarchy.Elements[parentIndex];

                SetParentAndSiblingIndex(childIndex, newSiblingIndex, parentIndex, ref parentElement, ref hierarchy);

                newSiblingIndex++;
                activeChildCount++;
                index++;
            }

            return index;
        }

        /// <summary>
        /// Reparents the element at childIndex to the given parentElement. If the child is already parented to the given parent
        /// element, then only the child's sibling index is adjusted (as needed) to the provided sibling index value.
        /// </summary>
        /// <param name="childIndex"></param>
        /// <param name="siblingIndex"></param>
        /// <param name="parentElement"></param>
        /// <param name="hierarchy"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetParentAndSiblingIndex(DataStoreIndex childIndex, int siblingIndex, DataStoreIndex parentIndex, ref HierarchyElement parentElement, ref NativeHierarchy hierarchy)
        {
            HierarchyElement childElement = hierarchy.Elements[childIndex];

            if (childElement.ParentID != parentElement.ID)
            {
                SetParentInternal(childIndex, ref parentElement, siblingIndex, ref hierarchy);
                return;
            }

            NovaList<DataStoreIndex> siblingIDs = parentElement.Children;

            if (siblingIDs.TryGetIndexOf(childIndex, out int foundSiblingIndex) && foundSiblingIndex != siblingIndex)
            {
                siblingIDs.RemoveAt(foundSiblingIndex);
                siblingIDs.Insert(siblingIndex, childIndex);
                parentElement.Children = siblingIDs;
                hierarchy.BatchGroupTracker.MarkChildDirty(childIndex, parentIndex);
            }
        }

        /// <summary>
        /// Adds an element as a hierarchical proxy (virtual sub parent) of the given parent element
        /// </summary>
        /// <param name="virtualProxyID"></param>
        /// <param name="firstPosition"></param>
        /// <param name="parentElement"></param>
        /// <param name="childCache"></param>
        /// <param name="hierarchy"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddVirtualProxy(DataStoreID virtualProxyID, int desiredChildrenPerProxy, bool firstPosition, ref HierarchyElement parentElement, ref NativeList<DataStoreIndex> childCache, ref NativeHierarchy hierarchy)
        {
            if (!hierarchy.VirtualProxies.TryGetValue(parentElement.ID, out ProxyContainer proxyContainer))
            {
                proxyContainer = new ProxyContainer()
                {
                    ProxyIDs = new NovaList<DataStoreID>(4, Allocator.Persistent),
                };
            }

            NovaList<DataStoreID> virtualProxyIDs = proxyContainer.ProxyIDs;

            int proxySiblingIndex = math.select(virtualProxyIDs.Length, 0, firstPosition);
            virtualProxyIDs.Insert(proxySiblingIndex, virtualProxyID);

            proxyContainer.ProxyIDs = virtualProxyIDs;
            proxyContainer.DesiredChildrenPerProxy = desiredChildrenPerProxy;

            hierarchy.VirtualProxies[parentElement.ID] = proxyContainer;

            if (virtualProxyIDs.Length == 1)
            {
                ReparentChildren(ref parentElement, virtualProxyID, ref childCache, ref hierarchy);
            }
            else
            {
                int activeChildCount = AppendChildrenToCache(ref virtualProxyIDs, ref childCache, ref hierarchy);
                RedistributeChildren(ref proxyContainer, ref childCache, activeChildCount, ref hierarchy);
            }

            // was modified, so could be stale. Get latest
            parentElement = hierarchy.Elements[hierarchy.Lookup[parentElement.ID]];
            SetParentInternal(hierarchy.Lookup[virtualProxyID], ref parentElement, proxySiblingIndex, ref hierarchy);
        }

        /// <summary>
        /// Updates the index of a virtual proxy (virtual sub parent)
        /// </summary>
        /// <param name="virtualProxyID"></param>
        /// <param name="newSiblingIndex"></param>
        /// <param name="parentElement"></param>
        /// <param name="childCache"></param>
        /// <param name="hierarchy"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateVirtualProxyIndex(DataStoreID virtualProxyID, int newSiblingIndex, ref HierarchyElement parentElement, ref NativeList<DataStoreIndex> childCache, ref NativeHierarchy hierarchy)
        {
            if (!hierarchy.VirtualProxies.TryGetValue(parentElement.ID, out ProxyContainer proxyContainer))
            {
                Debug.LogError($"Virtual Proxy, {virtualProxyID}, not found in parent proxy container.");
                return;
            }

            NovaList<DataStoreID> virtualProxyIDs = proxyContainer.ProxyIDs;

            if (!virtualProxyIDs.TryGetIndexOf(virtualProxyID, out int currentIndex))
            {
                Debug.LogError($"Virtual Proxy, {virtualProxyID}, not found in parent proxy container.");
                return;
            }

            if (currentIndex == newSiblingIndex)
            {
                return;
            }

            // Append children before changing proxy index to maintain child order
            int activeChildCount = AppendChildrenToCache(ref virtualProxyIDs, ref childCache, ref hierarchy);

            virtualProxyIDs.RemoveAt(currentIndex);
            virtualProxyIDs.Insert(newSiblingIndex, virtualProxyID);

            proxyContainer.ProxyIDs = virtualProxyIDs;
            hierarchy.VirtualProxies[parentElement.ID] = proxyContainer;

            // Update in child list too
            NovaList<DataStoreIndex> childProxies = parentElement.Children;
            childProxies.RemoveAt(currentIndex);
            childProxies.Insert(newSiblingIndex, hierarchy.Lookup[virtualProxyID]);
            parentElement.Children = childProxies;

            hierarchy.Elements[hierarchy.Lookup[parentElement.ID]] = parentElement;

            // Redistribute grandchildren
            RedistributeChildren(ref proxyContainer, ref childCache, activeChildCount, ref hierarchy);
        }

        /// <summary>
        /// Removes an element from being a hierarchical proxy (virtual sub parent) of the given parent element
        /// </summary>
        /// <param name="virtualProxyID"></param>
        /// <param name="parentElement"></param>
        /// <param name="childCache"></param>
        /// <param name="hierarchy"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveVirtualProxy(DataStoreID virtualProxyID, ref HierarchyElement parentElement, ref NativeList<DataStoreIndex> childCache, ref NativeHierarchy hierarchy)
        {
            if (!hierarchy.VirtualProxies.TryGetValue(parentElement.ID, out ProxyContainer proxyContainer))
            {
                Debug.LogError("Parent not tracking any virtual proxy elements.");
                return;
            }

            NovaList<DataStoreID> virtualProxyIDs = proxyContainer.ProxyIDs;

            if (!virtualProxyIDs.TryGetIndexOf(virtualProxyID, out int proxyIndexToRemove))
            {
                Debug.LogError("Element is not a proxy to Parent element");
                return;
            }

            Unparent(virtualProxyID, ref hierarchy);

            DataStoreIndex proxyIndex = hierarchy.Lookup[virtualProxyID];
            HierarchyElement proxyElement = hierarchy.Elements[proxyIndex];

            if (virtualProxyIDs.Length > 1)
            {
                // append before removing proxy to preserve order
                int activeChildCount = AppendChildrenToCache(ref virtualProxyIDs, ref childCache, ref hierarchy);
                virtualProxyIDs.RemoveAt(proxyIndexToRemove);
                proxyContainer.ProxyIDs = virtualProxyIDs;

                RedistributeChildren(ref proxyContainer, ref childCache, activeChildCount, ref hierarchy);

                hierarchy.VirtualProxies[parentElement.ID] = proxyContainer;
            }
            else
            {
                virtualProxyIDs.RemoveAt(0);
                virtualProxyIDs.Dispose();
                hierarchy.VirtualProxies.Remove(parentElement.ID);

                ReparentChildren(ref proxyElement, parentElement.ID, ref childCache, ref hierarchy);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetChildrenInOrder(DataStoreID parentID, DataStoreIndex parentIndex, ref NativeList<DataStoreIndex> newChildOrder, ref NativeHierarchy hierarchy)
        {
            // if the parent has hierarchical proxies, the children
            // need to be redistributed among them in the new order
            if (hierarchy.VirtualProxies.TryGetValue(parentID, out ProxyContainer proxyContainer))
            {
                RedistributeChildren(ref proxyContainer, ref newChildOrder, GetChildCount(ref proxyContainer.ProxyIDs, ref hierarchy), ref hierarchy);
            }
            else // otherwise just copy the new list
            {
                HierarchyElement parentElement = hierarchy.Elements[parentIndex];
                if (parentElement.ChildCount != newChildOrder.Length) // maybe can remove this...?
                {
                    Debug.LogError($"Called UpdateChildOrder with {newChildOrder.Length} children while hierarchy is tracking {parentElement.ChildCount}.");
                    return;
                }

                NovaList<DataStoreIndex> children = parentElement.Children;
                children.Clear();

                unsafe
                {
                    children.AddRangeNoResize(newChildOrder.GetRawReadonlyPtr(), newChildOrder.Length);
                }

                parentElement.Children = children;

                hierarchy.Elements[parentIndex] = parentElement;

                hierarchy.BatchGroupTracker.MarkContainingBatchDirty(parentIndex);
            }
        }

        /// <summary>
        /// Populates the cache with children under the given parent. If the parent has proxy
        /// elements (virtual nodes), the list will be populated with all of their children instead.
        /// Returns the number of children in the list which are active.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetChildren(DataStoreID parentID, ref NativeList<DataStoreIndex> childCache, ref NativeHierarchy hierarchy)
        {
            childCache.Clear();

            if (!hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
            {
                return 0;
            }

            int activeChildCount = 0;
            if (hierarchy.VirtualProxies.TryGetValue(parentID, out ProxyContainer proxyContainer))
            {
                activeChildCount = AppendChildrenToCache(ref proxyContainer.ProxyIDs, ref childCache, ref hierarchy);
            }
            else
            {
                HierarchyElement parent = hierarchy.Elements[parentIndex];

                int childCount = parent.ChildCount;
                if (childCount > 0)
                {
                    unsafe
                    {
                        childCache.AddRange(parent.Children.Ptr, childCount);
                    }
                }

                activeChildCount = childCount;
            }

            return activeChildCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetSiblingIndex(DataStoreID childID, DataStoreID parentID, bool lastSibling, int siblingPriority, ref NativeList<DataStoreIndex> childIndexCache, ref NativeHierarchy hierarchy)
        {
            if (!hierarchy.Lookup.TryGetValue(childID, out DataStoreIndex childIndex) ||
                !hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex))
            {
                return;
            }

            hierarchy.SiblingPriorities[childIndex] = siblingPriority;

            childIndexCache.Clear();
            GetChildren(parentID, ref childIndexCache, ref hierarchy);

            if (!childIndexCache.TryGetIndexOf(childIndex, out int siblingIndex))
            {
                // child not found
                return;
            }

            if ((lastSibling && siblingIndex == childIndexCache.Length - 1) ||
                (!lastSibling && siblingIndex == 0))
            {
                // nothing to updated
                return;
            }

            childIndexCache.RemoveAt(siblingIndex);
            if (lastSibling)
            {
                childIndexCache.Add(childIndex);
            }
            else
            {
                childIndexCache.Insert(0, childIndex);
            }

            SetChildrenInOrder(parentID, parentIndex, ref childIndexCache, ref hierarchy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromHierarchy(DataStoreID idToRemove, ref NativeHierarchy hierarchy)
        {
            if (!hierarchy.Lookup.TryGetValue(idToRemove, out DataStoreIndex indexToRemove))
            {
                return;
            }

            HierarchyElement toRemove = hierarchy.Elements[indexToRemove];
            DataStoreID parentID = toRemove.ParentID;

            // Make sure parent doesn't point to us
            if (hierarchy.Lookup.ContainsKey(parentID))
            {
                Unparent(idToRemove, ref hierarchy);
            }

            hierarchy.Lookup.Remove(idToRemove);
            hierarchy.Elements.RemoveAtSwapBack(indexToRemove);
            hierarchy.SiblingPriorities.RemoveAtSwapBack(indexToRemove);
            hierarchy.BatchGroupTracker.RemoveBatchRoot(idToRemove, DataStoreIndex.Invalid);
            hierarchy.BatchGroupTracker.RemoveAtSwapBack(indexToRemove);

            if (indexToRemove < hierarchy.Elements.Length) // otherwise removed last
            {
                HierarchyElement elementSwappedFromBack = hierarchy.Elements[indexToRemove];
                hierarchy.Lookup[elementSwappedFromBack.ID] = indexToRemove;

                if (hierarchy.Lookup.TryGetValue(elementSwappedFromBack.ParentID, out DataStoreIndex parentOfSwappedElementIndex))
                {
                    HierarchyElement parentOfSwappedElement = hierarchy.Elements[parentOfSwappedElementIndex];
                    NovaList<DataStoreIndex> swappedElementSiblings = parentOfSwappedElement.Children;
                    if (swappedElementSiblings.TryGetIndexOf(hierarchy.Elements.Length, out int siblingIndex))
                    {
                        swappedElementSiblings[siblingIndex] = indexToRemove;
                        parentOfSwappedElement.Children = swappedElementSiblings;
                        hierarchy.Elements[parentOfSwappedElementIndex] = parentOfSwappedElement;
                    }
                }
            }

            hierarchy.ReturnIndexList(ref toRemove.Children);

            // Since we're returning the list of children to
            // the pool, dispose doesn't really need to be called,
            // but it's just called here for the sake of good patterns,
            // since it does implement the disposable interface
            toRemove.Children = default;
            toRemove.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveBatchRoot(DataStoreID rootIDToRemove, ref NativeHierarchy hierarchy)
        {
            DataStoreID parentID = hierarchy.GetDirectParentID(rootIDToRemove);
            hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex);
            hierarchy.BatchGroupTracker.RemoveBatchRoot(rootIDToRemove, parentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddBatchRoot(DataStoreID rootIDToAdd, ref NativeHierarchy hierarchy)
        {
            if (!hierarchy.Lookup.TryGetValue(rootIDToAdd, out DataStoreIndex rootIndex))
            {
                return;
            }

            HierarchyElement root = hierarchy.Elements[rootIndex];
            DataStoreID parentID = root.ParentID;
            hierarchy.Lookup.TryGetValue(parentID, out DataStoreIndex parentIndex);
            hierarchy.BatchGroupTracker.AddBatchRoot(rootIDToAdd, parentIndex);
        }
    }
}
