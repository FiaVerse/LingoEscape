// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal interface ITexturePackSubscriber
    {
        void HandleTextureArrayRecreated(Texture2DArray textureArray);
    }
}
