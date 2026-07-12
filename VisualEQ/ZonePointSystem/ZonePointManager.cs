using System;
using System.Collections.Generic;
using System.Linq;
using VisualEQ.Database.Models;

namespace VisualEQ.ZonePointSystem
{
    // Owns the current zone's ZonePoint list and current selection. Same shape as
    // SpawnManager but simpler — no model loading, no batching, no placeholders.
    public class ZonePointManager
    {
        public List<ZonePoint> ZonePoints { get; } = new List<ZonePoint>();
        public ZonePoint Selected { get; private set; }

        public event Action<ZonePoint> ZonePointSelected;

        public int DirtyCount => ZonePoints.Count(zp => zp.IsDirty);

        public void LoadFromRows(IEnumerable<TrilogyZonePoint> rows)
        {
            ZonePoints.Clear();
            Selected = null;
            foreach (var row in rows.OrderBy(r => r.Id))
                ZonePoints.Add(new ZonePoint(row));
            Console.WriteLine($"[ZonePointManager] Loaded {ZonePoints.Count} zone points.");
        }

        public void Clear()
        {
            ZonePoints.Clear();
            Selected = null;
        }

        public void Select(int id)
        {
            var zp = ZonePoints.FirstOrDefault(p => p.Row.Id == id);
            Selected = zp;
            ZonePointSelected?.Invoke(zp);
        }

        public void Select(ZonePoint zp)
        {
            Selected = zp;
            ZonePointSelected?.Invoke(zp);
        }

        public void ClearSelection()
        {
            if (Selected == null) return;
            Selected = null;
            ZonePointSelected?.Invoke(null);
        }
    }
}
