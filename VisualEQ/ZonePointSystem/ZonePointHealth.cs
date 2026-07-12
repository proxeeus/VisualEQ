namespace VisualEQ.ZonePointSystem
{
    // Row-health classification driving both the volume tint and the sidebar badge.
    // Priority order when a row qualifies for multiple: Red > Purple > Yellow > Green.
    public enum ZonePointHealth
    {
        // source real, target real — normal working trigger
        Green,

        // source real, target has any wildcard (999999) — outdoor seamless-transition pattern
        Yellow,

        // source uses a legitimate wildcard (|x| or |y| >= 999998) — fall-through or
        // outdoor-edge trigger. Distinct from Red so users don't try to "fix" it.
        Purple,

        // source is (0, 0, 0) — the row cannot fire in-game until fixed
        Red,
    }

    public static class ZonePointWildcards
    {
        // Server accepts |coord| >= 999998 as the wildcard threshold; either 999999 or
        // -999999 works. UI writes 999999 (positive) when toggling a wildcard on.
        public const float Threshold = 999998f;
        public const float Sentinel  = 999999f;

        public static bool IsWildcard(float value) => value >= Threshold || value <= -Threshold;
    }
}
