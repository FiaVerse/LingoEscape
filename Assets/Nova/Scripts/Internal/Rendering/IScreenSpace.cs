// Copyright (c) Supernova Technologies LLC
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal interface IScreenSpace
    {
        int CameraID { get; }

        List<Camera> AdditionalCameras { get; }

        void Update();
    }
}

