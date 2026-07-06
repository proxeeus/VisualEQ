using System;
using System.Collections.Generic;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace VisualEQ.Engine
{
    // Renders path grid waypoints for the selected spawn: a polyline through the waypoints
    // in `number` order plus a small crosshair at each waypoint. Same dynamic VBO pattern
    // as SpawnMarkers — kept as a separate class so its style (color, width) evolves
    // independently from the state markers.
    public class PathGridRenderer
    {
        const int MaxLines = 4096;
        const int FloatsPerVertex = 7;   // 3 pos + 4 color

        readonly Vao _vao;
        readonly Buffer<float> _buffer;
        readonly Program _program;
        int _lineCount;

        public PathGridRenderer()
        {
            _buffer = new Buffer<float>(
                new float[MaxLines * 2 * FloatsPerVertex],
                BufferTarget.ArrayBuffer,
                BufferUsageHint.DynamicDraw);

            _vao = new Vao();
            _vao.Attach(_buffer, (0, typeof(Vector3)), (1, typeof(Vector4)));

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
            var count = Math.Min(lines.Count, MaxLines);
            var data = new float[count * 2 * FloatsPerVertex];
            for (int i = 0; i < count; i++)
            {
                var (a, b, c) = lines[i];
                int o = i * 2 * FloatsPerVertex;
                data[o + 0] = a.X; data[o + 1] = a.Y; data[o + 2] = a.Z;
                data[o + 3] = c.X; data[o + 4] = c.Y; data[o + 5] = c.Z; data[o + 6] = c.W;
                data[o + 7] = b.X; data[o + 8] = b.Y; data[o + 9] = b.Z;
                data[o + 10] = c.X; data[o + 11] = c.Y; data[o + 12] = c.Z; data[o + 13] = c.W;
            }
            _lineCount = count;
            if (count > 0) _buffer.Update(data);
        }

        public void Draw(Matrix4x4 projView)
        {
            if (_lineCount == 0) return;

            _program.Use();
            _program.SetUniform("uProjectionViewMat", projView);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.LineWidth(2f);

            _vao.Bind(() => GL.DrawArrays(PrimitiveType.Lines, 0, _lineCount * 2));

            GL.LineWidth(1f);
            GL.Disable(EnableCap.Blend);
        }
    }
}
