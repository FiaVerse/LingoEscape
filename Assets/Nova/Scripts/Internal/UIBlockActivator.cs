// Copyright (c) Supernova Technologies LLC

using Nova.Compat;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// A hidden component which synchronizes listeners with their hierarchies based on
    /// GameObject.active state rather than MonoBehaviour.enabled state. This behavior simplifies
    /// any confusion, as the default behavior of a Node is that it cannot be "disabled" independently
    /// of its GameObject state.
    /// </summary>
    [ExecuteAlways]
    [ExcludeFromPreset]
    [ExcludeFromObjectFactory]
    [DisallowMultipleComponent]
    [HideInInspector]
    [Obfuscation(ApplyToMembers = false)]
    [AddComponentMenu("")]
    internal class UIBlockActivator : MonoBehaviour
    {
        [NonSerialized, HideInInspector]
        private List<IGameObjectActiveReceiver> receivers = new List<IGameObjectActiveReceiver>();

        public void Register<T>(T receiver) where T : MonoBehaviour, IGameObjectActiveReceiver
        {
            if (!receivers.Contains(receiver))
            {
                receivers.Add(receiver);

                if (isActiveAndEnabled)
                {

                    receiver.HandleOnEnable();
                }
            }
        }

        public void Unregister(IGameObjectActiveReceiver receiver)
        {
            if (receivers.Remove(receiver))
            {
                if (isActiveAndEnabled)
                {

                    receiver.HandleOnDisable();
                }

                if (receivers.Count == 0)
                {
                    SelfDestruct();
                }
            }
        }

        private void GetReceivers()
        {
            IGameObjectActiveReceiver[] availableReceivers = GetComponents<IGameObjectActiveReceiver>();

            receivers.Clear();

            for (int i = 0; i < availableReceivers.Length; ++i)
            {
                var receiver = availableReceivers[i];

                if (receiver as MonoBehaviour != null)
                {
                    receivers.Add(receiver);
                }
            }
        }

        protected void SelfDestruct()
        {
            if (Application.isPlaying)
            {
                Destroy(this);
            }
            else if (NovaApplication.IsEditor)
            {
                GameObject go = gameObject;

                // Delay in case the entire GameObject is being
                // destroyed to avoid calling destroy twice
                NovaApplication.EditorDelayCall += () =>
                {
                    if (go != null)
                    {
                        DestroyImmediate(this);
                    }
                };
            }
        }

        private void OnEnable()
        {
            if (NovaApplication.IsEditor)
            {
                OnValidate();
            }

            int count = receivers.Count;

            if (count == 0)
            {
                GetReceivers();
                count = receivers.Count;
            }

            for (int i = 0; i < count; ++i)
            {
                IGameObjectActiveReceiver receiver = receivers[i];
                if (receiver as MonoBehaviour != null)
                {
                    receiver.HandleOnEnable();
                }
            }
        }

        private void OnDisable()
        {
            int count = receivers.Count;

            for (int i = count - 1; i >= 0; --i)
            {
                IGameObjectActiveReceiver receiver = receivers[i];
                if (receiver as MonoBehaviour != null)
                {
                    receiver.HandleOnDisable();
                }
            }
        }

        /// <summary>
        /// Not seeing another reliable option that works when you select a prefab but don't open it
        /// </summary>
        private void OnValidate()
        {
            if (!NovaApplication.IsEditor)
            {
                return;
            }

            if ((hideFlags & HideFlags.NotEditable) == 0)
            {
                hideFlags |= HideFlags.NotEditable;
            }

            if ((hideFlags & HideFlags.HideInInspector) == 0)
            {
                hideFlags |= HideFlags.HideInInspector;
            }
        }
    }
}
