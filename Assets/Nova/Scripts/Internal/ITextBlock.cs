// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Rendering;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nova
{
    internal interface ITextBlock : IRenderBlock<Internal.TextBlockData>
    {
        void UpdateMeshSize(ref TextMargin newMargin);
    }
}
