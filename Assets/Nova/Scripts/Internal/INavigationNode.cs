// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Internal
{
    internal interface INavigationNode
    {
        bool Enabled { get; }
        bool CaptureInput { get; }
        bool ScopeNavigation { get; }

        bool UseTargetNotFoundFallback(Vector3 direction);
        bool TryHandleScopedMove(IUIBlock previousChild, IUIBlock nextChild, Vector3 direction);
        bool TryGetNext(Vector3 direction, out IUIBlock toUIBlock);
        void EnsureInView(IUIBlock descendant);
    }
}
