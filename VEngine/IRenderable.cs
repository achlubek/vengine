﻿using OpenTK;

namespace VDGTech
{
    public interface IRenderable
    {
        void Draw(Matrix4 transformation);
    }
}