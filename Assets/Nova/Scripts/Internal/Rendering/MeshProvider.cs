// Copyright (c) Supernova Technologies LLC
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nova.Internal.Rendering
{
    internal unsafe class MeshProvider : IDisposable
    {
        private static readonly List<Vector3> singleFaceQuadCorners = new List<Vector3>()
        {
            new Vector3(.5f, .5f, 0f),
            new Vector3(.5f, -.5f, 0f),
            new Vector3(-.5f, -.5f, 0f),
            new Vector3(-.5f, .5f, 0f),
        };

        private NativeList<UIBlock3DVertData> uiblock3DVertData;
        private NativeList<ushort> uiBlock3DIndices;
        private int block3DCornerDivisionsUsed = -1;
        private int block3DChamferDivisionsUsed = -1;


        private Mesh _singleSidedQuadMesh = null;
        public Mesh SingleSidedQuadMesh
        {
            get
            {
                if (_singleSidedQuadMesh == null)
                {
                    _singleSidedQuadMesh = new Mesh();
                    _singleSidedQuadMesh.subMeshCount = 1;
                    List<Vector3> verts = new List<Vector3>();
                    // Front
                    verts.AddRange(singleFaceQuadCorners);
                    _singleSidedQuadMesh.SetVertices(verts);

                    _singleSidedQuadMesh.SetIndices(new ushort[]
                    {
                        // Front
                        0, 1, 2,
                        2, 3, 0,

                    }, MeshTopology.Triangles, 0);

                    _singleSidedQuadMesh.SetNormals(new List<Vector3>()
                    {
                        Vector3.back,
                        Vector3.back,
                        Vector3.back,
                        Vector3.back,
                    });
                }
                return _singleSidedQuadMesh;
            }
        }

        private Mesh _roundedCubeMesh = null;
        public Mesh RoundedCubeMesh
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_roundedCubeMesh == null)
                {
                    EnsureRoundedCubeData();
                    _roundedCubeMesh = new Mesh();
                    _roundedCubeMesh.subMeshCount = 1;

                    _roundedCubeMesh.SetVertexBufferParams(
                        uiblock3DVertData.Length,
                        new VertexAttributeDescriptor[]
                        {
                            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
                            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
                        });
                    _roundedCubeMesh.SetVertexBufferData(uiblock3DVertData.AsArray(), 0, 0, uiblock3DVertData.Length);

                    _roundedCubeMesh.SetIndices(uiBlock3DIndices.AsArray(), MeshTopology.Triangles, 0);
                }
                return _roundedCubeMesh;
            }
        }

        private void EnsureRoundedCubeData()
        {
            if (!uiblock3DVertData.IsCreated)
            {
                uiblock3DVertData = new NativeList<UIBlock3DVertData>(1000, Allocator.Persistent);
                uiBlock3DIndices = new NativeList<ushort>(6000, Allocator.Persistent);
            }

            uiblock3DVertData.Clear();
            uiBlock3DIndices.Clear();

            block3DCornerDivisionsUsed = NovaSettings.UIBlock3DCornerDivisions;
            block3DChamferDivisionsUsed = NovaSettings.UIBlock3DEdgeDivisions;

            UIBlock3DMeshJob job = new UIBlock3DMeshJob()
            {
                CornerDivisions = block3DCornerDivisionsUsed,
                EdgeDivisions = block3DChamferDivisionsUsed,

                Indices = uiBlock3DIndices,
                Verts = uiblock3DVertData,
                CornerCosSin = new NativeList<Unity.Mathematics.float2>(0, Allocator.TempJob),
                EdgeSinCos = new NativeList<Unity.Mathematics.float3>(0, Allocator.TempJob),
            };

            job.Run();

            job.CornerCosSin.Dispose();
            job.EdgeSinCos.Dispose();
        }

        #region Init and Cleanup
        private void HandleSettingsChanged()
        {
            if (NovaSettings.UIBlock3DCornerDivisions != block3DCornerDivisionsUsed ||
                NovaSettings.UIBlock3DEdgeDivisions != block3DChamferDivisionsUsed)
            {
                _roundedCubeMesh = null;
            }
        }

        public MeshProvider()
        {
            NovaSettings.OnRenderSettingsChanged += HandleSettingsChanged;
        }

        public void Dispose()
        {
            NovaSettings.OnRenderSettingsChanged -= HandleSettingsChanged;

            if (uiblock3DVertData.IsCreated)
            {
                uiblock3DVertData.Dispose();
                uiBlock3DIndices.Dispose();
            }
        }
        #endregion
    }
}

