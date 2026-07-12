using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using NsimGui.Widgets;

namespace NsimGui
{
    public class Gui : IEnumerable<BaseWidget>
    {
        static ulong _UniqueID;
        public static ulong UniqueID => _UniqueID++;

        static IO IO => ImGui.GetIO();

        readonly IGuiRenderer Renderer;

        public Vector2 Dimensions
        {
            get => IO.DisplaySize;
            set => IO.DisplaySize = value;
        }

        public Vector2 Scale
        {
            get => IO.DisplayFramebufferScale;
            set => IO.DisplayFramebufferScale = value;
        }

        public bool MouseWanted    => IO.WantCaptureMouse;
        public bool KeyboardWanted => IO.WantCaptureKeyboard;

        // Forward a typed character to ImGui (call from KeyPress event).
        public void HandleChar(char c) => ImGui.AddInputCharacter(c);

        // Update a physical key's pressed state (call from OnKeyDown/OnKeyUp).
        // keyCode is (int)OpenTK.Input.Key cast value.
        public void SetKeyDown(int keyCode, bool down) => IO.KeysDown[keyCode] = down;

        public void SetModifiers(bool ctrl, bool shift, bool alt)
        {
            IO.CtrlPressed  = ctrl;
            IO.ShiftPressed = shift;
            IO.AltPressed   = alt;
        }

        // Map a GuiKey (by its int value) to a physical key code (OpenTK Key cast to int).
        public void SetKeyMap(int guiKeyIndex, int physicalKeyIndex) =>
            IO.KeyMap[(GuiKey)guiKeyIndex] = physicalKeyIndex;

        public (int X, int Y) MousePosition
        {
            get => ((int)(IO.MousePosition.X * Scale.X), (int)(IO.MousePosition.Y * Scale.Y));
            set => IO.MousePosition = new Vector2(value.X, value.Y) / Scale;
        }

        public bool MouseLeft
        {
            get => IO.MouseDown[0];
            set => IO.MouseDown[0] = value;
        }
        public bool MouseRight
        {
            get => IO.MouseDown[0];
            set => IO.MouseDown[0] = value;
        }

        public int WheelDelta;

        public readonly List<BaseWidget> Widgets = new List<BaseWidget>();

        public void Add(BaseWidget widget) => Widgets.Add(widget);

        public void Remove(BaseWidget widget) => Widgets.Remove(widget);

        public unsafe Gui(IGuiRenderer renderer)
        {
            if (File.Exists("imgui.ini"))
                File.Delete("imgui.ini");

            Renderer = renderer;
            IO.FontAtlas.AddDefaultFont();
            var fontTex = IO.FontAtlas.GetTexDataAsAlpha8();
            IO.FontAtlas.SetTexID(Renderer.CreateTexture(
                TextureFormat.Alpha, fontTex.Width, fontTex.Height,
                PointerToArray<byte>(fontTex.Pixels, fontTex.Width * fontTex.Height)));
        }

        public void Render(float deltaTime)
        {
            IO.DeltaTime = deltaTime;
            // OpenTK's MouseWheelEventArgs.Delta is already in "notches" (±1 per notch);
            // ImGui expects IO.MouseWheel = ±1 per notch too. The previous /10 divisor
            // made scrolling feel dead (10 notches to move one line) — pass through as-is.
            IO.MouseWheel = WheelDelta;
            WheelDelta = 0;

            ImGui.NewFrame();

            // Snapshot the list so a widget's Render can safely Add/Remove other widgets
            // (e.g. zone-scoped views registering their windows when a zone loads mid-frame).
            var snapshot = Widgets.ToArray();
            foreach (var w in snapshot) w.Render(this);

            ImGui.Render();
            Renderer.Draw((Dimensions.X / Scale.X, Dimensions.Y / Scale.Y), BuildDrawCommandSets());
        }

        unsafe T[] PointerToArray<T>(void* ptr, int count) where T : struct
        {
            var sptr = (byte*)ptr;
            var arr = new T[count];
            var size = Marshal.SizeOf<T>();
            for (var i = 0; i < count; ++i)
            {
                arr[i] = Unsafe.Read<T>(sptr);
                sptr += size;
            }
            return arr;
        }

        unsafe IReadOnlyList<DrawCommandSet> BuildDrawCommandSets()
        {
            var data = ImGui.GetDrawData();
            ImGui.ScaleClipRects(data, Scale);
            var csets = new List<DrawCommandSet>();

            for (var n = 0; n < data->CmdListsCount; ++n)
            {
                var cmdList = data->CmdLists[n];
                var commands = new List<DrawCommand>();
                var ioff = 0U;
                for (var i = 0; i < cmdList->CmdBuffer.Size; ++i)
                {
                    var cmd = ((DrawCmd*)cmdList->CmdBuffer.Data)[i];
                    Debug.Assert(cmd.UserCallback == IntPtr.Zero);

                    commands.Add(new DrawCommand
                    {
                        TextureId = (int)cmd.TextureId,
                        Scissor = (
                            (int)cmd.ClipRect.X, (int)(IO.DisplaySize.Y - cmd.ClipRect.W),
                            (int)(cmd.ClipRect.Z - cmd.ClipRect.X), (int)(cmd.ClipRect.W - cmd.ClipRect.Y)
                        ),
                        IndexOffset = (int)ioff,
                        ElementCount = (int)cmd.ElemCount
                    });
                    ioff += cmd.ElemCount;
                }
                csets.Add(new DrawCommandSet
                {
                    VBufferData = PointerToArray<byte>(cmdList->VtxBuffer.Data, cmdList->VtxBuffer.Size * sizeof(DrawVert)),
                    IBufferData = PointerToArray<ushort>(cmdList->IdxBuffer.Data, cmdList->IdxBuffer.Size),
                    Commands = commands
                });
            }

            return csets;
        }

        public IEnumerator<BaseWidget> GetEnumerator() => Widgets.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
