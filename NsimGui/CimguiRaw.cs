using System.Runtime.InteropServices;

namespace NsimGui
{
    // Direct P/Invoke to the native cimgui.dll for the handful of functions that
    // ImGui.NET 0.4.6's managed wrapper never bound. All of these exist in the
    // native binding (verified via `strings cimgui.dll | grep ig...`) — the C#
    // wrapper just didn't expose them.
    //
    // Kept as a static class in NsimGui so any consumer (sidebar, popups, custom
    // widgets) can reach them without duplicating DllImport declarations. Add
    // more entries here as we hit other wrapper gaps.
    public static class CimguiRaw
    {
        // Native ImVec2 layout — {float x; float y;} matches the ImGuiNET.Vector2
        // structs used elsewhere but keeping a local copy avoids taking a dep on
        // System.Numerics / ImGuiNET assembly layout for this narrow API.
        [StructLayout(LayoutKind.Sequential)]
        public struct ImVec2
        {
            public float X;
            public float Y;
            public ImVec2(float x, float y) { X = x; Y = y; }
        }

        // Set the content-size hint for the NEXT window (BeginWindow OR BeginChild).
        // Passing size.y > 0 makes ImGui reserve enough virtual scroll extent to
        // cover that height, even when the actual rendered content is shorter or
        // when the ImGui.NET 0.4.6 outer-window scroll calculation caps at ~one
        // page. Set to 0 to defer to auto-measured content size.
        //
        // This is the fix for the "sidebar scrollbar caps at one page even though
        // widgets extend below it" bug — call this before BeginChild with a value
        // safely larger than any expected content height (e.g. 20000f) and the
        // scroll extent covers the full content.
        [DllImport("cimgui")]
        public static extern void igSetNextWindowContentSize(ImVec2 size);

        // Programmatic scroll control — read/write current scroll offset and query
        // the max for the current window. Handy for "reset scroll to top when the
        // selected NPC changes" (SetScrollY(0)) and clamp checks.
        [DllImport("cimgui")]
        public static extern float igGetScrollY();

        [DllImport("cimgui")]
        public static extern float igGetScrollMaxY();

        [DllImport("cimgui")]
        public static extern void igSetScrollY(float scroll_y);

        // Constrain the width of the NEXT item (input, combo, drag, etc.). Positive
        // values are absolute pixels; negative values are "window-content-width plus
        // this value" so -1 fills-to-end-of-line. Persists for one item only — call
        // once per widget you want to size. Fills the wrapper gap for a common need
        // in tabular editors (fixed-column widths without pushing / popping global
        // item widths).
        [DllImport("cimgui")]
        public static extern void igSetNextItemWidth(float item_width);
    }
}
