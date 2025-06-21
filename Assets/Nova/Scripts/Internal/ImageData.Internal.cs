// Copyright (c) Supernova Technologies LLC
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nova.Internal
{

    internal enum ImagePackMode
    {
        Unpacked = 0,
        Packed = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ImageID : IComparable<ImageID>, IEquatable<ImageID>
    {
        private int val;

        public static readonly ImageID Invalid = -1;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => val != -1;
        }

        public bool Equals(ImageID other)
        {
            return val == other.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return val.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(ImageID other)
        {
            return val - other.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ImageID(int val) => new ImageID()
        {
            val = val,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(ImageID val) => val.val;

        public override string ToString()
        {
            return val.ToString();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ImageData : System.IEquatable<ImageData>
    {
        public ImageAdjustment Adjustment;
        public ImagePackMode Mode;
        public ImageID ImageID;

        public bool HasImage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ImageID.IsValid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ImageData other)
        {
            return
                Adjustment.Equals(other.Adjustment) &&
                Mode == other.Mode &&
                ImageID == other.ImageID;
        }
    }
}
