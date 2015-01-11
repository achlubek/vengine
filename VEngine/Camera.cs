﻿using OpenTK;

namespace VDGTech
{
    public class Camera
    {
        static public Camera Current;
        public Matrix4 ViewMatrix, ProjectionMatrix;
        public Vector3 Position;
        public float Pitch, Roll;

        public Camera(Vector3 position, Vector3 lookAt, float aspectRatio, float fov, float near, float far)
        {
            Position = position;
            ViewMatrix = Matrix4.LookAt(position, lookAt, new Vector3(0, 1, 0));
            Matrix4.CreatePerspectiveFieldOfView(fov, aspectRatio, near, far, out ProjectionMatrix);
            if (Current == null) Current = this;
            Pitch = 0.0f;
            Roll = 0.0f;
        }

        public void LookAt(Vector3 location)
        {
            ViewMatrix = Matrix4.LookAt(Position, location, new Vector3(0, 1, 0));
        }

        public void ProcessMouseMovement(int deltax, int deltay)
        {
            Pitch += (float)deltax / 100.0f;
            if(Pitch > MathHelper.TwoPi) Pitch = 0.0f;

            Roll += (float)deltay / 100.0f;
            if(Roll > MathHelper.TwoPi) Roll = 0.0f;

            Update();
        }
        void Update()
        {
            var rotationX = Quaternion.FromAxisAngle(Vector3.UnitY, Pitch);
            var rotationY = Quaternion.FromAxisAngle(Vector3.UnitX, Roll);
            ViewMatrix = Matrix4.CreateTranslation(Position) * Matrix4.CreateFromQuaternion(rotationX) * Matrix4.CreateFromQuaternion(rotationY);
        }
        public void ProcessKeyboardState(OpenTK.Input.KeyboardState keys)
        {
            if (keys.IsKeyDown(OpenTK.Input.Key.W))
            {
                var rotationX = Quaternion.FromAxisAngle(Vector3.UnitY, -Pitch);
                var rotationY = Quaternion.FromAxisAngle(Vector3.UnitX, -Roll);
                Vector4 direction = -Vector4.UnitZ;
                direction = Vector4.Transform(direction, rotationY);
                direction = Vector4.Transform(direction, rotationX);
                Position -= direction.Xyz;
                Update();
            }
        }
    }
}