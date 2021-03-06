﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenTK;

namespace VEngine.FileFormats
{
    public class GameScene
    {
        public List<Camera> Cameras;
        public string FilePath;
        public List<Light> Lights;
        public List<GenericMaterial> Materials;
        public List<Mesh3d> Meshes;

        public GameScene(string filekey)
        {
            FilePath = Media.Get(filekey);
        }

        public delegate void ObjectFinishEventArgs(object sender, object obj);

        public event ObjectFinishEventArgs OnObjectFinish;

        public static string FromMesh3dList(List<Mesh3d> meshes, string directory, string nameprefix = "mesh_", bool saveFiles = false)
        {
            var materials = meshes.SelectMany<Mesh3d, GenericMaterial>((a) => a.GetLodLevels().Select<LodLevel, GenericMaterial>((ax) => ax.Material)).Distinct();
            var output = new StringBuilder();
            int i = 0;
            foreach(var material in materials)
            {
                output.Append("material ");
                output.Append(nameprefix);
                if(material.Name != null && material.Name.Length > 0)
                {
                    output.AppendLine(material.Name);
                }
                else
                {
                    material.Name = (i++).ToString();
                    output.AppendLine(material.Name);
                }

                output.Append("roughness ");
                output.AppendLine(material.Roughness.ToString(System.Globalization.CultureInfo.InvariantCulture));

                output.Append("color ");
                output.Append(material.DiffuseColor.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                output.Append(" ");
                output.Append(material.DiffuseColor.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                output.Append(" ");
                output.Append(material.DiffuseColor.Z.ToString(System.Globalization.CultureInfo.InvariantCulture));

                if(material.DiffuseTexture != null)
                {
                    output.Append("texture ");
                    output.AppendLine(material.DiffuseTexture.FileName);
                }
                if(material.NormalsTexture != null)
                {
                    output.Append("normalmap ");
                    output.AppendLine(material.NormalsTexture.FileName);
                }
                if(material.BumpTexture != null)
                {
                    output.Append("bumpmap ");
                    output.AppendLine(material.BumpTexture.FileName);
                }
                output.AppendLine();
            }
            foreach(var mesh in meshes)
            {
                var elements = mesh.GetLodLevels();
                var i0 = mesh.GetInstance(0);
                foreach(var elementC in elements)
                {
                    try
                    {
                        var element = elementC.Info3d.Manager;
                        if(saveFiles)
                        {
                            FileStream vboStream = new FileStream(directory + nameprefix + element.Name + ".vbo.raw", FileMode.Create);

                            foreach(var v in element.Vertices)
                                foreach(var v2 in v.ToFloatList())
                                    vboStream.Write(BitConverter.GetBytes(v2), 0, 4);

                            vboStream.Flush();
                            vboStream.Close();
                        }
                        output.Append("mesh ");
                        output.Append(nameprefix);
                        output.AppendLine(mesh.GetInstance(0).Name);
                        output.Append("usematerial ");
                        output.Append(nameprefix);
                        output.AppendLine(elementC.Material.Name);

                        output.Append("vbo ");
                        output.AppendLine(nameprefix + element.Name + ".vbo.raw");

                        output.Append("translate ");
                        output.Append(i0.GetPosition().X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        output.Append(" ");
                        output.Append(i0.GetPosition().Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        output.Append(" ");
                        output.AppendLine(i0.GetPosition().Z.ToString(System.Globalization.CultureInfo.InvariantCulture));

                        output.Append("rotate ");
                        output.Append(i0.GetOrientation().X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        output.Append(" ");
                        output.Append(i0.GetOrientation().Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        output.Append(" ");
                        output.Append(i0.GetOrientation().Z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        output.Append(" ");
                        output.AppendLine(i0.GetOrientation().W.ToString(System.Globalization.CultureInfo.InvariantCulture));

                        output.Append("scale ");
                        output.Append(i0.GetScale().X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        output.Append(" ");
                        output.Append(i0.GetScale().Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        output.Append(" ");
                        output.AppendLine(i0.GetScale().Z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        output.AppendLine();
                    }
                    catch (Exception e) { System.Windows.Forms.MessageBox.Show(e.Message); }
                }
            }
            return output.ToString();
        }

        public void Load()
        {
            LoadFromString(File.ReadAllLines(FilePath));
        }

        private void ApplyVboIbo(Mesh3d mesh, string vbo)
        {
            if(vbo.Length == 0)
                return;
            var obj = Object3dManager.LoadFromRaw(Media.Get(vbo));
            mesh.GetLodLevel(0).Info3d = new Object3dInfo(obj.Vertices);
            mesh.GetLodLevel(0).Info3d.Manager = obj;
            mesh.GetLodLevel(0).Info3d.Manager.Name = vbo;
        }

        private void LoadFromString(string[] lines)
        {
            Materials = new List<GenericMaterial>();
            Meshes = new List<Mesh3d>();
            Lights = new List<Light>();
            Cameras = new List<Camera>();
            var regx = new Regex("(.+?)[ ]+(.+)");
            var currentMaterial = new GenericMaterial(Vector3.One);
            Light tempLight = null;
            GenericMaterial tempMaterial = null;
            Mesh3d tempMesh = null;
            Camera tempCamera = null;
            Action flush = () =>
            {
                if(tempLight != null)
                {
                    Lights.Add(tempLight);
                    if(OnObjectFinish != null)
                        OnObjectFinish.Invoke(this, tempLight);
                }
                if(tempMaterial != null)
                {
                    Materials.Add(tempMaterial);
                    if(OnObjectFinish != null)
                        OnObjectFinish.Invoke(this, tempMaterial);
                }
                if(tempMesh != null)
                {
                    Meshes.Add(tempMesh);
                    if(OnObjectFinish != null)
                        OnObjectFinish.Invoke(this, tempMesh);
                }
                if(tempCamera != null)
                {
                    Cameras.Add(tempCamera);
                    if(OnObjectFinish != null)
                        OnObjectFinish.Invoke(this, tempCamera);
                }

                tempLight = null;
                tempMaterial = null;
                tempMesh = null;
                tempCamera = null;
            };
            string vertexbuffer = "";
            PhysicalBody physob = null;
            foreach(var l in lines)
            {
                if(l.StartsWith("//") || l.Trim().Length == 0)
                    continue;
                var regout = regx.Match(l);
                if(!regout.Success)
                {
                        throw new ArgumentException("Invalid line in scene string: " + l);
                }
                string command = regout.Groups[1].Value.Trim();
                string data = regout.Groups[2].Value.Trim();
                if(command.Length == 0 || data.Length == 0)
                    throw new ArgumentException("Invalid line in scene string: " + l);

                switch(command)
                {
                    // Mesh3d start
                    case "mesh":
                    {
                        flush();
                        tempMesh = Mesh3d.Empty;
                        tempMesh.AddInstance(new Mesh3dInstance(new TransformationManager(Vector3.Zero), data));
                        //tempMesh.Name = data;
                        break;
                    }

                    case "usematerial":
                    {
                        tempMesh.AddLodLevel(null, Materials.First((a) => a.Name == data), 0, 999999);
                        break;
                    }
                    case "scaleuv":
                    {
                        string[] literals = data.Split(' ');
                        float x, y;
                        if(!float.TryParse(literals[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMesh.GetLodLevel(0).Info3d.Manager.ScaleUV(x, y);
                        var obj = tempMesh.GetLodLevel(0).Info3d.Manager;
                        tempMesh.GetLodLevel(0).SetInfo3d(new Object3dInfo(tempMesh.GetLodLevel(0).Info3d.Manager.Vertices));
                        tempMesh.GetLodLevel(0).Info3d.Manager = obj;
                        break;
                    }
                    case "vbo":
                    {
                        vertexbuffer = data;
                        ApplyVboIbo(tempMesh, vertexbuffer);
                        vertexbuffer = "";
                        break;
                    }

                    case "physics":
                    {
                        var datas = data.Split(' ');
                        if(datas[0] == "convexhull")
                        {
                            if(tempMesh == null)
                                throw new ArgumentException("Invalid line in scene string: " + l);
                            var shape = tempMesh.GetLodLevel(0).Info3d.Manager.GetConvexHull();
                            float mass;
                            if(!float.TryParse(datas[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mass))
                                throw new ArgumentException("Invalid line in scene string: " + l);
                            physob = Game.World.Physics.CreateBody(mass, tempMesh.GetInstance(0), shape);
                        }
                        if(datas[0] == "boundingbox")
                        {
                            if(tempMesh == null)
                                throw new ArgumentException("Invalid line in scene string: " + l);
                            var shape = Physics.CreateBoundingBoxShape(tempMesh.GetLodLevel(0).Info3d.Manager);
                            float mass;
                            if(!float.TryParse(datas[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mass))
                                throw new ArgumentException("Invalid line in scene string: " + l);
                            physob = Game.World.Physics.CreateBody(mass, tempMesh.GetInstance(0), shape);
                        }
                        if(datas[0] == "accurate")
                        {
                            if(tempMesh == null)
                                throw new ArgumentException("Invalid line in scene string: " + l);
                            var shape = tempMesh.GetLodLevel(0).Info3d.Manager.GetAccurateCollisionShape();
                            float mass;
                            if(!float.TryParse(datas[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mass))
                                throw new ArgumentException("Invalid line in scene string: " + l);
                            physob = Game.World.Physics.CreateBody(mass, tempMesh.GetInstance(0), shape);
                        }
                        if(datas[0] == "enable")
                        {
                            if(physob == null)
                                throw new ArgumentException("Invalid line in scene string: " + l);
                            physob.Enable();
                        }
                        break;
                    }

                    case "translate":
                    {
                        string[] literals = data.Split(' ');
                        if(literals.Length != 3)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        float x, y, z;
                        if(!float.TryParse(literals[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(tempMesh != null)
                        {
                            tempMesh.GetInstance(0).Transformation.Translate(x, y, z);
                        }
                        if(tempCamera != null)
                        {
                            tempCamera.Transformation.Translate(x, y, z);
                        }
                        if(tempLight != null)
                        {
                            tempLight.Transformation.Translate(x, y, z);
                        }
                        break;
                    }
                    case "scale":
                    {
                        if(tempMesh == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        string[] literals = data.Split(' ');
                        if(literals.Length != 3)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        float x, y, z;
                        if(!float.TryParse(literals[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMesh.GetInstance(0).Transformation.Scale(x, y, z);
                        break;
                    }
                    case "rotate":
                    {
                        string[] literals = data.Split(' ');
                        if(literals.Length < 3 || literals.Length > 4)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        float x, y, z;
                        if(!float.TryParse(literals[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        Quaternion rot = Quaternion.Identity;
                        if(literals.Length == 3)
                        {
                            var rotx = Matrix3.CreateRotationX(MathHelper.DegreesToRadians(x));
                            var roty = Matrix3.CreateRotationY(MathHelper.DegreesToRadians(y));
                            var rotz = Matrix3.CreateRotationZ(MathHelper.DegreesToRadians(z));
                            rot = Quaternion.FromMatrix(rotx * roty * rotz);
                        }
                        if(literals.Length == 4)
                        {
                            float w;
                            if(!float.TryParse(literals[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w))
                                throw new ArgumentException("Invalid line in scene string: " + l);
                            rot = new Quaternion(x, y, z, w);
                        }

                        if(tempMesh != null)
                        {
                            tempMesh.GetInstance(0).Transformation.Rotate(rot);
                        }
                        if(tempCamera != null)
                        {
                            tempCamera.Transformation.Rotate(rot);
                        }
                        if(tempLight != null)
                        {
                            tempLight.Transformation.Rotate(rot);
                        }
                        break;
                    }

                    // Mesh3d end Material start
                    case "material":
                    {
                        flush();
                        tempMaterial = new GenericMaterial(Vector3.One);
                        tempMaterial.Name = data;
                        break;
                    }
                    case "type":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMaterial.Type = (GenericMaterial.MaterialType)Enum.Parse(typeof(GenericMaterial.MaterialType), data, true);
                        break;
                    }
                    case "invertnormalmap":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                       // tempMaterial.InvertNormalMap = data == "true" ? true : false;
                        break;
                    }
                    case "transparency":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        //tempMaterial.SupportTransparency = data == "true" ? true : false;
                        break;
                    }
                    case "normalmap":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMaterial.NormalsTexture = new Texture(Media.Get(data));
                        break;
                    }
                    case "bumpmap":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMaterial.BumpTexture = new Texture(Media.Get(data));
                        break;
                    }
                    case "roughnessmap":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMaterial.RoughnessTexture = new Texture(Media.Get(data));
                        break;
                    }
                    case "metalnessmap":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                       // tempMaterial.MetalnessMap = new Texture(Media.Get(data));
                        break;
                    }
                    case "roughness":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        float f;
                        if(!float.TryParse(data, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMaterial.Roughness = f;
                        break;
                    }
                    case "metalness":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        float f;
                        if(!float.TryParse(data, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        //tempMaterial.Metalness = f;
                        break;
                    }
                    case "color":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        string[] literals = data.Split(' ');
                        if(literals.Length != 3)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        float x, y, z;
                        if(!float.TryParse(literals[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        if(!float.TryParse(literals[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMaterial.DiffuseColor = new Vector3(x, y, z);
                        break;
                    }
                    case "texture":
                    {
                        if(tempMaterial == null)
                            throw new ArgumentException("Invalid line in scene string: " + l);
                        tempMaterial.DiffuseTexture = new Texture(Media.Get(data));
                        tempMaterial.SpecularTexture = tempMaterial.DiffuseTexture;
                        //tempMaterial.Mode = GenericMaterial.DrawMode.TextureMultipleColor;
                        break;
                    }

                    // Material end
                    default:
                    throw new ArgumentException("Invalid line in scene string: " + l);
                }
            }
            flush();
        }
    }
}