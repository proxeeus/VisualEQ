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

        // Bind-pose Z bounds of the always-render + base-head meshes (populated by
        // Loader.LoadCharacter). SpawnManager uses these to (a) size the mesh so
        // `npc.Size` matches rendered world height and (b) shift Position.Z so the
        // mesh's lowest vertex sits at spawn.z rather than the mesh's authored
        // origin — most dragons/wurms are authored with origin at chest and mesh
        // extending 20+ units below, so without this shift they render deep
        // underground when placed at DB spawn.z. 0/0 = not computed (fall back
        // to per-race table).
        public float AuthoredMinZ;
        public float AuthoredMaxZ;
        public float AuthoredHeight => AuthoredMaxZ - AuthoredMinZ;

        // XY bounds too — used by AniModelInstance.RebuildTransform to shift
        // meshes so their visible XY-center coincides with Position, not the
        // artist's arbitrary model-space origin. Dragons in particular are
        // authored with origin far from the visible body center (tail-anchored
        // is common), so without this shift the mesh renders far off to the
        // side of the DB spawn point (green selection cage on the floor, dragon
        // body over here in the wall).
        public float AuthoredMinX, AuthoredMaxX;
        public float AuthoredMinY, AuthoredMaxY;
        public Vector3 AuthoredCenterXY => new Vector3(
            (AuthoredMinX + AuthoredMaxX) * 0.5f,
            (AuthoredMinY + AuthoredMaxY) * 0.5f,
            0f);

        public void Add(AnimatedMesh mesh) => Meshes.Add(mesh);

        public void Draw(Matrix4x4 projView, Matrix4x4 modelMat, string animation, float aniTime, bool forward) => Meshes.ForEach(mesh => mesh.Draw(projView, modelMat, animation, aniTime, forward));
    }
}
