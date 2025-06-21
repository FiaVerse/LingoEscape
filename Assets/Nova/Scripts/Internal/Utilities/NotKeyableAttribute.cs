// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Utilities
{
    /// <summary>
    /// Wrapper around Unity's NotKeyable attribute so we don't force users to import
    /// the animation package
    /// </summary>
    internal class NotKeyableAttribute
#if ANIMATIONS
     : UnityEngine.Animations.NotKeyableAttribute
#else
     : System.Attribute
#endif
    {

    }
}
