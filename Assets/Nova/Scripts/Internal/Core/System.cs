// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using System;
using UnityEngine;

namespace Nova.Internal.Core
{
    internal abstract class System<T> where T : System<T>, new()
    {
        public static bool IsReady { get; private set; } = false;

        public static T Instance { get; private set; } = null;

        protected System()
        {
            if (NovaApplication.IsEditor)
            {
                AppDomain.CurrentDomain.DomainUnload -= DomainReloadStarted;
                AppDomain.CurrentDomain.DomainUnload += DomainReloadStarted;
            }

            IsReady = true;
            try
            {
                Init();
            }
            catch (Exception e)
            {
                Debug.LogError($"System {typeof(T)} Init failed with {e}");
            }
        }

        internal static void CreateInstance()
        {
            if (Instance == null)
            {
                Instance = new T();
            }
        }

        private static void DomainReloadStarted(object sender, EventArgs e)
        {
            DomainReloadStarted();
        }

        internal static void DomainReloadStarted()
        {
            try
            {
                Instance.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"System {typeof(T)} Dispose failed with {e}");
            }

            IsReady = false;
        }

        public static void ForceDispose()
        {
            DomainReloadStarted();
            Instance = null;
        }

        protected abstract void Init();
        protected abstract void Dispose();
    }
}
