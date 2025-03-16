using System;
using System.Numerics;
using ImGuiNET;
using MoreLinq;

namespace NsimGui.Widgets
{
    public class HBox : BaseContainerWidget
    {
        public override void Render(Gui gui)
        {
            var last = Children.Count - 1;
            Children.ForEach((child, i) =>
            {
                child.Render(gui);
                if (i != last)
                    ImGui.SameLine();
            });
        }
    }
}
