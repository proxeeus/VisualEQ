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

        // Push a stacked item-width for subsequent widgets. Positive values are
        // absolute pixels; negative values are "window-content-width plus this
        // value" so -1 fills-to-end-of-line. Must be paired with igPopItemWidth.
        //
        // NOTE: this dll doesn't export igSetNextItemWidth (added upstream in Dear
        // ImGui 1.72; the bundled cimgui.dll is older). Use Push/Pop per widget
        // instead — the effect is identical for one widget.
        [DllImport("cimgui")]
        public static extern void igPushItemWidth(float item_width);

        [DllImport("cimgui")]
        public static extern void igPopItemWidth();

        // Fetch pointers to the current per-frame overlay draw list and the current
        // font. The overlay list is the same list ImGui.GetOverlayDrawList() returns —
        // we grab the raw pointer separately so it can be handed to the wide AddText
        // overload below without going back through the managed wrapper.
        [DllImport("cimgui")]
        public static extern System.IntPtr igGetOverlayDrawList();

        [DllImport("cimgui")]
        public static extern System.IntPtr igGetFont();

        // Extended AddText that lets us specify a font pointer + explicit size in
        // pixels. Managed ImGui.NET 0.4.6 exposes only the short overload which
        // renders at the default font size (13 px), so world-space labels look tiny
        // at any distance. Passing igGetFont() + a bigger size scales the same
        // default font up cleanly without needing a second font baked into the atlas.
        //
        // NOTE: this cimgui build names the extended overload `ImDrawList_AddTextExt`
        // (verified by scanning exports). Newer cimgui rebrands to
        // `ImDrawList_AddText_FontPtr`; if the DLL is ever upgraded, add the new name
        // as an alias with EntryPoint.
        //
        // text_end may be null (IntPtr.Zero) — cimgui treats null as "read to null
        // terminator", so the marshalled null-terminated Ansi string is fine.
        [DllImport("cimgui", EntryPoint = "ImDrawList_AddTextExt", CharSet = CharSet.Ansi)]
        public static extern void ImDrawList_AddText_FontPtr(
            System.IntPtr self,
            System.IntPtr font,
            float font_size,
            ImVec2 pos,
            uint col,
            [MarshalAs(UnmanagedType.LPStr)] string text_begin,
            System.IntPtr text_end,
            float wrap_width,
            System.IntPtr cpu_fine_clip_rect);
    }
}
