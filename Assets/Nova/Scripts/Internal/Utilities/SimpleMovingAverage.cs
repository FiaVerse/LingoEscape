// Copyright (c) Supernova Technologies LLC
using UnityEngine;

namespace Nova.Internal.Utilities
{
    internal struct SimpleMovingAverage
    {
        private double current;
        public readonly double Weight;

        public float Value
        {
            get
            {
                return (float)current;
            }
            set
            {
                current = value;
            }
        }

        public SimpleMovingAverage(double start = 0, float sampleWeight = 0.5f)
        {
            current = start;
            Weight = Mathf.Clamp(sampleWeight, 0, 1);

        }

        public void AddSample(double sample)
        {
            current = (current * (1 - Weight)) + (Weight * sample);
        }
    }
}
