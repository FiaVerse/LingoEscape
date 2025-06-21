// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    [BurstCompile]
    internal struct UIBlock3DMeshJob : INovaJob
    {
        public int CornerDivisions;
        public int EdgeDivisions;

        public NativeList<ushort> Indices;
        public NativeList<UIBlock3DVertData> Verts;
        public NativeList<float2> CornerCosSin;
        public NativeList<float3> EdgeSinCos;

        private int totalVertsPerCorner;
        private int totalCornerDivisions;
        private int totalEdgeDivisions;
        private float cornerAngleStepRad;
        private float edgeAngleStepRad;
        private int firstFaceVertIndex;
        private bool flipWind;

        public void Execute()
        {
            PreCalcValues();

            // Back face
            firstFaceVertIndex = 0;
            flipWind = false;
            AddFace(-.5f, new float3(0f, 0f, -1f));

            // Front face
            firstFaceVertIndex = Verts.Length;
            flipWind = true;
            AddFace(.5f, new float3(0f, 0f, 1f));

            flipWind = false;

            ConnectFaces();
        }

        private void AddFaceConnection(int a, int b)
        {
            AddQuad(a, firstFaceVertIndex + a, firstFaceVertIndex + b, b);
        }

        private void ConnectFaces()
        {
            // Connect the edges
            // T
            AddFaceConnection(4 + 4 * totalVertsPerCorner - 1, 4 + totalVertsPerCorner - 1);

            // R
            AddFaceConnection(4 + totalEdgeDivisions - 1, 4 + totalVertsPerCorner + totalEdgeDivisions - 1);

            // B
            AddFaceConnection(4 + 2 * totalVertsPerCorner - 1, 4 + 3 * totalVertsPerCorner - 1);

            // L
            AddFaceConnection(4 + 2 * totalVertsPerCorner + totalEdgeDivisions - 1, 4 + 3 * totalVertsPerCorner + totalEdgeDivisions - 1);

            // Connect the corners
            for (int j = 0; j < 4; j++)
            {
                int offset = 4 + j * totalVertsPerCorner;
                for (int i = 1; i < totalCornerDivisions; ++i)
                {
                    AddFaceConnection(offset + (i + 1) * totalEdgeDivisions - 1, offset + i * totalEdgeDivisions - 1);
                }

                flipWind = !flipWind;
            }
        }

        private void AddFace(float zPos, float3 normal)
        {
            ////////// Center
            // TR
            Verts.Add(new UIBlock3DVertData()
            {
                VertPos = new float3(.5f, .5f, zPos),
                Normal = normal,
                CornerOffsetDir = new float3(-1f, -1f, 0f),
                EdgeOffsetDir = float3.zero,
            });

            // BR
            Verts.Add(new UIBlock3DVertData()
            {
                VertPos = new float3(.5f, -.5f, zPos),
                Normal = normal,
                CornerOffsetDir = new float3(-1f, 1f, 0f),
                EdgeOffsetDir = float3.zero,
            });

            // BL
            Verts.Add(new UIBlock3DVertData()
            {
                VertPos = new float3(-.5f, -.5f, zPos),
                Normal = normal,
                CornerOffsetDir = new float3(1f, 1f, 0f),
                EdgeOffsetDir = float3.zero,
            });

            // TL
            Verts.Add(new UIBlock3DVertData()
            {
                VertPos = new float3(-.5f, .5f, zPos),
                Normal = normal,
                CornerOffsetDir = new float3(1f, -1f, 0f),
                EdgeOffsetDir = float3.zero,
            });

            // Connect face
            AddQuad(0 + firstFaceVertIndex, 1 + firstFaceVertIndex, 2 + firstFaceVertIndex, 3 + firstFaceVertIndex);

            ////////////////////// Corners
            // TR
            UIBlock3DVertData vert = new UIBlock3DVertData()
            {
                VertPos = new float3(.5f, .5f, zPos),
                Normal = normal,
            };
            DoFaceCorner(ref vert, new float2(.5f, .5f), 0 + firstFaceVertIndex);

            // BR
            vert.VertPos = new float3(.5f, -.5f, zPos);
            flipWind = !flipWind;
            DoFaceCorner(ref vert, new float2(.5f, -.5f), 1 + firstFaceVertIndex);

            // BL
            vert.VertPos = new float3(-.5f, -.5f, zPos);
            flipWind = !flipWind;
            DoFaceCorner(ref vert, new float2(-.5f, -.5f), 2 + firstFaceVertIndex);

            // TL
            vert.VertPos = new float3(-.5f, .5f, zPos);
            flipWind = !flipWind;
            DoFaceCorner(ref vert, new float2(-.5f, .5f), 3 + firstFaceVertIndex);
            flipWind = !flipWind;

            int firstNonFaceIndex = firstFaceVertIndex + 4;

            //////////////// Edges
            // T
            int2 bottoms = new int2(firstFaceVertIndex + 3, firstFaceVertIndex);
            int2 tops = new int2(firstNonFaceIndex + 3 * totalVertsPerCorner + (totalCornerDivisions - 1) * totalEdgeDivisions, firstNonFaceIndex + (totalCornerDivisions - 1) * totalEdgeDivisions);
            DoEdges(ref bottoms, ref tops);

            // R
            //AddQuad(currentOffset, firstNonFaceIndex, firstNonFaceIndex + vertsPerCorner, currentOffset + 1);
            bottoms = new int2(firstFaceVertIndex, firstFaceVertIndex + 1);
            tops = new int2(firstNonFaceIndex, firstNonFaceIndex + totalVertsPerCorner);
            DoEdges(ref bottoms, ref tops);

            // B
            bottoms = new int2(firstFaceVertIndex + 1, firstFaceVertIndex + 2);
            tops = new int2(firstNonFaceIndex + 2 * totalVertsPerCorner - totalEdgeDivisions, firstNonFaceIndex + 3 * totalVertsPerCorner - totalEdgeDivisions);
            DoEdges(ref bottoms, ref tops);

            // L
            bottoms = new int2(firstFaceVertIndex + 2, firstFaceVertIndex + 3);
            tops = new int2(firstNonFaceIndex + 2 * totalVertsPerCorner, firstNonFaceIndex + 3 * totalVertsPerCorner);
            DoEdges(ref bottoms, ref tops);
        }

        private void DoEdges(ref int2 bottoms, ref int2 tops)
        {
            for (int i = 0; i < totalEdgeDivisions; i++)
            {
                AddQuad(tops.y, bottoms.y, bottoms.x, tops.x);
                bottoms = tops;
                tops += 1;
            }
        }

        private void DoFaceCorner(ref UIBlock3DVertData vert, float2 sign, int currentCornerIndex)
        {
            int baseIndex = Verts.Length;
            for (int i = 0; i < CornerCosSin.Length; i++)
            {
                float2 cosSin = CornerCosSin[i];
                cosSin *= sign;
                vert.CornerOffsetDir = 2f * (new float3(cosSin, vert.VertPos.z) - vert.VertPos);

                float3 edgeOriginDirection = new float3(-cosSin, -vert.VertPos.z);

                for (int j = 0; j < totalEdgeDivisions; j++)
                {
                    vert.EdgeOffsetDir = EdgeSinCos[j] * edgeOriginDirection * 2;

                    // The normals are just the inverse of the model space position when fully rounded. So we calculate that
                    // assuming a size of (1, 1, 1), corner radius 0.5, and edge radius of 0.5
                    float3 fullyRoundedPos = vert.VertPos + vert.CornerOffsetDir * 0.5f + vert.EdgeOffsetDir * 0.5f;
                    vert.Normal = math.normalize(fullyRoundedPos);
                    Verts.Add(vert);
                }

                if (i == 0)
                {
                    continue;
                }

                // Add face
                AddTriangle(currentCornerIndex, baseIndex + i * totalEdgeDivisions, baseIndex + (i - 1) * totalEdgeDivisions);

                // Connect chamfers
                for (int j = 0; j < EdgeDivisions + 1; ++j)
                {
                    AddQuad(baseIndex + i * totalEdgeDivisions + j, baseIndex + i * totalEdgeDivisions + j + 1, baseIndex + (i - 1) * totalEdgeDivisions + j + 1, baseIndex + (i - 1) * totalEdgeDivisions + j);
                }
            }
        }

        private void PreCalcValues()
        {
            totalCornerDivisions = CornerDivisions + 2;
            totalEdgeDivisions = EdgeDivisions + 2;
            totalVertsPerCorner = totalCornerDivisions * totalEdgeDivisions;
            cornerAngleStepRad = .5f * math.PI / (CornerDivisions + 1);
            edgeAngleStepRad = .5f * math.PI / (EdgeDivisions + 1);

            CornerCosSin.Length = totalCornerDivisions;
            for (int i = 0; i < totalCornerDivisions; i++)
            {
                float2 cosSin = default;
                math.sincos(i * cornerAngleStepRad, out cosSin.y, out cosSin.x);
                CornerCosSin[i] = cosSin;
            }

            EdgeSinCos.Length = totalEdgeDivisions;
            for (int i = 0; i < totalEdgeDivisions; i++)
            {
                float2 cosSin = default;
                math.sincos(i * edgeAngleStepRad, out cosSin.y, out cosSin.x);
                EdgeSinCos[i] = new float3(-cosSin.yy + 1f, -cosSin.x + 1f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(c, d, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddTriangle(int a, int b, int c)
        {
            if (flipWind)
            {
                Indices.Add((ushort)c);
                Indices.Add((ushort)b);
                Indices.Add((ushort)a);
            }
            else
            {
                Indices.Add((ushort)a);
                Indices.Add((ushort)b);
                Indices.Add((ushort)c);
            }
        }
    }
}

