using System;
using System.Text;
using ImGuiNET;

namespace NsimGui.Widgets
{
    public class InputText : BaseWidget
    {
        public Func<string> Label;
        private readonly byte[] _buffer;

        public string Value
        {
            get => Encoding.UTF8.GetString(_buffer).TrimEnd('\0');
            set
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                var bytes = Encoding.UTF8.GetBytes(value ?? "");
                Array.Copy(bytes, _buffer, Math.Min(bytes.Length, _buffer.Length - 1));
            }
        }

        public event Action<string> Changed;

        public InputText(string label, string initialValue = "", int bufferSize = 256)
        {
            Label = () => label;
            _buffer = new byte[bufferSize];
            Value = initialValue;
        }

        public InputText(Func<string> label, string initialValue = "", int bufferSize = 256)
        {
            Label = label;
            _buffer = new byte[bufferSize];
            Value = initialValue;
        }

        public override void Render(Gui gui)
        {
            var prev = Value;
            if (ImGui.InputText($"{Label()}###{Id}", _buffer, (uint)_buffer.Length, InputTextFlags.Default, null))
            {
                var curr = Value;
                if (curr != prev)
                    Changed?.Invoke(curr);
            }
        }
    }
}
