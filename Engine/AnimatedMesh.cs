using System;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace VisualEQ.Engine
{
    public class AnimatedMesh
    {
        public bool Enabled = true;

        public readonly Material Material;
        readonly MeshGeometry Geometry;

        // Preferred path: pre-built shared geometry, per-variant material. Used by
        // Loader.LoadCharacter so npc.Texture / npc.HelmTexture / npc.Face variants
        // of the same base model share the same VBOs / VAOs / index buffers.
        public AnimatedMesh(Material material, MeshGeometry geometry)
        {
            Material = material;
            Geometry = geometry;
        }

        // Backwards-compatible convenience overload — creates a fresh MeshGeometry
        // and owns it. Used by any legacy caller that hasn't been threaded through
        // a geometry cache yet.
        public AnimatedMesh(Material material, System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<float>>> animations, uint[] indices)
            : this(material, new MeshGeometry(indices, animations)) { }

        public void Draw(Matrix4x4 projView, Matrix4x4 modelMat, string animation, float aniTime, bool forward)
        {
            if (!Enabled) return;
            if (forward && Material.Deferred || !forward && !Material.Deferred) return;

            if (!Geometry.Animations.TryGetValue(animation, out var aniData) && !Geometry.Animations.TryGetValue("", out aniData))
                return;

            Material.Use(projView, MaterialUse.Animated);
            Material.SetModelMatrix(modelMat);
            const float fps = 1f / 10;
            var frameCount = (int)(aniTime / fps);
            Material.SetInterpolation(aniTime % fps / fps);

            aniData.Vaos[frameCount % aniData.Vaos.Count].Bind(() => GL.DrawElements(PrimitiveType.Triangles, Geometry.IndexBuffer.Length, DrawElementsType.UnsignedInt, IntPtr.Zero));
        }
    }
}
