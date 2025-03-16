using System.Collections.Generic;
using System.Numerics;

namespace VisualEQ.Engine
{
    public class AniModel
    {
        public readonly List<AnimatedMesh> Meshes = new List<AnimatedMesh>();

        public void Add(AnimatedMesh mesh) => Meshes.Add(mesh);

        public void Draw(Matrix4x4 projView, Matrix4x4 modelMat, string animation, float aniTime, bool forward) => Meshes.ForEach(mesh => mesh.Draw(projView, modelMat, animation, aniTime, forward));
    }
}
