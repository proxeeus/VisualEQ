using System.Numerics;

namespace VisualEQ.Engine
{
    // Runtime representation of one water/lava/slime region loaded from the zone's OES
    // `regn` chunks. AABBs are DB-space (X=east/west, Y=north/south, Z=up) — set at
    // convert time from raw WLD vertex positions. Query via EngineCore.TryGetLiquidSurfaceZAt.
    //
    // Kind values mirror OESRegion.KindWater/KindLava/KindSlime — kept as byte constants
    // rather than an enum here to avoid pulling Common.OES types into every Engine caller.
    public class LiquidRegion
    {
        public const byte KindWater = 0;
        public const byte KindLava  = 1;
        public const byte KindSlime = 2;

        public readonly string Name;
        public readonly byte Kind;
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public LiquidRegion(string name, byte kind, Vector3 min, Vector3 max)
        {
            Name = name;
            Kind = kind;
            Min  = min;
            Max  = max;
        }
    }
}
