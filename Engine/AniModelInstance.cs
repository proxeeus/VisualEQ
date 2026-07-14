using System.Numerics;
using static VisualEQ.Engine.Globals;

namespace VisualEQ.Engine
{
    public class AniModelInstance
    {
        public readonly AniModel Model;

        Matrix4x4 Transform = Matrix4x4.Identity;

        Vector3 _Position;
        Quaternion _Rotation = Quaternion.Identity;
        float _Scale = 1f;

        public Vector3 Position
        {
            get => _Position;
            set { _Position = value; RebuildTransform(); }
        }

        public Quaternion Rotation
        {
            get => _Rotation;
            set { _Rotation = value; RebuildTransform(); }
        }

        // Uniform scale, applied before rotation + translation. `npc_types.size`
        // in EQEmu is 6.0 for a standard humanoid; SpawnManager normalises to
        // this (Scale = size / 6). Values <= 0 are treated as 1 so unset/junk
        // data doesn't zero-out the mesh.
        public float Scale
        {
            get => _Scale;
            set { _Scale = value <= 0f ? 1f : value; RebuildTransform(); }
        }

        void RebuildTransform() =>
            Transform = Matrix4x4.CreateScale(_Scale)
                      * Matrix4x4.CreateFromQuaternion(_Rotation)
                      * Matrix4x4.CreateTranslation(_Position);

        string _Animation = "";
        float AnimationStartTime = FrameTime;
        public string Animation
        {
            get => _Animation;
            set
            {
                _Animation = value;
                AnimationStartTime = FrameTime;
            }
        }

        public AniModelInstance(AniModel model) => Model = model;

        public void Draw(Matrix4x4 projView, bool forward) =>
            Model.Draw(projView, Transform, Animation, FrameTime - AnimationStartTime, forward);
    }
}
