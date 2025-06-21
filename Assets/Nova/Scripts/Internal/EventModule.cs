// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Events
{
    internal class EventModule : IEventTarget, IEventTargetProvider
    {
        public EventDispatcher Dispatcher => eventDispatcher;
        public EventDispatcher.Targets Targets => eventTargets;
        public int TargetCount => eventTargets == null ? 0 : eventTargets.Count;

        public Type BaseTargetableType => typeof(EventModule);

        private struct ScopedCallback : IEquatable<ScopedCallback>
        {
            public bool LocalOnly;
            public MulticastDelegate Callback;

            public bool Equals(ScopedCallback other)
            {
                bool equal = EqualityComparer<MulticastDelegate>.Default.Equals(Callback, other.Callback);

                return equal;
            }

            public override int GetHashCode()
            {
                return Callback == null ? 0 : Callback.GetHashCode();
            }
        }

        private struct Callbacks
        {
            public HashList<ScopedCallback> List;
            public int AllowIndirectCount;

            public bool CanBeIndirect => AllowIndirectCount > 0;
        }

        private int selfRegistrationCount = 0;
        private IEventTarget owner = null;
        private EventDispatcher eventDispatcher = null;
        private EventDispatcher.Targets eventTargets = null;

        private Dictionary<Type, Callbacks> redirectedEvents = null;

        private static readonly Dictionary<Type, MulticastDelegate> EventWrappers = new Dictionary<Type, MulticastDelegate>();

        public bool WillCaptureEvent<TEvent>() where TEvent : struct, IEvent => eventDispatcher == null ? false : eventDispatcher.WillCaptureEvent<TEvent>();

        public void RegisterHandler<TEvent>(UIEventHandler<TEvent> eventHandler, bool localOnly) where TEvent : struct, IEvent
        {
            if (eventDispatcher == null)
            {
                eventDispatcher = new EventDispatcher();
            }

            if (redirectedEvents == null)
            {
                redirectedEvents = new Dictionary<Type, Callbacks>();
            }

            Type eventType = typeof(TEvent);

            if (!redirectedEvents.TryGetValue(eventType, out Callbacks typedEvents))
            {
                typedEvents = new Callbacks() { List = CollectionPool<HashList<ScopedCallback>, ScopedCallback>.Get() };
                redirectedEvents[eventType] = typedEvents;
            }
            else
            {
                ScopedCallback updated = new ScopedCallback() { Callback = eventHandler, LocalOnly = localOnly };
                int index = typedEvents.List.IndexOf(updated);

                if (index >= 0)
                {
                    bool local = typedEvents.List[index].LocalOnly;
                    if (local == localOnly)
                    {
                        return;
                    }

                    // replace
                    if (localOnly)
                    {
                        typedEvents.AllowIndirectCount--;
                    }
                    else
                    {
                        typedEvents.AllowIndirectCount++;
                    }

                    typedEvents.List.RemoveAt(index);
                    typedEvents.List.Add(updated);

                    redirectedEvents[eventType] = typedEvents;

                    return;
                }
            }

            if (typedEvents.List.Count == 0)
            {
                if (!EventWrappers.TryGetValue(eventType, out MulticastDelegate eventWrapper))
                {
                    eventWrapper = (UIEventHandler<TEvent, EventModule>)HandOff;
                    EventWrappers.Add(eventType, eventWrapper);
                }

                eventDispatcher.RegisterHandler(eventWrapper as UIEventHandler<TEvent, EventModule>);
            }

            typedEvents.List.Add(new ScopedCallback() { Callback = eventHandler, LocalOnly = localOnly });

            if (!localOnly)
            {
                typedEvents.AllowIndirectCount++;
                redirectedEvents[eventType] = typedEvents;
            }
        }

        public void UnregisterHandler<TEvent>(UIEventHandler<TEvent> eventHandler) where TEvent : struct, IEvent
        {
            if (eventDispatcher == null || redirectedEvents == null)
            {
                return;
            }

            Type eventType = typeof(TEvent);

            if (!redirectedEvents.TryGetValue(eventType, out Callbacks typedEvents))
            {
                return;
            }

            int index = typedEvents.List.IndexOf(new ScopedCallback() { Callback = eventHandler, LocalOnly = false });

            if (index < 0)
            {
                return;
            }

            if (!typedEvents.List[index].LocalOnly)
            {
                typedEvents.AllowIndirectCount--;
            }

            typedEvents.List.RemoveAt(index);

            if (typedEvents.List.Count == 0)
            {
                redirectedEvents.Remove(eventType);

                CollectionPool<HashList<ScopedCallback>, ScopedCallback>.Release(typedEvents.List);

                if (!EventWrappers.TryGetValue(eventType, out MulticastDelegate eventWrapper))
                {
                    return;
                }

                eventDispatcher.UnregisterHandler(eventWrapper as UIEventHandler<TEvent, EventModule>);
            }
            else
            {
                redirectedEvents[eventType] = typedEvents;
            }
        }

        private static void HandOff<TEvent>(TEvent evt, EventModule eventModule) where TEvent : struct, IEvent
        {
            Type eventType = typeof(TEvent);

            if (eventModule.redirectedEvents == null || !eventModule.redirectedEvents.TryGetValue(eventType, out Callbacks typedEvents))
            {
                return;
            }

            HashList<ScopedCallback> callbacks = typedEvents.List;

            for (int i = 0; i < callbacks.Count; ++i)
            {
                try
                {
                    ScopedCallback callback = callbacks[i];

                    if (callback.Callback is UIEventHandler<TEvent> typedEvent && (!callback.LocalOnly || (evt.Receiver == eventModule.owner as MonoBehaviour)))
                    {
                        typedEvent.Invoke(evt);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void RegisterHandler<TEvent, TTarget>(UIEventHandler<TEvent, TTarget> eventHandler) where TEvent : struct, IEvent where TTarget : class, IEventTarget
        {
            if (eventDispatcher == null)
            {
                eventDispatcher = new EventDispatcher();
            }

            eventDispatcher.RegisterHandler(eventHandler);
        }

        public void UnregisterHandler<TEvent, TTarget>(UIEventHandler<TEvent, TTarget> eventHandler) where TEvent : struct, IEvent where TTarget : class, IEventTarget
        {
            if (eventDispatcher == null)
            {
                return;
            }

            eventDispatcher.UnregisterHandler(eventHandler);
        }

        public void RegisterEventTargetProvider(IEventTargetProvider targetProvider)
        {
            if (eventTargets == null)
            {
                eventTargets = new EventDispatcher.Targets();
            }

            eventTargets.Add(targetProvider);
        }

        public void UnregisterEventTargetProvider(IEventTargetProvider targetProvider)
        {
            if (eventTargets == null)
            {
                return;
            }

            eventTargets.Remove(targetProvider);
        }

        public static void Dispatch<TEvent>(UIBlock source, TEvent evt, Type targetTypeFilter = null) where TEvent : struct, IEvent
        {
            PooledList<TargetedEvent<TEvent>> events = ListPool<TargetedEvent<TEvent>>.Get();

            UIBlock uiBlock = source;

            evt.ID = EventDispatcher.GetEventID();

            do
            {
                EventModule module = uiBlock.EventModule;
                EventDispatcher dispatcher = module.Dispatcher;

                bool hasEventTargets = module.TargetCount > 0;
                bool willCaptureEvent = dispatcher != null && dispatcher.WillCaptureEvent<TEvent>(targetTypeFilter);

                if (hasEventTargets)
                {
                    events.Add(new TargetedEvent<TEvent>(ref evt, module.Targets));
                }

                if (willCaptureEvent)
                {
                    module.LockOwnerAsTarget();

                    if (!hasEventTargets) // we just added one by locking
                    {
                        // The event dispatcher wants to fire an event,
                        // but currently nothing has been identified as a 
                        // targetable object, so we assume the dispatcher
                        // object is the relevant target.
                        events.Add(new TargetedEvent<TEvent>(ref evt, module.Targets));
                    }

                    dispatcher.SendToTargets(events, out bool consumed);

                    module.ReleaseOwnerAsTarget();

                    if (consumed)
                    {
                        break;
                    }
                }

                uiBlock = uiBlock.Parent;
                evt.Target = uiBlock;

            } while (uiBlock != null);

            ListPool<TargetedEvent<TEvent>>.Release(events);
        }

        public static void Consume<TEvent>(ref TEvent evt) where TEvent : struct, IEvent
        {
            EventDispatcher.Consume(ref evt);
        }

        public bool TryGetTarget(IEventTarget eventReceiver, Type eventType, out IEventTarget eventTarget)
        {
            eventTarget = this;
            return redirectedEvents != null && redirectedEvents.TryGetValue(eventType, out Callbacks typedEvents) && (typedEvents.CanBeIndirect || owner == eventReceiver);
        }

        private void LockOwnerAsTarget()
        {
            if (++selfRegistrationCount == 1)
            {
                RegisterEventTargetProvider(this);
            }
        }

        private void ReleaseOwnerAsTarget()
        {
            if (--selfRegistrationCount == 0)
            {
                UnregisterEventTargetProvider(this);
            }

            selfRegistrationCount = selfRegistrationCount < 0 ? 0 : selfRegistrationCount;
        }

        public EventModule(IEventTarget owner)
        {
            this.owner = owner;
        }
    }
}
