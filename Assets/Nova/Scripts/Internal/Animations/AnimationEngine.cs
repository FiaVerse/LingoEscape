// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nova.Internal.Animations
{
    internal class AnimationEngine : EngineBaseGeneric<AnimationEngine>
    {
        public interface IAnimationWithEventsWrapper : IAnimationWrapper
        {
            void Start(AnimationID animationID, int currentIteration);
            void End(AnimationID animationID);
            void Complete(AnimationID animationID);
            void Paused(AnimationID animationID);
            void Resumed(AnimationID animationID);
            void Canceled(AnimationID animationID);
        }

        public interface IAnimationWrapper
        {
            void UpdateAnimation(AnimationID animationID, float percentDone);

            void Remove(AnimationID animationID);
        }

        private struct AnimationInfo
        {
            public AnimationID ID;
            public float StartTime;
            public float UpdatedAt;
            public float Duration;
            public int RemainingIterations;
            public int CurrentIteration;
        }

        private float CurrentTime;
        private const float NotStartedTime = -1;

        private class AnimationWrapperCollection : Dictionary<AnimationID, IAnimationWrapper>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public new bool Remove(AnimationID id)
            {
                if (!TryGetValue(id, out IAnimationWrapper wrapper))
                {
                    return false;
                }

                wrapper.Remove(id);
                return base.Remove(id);
            }
        }

        private Queue<HashList<AnimationID>> idListPool = new Queue<HashList<AnimationID>>();

        private List<AnimationID> completedAnimations = new List<AnimationID>();
        private HashList<AnimationID> runningAnimations = new HashList<AnimationID>();
        private Dictionary<AnimationID, AnimationInfo> animations = new Dictionary<AnimationID, AnimationInfo>();
        private AnimationWrapperCollection animationUpdaters = new AnimationWrapperCollection();

        private Dictionary<AnimationID, AnimationID> originToGroup = new Dictionary<AnimationID, AnimationID>();
        private Dictionary<AnimationID, AnimationID> groupToOrigin = new Dictionary<AnimationID, AnimationID>();
        private Dictionary<AnimationID, AnimationID> childToGroup = new Dictionary<AnimationID, AnimationID>();
        private Dictionary<AnimationID, AnimationID> parentToGroup = new Dictionary<AnimationID, AnimationID>();
        private Dictionary<AnimationID, AnimationID> groupToParent = new Dictionary<AnimationID, AnimationID>();
        private Dictionary<AnimationID, HashList<AnimationID>> groupToChildren = new Dictionary<AnimationID, HashList<AnimationID>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsComplete(AnimationID animationID)
        {
            if (!animationID.IsGroupID && !animations.ContainsKey(animationID) && !originToGroup.ContainsKey(animationID))
            {
                return true;
            }

            if (animationID.IsGroupID && !groupToChildren.ContainsKey(animationID))
            {
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AnimationID Run<T>(ref T animation, float duration, int iterations, IAnimationWrapper wrapper) where T : struct
        {
            AnimationID animationID = Add(ref animation, duration, iterations, wrapper);
            runningAnimations.Add(animationID);
            return animationID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AnimationID RunAfter<T>(AnimationID parentID, ref T animation, float duration, int iterations, IAnimationWrapper wrapper) where T : struct
        {
            if (IsComplete(parentID))
            {
                return Run(ref animation, duration, iterations, wrapper);
            }

            AnimationID animationID = Add(ref animation, duration, iterations, wrapper);

            bool parentHasGroup = parentToGroup.TryGetValue(parentID, out AnimationID groupID);
            HashList<AnimationID> children = parentHasGroup ? groupToChildren[groupID] : null;

            if (!parentHasGroup)
            {
                groupID = AnimationID.CreateGroupID();

                parentToGroup.Add(parentID, groupID);
                groupToParent.Add(groupID, parentID);

                children = GetList();

                groupToChildren.Add(groupID, children);
            }

            // add reference from newly added child to group
            childToGroup.Add(animationID, groupID);

            // add child to sibling group
            children.Add(animationID);
            return animationID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AnimationID RunTogether<T>(AnimationID siblingID, ref T animation, float duration, int iterations, IAnimationWrapper wrapper) where T : struct
        {
            // sibling not found, no need to add to group
            if (!siblingID.IsGroupID && !animations.ContainsKey(siblingID))
            {
                return Run(ref animation, duration, iterations, wrapper);
            }

            return RunInGroup(siblingID, ref animation, duration, iterations, wrapper);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AnimationID RunTogether<T>(AnimationID sibling, ref T animation, IAnimationWrapper wrapper) where T : struct
        {
            AnimationInfo siblingAnimation;

            if (sibling.IsGroupID && groupToChildren.TryGetValue(sibling, out HashList<AnimationID> children))
            {
                siblingAnimation = animations[children[children.Count - 1]];
            }
            else if (!animations.TryGetValue(sibling, out siblingAnimation))
            {
                // sibling not found and not enough information to run on its own
                return default;
            }

            return RunInGroup(sibling, ref animation, siblingAnimation.Duration, siblingAnimation.RemainingIterations, wrapper);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AnimationID RunInGroup<T>(AnimationID siblingID, ref T animation, float duration, int iterations, IAnimationWrapper wrapper) where T : struct
        {
            AnimationID groupID = siblingID;

            if (!siblingID.IsGroupID && !childToGroup.TryGetValue(siblingID, out groupID))
            {
                groupID = AnimationID.CreateGroupID();

                HashList<AnimationID> children = GetList();

                children.Add(siblingID);
                childToGroup.Add(siblingID, groupID);
                groupToChildren.Add(groupID, children);
                groupToOrigin.Add(groupID, siblingID);
                originToGroup.Add(siblingID, groupID);
            }

            return RunInGroupInternal(groupID, ref animation, duration, iterations, wrapper);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AnimationID RunInGroupInternal<T>(AnimationID groupID, ref T animation, float duration, int iterations, IAnimationWrapper wrapper) where T : struct
        {
            if (!groupToChildren.TryGetValue(groupID, out HashList<AnimationID> children))
            {
                return Run(ref animation, duration, iterations, wrapper);
            }

            AnimationID childID = Add(ref animation, duration, iterations, wrapper);

            children.Add(childID);
            childToGroup.Add(childID, groupID);

            if (!groupToParent.ContainsKey(groupID)) // not waiting on a parent animation to complete
            {
                runningAnimations.Add(childID);
            }

            return groupID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pause(AnimationID handle)
        {
            AnimationID idToPause = GetTopLevelDependency(handle);

            if (!idToPause.IsGroupID && animations.ContainsKey(idToPause))
            {
                runningAnimations.Remove(idToPause);

                if (animationUpdaters.TryGetValue(idToPause, out IAnimationWrapper update) &&
                    update is IAnimationWithEventsWrapper stateful)
                {
                    stateful.Paused(idToPause);
                }

                return;
            }

            if (idToPause.IsGroupID && groupToChildren.TryGetValue(idToPause, out HashList<AnimationID> children))
            {
                int childCount = children.Count;
                for (int i = 0; i < childCount; ++i)
                {
                    AnimationID childID = children[i];
                    runningAnimations.Remove(childID);

                    if (animationUpdaters.TryGetValue(childID, out IAnimationWrapper update) &&
                        update is IAnimationWithEventsWrapper stateful)
                    {
                        stateful.Paused(childID);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resume(AnimationID handle)
        {
            AnimationID idToPause = GetTopLevelDependency(handle);

            if (!idToPause.IsGroupID && animations.TryGetValue(idToPause, out AnimationInfo animation))
            {
                runningAnimations.Add(idToPause);

                if (animation.StartTime != NotStartedTime)
                {
                    animation.StartTime = CurrentTime - (animation.UpdatedAt - animation.StartTime);
                    animations[idToPause] = animation;
                }

                if (animationUpdaters.TryGetValue(animation.ID, out IAnimationWrapper update) &&
                    update is IAnimationWithEventsWrapper stateful)
                {
                    stateful.Resumed(animation.ID);
                }

                return;
            }

            if (idToPause.IsGroupID && groupToChildren.TryGetValue(idToPause, out HashList<AnimationID> children))
            {
                for (int i = 0; i < children.Count; ++i)
                {
                    AnimationID childID = children[i];

                    if (!animations.TryGetValue(childID, out AnimationInfo childAnimation))
                    {
                        // We shouldn't hit this
                        UnityEngine.Debug.LogError("Child animation not found. Unable to resume");
                        continue;
                    }

                    runningAnimations.Add(childID);

                    if (childAnimation.StartTime != NotStartedTime)
                    {
                        childAnimation.StartTime = CurrentTime - (childAnimation.UpdatedAt - childAnimation.StartTime);
                        animations[childID] = childAnimation;
                    }

                    if (animationUpdaters.TryGetValue(childAnimation.ID, out IAnimationWrapper update) &&
                        update is IAnimationWithEventsWrapper stateful)
                    {
                        stateful.Resumed(childAnimation.ID);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Cancel(AnimationID animationID, bool cancelDependencies)
        {
            if (IsComplete(animationID))
            {
                // not found, nothing to cancel
                return;
            }

            CancelInternal(cancelDependencies ? GetTopLevelDependency(animationID) : animationID, completeBeforeCancel: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete(AnimationID handle)
        {
            if (IsComplete(handle))
            {
                // not found, nothing to complete
                return;
            }

            CancelInternal(GetTopLevelDependency(handle), completeBeforeCancel: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CancelInternal(AnimationID animationID, bool completeBeforeCancel)
        {
            HashList<AnimationID> children = null;

            if (childToGroup.TryGetValue(animationID, out AnimationID groupID))
            {
                _ = groupToChildren.TryGetValue(groupID, out children);
            }

            Cancel(animationID, groupID, children, completeBeforeCancel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AnimationID GetTopLevelDependency(AnimationID animationID)
        {
            while (childToGroup.TryGetValue(animationID, out AnimationID parentID) ||
                  (animationID.IsGroupID && groupToParent.TryGetValue(animationID, out parentID)) ||
                  originToGroup.TryGetValue(animationID, out parentID))
            {
                animationID = parentID;
            }

            return animationID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AnimationID Add<T>(ref T animation, float duration, int iterations, IAnimationWrapper wrapper) where T : struct
        {
            UID<AnimationID> animationID = StructHandle<AnimationID>.Storage<T>.Add(animation);

            AnimationInfo anim = new AnimationInfo()
            {
                ID = AnimationID.ForIndividual(animationID),
                StartTime = NotStartedTime,
                UpdatedAt = NotStartedTime,
                Duration = duration,
                RemainingIterations = iterations,
                CurrentIteration = 0,
            };

            animationUpdaters[anim.ID] = wrapper;
            animations[anim.ID] = anim;

            return anim.ID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Cancel(AnimationID animationID, AnimationID groupID, HashList<AnimationID> siblings, bool complete)
        {
            if (animationID.IsGroupID)
            {
                CancelGroup(animationID, complete);
            }
            else
            {
                if (complete)
                {
                    if (animations.TryGetValue(animationID, out AnimationInfo animation))
                    {
                        animation.RemainingIterations = 1;
                        animation.StartTime = CurrentTime - animation.Duration;

                        Step(ref animation);

                        animations.Remove(animationID);
                    }
                }
                else
                {
                    if (animationUpdaters.TryGetValue(animationID, out IAnimationWrapper update) &&
                        update is IAnimationWithEventsWrapper stateful)
                    {
                        stateful.Canceled(animationID);
                    }
                }

                animations.Remove(animationID);
                runningAnimations.Remove(animationID);
                animationUpdaters.Remove(animationID);
            }

            if (groupID.ID.IsValid && siblings != null)
            {
                siblings.Remove(animationID);
                childToGroup.Remove(animationID);

                if (siblings.Count == 0)
                {
                    if (groupToParent.TryGetValue(groupID, out AnimationID groupParentID))
                    {
                        groupToParent.Remove(groupID);
                        parentToGroup.Remove(groupParentID);
                    }

                    // Might not be there if it was scheduled as a chain animation. This is expected.
                    if (groupToOrigin.TryGetValue(groupID, out AnimationID origin))
                    {
                        groupToOrigin.Remove(groupID);
                        originToGroup.Remove(origin);
                    }

                    groupToChildren.Remove(groupID);
                    ReturnList(siblings);
                }
            }

            if (parentToGroup.TryGetValue(animationID, out AnimationID childGroupID))
            {
                CancelInternal(childGroupID, complete);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CancelGroup(AnimationID groupID, bool complete)
        {
            if (!groupToChildren.TryGetValue(groupID, out HashList<AnimationID> children))
            {
                // Need to do a try here to handle order-of-opts when exiting play mode
                return;
            }

            while (children.Count > 0)
            {
                AnimationID childID = children[children.Count - 1];
                Cancel(childID, groupID, children, complete);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            Step();
            CleanUp();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Step()
        {
            CurrentTime = UnityEngine.Time.unscaledTime;
            int running = runningAnimations.Count;

            if (running == 0)
            {
                return;
            }
            
            PooledList<AnimationID> runningAnimationsWhenStarted = ListPool<AnimationID>.Get();

            // Make a copy to handle the case where the set of running animations
            // could be modified while we're iterating over this collection
            runningAnimations.CopyTo(runningAnimationsWhenStarted);

            for (int i = 0; i < running; ++i)
            {
                AnimationID animationID = runningAnimationsWhenStarted[i];

                if (!animations.TryGetValue(animationID, out AnimationInfo animation))
                {
                    // means the animation was removed while
                    // updating another running animation
                    continue;
                }

                Step(ref animation);
            }

            ListPool<AnimationID>.Release(runningAnimationsWhenStarted);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Step(ref AnimationInfo animation)
        {
            IAnimationWrapper update = animationUpdaters[animation.ID];
            IAnimationWithEventsWrapper statefulUpdate = update as IAnimationWithEventsWrapper;

            if (animation.StartTime == NotStartedTime)
            {
                animation.StartTime = CurrentTime;

                if (statefulUpdate != null)
                {
                    statefulUpdate.Start(animation.ID, animation.CurrentIteration);
                }
            }

            animation.UpdatedAt = CurrentTime;
            animations[animation.ID] = animation;

            float percentDone = animation.Duration == 0 ? 1 : UnityEngine.Mathf.Clamp01((CurrentTime - animation.StartTime) / animation.Duration);

            // snap to 0 or 1 when close enough, otherwise
            // we'll hit some floating point arithmetic precision
            // limitations, which leads to visual artifacts when lerping
            if (Math.ApproximatelyZero(percentDone))
            {
                percentDone = 0;
            }
            else if (Math.ApproximatelyEqual(percentDone, 1))
            {
                percentDone = 1;
            }

            update.UpdateAnimation(animation.ID, percentDone);

            if (percentDone == 1)
            {
                if (statefulUpdate != null)
                {
                    statefulUpdate.End(animation.ID);
                }

                if (animation.RemainingIterations == 1)
                {
                    if (statefulUpdate != null)
                    {
                        statefulUpdate.Complete(animation.ID);
                    }

                    completedAnimations.Add(animation.ID);
                }
                else if (animation.RemainingIterations > 1)
                {
                    animation.RemainingIterations--;
                    animation.CurrentIteration++;
                    animation.StartTime = NotStartedTime;
                    animations[animation.ID] = animation;
                }
                else // infinite iteration count, just reset timer 
                {
                    animation.StartTime = NotStartedTime;
                    animation.CurrentIteration++;
                    animations[animation.ID] = animation;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CleanUp()
        {
            int completed = completedAnimations.Count;

            if (completed == 0)
            {
                return;
            }

            for (int i = 0; i < completed; ++i)
            {
                AnimationID animationID = completedAnimations[i];
                runningAnimations.Remove(animationID);
                animationUpdaters.Remove(animationID);
                animations.Remove(animationID);

                if (childToGroup.TryGetValue(animationID, out AnimationID parentGroupID) &&
                    groupToChildren.TryGetValue(parentGroupID, out HashList<AnimationID> siblings))
                {
                    siblings.Remove(animationID);
                    childToGroup.Remove(animationID);

                    if (siblings.Count == 0)
                    {
                        EnqueueChildren(parentGroupID);
                        groupToChildren.Remove(parentGroupID);

                        // Might not be there if it was scheduled as a chain animation. This is expected.
                        if (groupToOrigin.TryGetValue(parentGroupID, out AnimationID origin))
                        {
                            groupToOrigin.Remove(parentGroupID);
                            originToGroup.Remove(origin);
                        }

                        ReturnList(siblings);
                    }
                }

                EnqueueChildren(animationID);
            }

            completedAnimations.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnqueueChildren(AnimationID parentID)
        {
            if (!parentToGroup.TryGetValue(parentID, out AnimationID groupID))
            {
                return;
            }

            parentToGroup.Remove(parentID);
            groupToParent.Remove(groupID);

            HashList<AnimationID> childAnimations = groupToChildren[groupID];

            // queue all child animations/animation groups
            for (int j = 0; j < childAnimations.Count; ++j)
            {
                AnimationID childAnimationID = childAnimations[j];

                if (childAnimationID.IsGroupID && groupToChildren.TryGetValue(childAnimationID, out HashList<AnimationID> group))
                {
                    runningAnimations.AddRange(group);
                }
                else
                {
                    runningAnimations.Add(childAnimationID);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HashList<AnimationID> GetList()
        {
            return idListPool.Count > 0 ? idListPool.Dequeue() : new HashList<AnimationID>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReturnList(HashList<AnimationID> list)
        {
            list.Clear();

            idListPool.Enqueue(list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Init()
        {
            // Nothing to do
            Instance = this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Dispose()
        {
            idListPool.Clear();

            completedAnimations.Clear();
            runningAnimations.Clear();
            animations.Clear();
            animationUpdaters.Clear();

            childToGroup.Clear();
            parentToGroup.Clear();
            originToGroup.Clear();
            groupToParent.Clear();
            groupToChildren.Clear();
            groupToOrigin.Clear();
        }
    }
}
