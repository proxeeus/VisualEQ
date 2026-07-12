namespace VisualEQ.Database.Models
{
    // One row of trilogy_zone_points — a server-side zone-crossing trigger for the Trilogy
    // (v29c) client. Coordinates are EQ world coords (DB space): X = east/west, Y = north/south,
    // Z = up. Scene code swaps X/Y (see SpawnManager for the same treatment).
    //
    // UseNewZoning modes:
    //   0 → box (axis-aligned, XY square with side = 2 * Zrange, Z = 2 * MaxZDiff — infinite when 0)
    //   1 → X-plane crossing (plane at X = this.X, extends MinVert..MaxVert on Y)
    //   2 → Y-plane crossing (plane at Y = this.Y, extends MinVert..MaxVert on X)
    //
    // Wildcard sentinel: |coord| >= 999998 in a source X/Y means "this axis is ungated";
    // in a target coord it means "preserve player position on that axis" (same effect as keep*=1).
    public class TrilogyZonePoint
    {
        public int Id { get; set; }
        public string Zone { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }

        public string TargetZone { get; set; }
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public float TargetZ { get; set; }

        public int Zrange { get; set; }
        public int MaxZDiff { get; set; }
        public byte UseNewZoning { get; set; }

        public float MinVert { get; set; }
        public float MaxVert { get; set; }
        public float CenterPoint { get; set; }

        public int KeepX { get; set; }
        public int KeepY { get; set; }
        public int KeepZ { get; set; }

        public int ToZoneId { get; set; }
    }
}
