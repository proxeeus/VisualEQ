using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MoreLinq;
using VisualEQ.Common;
using OpenTK.Graphics.OpenGL4;

namespace VisualEQ.Engine
{
    // Shared per-mesh GL state: index buffer + per-animation vertex buffers and VAOs.
    // Instantiated once per (chr-zip, model, mesh-index, singleFrame) tuple and reused
    // across every AniModel variant that differs only in material (texture, helm, face).
    // Before this class existed, each variant AniModel duplicated its own VBOs/VAOs —
    // one Freeport with 24 human-male combos allocated 24 × 13 = 312 unique VAO sets
    // instead of the 13 the geometry actually needs.
    public class MeshGeometry
    {
        public readonly Buffer<uint> IndexBuffer;
        public readonly Dictionary<string, (IReadOnlyList<Vao> Vaos, IReadOnlyList<Buffer<float>> Buffers)> Animations;

        public MeshGeometry(uint[] indices, Dictionary<string, IReadOnlyList<IReadOnlyList<float>>> animations)
        {
            IndexBuffer = new Buffer<uint>(indices, BufferTarget.ElementArrayBuffer);
            Animations = animations.Select(kv =>
            {
                var buffers = kv.Value.Select(x => new Buffer<float>(x.ToArray())).ToList();
                var vaos = buffers.Count.Times(i =>
                {
                    var vao = new Vao();
                    vao.Attach(IndexBuffer);
                    vao.Attach(buffers[i], (0, typeof(Vector3)), (1, typeof(Vector3)), (2, typeof(Vector2)));
                    vao.Attach(buffers[(i + 1) % buffers.Count], (3, typeof(Vector3)), (4, typeof(Vector3)), (5, typeof(Vector2)));
                    return vao;
                }).ToList();
                return (kv.Key, ((IReadOnlyList<Vao>)vaos, (IReadOnlyList<Buffer<float>>)buffers));
            }).ToDictionary(x => x.Item1, x => x.Item2);
        }
    }
}
