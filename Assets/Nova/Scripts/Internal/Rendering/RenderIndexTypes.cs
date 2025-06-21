// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// An index used by the <see cref="RenderingDataStore"/> to index by specific type of render node
    /// </summary>
    
    internal struct RenderIndex : IEquatable<int>, IComparable<int>, IIndex<RenderIndex>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator RenderIndex(int index) => new RenderIndex()
        {
            index = index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(RenderIndex dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RenderIndex other)
        {
            return this.index == other.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(RenderIndex other)
        {
            return index.CompareTo(other.index);
        }

        public static readonly RenderIndex Invalid = new RenderIndex()
        {
            index = -1
        };
    }

    /// <summary>
    /// An index used by the <see cref="RenderingDataStore"/> when elements are accessed, potentially dirtying them
    /// </summary>
    
    internal struct AccessIndex : IEquatable<int>, IComparable<int>, IIndex<AccessIndex>
    {
        private int index;

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AccessIndex(int index) => new AccessIndex()
        {
            index = index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(AccessIndex dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(AccessIndex other)
        {
            return this.index == other.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(AccessIndex other)
        {
            return index.CompareTo(other.index);
        }

        public static readonly AccessIndex Invalid = new AccessIndex()
        {
            index = -1
        };
    }

    /// <summary>
    /// An element's assignment into a <see cref="DrawCall"/>
    /// </summary>
    
    internal struct DrawCallIndex : IEquatable<int>, IComparable<int>, IIndex<DrawCallIndex>
    {
        private int index;

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator DrawCallIndex(int index) => new DrawCallIndex()
        {
            index = index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(DrawCallIndex dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DrawCallIndex other)
        {
            return this.index == other.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(DrawCallIndex other)
        {
            return index.CompareTo(other.Index);
        }

        public static readonly DrawCallIndex Invalid = new DrawCallIndex()
        {
            index = -1
        };
    }

    /// <summary>
    /// Since there is not necessarily a 1:1 mapping of <see cref="UIBlock"/> to something that is rendered (e.g.
    /// drop shadows), we break things down further into <see cref="VisualElement"/>.
    /// </summary>
    
    internal struct VisualElementIndex : IEquatable<int>, IComparable<int>, IIndex<VisualElementIndex>
    {
        private int index;

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator VisualElementIndex(int index) => new VisualElementIndex()
        {
            index = index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(VisualElementIndex dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        public bool Equals(VisualElementIndex other)
        {
            return this.index == other.index;
        }

        public int CompareTo(VisualElementIndex other)
        {
            return index.CompareTo(other);
        }

        public static readonly VisualElementIndex Invalid = new VisualElementIndex()
        {
            index = -1
        };
    }

    
    internal struct ZLayerIndex : IEquatable<int>, IComparable<int>, IIndex<ZLayerIndex>
    {
        private int index;

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ZLayerIndex(int index) => new ZLayerIndex()
        {
            index = index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(ZLayerIndex dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        public bool Equals(ZLayerIndex other)
        {
            return this.index == other.index;
        }

        public int CompareTo(ZLayerIndex other)
        {
            return index.CompareTo(other);
        }

        public static readonly ZLayerIndex Invalid = new ZLayerIndex()
        {
            index = -1
        };
    }

    
    internal struct ComputeBufferIndex : IIndex<ComputeBufferIndex>
    {
        private uint index;

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ComputeBufferIndex(int index) => new ComputeBufferIndex()
        {
            index = (uint)index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator uint(ComputeBufferIndex dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComputeBufferIndex operator *(ComputeBufferIndex index, int multiplier) => new ComputeBufferIndex()
        {
            index = (uint)(index.index * multiplier)
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComputeBufferIndex operator +(ComputeBufferIndex index, int multiplier) => new ComputeBufferIndex()
        {
            index = (uint)(index.index + multiplier)
        };

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

        public bool Equals(ComputeBufferIndex other)
        {
            return this.index == other.index;
        }

        public int CompareTo(ComputeBufferIndex other)
        {
            return index.CompareTo(other);
        }

        public static readonly ComputeBufferIndex Invalid = new ComputeBufferIndex()
        {
            index = uint.MaxValue
        };
    }

    
    internal struct TexturePackSlice : IEquatable<int>, IComparable<int>, IIndex<TexturePackSlice>
    {
        public uint index;

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TexturePackSlice(int index) => new TexturePackSlice()
        {
            index = (uint)index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(TexturePackSlice dsIndex) => (int)dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        public bool Equals(TexturePackSlice other)
        {
            return this.index == other.index;
        }

        public int CompareTo(TexturePackSlice other)
        {
            return index.CompareTo(other);
        }

        public static readonly TexturePackSlice Invalid = new TexturePackSlice()
        {
            index = uint.MaxValue
        };
    }

    
    internal struct ShaderCacheIndex : IEquatable<int>, IComparable<int>, IIndex<ShaderCacheIndex>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ShaderCacheIndex(int index) => new ShaderCacheIndex()
        {
            index = index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(ShaderCacheIndex dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ShaderCacheIndex other)
        {
            return this.index == other.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(ShaderCacheIndex other)
        {
            return index.CompareTo(other.index);
        }

        public static readonly ShaderCacheIndex Invalid = new ShaderCacheIndex()
        {
            index = -1
        };
    }

    
    internal struct MaterialCacheIndex : IEquatable<int>, IComparable<int>, IIndex<MaterialCacheIndex>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator MaterialCacheIndex(int index) => new MaterialCacheIndex()
        {
            index = index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(MaterialCacheIndex dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(MaterialCacheIndex other)
        {
            return this.index == other.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(MaterialCacheIndex other)
        {
            return index.CompareTo(other.index);
        }

        public static readonly MaterialCacheIndex Invalid = new MaterialCacheIndex()
        {
            index = -1
        };
    }

    
    internal struct VisualModifierID : IEquatable<int>, IComparable<int>, IIndex<VisualModifierID>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int other)
        {
            return other == this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int other)
        {
            return ((int)this).CompareTo(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator VisualModifierID(int index) => new VisualModifierID()
        {
            index = index
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(VisualModifierID dsIndex) => dsIndex.index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ((int)this).GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((int)this).ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(VisualModifierID other)
        {
            return this.index == other.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(VisualModifierID other)
        {
            return index.CompareTo(other.index);
        }

        public static readonly VisualModifierID Invalid = new VisualModifierID()
        {
            index = -1
        };
    }
}

