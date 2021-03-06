﻿using System;
using System.IO;
using VEngine;
using Assimp;
using System.Text;
using OpenTK;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MeshConverter
{
    internal class Program
    {
        static string ftos(float v)
        {
            return v.ToString().Replace(',', '.');
        }

        static Dictionary<string, string> Arguments;

        static void RequireArgs(params string[] args)
        {
            bool fail = false;
            foreach (var a in args)
            {
                if (!Arguments.ContainsKey(a))
                {
                    Console.WriteLine("Argument not found: " + a);
                    fail = true;
                }
            }
            if (fail)
                System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        static double hash(double n)
        {
            double h = Math.Sin(n) * 758.5453;
            return h - Math.Floor(h);
        }

        static double mix(double a, double b, double m)
        {
            return a * (1.0 - m) + b * m;
        }

        static double configurablenoise(Vector3d x, double c1, double c2)
        {
            Vector3d p = new Vector3d(Math.Floor(x.X), Math.Floor(x.Y), Math.Floor(x.Z));
            Vector3d f = x - p;
            f.X = f.X * f.X * (3.0 - 2.0 * f.X);
            f.Y = f.Y * f.Y * (3.0 - 2.0 * f.Y);
            f.Z = f.Z * f.Z * (3.0 - 2.0 * f.Z);

            double h2 = c1;
            double h1 = c2;
            double h3 = h2 + h1;

            double n = p.X + p.Y * h1 + h2 * p.Z;
            return mix(mix(mix(hash(n + 0.0), hash(n + 1.0), f.X),
                    mix(hash(n + h1), hash(n + h1 + 1.0), f.X), f.Y),
                   mix(mix(hash(n + h2), hash(n + h2 + 1.0), f.X),
                    mix(hash(n + h3), hash(n + h3 + 1.0), f.X), f.Y), f.Z);

        }

        static double supernoise3dX(Vector3d p)
        {

            double a = configurablenoise(p, 883.0, 971.0);
            double b = configurablenoise(p + new Vector3d(0.5), 113.0, 157.0);
            return (a * b);
        }

        static ushort doubleToUInt16(double v)
        {
            return (ushort)((v * ((float)ushort.MaxValue)));
        }

        static double fbm(Vector3d v, int octavecount, double octaveweight, double octavescale)
        {
            double w = 1.0;
            double res = 0.0;
            double wsum = 0.0;
            for (int o = 0; o < octavecount; o++)
            {
                double noise = Math.Min(1.0, Math.Max(0.0, supernoise3dX(v)));
                res += w * noise;
                wsum += w;
                w *= octaveweight;
                v *= new Vector3d(octavescale, octavescale, octavescale);
            }
            return res;
        }

        static bool doublefaced = false;
        static void SaveMeshToFile(Mesh3d m, string name, string outfile)
        {
            StringBuilder materialb = new StringBuilder();
            var mat = m.GetLodLevel(0).Material;
            var i3d = m.GetLodLevel(0).Info3d;
            var inst = m.GetInstance(0);
            string meshname = name + ".mesh3d";
            string matname = name + ".material";

            string rawfile = name + ".raw";
            i3d.Manager.ReverseYUV(1);
            i3d.Manager.SaveRawWithTangents(outfile + "/" + rawfile);

            materialb.AppendLine(string.Format("diffuse {0} {1} {2}", ftos(mat.DiffuseColor.X), ftos(mat.DiffuseColor.Y), ftos(mat.DiffuseColor.Z)));
            materialb.AppendLine(string.Format("roughness {0}", ftos(mat.Roughness)));
            materialb.AppendLine(string.Format("metalness {0}", 0.2f));
            materialb.AppendLine();
            if (mat.NormalsTexture != null)
            {
                materialb.AppendLine("node");
                materialb.AppendLine(string.Format("texture {0}", mat.NormalsTexture.FileName));
                materialb.AppendLine("mix REPLACE");
                materialb.AppendLine("target NORMAL");
                materialb.AppendLine();
            }
            if (mat.DiffuseTexture != null)
            {
                materialb.AppendLine("node");
                materialb.AppendLine(string.Format("texture {0}", mat.DiffuseTexture.FileName));
                materialb.AppendLine("mix REPLACE");
                materialb.AppendLine("target DIFFUSE");
                materialb.AppendLine("modifier LINEARIZE");
                materialb.AppendLine();
            }
            if (mat.BumpTexture != null)
            {
                materialb.AppendLine("node");
                materialb.AppendLine(string.Format("texture {0}", mat.BumpTexture.FileName));
                materialb.AppendLine("mix REPLACE");
                materialb.AppendLine("target BUMP");
                materialb.AppendLine();
            }
            if (mat.RoughnessTexture != null)
            {
                materialb.AppendLine("node");
                materialb.AppendLine(string.Format("texture {0}", mat.RoughnessTexture.FileName));
                materialb.AppendLine("mix REPLACE");
                materialb.AppendLine("target ROUGHNESS");
                materialb.AppendLine();
            }
            File.WriteAllText(outfile + "/" + matname, materialb.ToString());

            StringBuilder meshb = new StringBuilder();
            meshb.AppendLine("lodlevel");
            meshb.AppendLine("start 0");
            meshb.AppendLine("end 99999");
            meshb.AppendLine(string.Format("info3d {0}", rawfile));
            meshb.AppendLine(string.Format("material {0}", matname));
            meshb.AppendLine();
            meshb.AppendLine("instance");
            meshb.AppendLine(string.Format("translate {0} {1} {2}", ftos(inst.Transformation.Position.R.X), ftos(inst.Transformation.Position.R.Y), ftos(inst.Transformation.Position.R.Z)));
            meshb.AppendLine(string.Format("scale {0} {1} {2}", ftos(inst.Transformation.ScaleValue.R.X), ftos(inst.Transformation.ScaleValue.R.Y), ftos(inst.Transformation.ScaleValue.R.Z)));
            meshb.AppendLine(string.Format("rotate {0} {1} {2} {3}", ftos(inst.Transformation.Orientation.R.X), ftos(inst.Transformation.Orientation.R.Y), ftos(inst.Transformation.Orientation.R.Z), ftos(inst.Transformation.Orientation.R.W)));
            File.WriteAllText(outfile + "/" + meshname, meshb.ToString());
        }

        static Dictionary<int, string> matdict = new Dictionary<int, string>();
        static int unnamed = 0;
        static List<string> usednames = new List<string>();
        static string mode, infile, outfile;
        private static void Main(string[] args)
        {
            Arguments = new Dictionary<string, string>();
            foreach (var a in args)
            {
                int i = a.IndexOf('=');
                if (i > 0)
                {
                    string k = a.Substring(0, i);
                    string v = a.Substring(i + 1);
                    Arguments.Add(k, v);
                }
            }
            mode = args[0];
            infile = args.Length > 1 ? args[1] : "";
            outfile = args.Length > 2 ? args[2] : "";
            Media.SearchPath = ".";

            if (mode == "scene2assets")
            {
                string scenename = args[3];
                var element = new VEngine.FileFormats.GameScene(infile);
                element.Load();
                int unnamed = 0;
                StringBuilder sceneb = new StringBuilder();
                var rand = new Random();
                foreach (var m in element.Meshes)
                {
                    string name = "mesh" + (unnamed++);
                    SaveMeshToFile(m, name, outfile);
                    sceneb.AppendLine("mesh3d " + name + ".mesh3d");
                }
                File.WriteAllText(outfile + "/" + scenename, sceneb.ToString());
            }
            if (mode == "obj2raw")
            {
                var element = Object3dManager.LoadFromObjSingle(infile);
                element.SaveRaw(outfile);
            }
            if (mode == "obj2rawtangsmooth")
            {
                var element = Object3dManager.LoadFromObjSingle(infile);
                element.RecalulateNormals(Object3dManager.NormalRecalculationType.Smooth, 1);
                element.SaveRawWithTangents(outfile);
            }
            if (mode == "raw2rawtang")
            {
                var element = Object3dManager.LoadFromRaw(infile);
                element.SaveRawWithTangents(outfile);
            }
            if (mode == "raw2rawtangfake")
            {
                var element = Object3dManager.LoadFromRaw(infile);
                element.SaveRawWithTangents2(outfile);
            }
            if (mode == "obj2rawtang")
            {
                var element = Object3dManager.LoadFromObjSingle(infile);
                element.SaveRawWithTangents(outfile);
            }
            if (mode == "raworigin2center")
            {
                Console.WriteLine("origin 2 center");
                var element = Object3dManager.LoadFromRaw(infile);
                element.OriginToCenter();
                element.SaveRawWithTangents(outfile);
            }
            if (mode == "objscene2assets")
            {
                Console.WriteLine("Conversion started");
                var elements = Object3dManager.LoadSceneFromObj(infile + ".obj", infile + ".mtl");

                var map = new List<string>();
                var r = new Random();
                StringBuilder sceneb = new StringBuilder();
                string scenename = args[3];

                Console.WriteLine("Found elements " + elements.Count);
                foreach (var m in elements)
                {
                    string n = m.GetInstance(0).Name;
                    if (n == null || n.Length == 0 || map.Contains(n))
                        n = m.GetLodLevel(0).Info3d.Manager.Name;
                    if (n == null || n.Length == 0 || map.Contains(n))
                        n = m.GetLodLevel(0).Material.Name;
                    if (n == null || n.Length == 0 || map.Contains(n))
                        n = Path.GetFileNameWithoutExtension(m.GetLodLevel(0).Material.DiffuseTexture.FileName);
                    if (n == null || n.Length == 0 || map.Contains(n))
                        n = Path.GetFileNameWithoutExtension(m.GetLodLevel(0).Material.BumpTexture.FileName);
                    if (n == null || n.Length == 0 || map.Contains(n))
                        n = Path.GetFileNameWithoutExtension(m.GetLodLevel(0).Material.NormalsTexture.FileName);
                    while (n == null || n.Length == 0 || map.Contains(n))
                        n = "unknown_" + r.Next();
                    Console.WriteLine("Converting mesh " + n);

                    SaveMeshToFile(m, n, outfile);
                    sceneb.AppendLine("mesh3d " + n + ".mesh3d");
                }
                Console.WriteLine("Saving scene");
                File.WriteAllText(outfile + "/" + scenename, sceneb.ToString());
            }

            if (mode == "ply2raw")
            {
                var ai = new AssimpContext();
                var vertexinfos = new List<VertexInfo>();
                var mi = ai.ImportFile(infile, PostProcessSteps.Triangulate);
                foreach (var m in mi.Meshes)
                {
                    var indices = m.GetIndices();

                    for (int i = 0; i < indices.Length; i++)
                    {
                        int f = indices[i];
                        var vp = m.Vertices[f];
                        var vn = m.Normals[f];
                        var vt = (m.TextureCoordinateChannels.Length == 0 || m.TextureCoordinateChannels[0].Count <= f) ? new Assimp.Vector3D(0) : m.TextureCoordinateChannels[0][f];
                        var vi = new VertexInfo()
                        {
                            Position = new Vector3(vp.X, vp.Y, vp.Z),
                            Normal = new Vector3(vn.X, vn.Y, vn.Z),
                            UV = new Vector2(vt.X, vt.Y)
                        };
                        vertexinfos.Add(vi);
                    }
                }

                var element = new Object3dManager(vertexinfos);
                element.SaveRaw(outfile);
            }
            if (mode == "noisetexture")
            {
                RequireArgs("resolution", "scale", "octavescale", "octavecount", "octaveweight", "seed", "out", "falloffmix", "falloffpower");
                string outf = Arguments["out"];
                int resolution = int.Parse(Arguments["resolution"]);
                int octavecount = int.Parse(Arguments["octavecount"]);
                float scale = float.Parse(Arguments["scale"], System.Globalization.CultureInfo.InvariantCulture);
                float octavescale = float.Parse(Arguments["octavescale"], System.Globalization.CultureInfo.InvariantCulture);
                float octaveweight = float.Parse(Arguments["octaveweight"], System.Globalization.CultureInfo.InvariantCulture);
                float seed = float.Parse(Arguments["seed"], System.Globalization.CultureInfo.InvariantCulture);
                float falloffmix = float.Parse(Arguments["falloffmix"], System.Globalization.CultureInfo.InvariantCulture);
                float falloffpower = 1.0f / float.Parse(Arguments["falloffpower"], System.Globalization.CultureInfo.InvariantCulture);
                BinaryWriter writer = new BinaryWriter(File.OpenWrite(outf));
                writer.Seek(0, SeekOrigin.Begin);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(resolution, resolution);
                for (int ix = 0; ix < resolution; ix++)
                {
                    for (int iy = 0; iy < resolution; iy++)
                    {
                        double x = (((double)ix) / ((double)resolution));
                        double y = (((double)iy) / ((double)resolution));
                        double len = Math.Min(1.0, new Vector2d(x * 2.0 - 1.0, y * 2.0 - 1.0).Length);
                        x *= scale;
                        y *= scale;
                        len = Math.Pow(len, falloffpower);
                        Vector3d v = new Vector3d(x, y, seed);
                        double res = fbm(v, octavecount, octaveweight, octavescale);
                        res = mix(res, 0.0, len * falloffmix);
                        writer.Write(doubleToUInt16(res));
                        int c = (int)(255.0 * (res));
                      //  Console.WriteLine(res);
                        bitmap.SetPixel(ix, iy, System.Drawing.Color.FromArgb(255, c, c, c));
                    }
                    Console.WriteLine("Progress: {0} %", (((double)ix) / ((double)resolution)) * 100.0);
                }
                bitmap.Save(outf + ".png", System.Drawing.Imaging.ImageFormat.Png);
                writer.Close();
            }
            if (mode == "generateterrain")
            {
                RequireArgs("resolution", "parts", "in", "inx", "iny", "out", "size", "uvscale", "height");
                var imgraw = File.ReadAllBytes(Arguments["in"]);
                var imgdata = new ushort[imgraw.Length / 2];
                Buffer.BlockCopy(imgraw, 0, imgdata, 0, imgraw.Length);
                int resolution = int.Parse(Arguments["resolution"]);
                int imgwidth = int.Parse(Arguments["inx"]);
                int imgheight = int.Parse(Arguments["iny"]);
                string ofile = Arguments["out"];
                float size = float.Parse(Arguments["size"], System.Globalization.CultureInfo.InvariantCulture) / 2.0f;
                float uvscale = float.Parse(Arguments["uvscale"], System.Globalization.CultureInfo.InvariantCulture);
                float height = float.Parse(Arguments["height"], System.Globalization.CultureInfo.InvariantCulture);
                int lx = 1;
                int parts = int.Parse(Arguments["parts"]);
                var start = new Vector2(-size);
                var end = new Vector2(size);
                var stepsize = (end - start) / ((float)parts);
                var realstepsize = (1.0f) / ((float)parts);
                for (int ApartX = 0; ApartX < parts; ApartX++)
                {
                    var tasks = new List<Task>();
                    for (int ApartY = 0; ApartY < parts; ApartY++)
                    {
                        int partX = ApartX;
                        int partY = ApartY;
                        var tx = new Task(new Action(() =>
                        {
                            var partstart = start + new Vector2(stepsize.X * (float)partX, stepsize.Y * (float)partY);
                            var partend = start + new Vector2(stepsize.X * (float)(partX + 1), stepsize.Y * (float)(partY + 1));
                            var t = VEngine.Generators.Object3dGenerator.CreateTerrain(partstart, partend, new Vector2(uvscale), Vector3.UnitY, resolution, (x, y) =>
                            {
                                float rx = realstepsize * (float)partX + realstepsize * (x);
                                float ry = realstepsize * (float)partY + realstepsize * (y);
                                int xpx = (int)(rx * imgwidth);
                                int ypx = (int)(ry * imgheight);
                                if (xpx >= imgwidth)
                                    xpx = imgwidth - 1;
                                if (ypx >= imgheight)
                                    ypx = imgheight - 1;
                                byte b0 = imgraw[(xpx + ypx * imgwidth) * 2];
                                byte b1 = imgraw[(xpx + ypx * imgwidth) * 2 + 1];
                                var col = BitConverter.ToUInt16(new byte[] { b0, b1 }, 0);
                                float f = ((float)(col) / (float)ushort.MaxValue) * height;
                                if (f < 0.01f)
                                    f = -50.0f;
                                return f;
                            });
                            Console.WriteLine("Starting saving " + ofile + "_" + partX + "x" + partY + ".raw");
                            t.ExtractTranslation2DOnly();
                            //t.RecalulateNormals(Object3dManager.NormalRecalculationType.Smooth, 0.0f);
                            t.Vertices.ForEach((a) =>
                            {
                                a.UV.X = realstepsize * (float)partX + realstepsize * (a.UV.X);
                                a.UV.Y = realstepsize * (float)partY + realstepsize * (a.UV.Y);
                            });
                            t.SaveRawWithTangents(ofile + "_" + partX + "x" + partY + ".raw");
                        }));
                        tasks.Add(tx);
                    }
                    tasks.ForEach((a) => {
                        a.Start();
                      //  a.Wait();

                        });
                    tasks.ForEach((a) => a.Wait());
                }
            }
            if (mode == "generateterraingrass")
            {
                RequireArgs("resolution", "in", "out", "size", "uvscale", "height", "threshold");
                var img = new System.Drawing.Bitmap(Arguments["in"]);
                int resolution = int.Parse(Arguments["resolution"]);
                string ofile = Arguments["out"];
                float size = float.Parse(Arguments["size"], System.Globalization.CultureInfo.InvariantCulture) / 2.0f;
                float uvscale = float.Parse(Arguments["uvscale"], System.Globalization.CultureInfo.InvariantCulture);
                float height = float.Parse(Arguments["height"], System.Globalization.CultureInfo.InvariantCulture);
                float threshold = float.Parse(Arguments["threshold"], System.Globalization.CultureInfo.InvariantCulture);
                int lx = 1;
                var start = new Vector2(-size);
                var end = new Vector2(size);
                var mixdir = (end - start);

                StringBuilder sb = new StringBuilder();

                var gethfn = new Func<float, float, float>((x, y) =>
                {
                    int xpx = (int)(x * img.Size.Width);
                    int ypx = (int)(y * img.Size.Height);
                    if (xpx >= img.Size.Width)
                        xpx = img.Size.Width - 1;
                    if (ypx >= img.Size.Height)
                        ypx = img.Size.Height - 1;
                    var col = img.GetPixel(xpx, ypx);
                    int zxzs = (int)(x * 100.0);
                    // if(zxzs > lx)
                    //  Console.WriteLine(zxzs);
                    if (zxzs > lx)
                        lx = zxzs;
                    return ((float)(col.R) / 255.0f) * height;
                });

                for (int x = 0; x < resolution; x++)
                {
                    for (int y = 0; y < resolution; y++)
                    {
                        float fx = (float)x / (float)resolution;
                        float fy = (float)y / (float)resolution;
                        float h = gethfn(fx, fy);
                        //  Console.WriteLine(h);
                        if (h > threshold)
                        {
                            Vector2 vx = start + mixdir * new Vector2(fx, fy);
                            sb.AppendLine("instance");
                            sb.Append("translate ");
                            sb.Append(vx.X);
                            sb.Append(" ");
                            sb.Append(h);
                            sb.Append(" ");
                            sb.Append(vx.Y);
                            sb.AppendLine();
                        }
                    }
                }
                File.WriteAllText(ofile, sb.ToString());


            }
            if (mode == "assimp2assets")
            {
                // convert.exe assimp2assets infile.dae outdir outname.scene
                var ai = new AssimpContext();
                var usednames = new List<string>();
                var mi = ai.ImportFile(infile, PostProcessSteps.Triangulate | PostProcessSteps.GenerateSmoothNormals);

                int cnt = 0;
                var scenesb = new StringBuilder();
                string scenename = args[3];
                doublefaced = args.Length == 5 && args[4] == "doublefaced";
                foreach (var m in mi.Materials)
                {
                    string name = usednames.Contains(m.Name) ? (m.Name + (unnamed++).ToString()) : m.Name;
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Format("diffuse {0} {1} {2}", ftos(m.ColorDiffuse.R), ftos(m.ColorDiffuse.G), ftos(m.ColorDiffuse.B)));
                    sb.AppendLine(string.Format("roughness {0}", ftos(1.0f / (m.Shininess + 1.0f))));
                    sb.AppendLine(string.Format("metalness {0}", ftos(0.0f)));
                    sb.AppendLine();

                    if (m.HasTextureDiffuse)
                    {
                        sb.AppendLine("node");
                        sb.AppendLine(string.Format("texture {0}", Path.GetFileName(m.TextureDiffuse.FilePath)));
                        sb.AppendLine("mix REPLACE");
                        sb.AppendLine("target DIFFUSE");
                        sb.AppendLine("modifier LINEARIZE");
                        sb.AppendLine();
                    }
                    if (m.HasTextureReflection)
                    {
                        sb.AppendLine("node");
                        sb.AppendLine(string.Format("texture {0}", Path.GetFileName(m.TextureReflection.FilePath)));
                        sb.AppendLine("mix REPLACE");
                        sb.AppendLine("target ROUGHNESS");
                        sb.AppendLine();
                    }
                    if (m.HasTextureSpecular)
                    {
                        sb.AppendLine("node");
                        sb.AppendLine(string.Format("texture {0}", Path.GetFileName(m.TextureSpecular.FilePath)));
                        sb.AppendLine("mix REPLACE");
                        sb.AppendLine("target ROUGHNESS");
                        sb.AppendLine();
                    }
                    if (m.HasTextureNormal)
                    {
                        sb.AppendLine("node");
                        sb.AppendLine(string.Format("texture {0}", Path.GetFileName(m.TextureNormal.FilePath)));
                        sb.AppendLine("mix REPLACE");
                        sb.AppendLine("target NORMAL");
                        sb.AppendLine();
                    }
                    if (m.HasTextureDisplacement)
                    {
                        sb.AppendLine("node");
                        sb.AppendLine(string.Format("texture {0}", Path.GetFileName(m.TextureDisplacement.FilePath)));
                        sb.AppendLine("mix REPLACE");
                        sb.AppendLine("target BUMP");
                        sb.AppendLine();
                    }
                    Console.WriteLine("Saving " + outfile + "/" + name + ".material");
                    File.WriteAllText(outfile + "/" + name + ".material", sb.ToString());
                    matdict.Add(cnt, outfile + "/" + name + ".material");
                    cnt++;
                }
                recurseNode(mi, mi.RootNode, scenesb, Matrix4x4.Identity);
                Console.WriteLine("Saving " + outfile + "/" + scenename);
                File.WriteAllText(outfile + "/" + scenename, scenesb.ToString());

            }


            if (mode == "assimp2skeleton")
            {
                // convert.exe assimp2skeleton infile.dae outfile.skeleton
                var ai = new AssimpContext();
                var usednames = new List<string>();
                var mi = ai.ImportFile(infile);
                string skeleton = saveSkeleton(mi, args[3]);

                File.WriteAllText(outfile, skeleton);


                Console.WriteLine("Done");
            }
        }

        static Assimp.Node findNodeByName(Assimp.Node root, string s)
        {
            if (root.Name == s) return root;
            var test = root.FindNode(s);
            if (test != null) return test;
            foreach (var a in root.Children)
            {

                var t = findNodeByName(a, s);
                if (t != null) return t;
            }
            return null;
        }

        static string saveSkeleton(Assimp.Scene scn, string rawfile)
        {
            StringBuilder sb = new StringBuilder();
            Dictionary<int, List<string>> vweights = new Dictionary<int, List<string>>();
            int vertexStart = 0;
            Object3dManager mgr = new Object3dManager(new VertexInfo[0]);
            foreach (var m in scn.Meshes)
            {
                for (int i = 0; i < m.VertexCount; i++)
                    vweights.Add(vertexStart + i, new List<string>());

                var indyk = m.GetIndices();
                for (int ix = 0; ix < indyk.Length; ix++)
                {
                    int i = indyk[ix];
                    var vt = new VertexInfo();
                    vt.Position = new Vector3(m.Vertices[i].X, m.Vertices[i].Y, m.Vertices[i].Z);
                    vt.Normal = new Vector3(m.Normals[i].X, m.Normals[i].Y, m.Normals[i].Z);
                    vt.UV = new Vector2(m.TextureCoordinateChannels[0][i].X, m.TextureCoordinateChannels[0][i].Y);
                    mgr.Vertices.Add(vt);
                }
                sb.AppendLine();
                int boneid = 0;
                List<string> names = new List<string>();
                foreach (var b in m.Bones)
                {
                    names.Add(b.Name);
                    Console.WriteLine(b.Name);
                }

                foreach (var b in m.Bones)
                {
                    sb.Append("bone " + boneid.ToString() + " ");
                    sb.AppendLine(b.Name);
                    Assimp.Node n = scn.RootNode;
                    var na = findNodeByName(n, b.Name);
                    if (na != null && na.Parent != null)
                    {

                        sb.AppendLine("parent " + names.FindIndex((wa) => wa == na.Parent.Name).ToString());
                    }
                    // sb.Append("weights ");
                    float radius = 0.0f;
                    var tx = na.Transform;
                    var parent = na.Parent;
                    while(parent != null)
                    {
                        tx = parent.Transform * tx;
                        parent = parent.Parent;
                    }
                    Assimp.Vector3D testpos, terstscale;
                    Assimp.Quaternion testquat;
                    tx.Decompose(out terstscale, out testquat, out testpos);
                    testpos *= terstscale;
                    Console.WriteLine(testpos.X + " " + testpos.Y + " " + testpos.Z);
                    Vector3 position = Vector3.Zero;
                    float weisum = 0.001f;
                    foreach (var v in b.VertexWeights)
                    {
                        Vector3 vert = new Vector3(m.Vertices[v.VertexID].X, m.Vertices[v.VertexID].Y, m.Vertices[v.VertexID].Z);
                        position += v.Weight * vert;
                        weisum += v.Weight;

                        vweights[vertexStart + v.VertexID].Add(string.Format("{0}={1}", boneid, v.Weight.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    position /= weisum;
                    weisum = 0.001f;
                    foreach (var v in b.VertexWeights)
                    {
                        Vector3 vert = new Vector3(m.Vertices[v.VertexID].X, m.Vertices[v.VertexID].Y, m.Vertices[v.VertexID].Z);
                        radius += v.Weight * (position - vert).Length;
                        weisum += v.Weight;

                        vweights[vertexStart + v.VertexID].Add(string.Format("{0}={1}", boneid, v.Weight.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    radius /= weisum;
                    Console.WriteLine(position.X + " " + position.Y + " " + position.Z);

                    sb.AppendLine();
                    sb.Append("position ");
                    sb.Append(position.X.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(position.Y.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(position.Z.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.AppendLine();
                    sb.Append("radius ");
                    sb.Append(radius.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    Console.WriteLine();

                    sb.AppendLine();
                    sb.Append("matrix ");
                  
                    Assimp.Quaternion q;
                    Assimp.Vector3D t;
                    Assimp.Vector3D s;
                    var a = b.OffsetMatrix;
                    //  a.Inverse();
                    a.Decompose(out s, out q, out t);
                    t /= s;

                    sb.Append(q.X.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(q.Y.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(q.Z.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(q.W.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");

                    sb.Append(t.X.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(t.Y.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(t.Z.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.AppendLine();

                    sb.Append(s.X.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(s.Y.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.Append(s.Z.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                    sb.AppendLine();
                    boneid++;
                }
                vertexStart += m.VertexCount;
            }
            while (mgr.Vertices.Count % 3 != 0) mgr.Vertices.RemoveAt(mgr.Vertices.Count - 1);
            mgr.SaveRawWithTangents(rawfile);
            sb.AppendLine();
            foreach (var vw in vweights)
            {
                sb.Append("vertex_weights ");
                sb.Append(vw.Key);
                foreach (var v in vw.Value)
                {
                    sb.Append(" ");
                    sb.Append(v);
                }
                if(vw.Value.Count == 0)
                {
                    sb.Append(" 0=0");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static void recurseNode(Assimp.Scene scn, Node node, StringBuilder scenesb, Matrix4x4 matrix)
        {
            Console.WriteLine("Scanning node " + node.Name);
            foreach (var mindex in node.MeshIndices)
            {
                var m = scn.Meshes[mindex];
                string name = usednames.Contains(m.Name) ? (m.Name + (unnamed++).ToString()) : m.Name;
                var sb = new StringBuilder();
                if (!File.Exists(outfile + "/" + name + ".mesh3d"))
                {
                    var vertexinfos = new List<VertexInfo>();
                    var indices = m.GetIndices();

                    for (int i = 0; i < indices.Length; i++)
                    {
                        int f = indices[i];
                        var vp = m.Vertices[f];
                        var vn = m.Normals[f];
                        var vt = (m.TextureCoordinateChannels.Length == 0 || m.TextureCoordinateChannels[0].Count <= f) ? new Assimp.Vector3D(0) : m.TextureCoordinateChannels[0][f];
                        var vi = new VertexInfo()
                        {
                            Position = new Vector3(vp.X, vp.Y, vp.Z),
                            Normal = new Vector3(vn.X, vn.Y, vn.Z),
                            UV = new Vector2(vt.X, vt.Y)
                        };
                        vertexinfos.Add(vi);
                    }
                    if (doublefaced)
                    {

                        for (int i = indices.Length - 1; i >= 0; i--)
                        {
                            int f = indices[i];
                            var vp = m.Vertices[f];
                            var vn = m.Normals[f];
                            var vt = (m.TextureCoordinateChannels.Length == 0 || m.TextureCoordinateChannels[0].Count <= f) ? new Assimp.Vector3D(0) : m.TextureCoordinateChannels[0][f];
                            var vi = new VertexInfo()
                            {
                                Position = new Vector3(vp.X, vp.Y, vp.Z),
                                Normal = new Vector3(vn.X, vn.Y, vn.Z),
                                UV = new Vector2(vt.X, vt.Y)
                            };
                            vertexinfos.Add(vi);
                        }
                    }
                    var element = new Object3dManager(vertexinfos);
                    Console.WriteLine("Saving " + outfile + "/" + name + ".raw");
                    element.SaveRawWithTangents(outfile + "/" + name + ".raw");

                    string matname = matdict[m.MaterialIndex];
                    scenesb.AppendLine("mesh3d " + outfile + "/" + name + ".mesh3d");
                    sb.AppendLine("lodlevel");
                    sb.AppendLine("start 0.0");
                    sb.AppendLine("end 99999.0");
                    sb.AppendLine("info3d " + outfile + "/" + name + ".raw");
                    sb.AppendLine("material " + matname);
                    sb.AppendLine();
                }
                sb.AppendLine("instance");
                Assimp.Vector3D pvec, pscl;
                Assimp.Quaternion pquat;
                var a = new Assimp.Quaternion(new Vector3D(1, 0, 0), MathHelper.DegreesToRadians(-90)).GetMatrix();
                var a2 = Matrix4x4.FromScaling(new Vector3D(0.01f));
                (matrix * node.Transform * new Assimp.Matrix4x4(a) * a2).Decompose(out pscl, out pquat, out pvec);
                var q1 = new OpenTK.Quaternion(pquat.X, pquat.Y, pquat.Z, pquat.W).Inverted();
                var m1 = Matrix4.CreateFromQuaternion(q1);
                m1[2, 0] = -m1[2, 0];
                m1[2, 1] = -m1[2, 1];
                m1[0, 2] = -m1[0, 2];
                m1[1, 2] = -m1[1, 2];
                var q2 = m1.ExtractRotation(true);

                sb.AppendLine(string.Format("translate {0} {1} {2}", ftos(pvec.X), ftos(pvec.Y), ftos(pvec.Z)));
                sb.AppendLine(string.Format("rotate {0} {1} {2} {3}", ftos(q1.X), ftos(q1.Y), ftos(q1.Z), ftos(q1.W)));
                sb.AppendLine(string.Format("scale {0} {1} {2}", ftos(pscl.X), ftos(pscl.Y), ftos(pscl.Z)));
                if (!File.Exists(outfile + "/" + name + ".mesh3d"))
                {
                    Console.WriteLine("Saving " + outfile + "/" + name + ".mesh3d");
                    File.WriteAllText(outfile + "/" + name + ".mesh3d", sb.ToString());
                }
                else
                {
                    Console.WriteLine("Extending " + outfile + "/" + name + ".mesh3d");
                    File.AppendAllText(outfile + "/" + name + ".mesh3d", sb.ToString());
                }
            }
            foreach (var c in node.Children)
                if (c != node)
                    recurseNode(scn, c, scenesb, matrix * node.Transform);
        }
    }
}