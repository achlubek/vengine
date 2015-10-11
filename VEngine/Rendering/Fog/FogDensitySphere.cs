﻿using System.Drawing;
using OpenTK;

namespace VEngine.Fog
{
    internal class FogDensitySphere
    {
        public Color Color;
        public float Density, Noise, NoiseSize, Radius, EdgeSmoothness;
        public Vector3 Position, Velocity;

        public FogDensitySphere(Vector3 position, float radius, float edgeSmoothness, Vector3 velocity, float density, float noise, float noiseSize, Color color)
        {
            Position = position;
            Radius = radius;
            EdgeSmoothness = edgeSmoothness;
            Velocity = velocity;
            Density = density;
            Noise = noise;
            NoiseSize = noiseSize;
            Color = color;
        }

        public FogDensitySphere(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
            EdgeSmoothness = 0;
            Velocity = Vector3.Zero;
            Density = 1;
            Noise = 0;
            NoiseSize = 0;
            Color = Color.White;
        }
    }
}