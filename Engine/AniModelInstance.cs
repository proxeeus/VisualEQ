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
        float _RenderZBias;

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

        // Render-time-only Z shift, added to Position.Z at draw time — NOT
        // reflected back through the Position getter. SpawnManager uses this to
        // foot-align meshes whose authored origin is well above the visual feet
        // (dragons/wurms have MinZ around -20, so at scale 1× they sink 20 units
        // below spawn.z). Kept out of Position so drag / save keep reading the
        // raw scene Z = DB spawn.z.
        public float RenderZBias
        {
            get => _RenderZBias;
            set { _RenderZBias = value; RebuildTransform(); }
        }

        // Pre-shift the mesh vertices so the authored XY-center is at (0,0),
        // THEN scale/rotate/translate. Without this, rotation happens around
        // the artist's model-space origin — which for dragons/wurms sits at
        // one end of the body — so the visible mesh renders off to the side
        // of Position (green DB spawn cage on the floor, dragon in the wall).
        void RebuildTransform()
        {
            var c = Model?.AuthoredCenterXY ?? Vector3.Zero;
            Transform = Matrix4x4.CreateTranslation(-c)
                      * Matrix4x4.CreateScale(_Scale)
                      * Matrix4x4.CreateFromQuaternion(_Rotation)
                      * Matrix4x4.CreateTranslation(new Vector3(_Position.X, _Position.Y, _Position.Z + _RenderZBias));
        }

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
