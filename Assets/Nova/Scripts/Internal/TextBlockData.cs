// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using Nova.Internal.Common;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct TextBlockData : IInitializable, IClearable
    {
        public NovaList<TextBlockMeshData> MeshData;
        public AABB TextBounds;
        public int QuadCount;
        /// <summary>
        /// Scale affects sdf, so we need to know if the scale of a text object has changed
        /// </summary>
        public float LossyYScale;

        public void Clear()
        {
            MeshData.Clear();
            QuadCount = 0;
            TextBounds = default;
            LossyYScale = default;
        }

        public void GetInstanceSliceForSubmesh(TextMaterialID materialID, out int start, out int count)
        {
            start = 0;
            for (int i = 0; i < MeshData.Length; ++i)
            {
                TextBlockMeshData meshData = MeshData[i];
                if (meshData.MaterialID != materialID)
                {
                    start += meshData.CharacterCount;
                    continue;
                }

                count = meshData.CharacterCount;
                return;
            }

            Debug.LogError("Failed to get vert index slice for a text material");
            count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCharShaderData(int charIndex, ref PerCharacterTextShaderData dest, ref float3 positionalOffset)
        {
            for (int i = 0; i < MeshData.Length; ++i)
            {
                TextBlockMeshData meshData = MeshData[i];
                if (charIndex >= meshData.CharacterCount)
                {
                    charIndex -= meshData.CharacterCount;
                    continue;
                }

                meshData.SetCharShaderData(charIndex, ref dest, ref positionalOffset);
                return;
            }
        }

        public void Dispose()
        {
            MeshData.DisposeListAndElements();
        }

        public void Init()
        {
            MeshData.Init(1);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextBlockMeshData : IInitializable, IClearable
    {
        public int VertCount;
        public int CharacterCount;
        public TextMaterialID MaterialID;
        public NovaList<float3> VertexPositions;
        public NovaList<Color32> Colors;
        public NovaList<Vector2> UVs0;
        public NovaList<Vector2> UVs1;

        public void Clear()
        {
            VertCount = 0;
            CharacterCount = 0;
            MaterialID = TextMaterialID.Invalid;
            VertexPositions.Clear();
            Colors.Clear();
            UVs0.Clear();
            UVs1.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCharShaderData(int charIndex, ref PerCharacterTextShaderData dest, ref float3 positionalOffset)
        {
            int startVert = charIndex * 4;
            for (int i = 0; i < 4; ++i)
            {
                int vertIndex = startVert + i;

                ref PerVertTextShaderData vertDest = ref dest[i];
                vertDest.Position = VertexPositions[vertIndex] + positionalOffset;
                // We only linearize if not using a new version of TMP, because new versions of TMP
                // already do that, so if we do it as well it will double apply.
                vertDest.Color.Set(ref Colors.ElementAt(vertIndex), linearize: !SystemSettings.NewTMP);
                vertDest.Texcoord0 = UVs0[vertIndex];
                vertDest.Texcoord1 = UVs1[vertIndex];
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBounds(ref float3 maxes, ref float3 mins, ref int vertCount)
        {
            vertCount += VertCount;
            for (int i = 0; i < VertCount; ++i)
            {
                float3 vertPos = VertexPositions[i];
                maxes = math.max(maxes, vertPos);
                mins = math.min(mins, vertPos);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Resize(int vertexCount)
        {
            VertCount = vertexCount;
            CharacterCount = VertCount / 4;
            VertexPositions.Resize(vertexCount);
            Colors.Resize(vertexCount);
            UVs0.Resize(vertexCount);
            UVs1.Resize(vertexCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UpdateInfo(TMP_MeshInfo meshInfo)
        {
            MaterialID = meshInfo.material.GetInstanceID();
            Resize(meshInfo.vertices.Length);
            NovaList<Vector3> asVec3 = VertexPositions.Reinterpret<float3, Vector3>();
            asVec3.CopyFrom(meshInfo.vertices, VertCount);
            Colors.CopyFrom(meshInfo.colors32, VertCount);
        }

        public void Dispose()
        {
            VertexPositions.Dispose();
            Colors.Dispose();
            UVs0.Dispose();
            UVs1.Dispose();
        }

        public void Init()
        {
            VertexPositions.Init(64);
            Colors.Init(64);
            UVs0.Init(64);
            UVs1.Init(64);
        }
    }
}
