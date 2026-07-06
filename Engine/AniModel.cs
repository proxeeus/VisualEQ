using System.Collections.Generic;
using System.Numerics;

namespace VisualEQ.Engine
{
    public class AniModel
    {
        public readonly List<AnimatedMesh> Meshes = new List<AnimatedMesh>();

        // Names of animation sets actually loaded into GL (populated by Loader.LoadCharacter).
        // Callers pick a candidate name from this set before setting AniModelInstance.Animation.
        public readonly HashSet<string> AvailableAnimations = new HashSet<string>();

        public void Add(AnimatedMesh mesh) => Meshes.Add(mesh);

        public void Draw(Matrix4x4 projView, Matrix4x4 modelMat, string animation, float aniTime, bool forward) => Meshes.ForEach(mesh => mesh.Draw(projView, modelMat, animation, aniTime, forward));
    }
}
