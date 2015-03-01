﻿using System;
using System.Linq;
using System.Collections.Generic;
using OpenTK;

namespace VDGTech
{
    public class InstancedMesh3d : IRenderable
    {
        public InstancedMesh3d(Object3dInfo objectInfo, IMaterial material)
        {
            Randomizer = new Random();
            Instances = 1;
            ObjectInfo = objectInfo;
            Material = material;
            UpdateMatrix();
        }

        public int Instances;
        public IMaterial Material;
        public List<Matrix4> Matrix, RotationMatrix;
        public List<float> Scales = new List<float>();
        private Object3dInfo ObjectInfo;
        public List<Quaternion> Orientations = new List<Quaternion>();
        public List<Vector3> Positions = new List<Vector3>();
        private Random Randomizer;
        public float SpecularSize = 1.0f, SpecularComponent = 1.0f, DiffuseComponent = 1.0f;

        public bool HasBeenModified
        {
            get;
            private set;
        }

        public void Draw()
        {
            if(Instances < 2)
                return;
            if(HasBeenModified)
            {
                UpdateMatrix();
                HasBeenModified = false;
            }
            if(Camera.Current == null)
                return;
            ShaderProgram shader = Material.GetShaderProgram();
            Material.Use();
            shader.SetUniform("ViewMatrix", Camera.Current.ViewMatrix);
            shader.SetUniform("ProjectionMatrix", Camera.Current.ProjectionMatrix);
            shader.SetUniform("LogEnchacer", 0.01f);
            shader.SetUniform("SpecularSize", SpecularSize);
            shader.SetUniform("SpecularComponent", SpecularComponent);
            shader.SetUniform("DiffuseComponent", DiffuseComponent);
            shader.SetUniformArray("LightsPs", LightPool.GetPMatrices());
            shader.SetUniformArray("LightsVs", LightPool.GetVMatrices());
            shader.SetUniformArray("LightsPos", LightPool.GetPositions());
            shader.SetUniformArray("LightsFarPlane", LightPool.GetFarPlanes());
            shader.SetUniformArray("LightsColors", LightPool.GetColors());
            shader.SetUniform("LightsCount", LightPool.GetPositions().Length);

            shader.SetUniform("CameraPosition", Camera.Current.Transformation.GetPosition());
            shader.SetUniform("FarPlane", Camera.Current.Far);
            shader.SetUniform("Time", (float)(DateTime.Now - GLThread.StartTime).TotalMilliseconds / 1000);
            shader.SetUniform("RandomSeed", (float)Randomizer.NextDouble());
            shader.SetUniform("resolution", GLThread.Resolution);
            shader.SetUniform("Instances", Instances);

            for(int i = 0; i < Instances; i += 1024)
            {
                shader.SetUniformArray("ModelMatrixes", Matrix.Skip(i).ToArray());
                shader.SetUniformArray("RotationMatrixes", RotationMatrix.Skip(i).ToArray());
                ObjectInfo.DrawInstanced(Math.Min(Instances - i, 1024));
     
            }
            GLThread.CheckErrors();
        }

        public Quaternion GetOrientation(int index)
        {
            return Orientations[index];
        }

        public Vector3 GetPosition(int index)
        {
            return Positions[index];
        }

        public InstancedMesh3d Rotate(int index, Quaternion rotation)
        {
            Orientations[index] = Quaternion.Multiply(Orientations[index], rotation);
            HasBeenModified = true;
            return this;
        }

        public InstancedMesh3d SetOrientation(int index, Quaternion orientation)
        {
            Orientations[index] = orientation;
            HasBeenModified = true;
            return this;
        }

        public InstancedMesh3d SetPosition(int index, Vector3 position)
        {
            Positions[index] = position;
            HasBeenModified = true;
            return this;
        }

        public InstancedMesh3d SetScale(int index, float scale)
        {
            Scales[index] = scale;
            UpdateMatrix();
            return this;
        }

        public InstancedMesh3d Translate(int index, Vector3 translation)
        {
            Positions[index] += translation;
            HasBeenModified = true;
            return this;
        }

        public void UpdateMatrix()
        {
            RotationMatrix = new List<Matrix4>();
            Matrix = new List<Matrix4>();
            if(Instances > Positions.Count)
                Instances = Positions.Count;
            for(int i = 0; i < Instances; i++)
            {
                RotationMatrix.Add(Matrix4.CreateFromQuaternion(Orientations[i]));
                Matrix.Add(RotationMatrix[i] * Matrix4.CreateScale(Scales[i]) * Matrix4.CreateTranslation(Positions[i]));
            }
        }
    }
}