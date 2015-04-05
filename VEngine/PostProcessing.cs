﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using VDGTech.Particles;
using System.Drawing;

namespace VDGTech
{
    public class PostProcessing
    {
        private int Width, Height;

        private ShaderProgram 
            BloomShader, 
            MSAAShader, 
            SSAOShader, 
            FogShader, 
            LightPointsShader, 
            LensBlurShader, 
            HDRShader,
            WorldPosWriterShader,
            NormalsWriterShader,
            GlobalIlluminationShader, 
            BlitShader, 
            DeferredShader, 
            CombinerShader,
            BackDepthWriterShader,
            ReflectShader,
            ScreenSpaceNormalsWriterShader;

        private Framebuffer 
            MSAAResolvingFrameBuffer, 
            Pass1FrameBuffer, 
            Pass2FrameBuffer, 
            LightPointsFrameBuffer, 
            BloomFrameBuffer, 
            FogFramebuffer, 
            WorldPositionFrameBuffer, 
            NormalsFrameBuffer, 
            ScreenSpaceNormalsFrameBuffer,
            SmallFrameBuffer, 
            GlobalIlluminationFrameBuffer,
            DiffuseColorFrameBuffer,
            BackDiffuseFrameBuffer,
            BackNormalsFrameBuffer;

        private Mesh3d PostProcessingMesh;

        private static uint[] postProcessingPlaneIndices = {
                0, 1, 2, 3, 2, 1
            };

        private static float[] postProcessingPlaneVertices = {
                -1.0f, -1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, -1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                -1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f
            };

        public PostProcessing(int initialWidth, int initialHeight)
        {
            Width = initialWidth;
            Height = initialHeight;
            MSAAResolvingFrameBuffer = new Framebuffer(initialWidth, initialHeight);
            MSAAResolvingFrameBuffer.SetMultiSample(true);

            DiffuseColorFrameBuffer = new Framebuffer(initialWidth, initialHeight);

            Pass1FrameBuffer = new Framebuffer(initialWidth, initialHeight);
            Pass2FrameBuffer = new Framebuffer(initialWidth, initialHeight);
            WorldPositionFrameBuffer = new Framebuffer(initialWidth, initialHeight);
            NormalsFrameBuffer = new Framebuffer(initialWidth, initialHeight);
            ScreenSpaceNormalsFrameBuffer = new Framebuffer(initialWidth / 3, initialHeight / 3);

            LightPointsFrameBuffer = new Framebuffer(initialWidth / 6, initialHeight / 6);
            BloomFrameBuffer = new Framebuffer(initialWidth / 6, initialHeight / 3);
            FogFramebuffer = new Framebuffer(initialWidth / 3, initialHeight / 3);
            SmallFrameBuffer = new Framebuffer(initialWidth / 10, initialHeight / 10);

            GlobalIlluminationFrameBuffer = new Framebuffer(initialWidth / 2, initialHeight / 2);
            //BackDiffuseFrameBuffer = new Framebuffer(initialWidth / 2, initialHeight / 2);
            //BackNormalsFrameBuffer = new Framebuffer(initialWidth / 2, initialHeight / 2); 

            WorldPosWriterShader = ShaderProgram.Compile(Media.ReadAllText("Generic.vertex.glsl"), Media.ReadAllText("WorldPosWriter.fragment.glsl"));
            NormalsWriterShader = ShaderProgram.Compile(Media.ReadAllText("Generic.vertex.glsl"), Media.ReadAllText("NormalsWriter.fragment.glsl"));
            ScreenSpaceNormalsWriterShader = ShaderProgram.Compile(Media.ReadAllText("Generic.vertex.glsl"), Media.ReadAllText("ScreenSpaceNormalsWriter.fragment.glsl"));
            BackDepthWriterShader = ShaderProgram.Compile(Media.ReadAllText("Generic.vertex.glsl"), Media.ReadAllText("BackDepthWriter.fragment.glsl"));

            BloomShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("Bloom.fragment.glsl"));
            MSAAShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("MSAA.fragment.glsl"));
            SSAOShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("SSAO.fragment.glsl"));
            FogShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("Fog.fragment.glsl"));
            LightPointsShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("LightPoints.fragment.glsl"));
            LensBlurShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("LensBlur.fragment.glsl"));
            HDRShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("HDR.fragment.glsl"));
            BlitShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("Blit.fragment.glsl"));
            DeferredShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("Deferred.fragment.glsl"));
            CombinerShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("Combiner.fragment.glsl"));
            GlobalIlluminationShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("GlobalIllumination.fragment.glsl"));
            ReflectShader = ShaderProgram.Compile(Media.ReadAllText("PostProcess.vertex.glsl"), Media.ReadAllText("Reflect.fragment.glsl"));

            Object3dInfo postPlane3dInfo = new Object3dInfo(postProcessingPlaneVertices, postProcessingPlaneIndices);
            PostProcessingMesh = new Mesh3d(postPlane3dInfo, new SolidColorMaterial(Color.Pink));
        }

        private Framebuffer LastFrameBuffer;

        private void SwitchToFB0()
        {
            Pass2FrameBuffer.RevertToDefault();
            GL.Viewport(0, 0, Width, Height);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            LastFrameBuffer.UseTexture(0);
        }
        private Framebuffer SwitchToFB1()
        {
            Pass1FrameBuffer.Use();
            LastFrameBuffer.UseTexture(0);
            LastFrameBuffer = Pass1FrameBuffer;
            return Pass1FrameBuffer;
        }
        private Framebuffer SwitchToFB2()
        {
            Pass2FrameBuffer.Use();
            LastFrameBuffer.UseTexture(0);
            LastFrameBuffer = Pass2FrameBuffer;
            return Pass2FrameBuffer;
        }

        private void SwitchToFB(Framebuffer buffer)
        {
            buffer.Use();
            if(buffer == Pass1FrameBuffer || buffer == Pass2FrameBuffer)
            {
                LastFrameBuffer.UseTexture(0);
                LastFrameBuffer = buffer;
            }
        }

        private void EnableFullBlend()
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
        }

        private void DisableBlending()
        {
            GL.Disable(EnableCap.Blend);
        }

        private void Bloom()
        {
            BloomShader.Use();
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }
        private void GlobalIllumination()
        {
            GlobalIlluminationShader.Use();
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }

        private void LightsPoints()
        {
            LightPointsShader.Use();
            LightPool.MapSimpleLightsToShader(LightPointsShader);
            SetLightingUniforms(LightPointsShader);
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }

        private void SSAO()
        {
            SSAOShader.Use();
            WorldPositionFrameBuffer.UseTexture(30);
            NormalsFrameBuffer.UseTexture(31);
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }
        private void Deferred()
        {
            DeferredShader.Use();
            WorldPositionFrameBuffer.UseTexture(30);
            NormalsFrameBuffer.UseTexture(31);
            LightPool.MapSimpleLightsToShader(DeferredShader);
            SetLightingUniforms(DeferredShader);
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }

        private void Fog()
        {
            FogShader.Use();
            WorldPositionFrameBuffer.UseTexture(30);
            FogShader.SetUniform("Time", (float)(DateTime.Now - GLThread.StartTime).TotalMilliseconds / 1000);
            SetLightingUniforms(FogShader);
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }
        private void HDR()
        {
            HDRShader.Use();
            HDRShader.SetUniform("Brightness", Camera.Current.Brightness);
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }
        private void LensBlur()
        {
            LensBlurShader.Use();
            if(Camera.Current != null)
            {
                LensBlurShader.SetUniform("CameraCurrentDepth", Camera.Current.CurrentDepthFocus);
                LensBlurShader.SetUniform("LensBlurAmount", Camera.Current.LensBlurAmount);
            }
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }

        private void Blit()
        {
            BlitShader.Use();
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }
        private void WriteBackFaces()
        {
            GL.CullFace(CullFaceMode.Front);
            BackDiffuseFrameBuffer.Use();
            World.Root.Draw();
            GL.CullFace(CullFaceMode.Back);
        }
        private void Combine()
        {
            CombinerShader.Use();
            CombinerShader.SetUniform("UseSimpleGI", UseSimpleGI);
            CombinerShader.SetUniform("UseFog", UseFog);
            CombinerShader.SetUniform("UseLightPoints", UseLightPoints);
            CombinerShader.SetUniform("UseDepth", UseDepth);
            CombinerShader.SetUniform("UseBloom", UseBloom);
            CombinerShader.SetUniform("UseDeferred", UseDeferred);
            CombinerShader.SetUniform("UseBilinearGI", UseBilinearGI);
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;
        }

        private Framebuffer SwitchBetweenFB()
        {
            if(LastFrameBuffer == Pass1FrameBuffer)
                return SwitchToFB2();
            else
                return SwitchToFB1();
        }

        public bool UseSimpleGI = true;
        public bool UseFog = true;
        public bool UseLightPoints = true;
        public bool UseDepth = false;
        public bool UseBloom = true;
        public bool UseDeferred = true;
        public bool UseBilinearGI = false;

        public void ExecutePostProcessing()
        {
            

            //WriteBackDepth();

            DisableBlending();

            // we dont need particles in normals and world pos passes so
            WorldPosWriterShader.Use();
            ShaderProgram.Lock = true;
            WorldPositionFrameBuffer.Use();
            World.Root.Draw();
            ShaderProgram.Lock = false;

            EnableFullBlend();
            MSAAResolvingFrameBuffer.Use();
            WorldPositionFrameBuffer.UseTexture(2);
            // and then draw the scene
            World.Root.Draw();
            //ParticleSystem.DrawAll();
            if(Skybox.Current != null)
                Skybox.Current.Draw();

            DisableBlending();

            NormalsWriterShader.Use();
            ShaderProgram.Lock = true;
            NormalsFrameBuffer.Use();
            World.Root.Draw();
            //BackNormalsFrameBuffer.Use();
           // GL.CullFace(CullFaceMode.Front);
            //World.Root.Draw();
            //GL.CullFace(CullFaceMode.Back);
            ShaderProgram.Lock = false;
            
            ScreenSpaceNormalsWriterShader.Use();
            ShaderProgram.Lock = true;
            ScreenSpaceNormalsFrameBuffer.Use();
            World.Root.Draw();
            ShaderProgram.Lock = false;

            //WriteBackFaces();

            LastFrameBuffer = MSAAResolvingFrameBuffer;

            EnableFullBlend();
            LightPool.UseTextures(2);

            // we are into the game! We have world pos and normals, and MSAA scene
            // Scene is already drawn into MSAA framebuffer so need to resolve it

            SwitchToFB1();
            MSAAShader.Use();
            ShaderProgram.Lock = true;
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;

            // now we have MSAA filetered img


            SwitchToFB(DiffuseColorFrameBuffer);
            LastFrameBuffer.UseTexture(0);
            Blit();

            if(UseLightPoints)
            {
                SwitchToFB(LightPointsFrameBuffer);
                LastFrameBuffer.UseTexture(0);
                LightsPoints();
            }

            if(UseFog)
            {
                SwitchToFB(FogFramebuffer);
                LastFrameBuffer.UseTexture(0);
                Fog();
            }

            //SwitchBetweenFB();
            //SSAO();

            if(UseDeferred)
            {
                SwitchBetweenFB();
                Deferred();
            }

            if(UseBloom)
            {
                SwitchToFB(BloomFrameBuffer);
                LastFrameBuffer.UseTexture(0);
                Bloom();
            }

            var oddfb = SwitchBetweenFB();
            var evenfb = oddfb == Pass1FrameBuffer ? Pass2FrameBuffer : Pass1FrameBuffer;
            if(UseBilinearGI || UseSimpleGI)
            {
                GlobalIlluminationFrameBuffer.UseTexture(0);
                Blit();

                SwitchToFB(GlobalIlluminationFrameBuffer);
                evenfb.UseTexture(0);
                DiffuseColorFrameBuffer.UseTexture(2);
                WorldPositionFrameBuffer.UseTexture(3);
                NormalsFrameBuffer.UseTexture(4);
                oddfb.UseTexture(5);
                ScreenSpaceNormalsFrameBuffer.UseTexture(6);
                //BackDiffuseFrameBuffer.UseTexture(7);
                //BackNormalsFrameBuffer.UseTexture(9);
                GlobalIllumination();
            }

            /*SwitchToFB(GlobalIlluminationFrameBuffer);

            ReflectShader.Use();
            LastFrameBuffer.UseTexture(0);
            ShaderProgram.Lock = true;
            //WorldPositionFrameBuffer.UseTexture(3);
            //NormalsFrameBuffer.UseTexture(4);
            ScreenSpaceNormalsFrameBuffer.UseTexture(5);
            PostProcessingMesh.Draw();
            ShaderProgram.Lock = false;*/

            SwitchToFB1();

            evenfb.UseTexture(0);
            FogFramebuffer.UseTexture(2);
            LightPointsFrameBuffer.UseTexture(3);
            BloomFrameBuffer.UseTexture(4);
            GlobalIlluminationFrameBuffer.UseTexture(5);
            DiffuseColorFrameBuffer.UseTexture(6);
            //BackDepthFrameBuffer.UseTexture(6);

            

            Combine();


            //SwitchToFB(SmallFrameBuffer);
           // Pass2FrameBuffer.UseTexture(0);
           // Blit();

           // SwitchBetweenFB();
           // if(World.Root.SkyDome != null) World.Root.SkyDome.Draw();
            //LensBlur();

            SwitchToFB0();
            HDR();
        }

        private void SetLightingUniforms(ShaderProgram shader)
        {
            shader.SetUniformArray("LightsPs", LightPool.GetPMatrices());
            shader.SetUniformArray("LightsVs", LightPool.GetVMatrices());
            shader.SetUniformArray("LightsPos", LightPool.GetPositions());
            shader.SetUniformArray("LightsFarPlane", LightPool.GetFarPlanes());
            shader.SetUniformArray("LightsColors", LightPool.GetColors());
            shader.SetUniform("LightsCount", LightPool.GetPositions().Length);
        }

        public void UpdateCameraFocus(Camera camera)
        {
            GLThread.Invoke(() => camera.CurrentDepthFocus = (camera.CurrentDepthFocus * 4.0f + SmallFrameBuffer.GetDepth(0.5f, 0.5f)) / 5.0f);
        }

        public void UpdateCameraBrightness(Camera camera)
        {
            if(!SmallFrameBuffer.Generated)
                return;
            GLThread.Invoke(() =>
            {
                var pixels = SmallFrameBuffer.GetColorBuffer();
                GLThread.RunAsync(() =>
                {
                    float average = 0.0f;
                    for(int i = 0; i < pixels.Length; i += 4)
                    {
                        var l = pixels[i].ToVector3().LengthFast;
                        average += l / pixels.Length;
                    }
                    camera.Brightness = (camera.Brightness * 7.0f + (1.5f - average * 8.0f)) / 8.0f;
                    if(camera.Brightness < 0.6f)
                        camera.Brightness = 0.6f;
                    if(camera.Brightness > 1.0f)
                        camera.Brightness = 1.0f;
                });
            });
        }
    }
}