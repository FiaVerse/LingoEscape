// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities.Extensions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal
{
    internal enum LengthType
    {
        Value,
        Percent
    };

    internal interface IRuntimeProperty<T> : IRuntimeProperty
    {
        bool Equals(ref T other);
    }

    internal interface IRuntimeProperty { }

    [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    internal struct LengthBounds : IRuntimeProperty<LengthBounds>
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        internal const int SizeOf = 2 * Length3.SizeOf;

        [FieldOffset(0 * Length.SizeOf)]
        public Length Left;
        [FieldOffset(1 * Length.SizeOf)]
        public Length Right;
        [FieldOffset(2 * Length.SizeOf)]
        public Length Bottom;
        [FieldOffset(3 * Length.SizeOf)]
        public Length Top;
        [FieldOffset(4 * Length.SizeOf)]
        public Length Front;
        [FieldOffset(5 * Length.SizeOf)]
        public Length Back;

        public readonly bool3 IsRelative => MinIsRelative | MaxIsRelative;

        public readonly bool3 MinIsRelative => new bool3(Left.IsRelative, Bottom.IsRelative, Front.IsRelative);
        public readonly bool3 MaxIsRelative => new bool3(Right.IsRelative, Top.IsRelative, Back.IsRelative);

        public readonly float3x2 Value => new float3x2(new float3(Left.Value, Bottom.Value, Front.Value),
                                                       new float3(Right.Value, Top.Value, Back.Value));

        public readonly float3x2 Percent => new float3x2(new float3(Left.Percent, Bottom.Percent, Front.Percent),
                                                         new float3(Right.Percent, Top.Percent, Back.Percent));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(LengthBounds lhs, LengthBounds rhs)
        {
            return !lhs.IsDifferent(ref rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(LengthBounds lhs, LengthBounds rhs)
        {
            return lhs.IsDifferent(ref rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ref LengthBounds other)
        {
            return !this.IsDifferent(ref other, SizeOf);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Calculated Calc(in LengthBounds length, in MinMax minMax, in float3 relativeTo)
        {
            return new Calculated()
            {
                Left = new Length.Calculated(length.Left, minMax.Left, relativeTo.x),
                Right = new Length.Calculated(length.Right, minMax.Right, relativeTo.x),
                Bottom = new Length.Calculated(length.Bottom, minMax.Bottom, relativeTo.y),
                Top = new Length.Calculated(length.Top, minMax.Top, relativeTo.y),
                Front = new Length.Calculated(length.Front, minMax.Front, relativeTo.z),
                Back = new Length.Calculated(length.Back, minMax.Back, relativeTo.z),
            };
        }

        [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
        public struct MinMax : IRuntimeProperty<MinMax>
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
        {
            public const int SizeOf = 2 * Length3.MinMax.SizeOf;

            [FieldOffset(0 * Length.SizeOf)]
            public Length.MinMax Left;
            [FieldOffset(1 * Length.SizeOf)]
            public Length.MinMax Right;
            [FieldOffset(2 * Length.SizeOf)]
            public Length.MinMax Bottom;
            [FieldOffset(3 * Length.SizeOf)]
            public Length.MinMax Top;
            [FieldOffset(4 * Length.SizeOf)]
            public Length.MinMax Front;
            [FieldOffset(5 * Length.SizeOf)]
            public Length.MinMax Back;

            public Length3.MinMax MinEdges => new Length3.MinMax() { X = Left, Y = Bottom, Z = Front };
            public Length3.MinMax MaxEdges => new Length3.MinMax() { X = Right, Y = Top, Z = Back };
            
            public float3x2 Min
            {
                get
                {
                                        //c0      //c1
                    return new float3x2(Left.Min, Right.Min, 
                                        Bottom.Min, Top.Min, 
                                        Front.Min, Back.Min);
                }
                set
                {
                    Left.Min = value.c0.x;
                    Right.Min = value.c1.x;
                    Bottom.Min = value.c0.y;
                    Top.Min = value.c1.y;
                    Front.Min = value.c0.z;
                    Back.Min = value.c1.z;
                }
            }

            public float3x2 Max
            {
                get
                {
                                        //c0      //c1
                    return new float3x2(Left.Max, Right.Max,
                                        Bottom.Max, Top.Max,
                                        Front.Max, Back.Max);
                }
                set
                {
                    Left.Max = value.c0.x;
                    Right.Max = value.c1.x;
                    Bottom.Max = value.c0.y;
                    Top.Max = value.c1.y;
                    Front.Max = value.c0.z;
                    Back.Max = value.c1.z;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(MinMax lhs, MinMax rhs)
            {
                return !lhs.IsDifferent(ref rhs);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(MinMax lhs, MinMax rhs)
            {
                return lhs.IsDifferent(ref rhs);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ref MinMax other)
            {
                return !this.IsDifferent(ref other, SizeOf);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 6 * Length.Calculated.SizeOf)]
        public struct Calculated
        {
            [FieldOffset(0 * Length.Calculated.SizeOf)]
            public Length.Calculated Left;
            [FieldOffset(1 * Length.Calculated.SizeOf)]
            public Length.Calculated Right;
            [FieldOffset(2 * Length.Calculated.SizeOf)]
            public Length.Calculated Bottom;
            [FieldOffset(3 * Length.Calculated.SizeOf)]
            public Length.Calculated Top;
            [FieldOffset(4 * Length.Calculated.SizeOf)]
            public Length.Calculated Front;
            [FieldOffset(5 * Length.Calculated.SizeOf)]
            public Length.Calculated Back;

            public readonly float3x2 Value => new float3x2(new float3(Left.Value, Bottom.Value, Front.Value),
                                                           new float3(Right.Value, Top.Value, Back.Value));

            public readonly float3x2 Percent => new float3x2(new float3(Left.Percent, Bottom.Percent, Front.Percent),
                                                             new float3(Right.Percent, Top.Percent, Back.Percent));

            public readonly float3 MinEdges => new float3(Left.Value, Bottom.Value, Front.Value);
            public readonly float3 MaxEdges => new float3(Right.Value, Top.Value, Back.Value);

            public readonly float3 Size
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float3x2 value = Value;
                    return value.c0 + value.c1;
                }
            }

            public readonly float3 RelativeSize
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float3x2 percent = Percent;
                    return percent.c0 + percent.c1;
                }
            }

            public readonly float3 Offset
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float3x2 value = Value;
                    return 0.5f * (value.c0 - value.c1);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    internal struct Length3 : IRuntimeProperty<Length3>
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        internal const int SizeOf = 3 * Length.SizeOf;

        [FieldOffset(0 * Length.SizeOf)]
        public Length X;
        [FieldOffset(1 * Length.SizeOf)]
        public Length Y;
        [FieldOffset(2 * Length.SizeOf)]
        public Length Z;

        [FieldOffset(0 * Length.SizeOf)]
        public Length2 XY;

        public float3 Raw
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                return new float3(X.Raw, Y.Raw, Z.Raw);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                X.Raw = value.x;
                Y.Raw = value.y;
                Z.Raw = value.z;
            }
        }

        public readonly float3 Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new float3(X.Value, Y.Value, Z.Value);
            }
        }

        public readonly float3 Percent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new float3(X.Percent, Y.Percent, Z.Percent);
            }
        }

        public bool3 IsRelative
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                return math.bool3(X.IsRelative, Y.IsRelative, Z.IsRelative);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                X.IsRelative = value.x;
                Y.IsRelative = value.y;
                Z.IsRelative = value.z;
            }
        }

        public Length this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                switch (index)
                {
                    case 0:
                        return X;
                    case 1:
                        return Y;
                    case 2:
                        return Z;
                }

                return default;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                switch (index)
                {
                    case 0:
                        X = value;
                        break;
                    case 1:
                        Y = value;
                        break;
                    case 2:
                        Z = value;
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 operator ==(Length3 lhs, Length3 rhs)
        {
            return new bool3(lhs.X == rhs.X, lhs.Y == rhs.Y, lhs.Z == rhs.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 operator !=(Length3 lhs, Length3 rhs)
        {
            return new bool3(lhs.X != rhs.X, lhs.Y != rhs.Y, lhs.Z != rhs.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return $"Length3({X.ToString()}, {Y.ToString()}, {Z.ToString()})";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ref Length3 other)
        {
            return !this.IsDifferent(ref other, SizeOf);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetRawFromValue(float3 value, ref Length3 length, ref MinMax minMax, float3 relativeTo)
        {
            return new float3(Length.GetRawFromValue(value.x, ref length.X, ref minMax.X, relativeTo.x),
                              Length.GetRawFromValue(value.y, ref length.Y, ref minMax.Y, relativeTo.y),
                              Length.GetRawFromValue(value.z, ref length.Z, ref minMax.Z, relativeTo.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Calculated Calc(Length3 length, MinMax minMax, float3 relativeTo)
        {
            return new Calculated()
            {
                X = new Length.Calculated(length.X, minMax.X, relativeTo.x),
                Y = new Length.Calculated(length.Y, minMax.Y, relativeTo.y),
                Z = new Length.Calculated(length.Z, minMax.Z, relativeTo.z),
            };
        }

        [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
        public struct MinMax : IRuntimeProperty<MinMax>
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
        {
            internal const int SizeOf = 3 * Length.MinMax.SizeOf;

            [FieldOffset(0 * Length.MinMax.SizeOf)]
            public Length.MinMax X;
            [FieldOffset(1 * Length.MinMax.SizeOf)]
            public Length.MinMax Y;
            [FieldOffset(2 * Length.MinMax.SizeOf)]
            public Length.MinMax Z;

            [FieldOffset(0 * Length.MinMax.SizeOf)]
            public Length2.MinMax XY;

            public float3 Min
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new float3(X.Min, Y.Min, Z.Min);
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    X.Min = value.x;
                    Y.Min = value.y;
                    Z.Min = value.z;
                }
            }

            public float3 Max
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new float3(X.Max, Y.Max, Z.Max);
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    X.Max = value.x;
                    Y.Max = value.y;
                    Z.Max = value.z;
                }
            }

            public Length.MinMax this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                readonly get
                {
                    switch (index)
                    {
                        case 0:
                            return X;
                        case 1:
                            return Y;
                        case 2:
                            return Z;
                    }

                    return default;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    switch (index)
                    {
                        case 0:
                            X = value;
                            break;
                        case 1:
                            Y = value;
                            break;
                        case 2:
                            Z = value;
                            break;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(MinMax lhs, MinMax rhs)
            {
                return lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(MinMax lhs, MinMax rhs)
            {
                return lhs.X != rhs.X || lhs.Y != rhs.Y || lhs.Z != rhs.Z;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static MinMax operator +(MinMax lhs, MinMax rhs)
            {
                return new MinMax() { X = lhs.X + rhs.X, Y = lhs.Y + rhs.Y, Z = lhs.Z + rhs.Z };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
            {
                return $"MinMax3([{X.ToString()}], [{Y.ToString()}], [{Z.ToString()}])";
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float3 Clamp(float3 value)
            {
                return new float3(X.Clamp(value.x), Y.Clamp(value.y), Z.Clamp(value.z));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float2 Clamp(float2 value)
            {
                return new float2(X.Clamp(value.x), Y.Clamp(value.y));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ref MinMax other)
            {
                return !this.IsDifferent(ref other, SizeOf);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
        public struct Calculated
        {
            internal const int SizeOf = 3 * Length.Calculated.SizeOf;

            [FieldOffset(0 * Length.Calculated.SizeOf)]
            public Length.Calculated X;
            [FieldOffset(1 * Length.Calculated.SizeOf)]
            public Length.Calculated Y;
            [FieldOffset(2 * Length.Calculated.SizeOf)]
            public Length.Calculated Z;

            [FieldOffset(0 * Length.Calculated.SizeOf)]
            public Length2.Calculated XY;

            public Length.Calculated this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                readonly get
                {
                    switch (index)
                    {
                        case 0:
                            return X;
                        case 1:
                            return Y;
                        case 2:
                            return Z;
                    }

                    return default;
                }
                set 
                {
                    switch (index)
                    {
                        case 0:
                            X = value;
                            break;
                        case 1:
                            Y = value;
                            break;
                        case 2:
                            Z = value;
                            break;
                    }
                }
            }

            public readonly float3 Value
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return new float3(X.Value, Y.Value, Z.Value);
                }
            }

            public readonly float3 Percent
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return new float3(X.Percent, Y.Percent, Z.Percent);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
            {
                return $"Calc3([{X.ToString()}], [{Y.ToString()}], [{Z.ToString()}])";
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    internal struct Length2 : IRuntimeProperty<Length2>
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        internal const int SizeOf = 2 * Length.SizeOf;

        [FieldOffset(0 * Length.SizeOf)]
        public Length X;
        [FieldOffset(1 * Length.SizeOf)]
        public Length Y;

        public float2 Raw
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                return new float2(X.Raw, Y.Raw);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                X.Raw = value.x;
                Y.Raw = value.y;
            }
        }

        public readonly float2 Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new float2(X.Value, Y.Value);
            }
        }

        public readonly float2 Percent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new float2(X.Percent, Y.Percent);
            }
        }

        public bool2 IsRelative
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                return new bool2(X.IsRelative, Y.IsRelative);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                X.IsRelative = value.x;
                Y.IsRelative = value.y;

            }
        }

        public Length this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                switch (index)
                {
                    case 0:
                        return X;
                    case 1:
                        return Y;
                }

                return default;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                switch (index)
                {
                    case 0:
                        X = value;
                        break;
                    case 1:
                        Y = value;
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 operator ==(Length2 lhs, Length2 rhs)
        {
            return new bool2(lhs.X == rhs.X, lhs.Y == rhs.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 operator !=(Length2 lhs, Length2 rhs)
        {
            return new bool2(lhs.X != rhs.X, lhs.Y != rhs.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return $"Length3({X.ToString()}, {Y.ToString()})";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ref Length2 other)
        {
            return !this.IsDifferent(ref other, SizeOf);
        }

        public static float2 GetRawFromValue(float2 value, ref Length2 length, ref MinMax minMax, float2 relativeTo)
        {
            return new float2(Length.GetRawFromValue(value.x, ref length.X, ref minMax.X, relativeTo.x),
                              Length.GetRawFromValue(value.y, ref length.Y, ref minMax.Y, relativeTo.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Calculated Calc(Length2 length, MinMax minMax, float2 relativeTo)
        {
            return new Calculated()
            {
                X = new Length.Calculated(length.X, minMax.X, relativeTo.x),
                Y = new Length.Calculated(length.Y, minMax.Y, relativeTo.y),
            };
        }

        [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
        public struct MinMax : IRuntimeProperty<MinMax>
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
        {
            internal const int SizeOf = 2 * Length.MinMax.SizeOf;

            [FieldOffset(0 * Length.MinMax.SizeOf)]
            public Length.MinMax X;
            [FieldOffset(1 * Length.MinMax.SizeOf)]
            public Length.MinMax Y;

            public float2 Min
            {
                get => new float2(X.Min, Y.Min);
                set
                {
                    X.Min = value.x;
                    Y.Min = value.y;
                }
            }

            public float2 Max
            {
                get => new float2(X.Max, Y.Max);
                set
                {
                    X.Max = value.x;
                    Y.Max = value.y;
                }
            }

            public Length.MinMax this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                readonly get
                {
                    switch (index)
                    {
                        case 0:
                            return X;
                        case 1:
                            return Y;
                    }

                    return default;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    switch (index)
                    {
                        case 0:
                            X = value;
                            break;
                        case 1:
                            Y = value;
                            break;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(MinMax lhs, MinMax rhs)
            {
                return lhs.X == rhs.X && lhs.Y == rhs.Y;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(MinMax lhs, MinMax rhs)
            {
                return lhs.X != rhs.X || lhs.Y != rhs.Y;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
            {
                return $"MinMax2([{X.ToString()}], [{Y.ToString()}])";
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float2 Clamp(float2 value)
            {
                return new float2(X.Clamp(value.x), Y.Clamp(value.y));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ref MinMax other)
            {
                return !this.IsDifferent(ref other, SizeOf);
            }

            public static readonly MinMax Unclamped = new MinMax() { Min = float.NegativeInfinity, Max = float.PositiveInfinity };
            public static readonly MinMax Positive = new MinMax() { Min = 0, Max = float.PositiveInfinity };
        }

        [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
        public struct Calculated
        {
            internal const int SizeOf = 2 * Length.Calculated.SizeOf;

            [FieldOffset(0 * Length.Calculated.SizeOf)]
            public Length.Calculated X;
            [FieldOffset(1 * Length.Calculated.SizeOf)]
            public Length.Calculated Y;

            [FieldOffset(0 * Length.Calculated.SizeOf)]
            public Length.Calculated First;
            [FieldOffset(1 * Length.Calculated.SizeOf)]
            public Length.Calculated Second;

            public readonly float2 Value => new float2(X.Value, Y.Value);
            public readonly float2 Percent => new float2(X.Percent, Y.Percent);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
            {
                return $"Calc2([{X.ToString()}], [{Y.ToString()}])";
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    internal struct Length
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        internal const int SizeOf = 8;

        [FieldOffset(0 * sizeof(float))]
        public float Raw;
        [FieldOffset(1 * sizeof(float))]
        public LengthType Type;

        public bool IsRelative
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                return Type == LengthType.Percent;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Type = value ? LengthType.Percent : LengthType.Value;
            }
        }

        public float Value => Type == LengthType.Value ? Raw : 0;
        public float Percent => Type == LengthType.Percent ? Raw : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Length lhs, Length rhs)
        {
            return lhs.Raw == rhs.Raw && lhs.Type == rhs.Type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Length lhs, Length rhs)
        {
            return lhs.Raw != rhs.Raw || lhs.Type != rhs.Type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float GetRawFromValue(float value, ref Length length, ref MinMax minMax, float relativeTo)
        {
            return length.Type == LengthType.Percent ? minMax.Clamp(value) / relativeTo : minMax.Clamp(value);
        }

        public override string ToString()
        {
            return IsRelative ? $"{(100 * Raw).ToString()}%" : $"{Raw.ToString()}f";
        }

        [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661  // Type defines operator == or operator != but does not override Object.Equals(object o)
        public struct MinMax
#pragma warning restore CS0660, CS0661  // Type defines operator == or operator != but does not override Object.Equals(object o)
        {
            internal const int SizeOf = 2 * sizeof(float);

            [FieldOffset(0 * sizeof(float))]
            public float Min;
            [FieldOffset(1 * sizeof(float))]
            public float Max;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(MinMax lhs, MinMax rhs)
            {
                return lhs.Min == rhs.Min && lhs.Max == rhs.Max;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(MinMax lhs, MinMax rhs)
            {
                return lhs.Min != rhs.Min || lhs.Max != rhs.Max;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static MinMax operator +(MinMax lhs, MinMax rhs)
            {
                return new MinMax() { Min = lhs.Min + rhs.Min, Max = lhs.Max + rhs.Max };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly float Clamp(float value)
            {
                if (float.IsInfinity(value))
                {
                    if (float.IsInfinity(Min))
                    {
                        return float.IsInfinity(Max) ? 0 : Max;
                    }

                    return Min;
                }

                return Min > value ? Min :
                       Max < value ? Max :
                       value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Clamp(float f, float min, float max)
            {
                return new MinMax() { Min = min, Max = max }.Clamp(f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
            {
                return this == Unclamped ? "Unclamped" :
                       this == Positive ? "Positive" :
                       $"Min = {Min.ToString()}f, Max = {Max.ToString()}f";
            }

            public static readonly MinMax Unclamped = new MinMax() { Min = float.NegativeInfinity, Max = float.PositiveInfinity };
            public static readonly MinMax Positive = new MinMax() { Min = 0, Max = float.PositiveInfinity };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator float2(MinMax minMax)
            {
                return new float2(minMax.Min, minMax.Max);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator MinMax(float2 minMax)
            {
                return new MinMax() { Min = minMax.x, Max = minMax.y };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static explicit operator MinMax(float minMax)
            {
                return new MinMax() { Min = minMax, Max = minMax };
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = SizeOf)]
#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
        public readonly struct Calculated
#pragma warning restore CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
        {
            internal const int SizeOf = 2 * sizeof(float);

            [FieldOffset(0 * sizeof(float))]
            public readonly float Value;
            [FieldOffset(1 * sizeof(float))]
            public readonly float Percent;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(Calculated lhs, Calculated rhs)
            {
                return lhs.Value == rhs.Value && lhs.Percent == rhs.Percent;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(Calculated lhs, Calculated rhs)
            {
                return lhs.Value != rhs.Value || lhs.Percent != rhs.Percent;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
            {
                return $"Value = {Value.ToString()}f, {(100 * Percent).ToString()}%";
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Calculated(in Length length, in MinMax minMax, float relativeTo)
            {
                bool relativeToZero = relativeTo == 0;

                float toClamp = length.IsRelative ? relativeToZero || length.Raw == 0 ? 0 : length.Raw.MultipliedBy(relativeTo) : length.Raw;

                Value = minMax.Clamp(toClamp);
                Percent = relativeToZero || Value == 0 ? 0 : Value / relativeTo;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Calculated(float value, float percent)
            {
                Value = value;
                Percent = percent;
            }
        }
    }

    [BurstCompile]
    internal static class PropertyExtensions
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDifferent<T>(ref this T property, ref T propertyToDiff) where T : unmanaged, IRuntimeProperty
        {
            unsafe
            {
                T* propPtr = (T*)UnsafeUtility.AddressOf(ref property);
                T* toDiffPtr = (T*)UnsafeUtility.AddressOf(ref propertyToDiff);
                return UnsafeUtility.MemCmp(propPtr, toDiffPtr, UnsafeUtility.SizeOf<T>()) != 0;
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDifferent<T>(ref this T property, ref T propertyToDiff, long sizeOf) where T : unmanaged, IRuntimeProperty
        {
            unsafe
            {
                T* propPtr = (T*)UnsafeUtility.AddressOf(ref property);
                T* toDiffPtr = (T*)UnsafeUtility.AddressOf(ref propertyToDiff);
                return UnsafeUtility.MemCmp(propPtr, toDiffPtr, sizeOf) != 0;
            }
        }
    }
}
