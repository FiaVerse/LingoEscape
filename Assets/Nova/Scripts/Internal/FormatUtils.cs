// Copyright (c) Supernova Technologies LLC
using System;
using System.Globalization;

namespace Nova
{
    internal static class FormatUtils
    {
        public const string FloatFormat = "F2";
        public static readonly IFormatProvider Formatter = CultureInfo.InvariantCulture.NumberFormat;
    }
}
