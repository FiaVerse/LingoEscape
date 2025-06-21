// Copyright (c) Supernova Technologies LLC
using System;

namespace Nova.Internal
{
    internal interface IInputTarget
    {
        event RawInput.PointerInputChangeEvent OnPointerInputChanged;
        event RawInput.VectorInputChangeEvent OnVector3InputChanged;
        event RawInput.InputCanceledEvent OnInputCanceled;

        void SetGestureRecognizer(IGestureRecognizer recognizer);
        void ClearGestureRecognizer();
        IGestureRecognizer GestureRecognizer { get; }

        void SetNavigationNode(INavigationNode node);
        void ClearNavigationNode();
        INavigationNode Nav { get; }
        bool ScopeNavigation { get; }
        bool CaptureNavInput { get; }

        bool CapturesInput<T>() where T : unmanaged, IEquatable<T>;
        Input<T>? GetInput<T>(uint sourceID) where T : unmanaged, IEquatable<T>;
        bool TryGetInputSource(uint sourceID, out Interaction source);
        void SetInput<T>(Interaction source, Input<T>? input) where T : unmanaged, IEquatable<T>;
        void CancelInput(Interaction source);
        void CancelInput();
    }
}
