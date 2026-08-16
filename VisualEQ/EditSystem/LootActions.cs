using System;
using System.Collections.Generic;
using System.Globalization;

namespace VisualEQ.EditSystem
{
    // Slice 6c follow-up — narrow catalog for the loottable-header field
    // editor. Only the four columns the sidebar currently exposes; expand
    // when the UI gains more (expansion / content_flags / done).
    public enum LootTableFieldKind { String, Int }

    public static class LootTableFieldCatalog
    {
        public sealed class FieldDef
        {
            public string ColumnName;
            public LootTableFieldKind Kind;
            public FieldDef(string col, LootTableFieldKind kind) { ColumnName = col; Kind = kind; }
        }

        static readonly Dictionary<string, FieldDef> _fields = new Dictionary<string, FieldDef>
        {
            { "name",    new FieldDef("name",    LootTableFieldKind.String) },
            { "mincash", new FieldDef("mincash", LootTableFieldKind.Int) },
            { "maxcash", new FieldDef("maxcash", LootTableFieldKind.Int) },
            { "avgcoin", new FieldDef("avgcoin", LootTableFieldKind.Int) },
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
                default:       return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        public static object Parse(string s, LootTableFieldKind kind)
        {
            if (s == null) throw new InvalidOperationException("null value for non-nullable loottable field");
            switch (kind)
            {
                case LootTableFieldKind.String: return s;
                case LootTableFieldKind.Int:    return int.Parse(s, CultureInfo.InvariantCulture);
                default: throw new InvalidOperationException($"unknown kind {kind}");
            }
        }

        public static bool ValuesEqual(string a, string b, LootTableFieldKind kind)
        {
            if (kind == LootTableFieldKind.Int)
            {
                return int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ai)
                    && int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bi)
                    && ai == bi;
            }
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }

    // Slice 6c follow-up — one undoable field edit on a `loottable` row.
    // Mirrors NpcFieldEditAction: sparse per-field storage via LootTableEdit's
    // OriginalValues / CurrentValues dicts, walk-back cleanup when Current
    // matches Original, dynamic UPDATE SET clause at commit.
    public sealed class LootTableFieldEditAction : IEditAction
    {
        public int LoottableId { get; }
        public string FieldName { get; }
        public object FromValue { get; }
        public object ToValue { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Edited {FieldName} on loottable '{DisplayName}' (#{LoottableId})";
        public string TargetKey   => $"loottable:{LoottableId}:{FieldName}";

        public LootTableFieldEditAction(int loottableId, string fieldName, object fromValue, object toValue, string displayName)
        {
            if (LootTableFieldCatalog.Get(fieldName) == null)
                throw new ArgumentException($"Unknown loottable field '{fieldName}'", nameof(fieldName));
            LoottableId = loottableId;
            FieldName   = fieldName;
            FromValue   = fromValue;
            ToValue     = toValue;
            DisplayName = displayName ?? "?";
            Timestamp   = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => WriteToBuffer(controller, ToValue);
        public void Revert(Controller controller) => WriteToBuffer(controller, FromValue);

        void WriteToBuffer(Controller controller, object value)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            var def = LootTableFieldCatalog.Get(FieldName);
            if (def == null) return;

            var stringified = LootTableFieldCatalog.Stringify(value);
            var originalStr = LootTableFieldCatalog.Stringify(FromValue);

            if (!buffer.LootTables.TryGetValue(LoottableId, out var edit))
            {
                edit = new LootTableEdit
                {
                    LoottableId = LoottableId,
                    DisplayName = DisplayName,
                };
                buffer.LootTables[LoottableId] = edit;
            }
            if (!edit.OriginalValues.ContainsKey(FieldName))
                edit.OriginalValues[FieldName] = originalStr;

            edit.CurrentValues[FieldName] = stringified;
            edit.LastModifiedAt           = DateTime.UtcNow;

            // Walk-back: if Current == Original for this field, drop the entry.
            // If the whole LootTableEdit ends up empty, drop the whole row op.
            if (edit.OriginalValues.TryGetValue(FieldName, out var origStored) &&
                LootTableFieldCatalog.ValuesEqual(origStored, stringified, def.Kind))
            {
                edit.OriginalValues.Remove(FieldName);
                edit.CurrentValues.Remove(FieldName);
                if (edit.CurrentValues.Count == 0)
                    buffer.LootTables.Remove(LoottableId);
            }

            controller.MarkBufferDirty();
        }
    }

    // Slice 6b — undoable operation on loottable_entries. Same encoding as
    // NpcFactionEntryEditAction: (from, to) pair signals insert / update /
    // delete uniformly.
    //   from == null && to != null → user added the lootdrop to the table
    //   from != null && to != null → user changed roll params
    //   from != null && to == null → user removed the lootdrop from the table
    //
    // Undo/redo works because Apply writes `to` into the buffer op and Revert
    // writes `from`; walk-back cleanup drops the op when Current matches
    // Original so committing a reverted edit doesn't emit a wasteful UPDATE.
    public sealed class LootTableEntryEditAction : IEditAction
    {
        public int LoottableId { get; }
        public int LootdropId  { get; }
        public LootTableEntrySnapshot FromValue { get; }
        public LootTableEntrySnapshot ToValue   { get; }
        public string LootdropName { get; }
        public DateTime Timestamp { get; }

        public string Description => Describe();
        public string TargetKey   => $"lte:{LoottableId}:{LootdropId}";

        public LootTableEntryEditAction(int loottableId, int lootdropId,
            LootTableEntrySnapshot from, LootTableEntrySnapshot to, string lootdropName)
        {
            LoottableId  = loottableId;
            LootdropId   = lootdropId;
            FromValue    = from;
            ToValue      = to;
            LootdropName = lootdropName ?? "?";
            Timestamp    = DateTime.UtcNow;
        }

        string Describe()
        {
            if (FromValue == null && ToValue != null) return $"Added lootdrop '{LootdropName}' to loottable #{LoottableId}";
            if (FromValue != null && ToValue == null) return $"Removed lootdrop '{LootdropName}' from loottable #{LoottableId}";
            return $"Edited lootdrop '{LootdropName}' in loottable #{LoottableId}";
        }

        public void Apply(Controller controller)  => WriteToBuffer(controller, ToValue);
        public void Revert(Controller controller) => WriteToBuffer(controller, FromValue);

        void WriteToBuffer(Controller controller, LootTableEntrySnapshot target)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            var key = EditBuffer.LootTableEntryKey(LoottableId, LootdropId);
            if (!buffer.LootTableEntries.TryGetValue(key, out var op))
            {
                op = new LootTableEntryOp
                {
                    LoottableId  = LoottableId,
                    LootdropId   = LootdropId,
                    Original     = FromValue,
                    LootdropName = LootdropName,
                };
                buffer.LootTableEntries[key] = op;
            }
            else if (string.IsNullOrEmpty(op.LootdropName))
            {
                op.LootdropName = LootdropName;
            }

            op.Current = target;
            op.LastModifiedAt = DateTime.UtcNow;

            // Walk-back cleanup: if Current now matches Original, the user has
            // reverted their edit. Drop the op so commit doesn't emit a
            // wasteful UPDATE.
            if (SnapshotEquals(op.Original, op.Current))
                buffer.LootTableEntries.Remove(key);

            controller.MarkBufferDirty();
        }

        static bool SnapshotEquals(LootTableEntrySnapshot a, LootTableEntrySnapshot b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Multiplier  == b.Multiplier
                && a.DropLimit   == b.DropLimit
                && a.MinDrop     == b.MinDrop
                && a.Probability == b.Probability;
        }
    }

    // Slice 6b — undoable operation on lootdrop_entries. Mirrors the
    // LootTableEntryEditAction structure — same insert/update/delete
    // encoding, same walk-back cleanup.
    public sealed class LootDropEntryEditAction : IEditAction
    {
        public int LootdropId { get; }
        public int ItemId     { get; }
        public LootDropEntrySnapshot FromValue { get; }
        public LootDropEntrySnapshot ToValue   { get; }
        public string ItemName { get; }
        public DateTime Timestamp { get; }

        public string Description => Describe();
        public string TargetKey   => $"lde:{LootdropId}:{ItemId}";

        public LootDropEntryEditAction(int lootdropId, int itemId,
            LootDropEntrySnapshot from, LootDropEntrySnapshot to, string itemName)
        {
            LootdropId = lootdropId;
            ItemId     = itemId;
            FromValue  = from;
            ToValue    = to;
            ItemName   = itemName ?? "?";
            Timestamp  = DateTime.UtcNow;
        }

        string Describe()
        {
            if (FromValue == null && ToValue != null) return $"Added item '{ItemName}' to lootdrop #{LootdropId}";
            if (FromValue != null && ToValue == null) return $"Removed item '{ItemName}' from lootdrop #{LootdropId}";
            return $"Edited item '{ItemName}' in lootdrop #{LootdropId}";
        }

        public void Apply(Controller controller)  => WriteToBuffer(controller, ToValue);
        public void Revert(Controller controller) => WriteToBuffer(controller, FromValue);

        void WriteToBuffer(Controller controller, LootDropEntrySnapshot target)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            var key = EditBuffer.LootDropEntryKey(LootdropId, ItemId);
            if (!buffer.LootDropEntries.TryGetValue(key, out var op))
            {
                op = new LootDropEntryOp
                {
                    LootdropId = LootdropId,
                    ItemId     = ItemId,
                    Original   = FromValue,
                    ItemName   = ItemName,
                };
                buffer.LootDropEntries[key] = op;
            }
            else if (string.IsNullOrEmpty(op.ItemName))
            {
                op.ItemName = ItemName;
            }

            op.Current = target;
            op.LastModifiedAt = DateTime.UtcNow;

            if (SnapshotEquals(op.Original, op.Current))
                buffer.LootDropEntries.Remove(key);

            controller.MarkBufferDirty();
        }

        static bool SnapshotEquals(LootDropEntrySnapshot a, LootDropEntrySnapshot b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.ItemCharges     == b.ItemCharges
                && a.EquipItem       == b.EquipItem
                && a.Chance          == b.Chance
                && a.DisabledChance  == b.DisabledChance
                && a.TrivialMinLevel == b.TrivialMinLevel
                && a.TrivialMaxLevel == b.TrivialMaxLevel
                && a.Multiplier      == b.Multiplier
                && a.NpcMinLevel     == b.NpcMinLevel
                && a.NpcMaxLevel     == b.NpcMaxLevel;
        }
    }
}
