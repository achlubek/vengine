﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

//using BEPUutilities;
using BulletSharp;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace VEngine
{
    public class VertexInfo
    {
        public Vector3 Position, Normal;
        public Vector2 UV;

        public List<float> ToFloatList()
        {
            return new List<float> { Position.X, Position.Y, Position.Z, UV.X, UV.Y, Normal.X, Normal.Y, Normal.Z };
        }

        public static List<float> ToFloatList(VertexInfo[] vbo)
        {
            List<float> bytes = new List<float>(vbo.Length * 8);
            foreach(var v in vbo)
                bytes.AddRange(v.ToFloatList());
            return bytes;
        }
        public static List<float> ToFloatList(List<VertexInfo> vbo)
        {
            List<float> bytes = new List<float>(vbo.Count * 8);
            foreach(var v in vbo)
                bytes.AddRange(v.ToFloatList());
            return bytes;
        }

        public static List<VertexInfo> FromFloatArray(float[] vbo)
        {
            List<VertexInfo> vs = new List<VertexInfo>(vbo.Length / 8);
            var cnt = vbo.Length;
            for(int i = 0; i < cnt; i += 8)
            {
                var v = new VertexInfo();
                v.Position.X = vbo[i];
                v.Position.Y = vbo[i + 1];
                v.Position.Z = vbo[i + 2];
                v.UV.X = vbo[i + 3];
                v.UV.Y = vbo[i + 4];
                v.Normal.X = vbo[i + 5];
                v.Normal.Y = vbo[i + 6];
                v.Normal.Z = vbo[i + 7];
                vs.Add(v);
            }
            return vs;
        }
    }
    public class Object3dManager
    {
        public List<VertexInfo> Vertices;

        public AxisAlignedBoundingBox AABB;

        public string Name = "unnamed";

        private BvhTriangleMeshShape CachedBvhTriangleMeshShape;

        public static Object3dManager Empty
        {
            get
            {
                return new Object3dManager(new VertexInfo[0]);
            }
        }

        public struct AxisAlignedBoundingBox
        {
            public Vector3 Minimum, Maximum;
        }

        public class MaterialInfo
        {
            public string AlphaMask;

            public Color DiffuseColor, SpecularColor, AmbientColor;

            public string TextureName, BumpMapName, NormapMapName, SpecularMapName;

            public float Transparency, SpecularStrength;

            public MaterialInfo()
            {
                DiffuseColor = Color.White;
                SpecularColor = Color.White;
                AmbientColor = Color.White;
                Transparency = 1.0f;
                SpecularStrength = 1.0f;
                TextureName = "";
                BumpMapName = "";
                NormapMapName = "";
                SpecularMapName = "";
                AlphaMask = "";
            }
        }

        private class ObjFileData
        {
            public string Name, MaterialName;
            public List<VertexInfo> VBO;
        }

        public Object3dManager(VertexInfo[] vertices)
        {
            Vertices = vertices.ToList();
        }
        public Object3dManager(List<VertexInfo> vertices)
        {
            Vertices = vertices;
        }

        public static Object3dManager[] LoadFromObj(string infile)
        {
            string[] lines = File.ReadAllLines(infile);
            var data = ParseOBJString(lines);
            return data.Select<ObjFileData, Object3dManager>(a => new Object3dManager(a.VBO)).ToArray();
        }

        public static Object3dManager LoadFromObjSingle(string infile)
        {
            string[] lines = File.ReadAllLines(infile);
            var data = ParseOBJStringSingle(lines);
            return new Object3dManager(data.VBO);
        }

        public static Object3dManager LoadFromRaw(string vboFile)
        {
            var vboBytes = File.ReadAllBytes(vboFile);

            var vboFloats = new float[vboBytes.Length / 4];
            Buffer.BlockCopy(vboBytes, 0, vboFloats, 0, vboBytes.Length);

            return new Object3dManager(VertexInfo.FromFloatArray(vboFloats));
        }

        public void SaveRaw(string outfile)
        {

            MemoryStream vboStream = new MemoryStream();

            foreach(var v in Vertices)
                foreach(var v2 in v.ToFloatList())
                    vboStream.Write(BitConverter.GetBytes(v2), 0, 4);

            vboStream.Flush();
            File.WriteAllBytes(outfile, vboStream.ToArray());
        }

        public static Dictionary<string, MaterialInfo> LoadMaterialsFromMtl(string filename)
        {
            Dictionary<string, MaterialInfo> materials = new Dictionary<string, MaterialInfo>();
            MaterialInfo currentMaterial = new MaterialInfo();
            string currentName = "";
            string[] lines = File.ReadAllLines(filename);
            Match match;
            foreach(string line in lines)
            {
                if(line.StartsWith("newmtl"))
                {
                    match = Regex.Match(line, @"newmtl (.+)");
                    if(currentName != "")
                    {
                        materials.Add(currentName, currentMaterial);
                        currentMaterial = new MaterialInfo();
                    }
                    currentName = match.Groups[1].Value;
                }
                if(line.StartsWith("Ns"))
                {
                    match = Regex.Match(line, @"Ns ([0-9.-]+)");
                    float val = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    //currentMaterial.SpecularStrength = val;
                }
                if(line.StartsWith("d"))
                {
                    match = Regex.Match(line, @"d ([0-9.-]+)");
                    float val = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    // currentMaterial.Transparency = val;
                }
                if(line.StartsWith("Ka"))
                {
                    match = Regex.Match(line, @"Ka ([0-9.-]+) ([0-9.-]+) ([0-9.-]+)");
                    int r = (int)(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    int g = (int)(float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    int b = (int)(float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    if(r > 255)
                        r = 255;
                    if(g > 255)
                        g = 255;
                    if(b > 255)
                        b = 255;
                    // currentMaterial.AmbientColor = Color.FromArgb(r, g, b);
                }
                if(line.StartsWith("Kd"))
                {
                    match = Regex.Match(line, @"Kd ([0-9.-]+) ([0-9.-]+) ([0-9.-]+)");
                    int r = (int)(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    int g = (int)(float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    int b = (int)(float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    if(r > 255)
                        r = 255;
                    if(g > 255)
                        g = 255;
                    if(b > 255)
                        b = 255;
                    currentMaterial.DiffuseColor = Color.FromArgb(r, g, b);
                }
                if(line.StartsWith("Ks"))
                {
                    match = Regex.Match(line, @"Ks ([0-9.-]+) ([0-9.-]+) ([0-9.-]+)");
                    int r = (int)(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    int g = (int)(float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    int b = (int)(float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
                    if(r > 255)
                        r = 255;
                    if(g > 255)
                        g = 255;
                    if(b > 255)
                        b = 255;
                    //currentMaterial.SpecularColor = Color.FromArgb(r, g, b);
                }
                if(line.StartsWith("map_Kd"))
                {
                    match = Regex.Match(line, @"map_Kd (.+)");
                    currentMaterial.TextureName = Path.GetFileName(match.Groups[1].Value);
                }
                if(line.StartsWith("map_d"))
                {
                    match = Regex.Match(line, @"map_d (.+)");
                    currentMaterial.AlphaMask = Path.GetFileName(match.Groups[1].Value);
                }
                if(line.StartsWith("map_Bump"))
                {
                    match = Regex.Match(line, @"map_Bump (.+)");
                    currentMaterial.BumpMapName = Path.GetFileName(match.Groups[1].Value);
                }
            }
            if(currentName != "")
                materials.Add(currentName, currentMaterial);
            return materials;
        }
        
        public static List<Mesh3d> LoadSceneFromObj(string objfile, string mtlfile, float scale = 1.0f)
        {
            string[] lines = File.ReadAllLines(objfile);
            var objs = ParseOBJString(lines);
            var mtllib = LoadMaterialsFromMtl(mtlfile);
            List<Mesh3d> meshes = new List<Mesh3d>();
            Dictionary<string, GenericMaterial> texCache = new Dictionary<string, GenericMaterial>();
            Dictionary<Color, GenericMaterial> colorCache = new Dictionary<Color, GenericMaterial>();
            Dictionary<GenericMaterial, MaterialInfo> mInfos = new Dictionary<GenericMaterial, MaterialInfo>();
            Dictionary<GenericMaterial, List<Object3dManager>> linkCache = new Dictionary<GenericMaterial, List<Object3dManager>>();
            var colorPink = new GenericMaterial(Color.Pink);
            mInfos = new Dictionary<GenericMaterial, MaterialInfo>();
            foreach(var obj in objs)
            {
                var mat = mtllib.ContainsKey(obj.MaterialName) ? mtllib[obj.MaterialName] : null;
                GenericMaterial material = null;
                if(mat != null && mat.TextureName.Length > 0)
                {
                    if(texCache.ContainsKey(mat.TextureName + mat.AlphaMask))
                    {
                        material = texCache[mat.TextureName + mat.AlphaMask];

                        material.Name = obj.MaterialName;
                        mInfos[material] = mat;
                    }
                    else
                    {
                        var m = GenericMaterial.FromMedia(Path.GetFileName(mat.TextureName));
                        m.NormalMapScale = 10;
                        material = m;

                        material.Name = obj.MaterialName;
                        mInfos[material] = mat;
                        texCache.Add(mat.TextureName + mat.AlphaMask, material);
                        // material = colorPink;
                    }
                    //material = new GenericMaterial(Color.Pink);
                }
                else if(mat != null)
                {
                    if(colorCache.ContainsKey(mat.DiffuseColor))
                    {
                        material = colorCache[mat.DiffuseColor];
                        mInfos[material] = mat;
                    }
                    else
                    {
                        material = new GenericMaterial(mat.DiffuseColor);
                        mInfos[material] = mat;
                        //  colorCache.Add(mat.DiffuseColor, material);
                    }
                }
                else
                {
                    material = colorPink;
                    mInfos[material] = mat;
                }

                for(int i = 0; i < obj.VBO.Count; i ++)
                {
                    obj.VBO[i].Position *= scale;
                }
                var o3di = new Object3dManager(obj.VBO);
                o3di.Name = obj.Name;
                if(!linkCache.ContainsKey(material))
                    linkCache.Add(material, new List<Object3dManager> { o3di });
                else
                    linkCache[material].Add(o3di);
            }
            foreach(var kv in linkCache)
            {
                Object3dManager o3di = kv.Value[0];
                if(kv.Value.Count > 1)
                {
                    foreach(var e in kv.Value.Skip(1))
                        o3di.Append(e);
                }
               // var trans = o3di.GetAverageTranslationFromZero();
               // o3di.OriginToCenter();
                //o3di.CorrectFacesByNormals();
                // o3di.CorrectFacesByNormals();
                var oi = new Object3dInfo(o3di.Vertices);
                oi.Manager = o3di;
                Mesh3d mesh = Mesh3d.Create(oi, kv.Key);
                //kv.Key.SpecularComponent = 1.0f - mInfos[kv.Key].SpecularStrength + 0.01f;
                kv.Key.Roughness = (1);
                // kv.Key.ReflectionStrength = 1.0f - (mInfos[kv.Key].SpecularStrength);
                //kv.Key.DiffuseComponent = mInfos[kv.Key].DiffuseColor.GetBrightness() + 0.01f;
                var kva = kv.Key;
                if(!mInfos.ContainsKey(kva))
                    kva = mInfos.Keys.First();
                if(mInfos[kva].BumpMapName.Length > 1)
                    ((GenericMaterial)kv.Key).SetBumpMapFromMedia(mInfos[kv.Key].BumpMapName);
                // mesh.SpecularComponent = kv.Key.SpecularStrength;
              //  mesh.GetInstance(0).Translate(trans);
                // mesh.SetCollisionShape(o3di.GetConvexHull(mesh.Transformation.GetPosition(),
                // 1.0f, 1.0f));
                meshes.Add(mesh);
            }
            return meshes;
        }

        public void Append(Object3dManager info)
        {
            Vertices.AddRange(info.Vertices);
        }

        public Object3dManager Copy()
        {
            return new Object3dManager(Vertices);
        }

        public Object3dManager CopyDeep()
        {
            return new Object3dManager(Vertices.ToArray());
        }
        
        public void FlipFaces()
        {
            for(int i = 0; i < Vertices.Count; i += 3)
            {
                var tmp = Vertices[i];
                Vertices[i] = Vertices[i + 2];
                Vertices[i + 2] = tmp;
            }
        }
        
        public BvhTriangleMeshShape GetAccurateCollisionShape()
        {
            //if (CachedBvhTriangleMeshShape != null) return CachedBvhTriangleMeshShape;
            List<Vector3> vectors = GetRawVertexList();
            var smesh = new TriangleIndexVertexArray(Enumerable.Range(0, Vertices.Count).ToArray(), vectors.Select((a) => a).ToArray());
            CachedBvhTriangleMeshShape = new BvhTriangleMeshShape(smesh, false);
            //CachedBvhTriangleMeshShape.LocalScaling = new Vector3(scale);
            return CachedBvhTriangleMeshShape;
        }

        public Vector3 GetAverageTranslationFromZero()
        {
            float averagex = 0, averagey = 0, averagez = 0;
            for(int i = 0; i < Vertices.Count; i ++)
            {
                var vertex = Vertices[i].Position;
                averagex += vertex.X;
                averagey += vertex.Y;
                averagez += vertex.Z;
            }
            averagex /= (float)Vertices.Count;
            averagey /= (float)Vertices.Count;
            averagez /= (float)Vertices.Count;
            return new Vector3(averagex, averagey, averagez);
        }

        public Vector3 GetAxisAlignedBox()
        {
            float maxx = 0, maxy = 0, maxz = 0;
            float minx = 999999, miny = 999999, minz = 999999;
            for(int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i].Position;

                maxx = maxx < vertex.X ? vertex.X : maxx;
                maxy = maxy < vertex.Y ? vertex.Y : maxy;
                maxz = maxz < vertex.Z ? vertex.Z : maxz;

                minx = minx > vertex.X ? vertex.X : minx;
                miny = miny > vertex.Y ? vertex.Y : miny;
                minz = minz > vertex.Z ? vertex.Z : minz;
            }
            return new Vector3(maxx - minx, maxy - miny, maxz - minz);
        }

        public ConvexHullShape GetConvexHull(float scale = 1.0f)
        {
            //if (CachedBvhTriangleMeshShape != null) return CachedBvhTriangleMeshShape;
            List<Vector3> vectors = GetRawVertexList();
            var convex = new ConvexHullShape(vectors.ToArray());
            return convex;
        }

        public float GetDivisorFromPoint(Vector3 point)
        {
            List<Vector3> vectors = new List<Vector3>();
            float maxval = 0.0001f;
            for(int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i].Position;
                if((vertex - point).Length > maxval)
                    maxval = vertex.Length;
            }
            return maxval;
        }
        
        public float GetNormalizeDivisor()
        {
            List<Vector3> vectors = new List<Vector3>();
            float maxval = 0.0001f;
            for(int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i].Position;
                if(vertex.Length > maxval)
                    maxval = vertex.Length;
            }
            return maxval;
        }

        

        public List<Vector3> GetRawVertexList()
        {
            var ot = new List<Vector3>(Vertices.Count);
            for(int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i].Position;
                ot.Add(vertex);
            }
            return ot;
        }

        public void MakeDoubleFaced()
        {
            var copy = this.CopyDeep();
            copy.FlipFaces();
            Append(copy);
        }

        public void Normalize()
        {
            List<Vector3> vectors = new List<Vector3>();
            float maxval = 0.0001f;
            for(int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i].Position;
                if(vertex.Length > maxval)
                    maxval = vertex.Length;
            }
            for(int i = 0; i < Vertices.Count; i++)
            {
                Vertices[i].Position /= maxval;
            }
        }

        public void OriginToCenter()
        {
            float averagex = 0, averagey = 0, averagez = 0;
            for(int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i].Position;
                averagex += vertex.X;
                averagey += vertex.Y;
                averagez += vertex.Z;
            }
            averagex /= Vertices.Count;
            averagey /= Vertices.Count;
            averagez /= Vertices.Count;
            for(int i = 0; i < Vertices.Count; i++)
            {
                Vertices[i].Position -= new Vector3(averagex, averagey, averagez);
            }
        }

        public void ScaleUV(float x, float y)
        {
            for(int i = 0; i < Vertices.Count; i++)
            {
                Vertices[i].UV *= new Vector2(x, y);
            }
        }

        public void ScaleUV(float scale)
        {
            for(int i = 0; i < Vertices.Count; i++)
            {
                Vertices[i].UV *= new Vector2(scale);
            }
        }

        public void Transform(Matrix4 ModelMatrix, Matrix4 RotationMatrix)
        {
            for(int i = 0; i < Vertices.Count; i ++)
            {
                Vertices[i].Position = Vector3.Transform(Vertices[i].Position, ModelMatrix);
                Vertices[i].Normal = Vector3.Transform(Vertices[i].Normal, RotationMatrix);
            }
        }

        public void UpdateBoundingBox()
        {
            var vertices = GetRawVertexList();
            var a = vertices[0];
            var b = vertices[0];
            foreach(var v in vertices)
            {
                a = Min(a, v);
                b = Max(b, v);
            }
            AABB = new AxisAlignedBoundingBox()
            {
                Minimum = a,
                Maximum = b
            };
        }
        
        private static float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        private static Vector3 Max(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Max(a.X, b.X),
                Max(a.Y, b.Y),
                Max(a.Z, b.Z)
            );
        }

        private static float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        private static Vector3 Min(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Min(a.X, b.X),
                Min(a.Y, b.Y),
                Min(a.Z, b.Z)
            );
        }

        private static List<ObjFileData> ParseOBJString(string[] lines)
        {
            List<ObjFileData> objects = new List<ObjFileData>();
            List<Vector3> temp_vertices = new List<Vector3>(), temp_normals = new List<Vector3>();
            List<Vector2> temp_uvs = new List<Vector2>();
            List<VertexInfo> out_vertex_buffer = new List<VertexInfo>();
            ;
            //out_vertex_buffer.AddRange(Enumerable.Repeat<double>(0, 8));
            uint vcount = 0;

            ObjFileData current = new ObjFileData();
            string currentMaterial = "";

            Match match = Match.Empty;
            foreach(string line in lines)
            {
                if(line.StartsWith("o"))
                {
                    match = Regex.Match(line, @"o (.+)");
                    current.VBO = out_vertex_buffer;
                    if(current.VBO.Count >= 1)
                    {
                        current.MaterialName = currentMaterial;
                        objects.Add(current);
                    }
                    current = new ObjFileData();
                    current.Name = match.Groups[1].Value;
                    vcount = 0;
                    //temp_vertices = new List<Vector3>();
                    //temp_normals = new List<Vector3>();
                    //temp_uvs = new List<Vector2>();
                    out_vertex_buffer = new List<VertexInfo>();
                }
                if(line.StartsWith("usemtl"))
                {
                    match = Regex.Match(line, @"usemtl (.+)");
                    currentMaterial = match.Groups[1].Value;
                }
                if(line.StartsWith("vt"))
                {
                    match = Regex.Match(line.Replace("nan", "0"), @"vt ([0-9.-]+) ([0-9.-]+)");
                    temp_uvs.Add(new Vector2(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1.0f - float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
                }
                else if(line.StartsWith("vn"))
                {
                    match = Regex.Match(line, @"vn ([0-9.-]+) ([0-9.-]+) ([0-9.-]+)");
                    temp_normals.Add(new Vector3(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)));
                }
                else if(line.StartsWith("v"))
                {
                    match = Regex.Match(line, @"v ([0-9.-]+) ([0-9.-]+) ([0-9.-]+)");
                    temp_vertices.Add(new Vector3(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)));
                }
                else if(line.StartsWith("f"))
                {
                    match = Regex.Match(line, @"f ([0-9]+)/([0-9]+)/([0-9]+) ([0-9]+)/([0-9]+)/([0-9]+) ([0-9]+)/([0-9]+)/([0-9]+)");
                    if(match.Success)
                    {
                        for(int i = 1; ;)
                        {
                            Vector3 vertex = temp_vertices[int.Parse(match.Groups[i++].Value) - 1];
                            Vector2 uv = temp_uvs[int.Parse(match.Groups[i++].Value) - 1];
                            Vector3 normal = temp_normals[int.Parse(match.Groups[i++].Value) - 1];

                            out_vertex_buffer.Add(new VertexInfo() { Position = vertex, Normal = normal, UV = uv });
                            if(i >= 9)
                                break;
                        }
                    }
                    else
                    {
                        match = Regex.Match(line, @"f ([0-9]+)//([0-9]+) ([0-9]+)//([0-9]+) ([0-9]+)//([0-9]+)");
                        if(match.Success)
                        {
                            for(int i = 1; ;)
                            {
                                Vector3 vertex = temp_vertices[int.Parse(match.Groups[i++].Value) - 1];
                                Vector3 normal = temp_normals[int.Parse(match.Groups[i++].Value) - 1];

                                out_vertex_buffer.Add(new VertexInfo() { Position = vertex, Normal = normal, UV = normal.Xz });
                                if(i >= 6)
                                    break;
                            }
                        }
                    }
                }
            }
            current.VBO = out_vertex_buffer;
            current.MaterialName = currentMaterial;
            objects.Add(current);
            current = new ObjFileData();
            current.Name = match.Groups[1].Value;
            return objects;
        }

        private static ObjFileData ParseOBJStringSingle(string[] lines)
        {
            List<ObjFileData> objects = new List<ObjFileData>();
            List<Vector3> temp_vertices = new List<Vector3>(), temp_normals = new List<Vector3>();
            List<Vector2> temp_uvs = new List<Vector2>();
            List<VertexInfo> out_vertex_buffer = new List<VertexInfo>();

            uint vcount = 0;

            ObjFileData current = new ObjFileData();

            Match match = Match.Empty;
            foreach(string line in lines)
            {
                if(line.StartsWith("vt"))
                {
                    match = Regex.Match(line, @"vt ([0-9.-]+) ([0-9.-]+)");
                    temp_uvs.Add(new Vector2(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
                }
                else if(line.StartsWith("vn"))
                {
                    match = Regex.Match(line, @"vn ([0-9.-]+) ([0-9.-]+) ([0-9.-]+)");
                    temp_normals.Add(new Vector3(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)));
                }
                else if(line.StartsWith("v"))
                {
                    match = Regex.Match(line, @"v ([0-9.-]+) ([0-9.-]+) ([0-9.-]+)");
                    temp_vertices.Add(new Vector3(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)));
                }
                else if(line.StartsWith("f"))
                {
                    match = Regex.Match(line, @"f ([0-9]+)/([0-9]+)/([0-9]+) ([0-9]+)/([0-9]+)/([0-9]+) ([0-9]+)/([0-9]+)/([0-9]+)");
                    if(match.Success)
                    {
                        for(int i = 1; ;)
                        {
                            Vector3 vertex = temp_vertices[int.Parse(match.Groups[i++].Value) - 1];
                            Vector2 uv = temp_uvs[int.Parse(match.Groups[i++].Value) - 1];
                            Vector3 normal = temp_normals[int.Parse(match.Groups[i++].Value) - 1];

                            out_vertex_buffer.Add(new VertexInfo() { Position = vertex, Normal = normal, UV = uv });
                            if(i >= 9)
                                break;
                        }
                    }
                    else
                    {
                        match = Regex.Match(line, @"f ([0-9]+)//([0-9]+) ([0-9]+)//([0-9]+) ([0-9]+)//([0-9]+)");
                        if(match.Success)
                        {
                            for(int i = 1; ;)
                            {
                                Vector3 vertex = temp_vertices[int.Parse(match.Groups[i++].Value) - 1];
                                Vector3 normal = temp_normals[int.Parse(match.Groups[i++].Value) - 1];

                                out_vertex_buffer.Add(new VertexInfo() { Position = vertex, Normal = normal, UV = normal.Xz });
                                if(i >= 6)
                                    break;
                            }
                        }
                    }
                }
            }
            current.VBO = out_vertex_buffer;
            objects.Add(current);
            current = new ObjFileData();
            current.Name = match.Groups[1].Value;
            return objects.First();
        }

        private Vector3 CalculateTangent(Vector3 normal, Vector3 v1, Vector3 v2, Vector2 st1, Vector2 st2)
        {
            float coef = 1.0f / (st1.X * st2.Y - st2.X * st1.Y);
            var tangent = Vector3.Zero;

            tangent.X = coef * ((v1.X * st2.Y) + (v2.X * -st1.X));
            tangent.Y = coef * ((v1.Y * st2.Y) + (v2.Y * -st1.X));
            tangent.Z = coef * ((v1.Z * st2.Y) + (v2.Z * -st1.X));

            //float3 binormal = normal.crossProduct(tangent);
            return tangent;
        }
    }
}