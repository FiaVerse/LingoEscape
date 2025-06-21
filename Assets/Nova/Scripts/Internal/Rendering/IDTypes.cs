// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
#pragma warning disable CS0660
#pragma warning disable CS0661

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// The ID of a specific DrawCall type. Elements that have the same <see cref="DrawCallDescriptorID"/>
    /// can theoretically be batched (assuming other criteria are met as well).
    /// </summary>
    
    internal struct DrawCallDescriptorID : IIndex<DrawCallDescriptorID>, IEquatable<int>, IComparable<int>
    {
        private int index;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index >= 0;
        }

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index;
        }

        public static implicit operator int(DrawCallDescriptorID id) => id.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator DrawCallDescriptorID(int id) => new DrawCallDescriptorID()
        {
            index = id
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(DrawCallDescriptorID x, DrawCallDescriptorID y) => x.index == y.index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(DrawCallDescriptorID x, DrawCallDescriptorID y) => x.index != y.index;

        public bool Equals(int other)
        {
            return index == other;
        }

        public int CompareTo(int other)
        {
            return index.CompareTo(other);
        }

        public int CompareTo(DrawCallDescriptorID other)
        {
            return index.CompareTo(other.index);
        }

        public bool Equals(DrawCallDescriptorID other)
        {
            return index.Equals(other);
        }
    }

    /// <summary>
    /// The ID for a specific draw call. This is different than <see cref="DrawCallDescriptorID"/>
    /// because there might be multiple draw calls for a specific <see cref="DrawCallDescriptorID"/> if,
    /// for example, the way elements overlap necessitates splitting elements that *could* be batched together
    /// into separate draw calls
    /// </summary>
    
    internal struct DrawCallID : IIndex<DrawCallID>
    {
        private int index;

        public int Index => index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(DrawCallID id) => id.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator DrawCallID(int id) => new DrawCallID()
        {
            index = id
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(DrawCallID x, DrawCallID y) => x.index == y.index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(DrawCallID x, DrawCallID y) => x.index != y.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(DrawCallID other)
        {
            return index.CompareTo(other.index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DrawCallID other)
        {
            return index.Equals(other.index);
        }

        public static DrawCallID Max(ref DrawCallID a, ref DrawCallID b)
        {
            return math.max(a.index, b.index);
        }

        public static readonly DrawCallID Invalid = new DrawCallID()
        {
            index = -1
        };
    }

    /// <summary>
    /// An ID assigned to a group of elements that are all coplanar
    /// </summary>
    
    internal struct CoplanarSetID : IIndex<CoplanarSetID>
    {
        private int index;

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index;
        }
        public static implicit operator CoplanarSetID(int id) => new CoplanarSetID()
        {
            index = id
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator int(CoplanarSetID id) => id.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CoplanarSetID lhs, CoplanarSetID rhs) => lhs.index == rhs.index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CoplanarSetID lhs, CoplanarSetID rhs) => lhs.index != rhs.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return index.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return index.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(CoplanarSetID other)
        {
            return index.CompareTo(other.index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CoplanarSetID other)
        {
            return index.Equals(other.index);
        }

        public static readonly CoplanarSetID Invalid = new CoplanarSetID() { index = -1 };
    }

    
    internal struct RotationSetID : IIndex<RotationSetID>
    {
        private int index;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index > -1;
        }

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index;
        }
        public static implicit operator RotationSetID(int id) => new RotationSetID()
        {
            index = id
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator int(RotationSetID id) => id.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(RotationSetID lhs, RotationSetID rhs) => lhs.index == rhs.index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(RotationSetID lhs, RotationSetID rhs) => lhs.index != rhs.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return index.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return index.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(RotationSetID other)
        {
            return index.CompareTo(other.index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RotationSetID other)
        {
            return index.Equals(other.index);
        }

        public static readonly RotationSetID Invalid = new RotationSetID() { index = -1 };
    }

    internal struct TextMaterialID : IEquatable<TextMaterialID>, IComparable<TextMaterialID>
    {
        private int val;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => val != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TextMaterialID other)
        {
            return val == other.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return val.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TextMaterialID(int val) => new TextMaterialID()
        {
            val = val
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TextMaterialID lhs, TextMaterialID rhs)
        {
            return lhs.val == rhs.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TextMaterialID lhs, TextMaterialID rhs)
        {
            return lhs.val != rhs.val;
        }

        public int CompareTo(TextMaterialID other)
        {
            return val - other.val;
        }

        public override string ToString()
        {
            return val.ToString();
        }

        public static readonly TextMaterialID Invalid = new TextMaterialID()
        {
            val = 0
        };
    }

    internal struct TextureID : IEquatable<TextureID>, IComparable<TextureID>
    {
        private int val;

        public static readonly TextureID Invalid = 0;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => val != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TextureID other)
        {
            return val == other.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return val.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TextureID lhs, TextureID rhs)
        {
            return lhs.val == rhs.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TextureID lhs, TextureID rhs)
        {
            return lhs.val != rhs.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(TextureID other)
        {
            return val - other.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TextureID(int val) => new TextureID()
        {
            val = val,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(TextureID val) => val.val;

        public override string ToString()
        {
            return val.ToString();
        }
    }

    internal struct TexturePackID : IIndex<TexturePackID>
    {
        private int val;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => val >= 0;
        }

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => val;
        }

        public bool Equals(TexturePackID other)
        {
            return val == other.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return val.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TexturePackID lhs, TexturePackID rhs)
        {
            return lhs.val == rhs.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TexturePackID lhs, TexturePackID rhs)
        {
            return lhs.val != rhs.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(TexturePackID other)
        {
            return val - other.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TexturePackID(int val) => new TexturePackID()
        {
            val = val,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(TexturePackID val) => val.val;

        public override string ToString()
        {
            return val.ToString();
        }

        public static readonly TexturePackID Invalid = new TexturePackID()
        {
            val = -1
        };
    }
}
