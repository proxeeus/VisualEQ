using System;
using System.Collections.Generic;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace VisualEQ.Engine
{
    // Draws trilogy_zone_point trigger volumes. Two persistent dynamic VBOs so per-frame
    // cost is BufferSubData + one draw call each. Same pattern as SpawnMarkers /
    // PathGridRenderer — the Controller computes the primitive lists and pushes them each
    // frame; the engine's render thread consumes them here.
    //
    // Primitive types:
    //   • Line list — box wireframes, plane wireframes, direction arrows, handle crosses.
    //   • Triangle list — semi-transparent volumetric fills for box interiors, plane walls,
    //     and wildcard-source horizontal slabs.
    public class ZonePointRenderer
    {
        const int MaxLineVerts = 32768;   // 16k line segments
        const int MaxTriVerts  = 32768;   // ~10k triangles
        const int FloatsPerVertex = 7;    // 3 pos + 4 color

        readonly Vao _lineVao;
        readonly Buffer<float> _lineBuffer;
        readonly Vao _triVao;
        readonly Buffer<float> _triBuffer;
        readonly Program _program;

        int _lineVertCount;
        int _triVertCount;

        public ZonePointRenderer()
        {
            _lineBuffer = new Buffer<float>(
                new float[MaxLineVerts * FloatsPerVertex],
                BufferTarget.ArrayBuffer,
                BufferUsageHint.DynamicDraw);
            _lineVao = new Vao();
            _lineVao.Attach(_lineBuffer, (0, typeof(Vector3)), (1, typeof(Vector4)));

            _triBuffer = new Buffer<float>(
                new float[MaxTriVerts * FloatsPerVertex],
                BufferTarget.ArrayBuffer,
                BufferUsageHint.DynamicDraw);
            _triVao = new Vao();
            _triVao.Attach(_triBuffer, (0, typeof(Vector3)), (1, typeof(Vector4)));

            _program = new Program(@"
#version 410
precision highp float;
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec4 aColor;
uniform mat4 uProjectionViewMat;
out vec4 vColor;

void main() {
    vColor = aColor;
    gl_Position = uProjectionViewMat * vec4(aPosition, 1);
}
", @"
#version 410
precision highp float;
in vec4 vColor;
layout (location = 0) out vec4 color;

void main() {
    color = vColor;
}
");
        }

        public void SetLines(IReadOnlyList<(Vector3 A, Vector3 B, Vector4 Color)> lines)
        {
            var count = Math.Min(lines.Count * 2, MaxLineVerts);
            var data = new float[count * FloatsPerVertex];
            int written = 0;
            for (int i = 0; i < lines.Count && written < count; i++)
            {
                var (a, b, c) = lines[i];
                WriteVertex(data, (written++) * FloatsPerVertex, a, c);
                WriteVertex(data, (written++) * FloatsPerVertex, b, c);
            }
            _lineVertCount = written;
            if (written > 0) _lineBuffer.Update(data);
        }

        public void SetTriangles(IReadOnlyList<(Vector3 A, Vector3 B, Vector3 C, Vector4 Color)> tris)
        {
            var count = Math.Min(tris.Count * 3, MaxTriVerts);
            var data = new float[count * FloatsPerVertex];
            int written = 0;
            for (int i = 0; i < tris.Count && written < count; i++)
            {
                var (a, b, c, color) = tris[i];
                WriteVertex(data, (written++) * FloatsPerVertex, a, color);
                WriteVertex(data, (written++) * FloatsPerVertex, b, color);
                WriteVertex(data, (written++) * FloatsPerVertex, c, color);
            }
            _triVertCount = written;
            if (written > 0) _triBuffer.Update(data);
        }

        static void WriteVertex(float[] data, int offset, Vector3 pos, Vector4 color)
        {
            data[offset + 0] = pos.X;
            data[offset + 1] = pos.Y;
            data[offset + 2] = pos.Z;
            data[offset + 3] = color.X;
            data[offset + 4] = color.Y;
            data[offset + 5] = color.Z;
            data[offset + 6] = color.W;
        }

        public void Draw(Matrix4x4 projView)
        {
            if (_lineVertCount == 0 && _triVertCount == 0) return;

            _program.Use();
            _program.SetUniform("uProjectionViewMat", projView);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            // Fills first so wireframes overdraw cleanly. Depth-write off on the fills so
            // volumes don't punch holes in each other or in overlapping spawn markers.
            if (_triVertCount > 0)
            {
                GL.DepthMask(false);
                _triVao.Bind(() => GL.DrawArrays(PrimitiveType.Triangles, 0, _triVertCount));
                GL.DepthMask(true);
            }

            if (_lineVertCount > 0)
            {
                GL.LineWidth(2f);
                _lineVao.Bind(() => GL.DrawArrays(PrimitiveType.Lines, 0, _lineVertCount));
                GL.LineWidth(1f);
            }

            GL.Disable(EnableCap.Blend);
        }
    }
}
