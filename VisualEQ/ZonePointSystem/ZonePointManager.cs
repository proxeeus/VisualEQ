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

        // Rows owned by other zones whose target_zone == the currently-loaded zone. Only
        // the heading is meant to be edited via the inspector (that changes which way
        // arriving players face). All other fields render read-only.
        public List<ZonePoint> IncomingPoints { get; } = new List<ZonePoint>();

        // Peer-zone rows for the sandwich detector — every row owned by each destination
        // zone that the current zone can send players to. Keyed by shortname (lowercase).
        // Not rendered directly; only used to check "does A's landing coord in B fall
        // inside any B-owned trigger's fire region?".
        public Dictionary<string, List<TrilogyZonePoint>> PeerZoneRows { get; }
            = new Dictionary<string, List<TrilogyZonePoint>>(System.StringComparer.OrdinalIgnoreCase);

        public ZonePoint Selected { get; private set; }

        public event Action<ZonePoint> ZonePointSelected;

        public int DirtyCount => ZonePoints.Count(zp => zp.IsDirty) + IncomingPoints.Count(zp => zp.IsDirty);

        // Monotonically-decreasing counter for pending-insert temp ids. Real DB ids are
        // always positive AUTO_INCREMENT; using negatives means we can distinguish
        // "not yet persisted" via a single check (Id < 0) and there's no collision risk
        // with existing rows.
        int _nextTempId = -1;
        public int NextTempId() => _nextTempId--;

        public void LoadFromRows(IEnumerable<TrilogyZonePoint> rows)
        {
            ZonePoints.Clear();
            Selected = null;
            foreach (var row in rows.OrderBy(r => r.Id))
                ZonePoints.Add(new ZonePoint(row));
            Console.WriteLine($"[ZonePointManager] Loaded {ZonePoints.Count} zone points.");
        }

        // Sibling loader for cross-zone incoming rows. Sets IsIncoming so the renderer +
        // inspector treat them differently. Selection uses the same slot as regular
        // ZonePoints so only one thing is highlighted at a time.
        public void LoadIncomingFromRows(IEnumerable<TrilogyZonePoint> rows)
        {
            IncomingPoints.Clear();
            foreach (var row in rows.OrderBy(r => r.Id))
            {
                var zp = new ZonePoint(row) { IsIncoming = true };
                IncomingPoints.Add(zp);
            }
            Console.WriteLine($"[ZonePointManager] Loaded {IncomingPoints.Count} incoming zone points.");
        }

        public void Clear()
        {
            ZonePoints.Clear();
            IncomingPoints.Clear();
            PeerZoneRows.Clear();
            Selected = null;
            _nextTempId = -1;
        }

        // Populates PeerZoneRows from a flat list of foreign-zone rows. Grouped by the
        // row's own `zone` field so the detector can look up "all triggers in B" via
        // PeerZoneRows["B"].
        public void LoadPeerRows(IEnumerable<TrilogyZonePoint> rows)
        {
            PeerZoneRows.Clear();
            foreach (var r in rows)
            {
                if (string.IsNullOrEmpty(r.Zone)) continue;
                if (!PeerZoneRows.TryGetValue(r.Zone, out var list))
                {
                    list = new List<TrilogyZonePoint>();
                    PeerZoneRows[r.Zone] = list;
                }
                list.Add(r);
            }
        }

        // Adds a ZonePoint (usually pending-insert) to the list. Silently no-ops on
        // duplicate id so undo/redo of the same insert is idempotent.
        public void Add(ZonePoint zp)
        {
            if (ZonePoints.Any(z => z.Row.Id == zp.Row.Id)) return;
            ZonePoints.Add(zp);
        }

        // Removes by id — used by delete/undo-insert paths. Clears selection if the
        // removed row was selected.
        public void Remove(int id)
        {
            var idx = ZonePoints.FindIndex(z => z.Row.Id == id);
            if (idx < 0) return;
            var wasSelected = ReferenceEquals(ZonePoints[idx], Selected);
            ZonePoints.RemoveAt(idx);
            if (wasSelected) ClearSelection();
        }

        public void Select(int id)
        {
            var zp = ZonePoints.FirstOrDefault(p => p.Row.Id == id)
                  ?? IncomingPoints.FirstOrDefault(p => p.Row.Id == id);
            Selected = zp;
            ZonePointSelected?.Invoke(zp);
        }

        // Enumerates every ZonePoint currently loaded — regular + incoming. Consumers
        // that only care about identity (find-by-id) should use this so incoming rows
        // participate in the same lookup path as owned ones.
        public IEnumerable<ZonePoint> AllPoints()
        {
            foreach (var p in ZonePoints) yield return p;
            foreach (var p in IncomingPoints) yield return p;
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
