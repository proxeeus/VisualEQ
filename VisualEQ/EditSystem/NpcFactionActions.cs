using System;
using System.Collections.Generic;
using System.Globalization;

namespace VisualEQ.EditSystem
{
    // Slice 7b — narrow catalog for the npc_faction row editor. Three columns
    // exposed in the sidebar: name, primaryfaction (fk to faction_list),
    // ignore_primary_assist (bool as tinyint). Mirrors LootTableFieldCatalog.
    public enum NpcFactionFieldKind { String, Int }

    public static class NpcFactionFieldCatalog
    {
        public sealed class FieldDef
        {
            public string ColumnName;
            public NpcFactionFieldKind Kind;
            public FieldDef(string col, NpcFactionFieldKind kind) { ColumnName = col; Kind = kind; }
        }

        static readonly Dictionary<string, FieldDef> _fields = new Dictionary<string, FieldDef>
        {
            { "name",                  new FieldDef("name",                  NpcFactionFieldKind.String) },
            { "primaryfaction",        new FieldDef("primaryfaction",        NpcFactionFieldKind.Int) },
            { "ignore_primary_assist", new FieldDef("ignore_primary_assist", NpcFactionFieldKind.Int) },
        };

        public static FieldDef Get(string field)
            => field != null && _fields.TryGetValue(field, out var def) ? def : null;

        public static string Stringify(object v)
        {
            if (v == null) return null;
            switch (v)
            {
                case string s: return s;
                case int i:    return i.ToString(CultureInfo.InvariantCulture);
                case sbyte sb: return sb.ToString(CultureInfo.InvariantCulture);
                case byte b:   return b.ToString(CultureInfo.InvariantCulture);
                case bool bo:  return bo ? "1" : "0";
                default:       return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        public static object Parse(string s, NpcFactionFieldKind kind)
        {
            if (s == null) throw new InvalidOperationException("null value for non-nullable npc_faction field");
            switch (kind)
            {
                case NpcFactionFieldKind.String: return s;
                case NpcFactionFieldKind.Int:    return int.Parse(s, CultureInfo.InvariantCulture);
                default: throw new InvalidOperationException($"unknown kind {kind}");
            }
        }

        public static bool ValuesEqual(string a, string b, NpcFactionFieldKind kind)
        {
            if (kind == NpcFactionFieldKind.Int)
                return int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ai)
                    && int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bi)
                    && ai == bi;
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }

    // Slice 7b — one undoable field edit on an npc_faction row. Mirrors
    // LootTableFieldEditAction — sparse storage via NpcFactionRowEdit, walk-
    // back cleanup, dynamic UPDATE SET at commit.
    public sealed class NpcFactionFieldEditAction : IEditAction
    {
        public int NpcFactionId { get; }
        public string FieldName { get; }
        public object FromValue { get; }
        public object ToValue { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Edited {FieldName} on faction set '{DisplayName}' (#{NpcFactionId})";
        public string TargetKey   => $"npcfacrow:{NpcFactionId}:{FieldName}";

        public NpcFactionFieldEditAction(int npcFactionId, string fieldName, object fromValue, object toValue, string displayName)
        {
            if (NpcFactionFieldCatalog.Get(fieldName) == null)
                throw new ArgumentException($"Unknown npc_faction field '{fieldName}'", nameof(fieldName));
            NpcFactionId = npcFactionId;
            FieldName    = fieldName;
            FromValue    = fromValue;
            ToValue      = toValue;
            DisplayName  = displayName ?? "?";
            Timestamp    = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => WriteToBuffer(controller, ToValue);
        public void Revert(Controller controller) => WriteToBuffer(controller, FromValue);

        void WriteToBuffer(Controller controller, object value)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            var def = NpcFactionFieldCatalog.Get(FieldName);
            if (def == null) return;

            var stringified = NpcFactionFieldCatalog.Stringify(value);
            var originalStr = NpcFactionFieldCatalog.Stringify(FromValue);

            if (!buffer.NpcFactions.TryGetValue(NpcFactionId, out var edit))
            {
                edit = new NpcFactionRowEdit
                {
                    NpcFactionId = NpcFactionId,
                    DisplayName  = DisplayName,
                };
                buffer.NpcFactions[NpcFactionId] = edit;
            }
            if (!edit.OriginalValues.ContainsKey(FieldName))
                edit.OriginalValues[FieldName] = originalStr;

            edit.CurrentValues[FieldName] = stringified;
            edit.LastModifiedAt           = DateTime.UtcNow;

            if (edit.OriginalValues.TryGetValue(FieldName, out var origStored) &&
                NpcFactionFieldCatalog.ValuesEqual(origStored, stringified, def.Kind))
            {
                edit.OriginalValues.Remove(FieldName);
                edit.CurrentValues.Remove(FieldName);
                if (edit.CurrentValues.Count == 0)
                    buffer.NpcFactions.Remove(NpcFactionId);
            }

            controller.MarkBufferDirty();
        }
    }

    // One undoable operation on npc_faction_entries. Insert / Update / Delete are
    // encoded via the (from, to) pair — same shape NpcFieldEditAction uses:
    //   from == null && to != null → user added an entry
    //   from != null && to != null → user changed an entry's value/npc_value/temp
    //   from != null && to == null → user removed an entry
    //
    // Undo/redo work because Apply(controller) writes `to` into the buffer op and
    // Revert(controller) writes `from` — the walk-back cleanup drops the op from
    // buffer.NpcFactionEntries when the current state matches the original.
    public sealed class NpcFactionEntryEditAction : IEditAction
    {
        public int NpcFactionId { get; }
        public int FactionId    { get; }
        public NpcFactionEntrySnapshot FromValue { get; }
        public NpcFactionEntrySnapshot ToValue   { get; }
        public string FactionName { get; }
        public DateTime Timestamp { get; }

        public string Description => Describe();
        public string TargetKey   => $"npcfac:{NpcFactionId}:{FactionId}";

        public NpcFactionEntryEditAction(int npcFactionId, int factionId,
            NpcFactionEntrySnapshot from, NpcFactionEntrySnapshot to, string factionName)
        {
            NpcFactionId = npcFactionId;
            FactionId    = factionId;
            FromValue    = from;
            ToValue      = to;
            FactionName  = factionName ?? "?";
            Timestamp    = DateTime.UtcNow;
        }

        string Describe()
        {
            if (FromValue == null && ToValue != null) return $"Added faction '{FactionName}' to set #{NpcFactionId}";
            if (FromValue != null && ToValue == null) return $"Removed faction '{FactionName}' from set #{NpcFactionId}";
            return $"Edited faction '{FactionName}' in set #{NpcFactionId}";
        }

        public void Apply(Controller controller)  => WriteToBuffer(controller, ToValue);
        public void Revert(Controller controller) => WriteToBuffer(controller, FromValue);

        void WriteToBuffer(Controller controller, NpcFactionEntrySnapshot target)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            var key = EditBuffer.NpcFactionEntryKey(NpcFactionId, FactionId);
            // Compute the "original" for this key by consulting the existing op, if
            // any. First-touch: FromValue is the DB baseline; snapshot it so revert
            // paths can restore it later. Subsequent touches: the recorded op's
            // Original stays the true DB baseline; only Current changes.
            if (!buffer.NpcFactionEntries.TryGetValue(key, out var op))
            {
                op = new NpcFactionEntryOp
                {
                    NpcFactionId = NpcFactionId,
                    FactionId    = FactionId,
                    Original     = FromValue,
                    FactionName  = FactionName,
                };
                buffer.NpcFactionEntries[key] = op;
            }
            else if (string.IsNullOrEmpty(op.FactionName))
            {
                op.FactionName = FactionName;
            }

            op.Current = target;
            op.LastModifiedAt = DateTime.UtcNow;

            // Walk-back cleanup: if Current now matches Original, the user has
            // reverted their edit and the op is a no-op. Drop it so commit doesn't
            // emit a wasteful UPDATE and undo stops at the pre-edit state.
            if (SnapshotEquals(op.Original, op.Current))
                buffer.NpcFactionEntries.Remove(key);

            controller.MarkBufferDirty();
        }

        static bool SnapshotEquals(NpcFactionEntrySnapshot a, NpcFactionEntrySnapshot b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Value == b.Value && a.NpcValue == b.NpcValue && a.Temp == b.Temp;
        }
    }
}
