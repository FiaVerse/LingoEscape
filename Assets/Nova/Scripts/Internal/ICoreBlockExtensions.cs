// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Utilities.Extensions;

namespace Nova.Internal
{
    internal static class ICoreBlockExtensions
    {
        /// <summary>
        /// Iterates through transform children, grabs their ICoreBlock component, and inserts that child into
        /// the parent's internal list. Doesn't assign child.Parent since that could result in a recursive 
        /// child-parent initialization.
        /// </summary>
        /// <param name="parentBlock"></param>
        private static void EnsureChildrenUpToDate(ICoreBlock parentBlock, PooledList<int> childPriorities = null)
        {
            if (!parentBlock.ChildrenAreDirty)
            {
                return;
            }

            parentBlock.ChildIDs.Clear();
            parentBlock.Children.Clear();

            if (!parentBlock.IsVirtual)
            {
                for (int i = 0; i < parentBlock.Transform.childCount; ++i)
                {
                    UnityEngine.Transform child = parentBlock.Transform.GetChild(i);
                    if (child.TryGetComponent(out ICoreBlock childBlock) && childBlock.ShouldBeRegistered())
                    {
                        if (childPriorities != null)
                        {
                            childPriorities.Add(childBlock.SiblingPriority);
                        }

                        parentBlock.ChildIDs.Add(childBlock.UniqueID);
                        parentBlock.Children.Add(childBlock);
                    }
                }
            }

            parentBlock.ChildrenAreDirty = false;
            parentBlock.ChildHandledHierarchyChangeForParent = false;
        }

        public static bool ShouldBeRegistered(this ICoreBlock coreBlock)
        {
            return coreBlock.Activated;
        }

        public static void EnsureRegistration(this ICoreBlock coreBlock)
        {
            if (coreBlock.IsRegistered == coreBlock.ShouldBeRegistered())
            {
                return;
            }

            coreBlock.Unregister();
            coreBlock.RegisterWithHierarchy();
        }

        public static void SetAsFirstSibling(this ICoreBlock coreBlock)
        {
            ICoreBlock parent = coreBlock.Parent as ICoreBlock;

            if (parent == null)
            {
                return;
            }

            DataStoreID childID = coreBlock.UniqueID;
            int currentIndex = parent.ChildIDs.IndexOf(childID);

            if (currentIndex == -1)
            {
                UnityEngine.Debug.LogError("Child Node not found in Parent list");
                return;
            }

            parent.ChildIDs.RemoveAt(currentIndex);
            parent.ChildIDs.Insert(0, childID);

            parent.Children.RemoveAt(currentIndex);
            parent.Children.Insert(0, coreBlock);

            HierarchyDataStore.Instance.SetAsFirstSibling(parent.UniqueID, childID);

            parent.ChildHandledHierarchyChangeForParent = true;
        }

        public static void SetAsLastSibling(this ICoreBlock coreBlock, int newSiblingPriority)
        {
            ICoreBlock parent = coreBlock.Parent;
            if (parent == null)
            {
                return;
            }

            DataStoreID childID = coreBlock.UniqueID;
            int currentIndex = parent.ChildIDs.IndexOf(childID);

            if (currentIndex == -1)
            {
                UnityEngine.Debug.LogError("Child Node not found in Parent list");
                return;
            }

            parent.ChildIDs.RemoveAt(currentIndex);
            parent.ChildIDs.Add(childID);

            parent.Children.RemoveAt(currentIndex);
            parent.Children.Add(coreBlock);

            HierarchyDataStore.Instance.SetAsLastSibling(parent.UniqueID, childID, newSiblingPriority);

            parent.ChildHandledHierarchyChangeForParent = true;
        }

        public static void RefreshChildren(this ICoreBlock coreBlock)
        {
            if (!coreBlock.ShouldBeRegistered())
            {
                return;
            }

            coreBlock.ChildrenAreDirty = !coreBlock.ChildHandledHierarchyChangeForParent;

            if (coreBlock.ChildrenAreDirty)
            {
                PooledList<int> childPriorities = ListPool<int>.Get();
                EnsureChildrenUpToDate(coreBlock, childPriorities);

                if (coreBlock.ChildIDs.Count > 0)
                {
                    HierarchyDataStore.Instance.UpdateChildOrder(coreBlock.UniqueID, coreBlock.ChildIDs.ToReadOnly(), childPriorities.ToReadOnly());
                }

                ListPool<int>.Release(childPriorities);
            }

            // Reset this flag in case other hierarchy changes occur
            coreBlock.ChildHandledHierarchyChangeForParent = false;

            if (coreBlock.ChildIDs.Count == 0 && !coreBlock.ShouldBeRegistered())
            {
                coreBlock.Unregister();
            }
        }

        public static void UnregisterFromParent(this ICoreBlock coreBlock)
        {
            if (!coreBlock.IsRegistered)
            {
                return;
            }

            ICoreBlock parent = coreBlock.Parent;

            int currentIndex = parent.ChildIDs.IndexOf(coreBlock.UniqueID);

            if (currentIndex != -1)
            {
                parent.ChildIDs.RemoveAt(currentIndex);
                parent.Children.RemoveAt(currentIndex);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"{coreBlock.Name} not registered with parent {parent.Name}");
            }

            parent.ChildHandledHierarchyChangeForParent = true;

            HierarchyDataStore.Instance.UnregisterFromParent(coreBlock);
        }

        public static void EnsureRegisteredWithParent(this ICoreBlock coreBlock)
        {
            ICoreBlock parent = coreBlock.Parent;

            if (parent == null || !coreBlock.ShouldBeRegistered())
            {
                return;
            }


            bool wasRegistered = coreBlock.IsRegistered;
            if (!wasRegistered)
            {
                coreBlock.RegisterWithHierarchy();

                if (!coreBlock.IsRegistered)
                {
                    UnityEngine.Debug.LogWarning($"{coreBlock.Name} didn't register. ShouldBeRegistered == {coreBlock.ShouldBeRegistered()}");
                }
            }

            bool parentWasRegistered = parent.IsRegistered;
            if (!parentWasRegistered)
            {
                parent.RegisterWithHierarchy();

                if (!parent.IsRegistered)
                {
                    UnityEngine.Debug.LogWarning($"Parent {parent.Name} didn't register. Aborting.");
                    return;
                }
            }

            // Make sure this child is inserted at the right index within the
            // parent's local list of children -- admittedly a bit funky to have
            // the child modifying the parent's list directly, but leaving for now.

            int siblingIndex = wasRegistered && parentWasRegistered ? HierarchyDataStore.Instance.RegisterWithParent(coreBlock, parent) : HierarchyDataStore.Instance.GetSiblingIndex(coreBlock.UniqueID);

            if (siblingIndex < 0 || siblingIndex > parent.ChildIDs.Count)
            {
                UnityEngine.Debug.LogError($"Sibling index [{siblingIndex}]. Expected [0, {parent.ChildIDs.Count})");
            }

            if (siblingIndex == parent.ChildIDs.Count || parent.ChildIDs[siblingIndex] != coreBlock.UniqueID)
            {
                parent.ChildIDs.Insert(siblingIndex, coreBlock.UniqueID);
                parent.Children.Insert(siblingIndex, coreBlock);
            }

            // Tell the parent "hey, you don't need to process all your children because
            // I knew I was dirtied and handled the child ordering adjustment for you"
            parent.ChildHandledHierarchyChangeForParent = true;
        }

        public static void UnregisterFromHierarchy(this ICoreBlock coreBlock)
        {
            coreBlock.Parent = null;

            for (int i = coreBlock.ChildIDs.Count - 1; i >= 0; --i)
            {
                coreBlock.Children[i].Parent = null;
            }

            coreBlock.Children.Clear();
            coreBlock.ChildIDs.Clear();

            coreBlock.Unregister();
        }

        public static void RegisterWithHierarchy(this ICoreBlock coreBlock)
        {
            if (!coreBlock.ShouldBeRegistered() || coreBlock.IsRegistered)
            {
                return;
            }

            if (coreBlock.Parent == null)
            {
                // We are a root
                coreBlock.Register();
            }
            else if (!coreBlock.Parent.IsRegistered)
            {
                // Make sure Parent knows about us. MonoBehaviour.OnTransformParentChanged
                // happens before MonoBehaviour.OnTransformChildrenChanged, so the parent flag might be
                // clean when we are trying to register, which means the parent doesn't know yet that 
                // its child list is actually dirty.
                coreBlock.Parent.ChildrenAreDirty = true;

                // Tell the parent to register. We don't have to do anything after this
                // because the parent will come back to us and tell us to register
                coreBlock.Parent.RegisterWithHierarchy();
                bool parentRegistered = coreBlock.Parent.IsRegistered;

                if (parentRegistered && !coreBlock.IsRegistered)
                {
                    UnityEngine.Debug.LogError($"Parent [{coreBlock.Parent.Name}] should have told us [{coreBlock.Name}] to register, but apparently that failed...? ShouldBeRegistered == {coreBlock.ShouldBeRegistered()}");
                    coreBlock.Register();
                }
            }
            else
            {
                // Parent has registered, so we may register now
                coreBlock.Register();
            }

            // Cache to save the state because we might be about to change it
            bool childHandledHierarchyChange = coreBlock.ChildHandledHierarchyChangeForParent;

            if (coreBlock.ChildrenAreDirty)
            {
                EnsureChildrenUpToDate(coreBlock);
            }

            // Now we need to go through the children and make sure
            // they are registered to us
            for (int i = 0; i < coreBlock.ChildIDs.Count; ++i)
            {
                coreBlock.Children[i].Parent = coreBlock;
            }

            // reset to previous value because we know the children we just processed
            // set it to true, and we might be in the middle of a call to process this.
            coreBlock.ChildHandledHierarchyChangeForParent = childHandledHierarchyChange;
        }

        /// <summary>
        /// Internal Parent getter, includes virtual parent blocks. The public <see cref="Parent"/> only points to parents in Unity's Transform hierarchy.
        /// </summary>
        public static IUIBlock GetParentBlock(this IUIBlock uiBlock) => HierarchyDataStore.Instance.GetHierarchyParent(uiBlock.UniqueID) as IUIBlock;
        public static NovaList<DataStoreIndex> GetChildIndices(this IUIBlock uiBlock) => HierarchyDataStore.Instance.GetChildIndices(uiBlock.Index);
        public static int GetChildIndex(this IUIBlock uiBlock, IUIBlock child)
        {
            if (child == null)
            {
                return -1;
            }

            NovaList<DataStoreIndex> children = uiBlock.GetChildIndices();

            if (children.TryGetIndexOf(child.Index, out int index))
            {
                return index;
            }

            return -1;
        }

        public static IUIBlock GetChildAtIndex(this IUIBlock uiBlock, int index)
        {
            if (index < 0)
            {
                return null;
            }

            NovaList<DataStoreIndex> children = uiBlock.GetChildIndices();

            if (index >= children.Length)
            {
                return null;
            }

            return HierarchyDataStore.Instance.Elements[HierarchyDataStore.Instance.IDToIndexMap.ToID(children[index])] as IUIBlock;
        }

        public static int GetChildCount(this IUIBlock uiBlock) => uiBlock.GetChildIndices().Length;

        public static bool IsDescendantOf(this IUIBlock uiBlock, IUIBlock ancestor) => ancestor == null ? false : HierarchyDataStore.Instance.IsDescendantOf(uiBlock.Index, ancestor.UniqueID, out _);

        public static IUIBlock GetDirectChild(this IUIBlock uiBlock, IUIBlock descendant)
        {
            if (descendant == null)
            {
                return null;
            }

            if (!HierarchyDataStore.Instance.IsDescendantOf(descendant.Index, uiBlock.UniqueID, out IHierarchyBlock directChild))
            {
                return null;
            }

            return directChild as IUIBlock;
        }
    }
}

