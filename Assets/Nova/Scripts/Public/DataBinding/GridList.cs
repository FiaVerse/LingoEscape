// Copyright (c) Supernova Technologies LLC
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Nova
{
    /// <summary>
    /// A two-dimensional indexer
    /// </summary>
    /// <seealso cref="Row"/>
    /// <seealso cref="Column"/>
    public struct GridIndex : IEquatable<GridIndex>
    {
        /// <summary>
        /// The row index
        /// </summary>
        public int Row;
        /// <summary>
        /// The column index
        /// </summary>
        public int Column;

        /// <summary>
        /// Create a new grid index
        /// </summary>
        /// <param name="row">The value to assign to <see cref="Row"/></param>
        /// <param name="col">The value to assign to <see cref="Column"/></param>
        public GridIndex(int row, int col)
        {
            Row = row;
            Column = col;
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if <c><paramref name="lhs"/>.Row == <paramref name="rhs"/>.Row &amp;&amp; <paramref name="lhs"/>.Column == <paramref name="rhs"/>.Column</c></returns>
        public static bool operator ==(GridIndex lhs, GridIndex rhs)
        {
            return lhs.Column == rhs.Column &&
                   lhs.Row == rhs.Row;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if <c><paramref name="lhs"/>.Row != <paramref name="rhs"/>.Row || <paramref name="lhs"/>.Column != <paramref name="rhs"/>.Column</c></returns>
        public static bool operator !=(GridIndex lhs, GridIndex rhs)
        {
            return lhs.Column != rhs.Column ||
                   lhs.Row != rhs.Row;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridIndex"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(GridIndex other)
        {
            return this == other;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridIndex"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public override bool Equals(object other)
        {
            switch (other)
            {
                case GridIndex index:
                    return this == index;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The hashcode for this <see cref="GridIndex"/>
        /// </summary>
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Row.GetHashCode();
            hash = (hash * 7) + Column.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Convert this <see cref="GridIndex"/> to a string
        /// </summary>
        /// <returns>The string representation of this <see cref="GridIndex"/></returns>
        public override string ToString()
        {
            return $"r: {Row}, c: {Column}";
        }
    }

    /// <summary>
    /// A read-only utility wrapper around an <see cref="IList{T}"/> (see <see cref="Source"/>) to simplify indexing into the list by rows and columns
    /// </summary>
    /// <typeparam name="T">The type of element in the underlying <see cref="Source"/></typeparam>
    public readonly struct GridList<T> : IEquatable<GridList<T>>
    {
        private readonly ref struct Row
        {
            public readonly int Length;
            public readonly T this[int col]
            {
                get
                {
                    if (col < 0 || col >= Length)
                    {
                        UnityEngine.Debug.LogError($"Column [{col}] out of range [0, {Length})");
                        return default(T);
                    }

                    int sourceIndex = firstIndex + (col * nextElementOffset);
                    return (T)source[sourceIndex];
                }
            }

            private readonly int firstIndex;
            private readonly int nextElementOffset;
            private readonly IList<T> source;
            internal Row(IList<T> source, int firstIndex, int nextElementOffset, int count)
            {
                this.source = source;
                this.firstIndex = firstIndex;
                this.nextElementOffset = nextElementOffset;
                this.Length = count;
            }
        }

        private readonly ref struct Column
        {
            public readonly int Length;
            public readonly T this[int row]
            {
                get
                {
                    if (row < 0 || row >= Length)
                    {
                        UnityEngine.Debug.LogError($"Row [{row}] out of range [0, {Length})");
                        return default(T);
                    }

                    int sourceIndex = firstIndex + (row * nextElementOffset);
                    return (T)source[sourceIndex];
                }
            }

            private readonly int firstIndex;
            private readonly int nextElementOffset;
            private readonly IList<T> source;
            internal Column(IList<T> source, int firstIndex, int nextElementOffset, int count)
            {
                this.source = source;
                this.firstIndex = firstIndex;
                this.nextElementOffset = nextElementOffset;
                this.Length = count;
            }
        }

        /// <summary>
        /// Indicates if this grid was configured to have a dynamic number of rows and a fixed number of columns
        /// </summary>
        public readonly bool InfiniteRows => rows < 0;

        /// <summary>
        /// Indicates if this grid was configured to have a dynamic number of columns and a fixed number of rows
        /// </summary>
        public readonly bool InfiniteColumns => columns < 0;

        private readonly int rows;
        private readonly int columns;

        /// <summary>
        /// The underlying list wrapped by this <see cref="GridList{T}"/>
        /// </summary>
        public readonly IList<T> Source;

        /// <summary>
        /// The number of rows in the grid
        /// </summary>
        public readonly int Rows
        {
            get
            {
                if (Source == null)
                {
                    return 0;
                }

                if (InfiniteRows)
                {
                    return GetRowIndex(Source.Count);
                }

                return math.min(Source.Count, rows);
            }
        }

        /// <summary>
        /// The number of columns in the grid
        /// </summary>
        public readonly int Columns
        {
            get
            {
                if (Source == null)
                {
                    return 0;
                }

                if (InfiniteColumns)
                {
                    return GetColumnIndex(Source.Count);
                }

                return math.min(Source.Count, columns);
            }
        }

        /// <summary>
        /// Get the element stored at the given <paramref name="row"/> and <paramref name="column"/>
        /// </summary>
        /// <param name="row">The row index</param>
        /// <param name="column">The column index</param>
        /// <returns>The element of type <typeparamref name="T"/> at the given <paramref name="row"/> and <paramref name="column"/></returns>
        /// <exception cref="InvalidOperationException">Thrown when the grid hasn't been constructed via <see cref="CreateWithRows(IList{T}, int)"/> or <see cref="CreateWithColumns(IList{T}, int)"/></exception>
        public readonly T this[int row, int column]
        {
            get
            {
                ThrowIfUnconfigured(this);

                return InfiniteRows ? GetColumn(column)[row] : GetRow(row)[column];
            }
        }

        /// <summary>
        /// Get the element stored at the given <paramref name="index"/>
        /// </summary>
        /// <param name="index">The <see cref="GridIndex"/> of the element to access</param>
        /// <returns>The element of type <typeparamref name="T"/> at the given <paramref name="index"/></returns>
        /// <exception cref="InvalidOperationException">Thrown when the grid hasn't been constructed via <see cref="CreateWithRows(IList{T}, int)"/> or <see cref="CreateWithColumns(IList{T}, int)"/></exception>
        public readonly T this[GridIndex index]
        {
            get
            {
                ThrowIfUnconfigured(this);

                return InfiniteRows ? GetColumn(index.Column)[index.Row] : GetRow(index.Row)[index.Column];
            }
        }

        /// <summary>
        /// Convert a 1D index into the underlying list, <see cref="Source"/>, into a 2D <see cref="GridIndex"/> for the current grid
        /// </summary>
        /// <param name="sourceIndex">The index into the underlying list, <see cref="Source"/></param>
        /// <returns>The <see cref="GridIndex"/> into this grid of the element at the <paramref name="sourceIndex"/> of the underlying list</returns>
        /// <seealso cref="ToSourceIndex(GridIndex)"/>
        /// <exception cref="InvalidOperationException">Thrown when the grid hasn't been constructed via <see cref="CreateWithRows(IList{T}, int)"/> or <see cref="CreateWithColumns(IList{T}, int)"/></exception>
        public GridIndex ToGridIndex(int sourceIndex)
        {
            ThrowIfUnconfigured(this);

            return new GridIndex()
            {
                Row = GetRowIndex(sourceIndex),
                Column = GetColumnIndex(sourceIndex),
            };
        }

        /// <summary>
        /// Convert a 2D <paramref name="gridIndex"/> into the current grid into a 1D index into <see cref="Source"/>
        /// </summary>
        /// <param name="gridIndex">The index into the current grid</param>
        /// <returns>The 1D index in the underlying list of the element at <paramref name="gridIndex"/> in this grid</returns>
        /// <exception cref="InvalidOperationException">Thrown when the grid hasn't been constructed via <see cref="CreateWithRows(IList{T}, int)"/> or <see cref="CreateWithColumns(IList{T}, int)"/></exception>
        public int ToSourceIndex(GridIndex gridIndex)
        {
            ThrowIfUnconfigured(this);

            return InfiniteRows ? (gridIndex.Row * columns) + gridIndex.Column : (gridIndex.Column * rows) + gridIndex.Row;
        }

        /// <summary>
        /// Get the grid row index of the element at <paramref name="sourceIndex"/>
        /// </summary>
        /// <param name="sourceIndex">A 1D index into <see cref="Source"/></param>
        /// <returns>The row into the grid of the element at <paramref name="sourceIndex"/> into <see cref="Source"/></returns>
        /// <exception cref="InvalidOperationException">Thrown when the grid hasn't been constructed via <see cref="CreateWithRows(IList{T}, int)"/> or <see cref="CreateWithColumns(IList{T}, int)"/></exception>
        public readonly int GetRowIndex(int sourceIndex)
        {
            ThrowIfUnconfigured(this);

            return InfiniteRows ? sourceIndex / columns : sourceIndex % rows;
        }

        /// <summary>
        /// Get the grid column index of the element at <paramref name="sourceIndex"/>
        /// </summary>
        /// <param name="sourceIndex">A 1D index into <see cref="Source"/></param>
        /// <returns>The column into the grid of the element at <paramref name="sourceIndex"/> into <see cref="Source"/></returns>
        /// <exception cref="InvalidOperationException">Thrown when the grid hasn't been constructed via <see cref="CreateWithRows(IList{T}, int)"/> or <see cref="CreateWithColumns(IList{T}, int)"/></exception>
        public readonly int GetColumnIndex(int sourceIndex)
        {
            ThrowIfUnconfigured(this);

            return InfiniteColumns ? sourceIndex / rows : sourceIndex % columns;
        }

        /// <summary>
        /// Get a temporary, immutable slice of the current grid containing all elements in the requested <paramref name="row"/>
        /// </summary>
        /// <remarks>Does not make a copy of the elements in the row, just another utility to index into <see cref="Source"/></remarks>
        /// <param name="row">The index of the row to get</param>
        /// <returns>A temporary, immutable slice of the current grid conaining all elements in the requested <paramref name="row"/></returns>
        private readonly Row GetRow(int row)
        {
            int firstIndex = row * Columns;
            int nextIndex = InfiniteRows ? Columns : 1;
            int count = firstIndex + Columns <= Source.Count ? Columns : firstIndex + Columns - Source.Count;
            return new Row(Source, firstIndex, nextIndex, count);
        }

        /// <summary>
        /// Get a temporary, immutable slice of the current grid containing all elements in the requested <paramref name="column"/>
        /// </summary>
        /// <remarks>Does not make a copy of the elements in the column, just another utility to index into <see cref="Source"/></remarks>
        /// <param name="column">The index of the column to get</param>
        /// <returns>A temporary, immutable slice of the current grid conaining all elements in the requested <paramref name="column"/></returns>
        private readonly Column GetColumn(int column)
        {
            int firstIndex = column * Rows;
            int nextIndex = InfiniteColumns ? Rows : 1;
            int count = firstIndex + Rows <= Source.Count ? Rows : firstIndex + Rows - Source.Count;
            return new Column(Source, firstIndex, nextIndex, count);
        }

        /// <summary>
        /// Wraps the provided <paramref name="source"/> in a <see cref="GridList{T}"/> with a dynamic number of rows and a fixed number of <paramref name="columns"/>
        /// </summary>
        /// <param name="source">The underlying list to wrap in a in a <see cref="GridList{T}"/></param>
        /// <param name="columns">The number of columns in the grid</param>
        /// <returns>A new grid wrapping <paramref name="source"/> with a fixed number of <paramref name="columns"/></returns>
        public static GridList<T> CreateWithColumns(IList<T> source, int columns)
        {
            return new GridList<T>(source, -1, math.max(1, columns));
        }

        /// <summary>
        /// Wraps the provided <paramref name="source"/> in a <see cref="GridList{T}"/> with a dynamic number of columns and a fixed number of <paramref name="rows"/>
        /// </summary>
        /// <param name="source">The underlying list to wrap in a in a <see cref="GridList{T}"/></param>
        /// <param name="rows">The number of rows in the grid</param>
        /// <returns>A new grid wrapping <paramref name="source"/> with a fixed number of <paramref name="rows"/></returns>
        public static GridList<T> CreateWithRows(IList<T> source, int rows)
        {
            return new GridList<T>(source, math.max(1, rows), -1);
        }

        private GridList(IList<T> source, int rows = 1, int columns = 1)
        {
            this.Source = source;
            this.rows = math.clamp(rows, -1, int.MaxValue);
            this.columns = math.clamp(columns, -1, int.MaxValue);
        }

        /// <summary>
        /// Throw exception for an invalid grid state
        /// </summary>
        /// 
        /// <exception cref="InvalidOperationException">Thrown when the grid hasn't been constructed via <see cref="CreateWithRows(IList{T}, int)"/> or <see cref="CreateWithColumns(IList{T}, int)"/></exception>
        private static void ThrowIfUnconfigured(GridList<T> grid)
        {
            if (grid.rows != 0 && grid.columns != 0)
            {
                return;
            }

            throw new InvalidOperationException($"{typeof(GridList<T>).Name} has not been initialized. Use {nameof(GridList<T>)}.{nameof(CreateWithRows)} or {nameof(GridList<T>)}.{nameof(CreateWithRows)} to initialize.");
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if <c><paramref name="lhs"/>.Source == <paramref name="rhs"/>.Source &amp;&amp; <paramref name="lhs"/>.Rows == <paramref name="rhs"/>.Rows &amp;&amp; <paramref name="lhs"/>.Columns == <paramref name="rhs"/>.Columns</c></returns>
        public static bool operator ==(GridList<T> lhs, GridList<T> rhs)
        {
            return lhs.Source == rhs.Source && lhs.rows == rhs.rows && lhs.columns == rhs.columns;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if <c><paramref name="lhs"/>.Source != <paramref name="rhs"/>.Source || <paramref name="lhs"/>.Rows != <paramref name="rhs"/>.Rows || <paramref name="lhs"/>.Columns != <paramref name="rhs"/>.Columns</c></returns>
        public static bool operator !=(GridList<T> lhs, GridList<T> rhs)
        {
            return lhs.Source != rhs.Source && lhs.rows != rhs.rows && lhs.columns != rhs.columns;
        }

        /// <summary>
        /// The hashcode for this <see cref="GridList{T}"/>
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + rows.GetHashCode();
            hash = (hash * 7) + columns.GetHashCode();

            if (Source != null)
            {
                hash = (hash * 7) + Source.GetHashCode();
            }

            return hash;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridList{T}"/> to compare</param>
        /// <returns>
        /// <see langword="true"/> if <c>this == <paramref name="other"/></c>
        /// </returns>
        public override bool Equals(object other)
        {
            if (other is GridList<T> grid)
            {
                return this == grid;
            }

            return false;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridList{T}"/> to compare</param>
        /// <returns>
        /// <see langword="true"/> if <c>this == <paramref name="other"/></c>
        /// </returns>
        public bool Equals(GridList<T> other)
        {
            return this == other;
        }
    }


    /// <summary>
    /// An untyped GridList for internal use
    /// </summary>
    internal readonly ref struct GridList
    {
        public readonly int Rows
        {
            get
            {
                if (InfiniteRows)
                {
                    return GetRowIndex(cellCount);
                }

                return math.min(cellCount, rows);
            }
        }

        public readonly int Columns
        {
            get
            {
                if (InfiniteColumns)
                {
                    return GetColumnIndex(cellCount);
                }

                return math.min(cellCount, columns);
            }
        }

        public GridIndex ToGridIndex(int sourceIndex)
        {
            return new GridIndex()
            {
                Row = GetRowIndex(sourceIndex),
                Column = GetColumnIndex(sourceIndex),
            };
        }

        public int ToIndex(GridIndex gridIndex)
        {
            return InfiniteRows ? (gridIndex.Row * columns) + gridIndex.Column : (gridIndex.Column * rows) + gridIndex.Row;
        }

        public readonly int GetRowIndex(int sourceIndex)
        {
            return InfiniteRows ? sourceIndex / columns : sourceIndex % rows;
        }

        public readonly int GetColumnIndex(int sourceIndex)
        {
            return InfiniteColumns ? sourceIndex / rows : sourceIndex % columns;
        }

        public readonly bool InfiniteRows => rows < 0;
        public readonly bool InfiniteColumns => columns < 0;

        private readonly int rows;
        private readonly int columns;
        private readonly int cellCount;

        public static GridList CreateWithInfiniteRows(int cellCount, int columns)
        {
            return new GridList(cellCount, columns: columns);
        }

        private GridList(int cellCount, int rows = -1, int columns = -1)
        {
            this.cellCount = cellCount;
            this.rows = math.clamp(rows, -1, int.MaxValue);
            this.columns = math.clamp(columns, -1, int.MaxValue);
        }
    }
}
