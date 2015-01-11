﻿namespace VDGTech
{
    public class PostProcessLoadingMaterial : IMaterial
    {
        private ShaderProgram Program;

        public PostProcessLoadingMaterial()
        {
            Program = new ShaderProgram(Media.ReadAllText("post.vs"), Media.ReadAllText("spheres.fs"));
        }

        public ShaderProgram GetShaderProgram()
        {
            return Program;
        }

        public void Use()
        {
            Program.Use();
        }
    }
}