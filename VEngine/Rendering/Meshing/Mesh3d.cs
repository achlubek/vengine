﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;

namespace VEngine
{
    public class Mesh3d : IRenderable
    {
        private List<Mesh3dInstance> Instances;

        private List<LodLevel> LodLevels;

        public bool AutoRecalculateMatrixForOver16Instances = false;

        private Mesh3d()
        {
            LodLevels = new List<LodLevel>();
            Instances = new List<Mesh3dInstance>();
        }

        public void IterationSortInstancesByDistanceFrom(Vector3 point, int iterations)
        {
            Mesh3dInstance tmp = null;
            var cnt = Instances.Count - 1;// this is ok
            for(int x = 0; x < iterations; x++)
            {
                for(int i = 0; i < cnt; i++)
                {
                    float dst1 = (Instances[i].Transformation.Position - point).Length;
                    float dst2 = (Instances[i + 1].Transformation.Position - point).Length;
                    if(dst1 < dst2)
                    {
                        tmp = Instances[i];
                        Instances[i] = Instances[i + 1];
                        Instances[i + 1] = tmp;
                    }
                }
            }
        }

        public void FullSortInstancesByDistanceFrom(Vector3 point)
        {
            Instances.Sort((a, b) =>
            {
                float dst1 = (a.Transformation.Position - point).Length;
                float dst2 = (b.Transformation.Position - point).Length;
                if(dst1 < dst2)
                {
                    return 1;
                }
                else
                    return -1;
            });


        }

        public static Mesh3d Empty
        {
            get
            {
                return new Mesh3d();
            }
        }

        public static Mesh3d Create(Object3dInfo objectInfo, GenericMaterial material)
        {
            var m = new Mesh3d();
            m.AddLodLevel(objectInfo, material, 0, 99999);
            m.AddInstance(new Mesh3dInstance(new TransformationManager(Vector3.Zero), ""));
            return m;
        }

        public void AddInstance(Mesh3dInstance instance)
        {
            Instances.Add(instance);
        }

        public Mesh3dInstance AddInstance(TransformationManager transformation, string name = "unnamed")
        {
            var i = new Mesh3dInstance(transformation, name);
            Instances.Add(i);
            return i;
        }

        public void AddLodLevel(LodLevel level)
        {
            LodLevels.Add(level);
        }

        public LodLevel AddLodLevel(Object3dInfo info, GenericMaterial material, float distStart, float distEnd)
        {
            var i = new LodLevel(info, material, distStart, distEnd);
            LodLevels.Add(i);
            return i;
        }

        public void ClearInstances()
        {
            Instances.Clear();
        }

        public void ClearLodLevels()
        {
            LodLevels.Clear();
        }

        public void Draw()
        {
            for(int i = 0; i < LodLevels.Count; i++)
                LodLevels[i].Draw(this, Instances.Count);
        }

        public Mesh3dInstance GetInstance(int i)
        {
            return Instances[i];
        }

        public List<Mesh3dInstance> GetInstances()
        {
            return Instances;
        }

        public LodLevel GetLodLevel(int i)
        {
            return LodLevels[i];
        }

        public List<LodLevel> GetLodLevels()
        {
            return LodLevels;
        }

        // Instance Managing
        public void RemoveInstance(Mesh3dInstance instance)
        {
            Instances.Remove(instance);
        }

        // lod managing
        public void RemoveLodLevel(LodLevel level)
        {
            LodLevels.Remove(level);
        }

        public int GIContainer = -1;

        public void SetUniforms()
        {
            if(Instances.Count < 16 || AutoRecalculateMatrixForOver16Instances)
            {
                // rework
                if(Instances.Count((a) => a.Transformation.HasBeenModified()) > 0)
                    UpdateMatrix(true);
                Instances.ForEach((a) => a.Transformation.ClearModifiedFlag());
            }

            var shader = ShaderProgram.Current;
            shader.SetUniform("Instances", Instances.Count);
            shader.SetUniform("GIContainer", GIContainer);
        }

        // rest
        public void UpdateMatrix(bool instantRebuffer = false)
        {
            for(int i = 0; i < LodLevels.Count; i++)
                LodLevels[i].UpdateMatrix(Instances, instantRebuffer);
        }

        public void UpdateMatrixSingleLodLevel(int level, bool instantRebuffer = false)
        {
            LodLevels[level].UpdateMatrix(Instances, instantRebuffer);
        }
    }
}