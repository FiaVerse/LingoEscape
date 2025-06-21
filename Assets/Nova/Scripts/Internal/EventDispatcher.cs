// Copyright (c) Supernova Technologies LLC
//#define PROFILE_EVENTS
using Nova.Internal.Collections;
using System;
using System.Collections.Generic;

namespace Nova.Events
{
    internal struct TargetedEvent<TEvent> where TEvent : IEvent
    {
        public TEvent Event;
        public EventDispatcher.Targets Targets;

        public TargetedEvent(ref TEvent evt, EventDispatcher.Targets targets)
        {
            Event = evt;
            Targets = targets;
        }
    }

    /// <summary>
    /// A mechanism to deliver generic events to both generic event handlers or event handlers filtered by Event Target types 
    /// </summary>
    internal class EventDispatcher
    {
        /// <summary>
        /// Tracks a list of Event Target Providers, which each provide an Event Target, an object whose type can act as an event filter
        /// </summary>
        internal class Targets
        {
            public int Count => targetProviders == null ? 0 : targetProviders.Count;

            private HashList<IEventTargetProvider> targetProviders = null;

            public void Add(IEventTargetProvider provider)
            {
                if (targetProviders == null)
                {
                    targetProviders = CollectionPool<HashList<IEventTargetProvider>, IEventTargetProvider>.Get();
                }

                targetProviders.Add(provider);
            }

            public void Remove(IEventTargetProvider provider)
            {
                targetProviders.Remove(provider);
            }

            public void Group(IEventTarget receiver, Type eventType, PooledDictionary<Type, PooledList<IEventTarget>> targetGroups, PooledList<Type> currentTargetTypes, HashList<Type> baseTypeBuckets)
            {
                for (int i = 0; i < targetProviders.Count; ++i)
                {
                    IEventTargetProvider targetProvider = targetProviders[i];

                    if (!targetProvider.TryGetTarget(receiver, eventType, out IEventTarget target) || target == null)
                    {
                        continue;
                    }

                    Type targetProvidedBaseType = targetProvider.BaseTargetableType;
                    Type targetType = target.GetType();

                    do
                    {
                        AddToGroup(targetGroups, currentTargetTypes, targetType, target);

                        if (targetType.Equals(targetProvidedBaseType) ||
                            baseTypeBuckets.Contains(targetType))
                        {
                            break;
                        }

                        targetType = targetType.BaseType;

                    } while (targetType != null && targetProvidedBaseType.IsAssignableFrom(targetType));
                }
            }

            private void AddToGroup(PooledDictionary<Type, PooledList<IEventTarget>> currentTargetGroup, PooledList<Type> currentTargetTypes, Type targetType, IEventTarget target)
            {
                if (!currentTargetGroup.TryGetValue(targetType, out PooledList<IEventTarget> typedTargets))
                {
                    typedTargets = ListPool<IEventTarget>.Get();
                    currentTargetGroup.Add(targetType, typedTargets);
                }

                if (typedTargets.Count == 0)
                {
                    currentTargetTypes.Add(targetType);
                }

                typedTargets.Add(target);
            }
        }

        private class Event<TTarget> : EventWrapper where TTarget : class, IEventTarget
        {
            public override void Fire<TEvent>(MulticastDelegate action, ref TEvent evt, IEventTarget target)
            {
                try
                {
                    switch (action)
                    {
                        case UIEventHandler<TEvent, TTarget> callback:
                            callback.Invoke(evt, (TTarget)target);
                            break;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        private abstract class EventWrapper
        {
            public abstract void Fire<TEvent>(MulticastDelegate callback, ref TEvent evt, IEventTarget target) where TEvent : struct, IEvent;
        }

        private static Dictionary<Type, EventWrapper> SharedEventWrappers = new Dictionary<Type, EventWrapper>();

        private static HashSet<int> ActiveEventIDs = new HashSet<int>();

        private const int InvalidEventID = 0;

        [NonSerialized]
        private static int EventID = 1;

        private Dictionary<UID<MulticastDelegate>, EventWrapper> eventHandlerToEventRouter = new Dictionary<UID<MulticastDelegate>, EventWrapper>();
        private Dictionary<Type, int> targetTypeCounts = new Dictionary<Type, int>();
        private Dictionary<Type, List<UID<MulticastDelegate>>> eventTypeToEventHandlerList = new Dictionary<Type, List<UID<MulticastDelegate>>>();
        private Dictionary<UID<MulticastDelegate>, Type> eventTargetTypes = new Dictionary<UID<MulticastDelegate>, Type>();
        private Dictionary<MulticastDelegate, UID<MulticastDelegate>> handlersToIDs = new Dictionary<MulticastDelegate, UID<MulticastDelegate>>();
        private Dictionary<UID<MulticastDelegate>, MulticastDelegate> idsToHandlers = new Dictionary<UID<MulticastDelegate>, MulticastDelegate>();

        public static int GetEventID()
        {
            return EventID++;
        }

        public static void Consume<TEvent>(ref TEvent evt) where TEvent : struct, IEvent
        {
            ActiveEventIDs.Remove(evt.ID);
        }

        public bool WillCaptureEvent<TEvent>(Type targetTypeFilter = null) where TEvent : struct, IEvent
        {
            if (!eventTypeToEventHandlerList.TryGetValue(typeof(TEvent), out List<UID<MulticastDelegate>> targetedEventWrappers) || targetedEventWrappers == null)
            {
                return false;
            }

            if (targetTypeFilter == null)
            {
                return targetedEventWrappers.Count > 0;
            }

            for (int i = 0; i < targetedEventWrappers.Count; ++i)
            {
                if (eventTargetTypes[targetedEventWrappers[i]].IsAssignableFrom(targetTypeFilter))
                {
                    return true;
                }
            }

            return false;
        }

        public void RegisterHandler<TEvent, TTarget>(UIEventHandler<TEvent, TTarget> eventHandler) where TEvent : struct, IEvent where TTarget : class, IEventTarget
        {
            Type eventType = typeof(TEvent);
            Type targetType = typeof(TTarget);

            if (targetType.IsInterface)
            {
                throw new ArgumentException($"Provided target type, {targetType.Name}, is an interface, but event target types must be a class type.");
            }

            if (!eventTypeToEventHandlerList.TryGetValue(eventType, out var eventHandlerList))
            {
                eventHandlerList = new List<UID<MulticastDelegate>>();
                eventTypeToEventHandlerList[eventType] = eventHandlerList;
            }

            if (!SharedEventWrappers.TryGetValue(targetType, out EventWrapper targetWrapper))
            {
                targetWrapper = new Event<TTarget>();
                SharedEventWrappers[targetType] = targetWrapper;
            }

            if (!targetTypeCounts.ContainsKey(targetType))
            {
                targetTypeCounts[targetType] = 0;
            }

            targetTypeCounts[targetType]++;

            UID<MulticastDelegate> handlerID = UID<MulticastDelegate>.Create();

            eventHandlerList.Add(handlerID);
            eventHandlerToEventRouter[handlerID] = targetWrapper;
            eventTargetTypes[handlerID] = targetType;
            handlersToIDs[eventHandler] = handlerID;
            idsToHandlers[handlerID] = eventHandler;
        }

        public void UnregisterHandler<TEvent, TTarget>(UIEventHandler<TEvent, TTarget> eventHandler) where TEvent : struct, IEvent where TTarget : class, IEventTarget
        {
            Type eventType = typeof(TEvent);
            Type targetType = typeof(TTarget);

            if (!handlersToIDs.TryGetValue(eventHandler, out UID<MulticastDelegate> handlerID))
            {
                // not tracking the event, nothing to unregister
                return;
            }

            if (!eventTypeToEventHandlerList.TryGetValue(eventType, out var eventHandlerList))
            {
                return;
            }

            bool removed = eventHandlerList.Remove(handlerID);
            eventHandlerToEventRouter.Remove(handlerID);
            eventTargetTypes.Remove(handlerID);
            handlersToIDs.Remove(eventHandler);
            idsToHandlers.Remove(handlerID);

            if (removed && targetTypeCounts.ContainsKey(targetType))
            {
                targetTypeCounts[targetType]--;

                if (targetTypeCounts[targetType] == 0)
                {
                    targetTypeCounts.Remove(targetType);
                }
            }
        }

        private struct Callback<TEvent> where TEvent : struct, IEvent
        {
            public EventWrapper Wrapper;
            public MulticastDelegate Handler;
            public TEvent Event;
            public IEventTarget Target;

            public void Invoke()
            {
                Wrapper.Fire(Handler, ref Event, Target);
            }

            public Callback(EventWrapper wrapper, MulticastDelegate handler, ref TEvent evt, IEventTarget target)
            {
                Wrapper = wrapper;
                Handler = handler;
                Event = evt;
                Target = target;
            }
        }

        public void SendToTargets<TEvent>(PooledList<TargetedEvent<TEvent>> events, out bool consumed) where TEvent : struct, IEvent
        {
            PooledList<Callback<TEvent>> callbackQueue = null;
            FilterTargets(events, out callbackQueue);
            InvokeAll(callbackQueue, out consumed);
        }

        private void FilterTargets<TEvent>(PooledList<TargetedEvent<TEvent>> events, out PooledList<Callback<TEvent>> callbackQueue) where TEvent : struct, IEvent
        {
            callbackQueue = null;

            HashList<Type> targetBaseTypes = CollectionPool<HashList<Type>, Type>.Get();
            if (!TryGetHandlers<TEvent>(ref targetBaseTypes, out List<UID<MulticastDelegate>> eventHandlerIDs))
            {
                CollectionPool<HashList<Type>, Type>.Release(targetBaseTypes);
                return;
            }

            Type eventType = typeof(TEvent);

            PooledList<IEventTarget> targets = ListPool<IEventTarget>.Get();
            callbackQueue = ListPool<Callback<TEvent>>.Get();

            for (int i = 0; i < events.Count; ++i)
            {
                TargetedEvent<TEvent> evt = events[i];
                TEvent eventData = evt.Event;
                Targets eventTargets = evt.Targets;

                // Events can trigger other events to fire, so we need to do some
                // basic validation that the objects tied to the event are still alive
                if (eventData.Target == null || eventData.Receiver == null)
                {
                    continue;
                }

                PooledDictionary<Type, PooledList<IEventTarget>> targetsGroupedByType = DictionaryPool<Type, PooledList<IEventTarget>>.Get();
                PooledList<Type> groupTypes = ListPool<Type>.Get();

                eventTargets.Group(eventData.Receiver, eventType, targetsGroupedByType, groupTypes, targetBaseTypes);

                for (int j = 0; j < eventHandlerIDs.Count; ++j)
                {
                    int startIndex = targets.Count;

                    UID<MulticastDelegate> handlerID = eventHandlerIDs[j];

                    if (!TryAppend(targetsGroupedByType, eventTargetTypes[handlerID], ref targets))
                    {
                        continue;
                    }

                    EventWrapper targetWrapper = eventHandlerToEventRouter[handlerID];

                    for (int k = startIndex; k < targets.Count; ++k)
                    {
                        callbackQueue.Add(new Callback<TEvent>(targetWrapper, idsToHandlers[handlerID], ref eventData, targets[k]));
                    }
                }

                ReleaseAll(targetsGroupedByType, groupTypes);
            }

            ListPool<IEventTarget>.Release(targets);
            CollectionPool<HashList<Type>, Type>.Release(targetBaseTypes);
        }

        private void InvokeAll<TEvent>(PooledList<Callback<TEvent>> callbackQueue, out bool consumed) where TEvent : struct, IEvent
        {
            consumed = false;

            if (callbackQueue == null)
            {
                return;
            }

            int callbackCount = callbackQueue.Count;
            int eventID = callbackCount > 0 ? callbackQueue[0].Event.ID : InvalidEventID;

            if (eventID != InvalidEventID)
            {
                ActiveEventIDs.Add(eventID);
            }

            for (int i = 0; i < callbackCount; ++i)
            {
                callbackQueue[i].Invoke();
            }

            if (eventID != InvalidEventID)
            {
                // If it's not there, then evt.Consume() has been called.
                consumed = !ActiveEventIDs.Remove(eventID);
            }

            ListPool<Callback<TEvent>>.Release(callbackQueue);
        }

        private bool TryGetHandlers<TEvent>(ref HashList<Type> targetBaseTypes, out List<UID<MulticastDelegate>> handlerIDs)
        {
            Type eventType = typeof(TEvent);

            if (!eventTypeToEventHandlerList.TryGetValue(eventType, out handlerIDs))
            {
                return false;
            }

            int handlerCount = handlerIDs.Count;
            for (int i = 0; i < handlerCount; ++i)
            {
                targetBaseTypes.Add(eventTargetTypes[handlerIDs[i]]);
            }

            return handlerCount > 0;
        }

        private static bool TryAppend(Dictionary<Type, PooledList<IEventTarget>> targetsGroupsByType, Type eventTargetType, ref PooledList<IEventTarget> targets)
        {
            int targetCount = targets.Count;

            if (targetsGroupsByType.TryGetValue(eventTargetType, out PooledList<IEventTarget> readOnlyTargets))
            {
                targets.AddRange(readOnlyTargets);
                return targets.Count > targetCount;
            }

            return false;
        }

        private static void ReleaseAll(PooledDictionary<Type, PooledList<IEventTarget>> targetsGroupedByType, PooledList<Type> currentTargetTypes)
        {
            for (int i = 0; i < currentTargetTypes.Count; ++i)
            {
                ListPool<IEventTarget>.Release(targetsGroupedByType[currentTargetTypes[i]]);
            }

            DictionaryPool<Type, PooledList<IEventTarget>>.Release(targetsGroupedByType);
            ListPool<Type>.Release(currentTargetTypes);
        }
    }
}