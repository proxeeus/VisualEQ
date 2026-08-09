using System;

namespace VisualEQ.EditSystem
{
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
                };
                buffer.NpcFactionEntries[key] = op;
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
