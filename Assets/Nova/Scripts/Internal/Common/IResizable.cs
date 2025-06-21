// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Common
{
    internal interface IResizable : IClearable
    {
        int Length { set; }
    }

    internal interface IClearable
    {
        void Clear();
    }
}

