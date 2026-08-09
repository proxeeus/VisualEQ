using System;
using System.Linq;

namespace VisualEQ.EditSystem
{
    // One undoable field edit on an npc_types row. Field name is the SQL column identifier
    // (looked up in NpcFieldCatalog); values are boxed primitives handled via the catalog's
    // Stringify / Parse helpers so the same class covers ints, floats, longs, and strings
    // without a separate action type per field.
    //
    // Multiple spawn2 rows may reference the same npc_types row (that's the whole point of
    // the shared record + Ctrl-D-clone flow). Since this action mutates only the DB row,
    // scene-side redraw of every affected SpawnPoint happens implicitly the next time the
    // sidebar re-fetches the NpcTypeFull cache after commit.
    public sealed class NpcFieldEditAction : IEditAction
    {
        public int NpcId { get; }
        public string FieldName { get; }
        public object FromValue { get; }
        public object ToValue { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Edited {FieldName} on '{DisplayName}' (npc #{NpcId})";
        public string TargetKey   => $"npc:{NpcId}:{FieldName}";

        public NpcFieldEditAction(int npcId, string fieldName, object fromValue, object toValue, string displayName)
        {
            if (NpcFieldCatalog.Get(fieldName) == null)
                throw new ArgumentException($"Unknown NPC field '{fieldName}'", nameof(fieldName));
            NpcId       = npcId;
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

            var def = NpcFieldCatalog.Get(FieldName);
            if (def == null) return;

            var stringified = NpcFieldCatalog.StringifyValue(value);
            var originalStr = NpcFieldCatalog.StringifyValue(FromValue);

            // Seed the NpcEdit on first touch. OriginalValues per-field is populated the
            // first time THIS field is edited so subsequent edits keep the true baseline
            // even if the user oscillates the value through several intermediate states.
            if (!buffer.Npcs.TryGetValue(NpcId, out var edit))
            {
                edit = new NpcEdit
                {
                    NpcId       = NpcId,
                    DisplayName = DisplayName,
                };
                buffer.Npcs[NpcId] = edit;
            }
            if (!edit.OriginalValues.ContainsKey(FieldName))
                edit.OriginalValues[FieldName] = originalStr;

            edit.CurrentValues[FieldName] = stringified;
            edit.LastModifiedAt           = DateTime.UtcNow;

            // Walk-back-to-original cleanup: if the current value now matches the original,
            // drop the per-field entries so a Commit doesn't emit a no-op UPDATE. If the
            // whole NpcEdit ends up empty, drop it from buffer.Npcs entirely.
            if (edit.OriginalValues.TryGetValue(FieldName, out var origStored) &&
                NpcFieldCatalog.ValuesEqual(origStored, stringified, def.Kind))
            {
                edit.OriginalValues.Remove(FieldName);
                edit.CurrentValues.Remove(FieldName);
                if (edit.CurrentValues.Count == 0)
                    buffer.Npcs.Remove(NpcId);
            }

            controller.MarkBufferDirty();
        }
    }
}
