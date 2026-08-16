using System;

namespace VisualEQ.EditSystem
{
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
