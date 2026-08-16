using System;
using System.Collections.Generic;
using System.Linq;
using VisualEQ.Database.Models;
using VisualEQ.SpawnSystem;

namespace VisualEQ.EditSystem
{
    // Add / remove / re-weight a single spawnentry row inside an already-loaded
    // spawngroup. Three actions share this file because they cooperate through
    // buffer.SpawnEntries (Original/Current null-encoding) and through
    // SpawnPoint.Record.Entries (the in-memory list the sidebar cycler + world
    // nameplate renderer read from).
    //
    // Persisted vs pending-insert spawngroups:
    //   * Persisted (Spawn.Id > 0, SpawnGroupId > 0) — ops land in
    //     buffer.SpawnEntries keyed on (spawngroupID, npcID). Commit path
    //     runs DELETE → INSERT → UPDATE.
    //   * Pending-insert (Spawn.Id < 0, SpawnGroupId = 0 until commit) — ops
    //     mutate SpawnInsert.Entries in place. The SpawnInsert commit chain
    //     (spawngroup → spawnentries → spawn2) already writes those entries
    //     as a single unit, so a parallel SpawnEntries op keyed on a stale
    //     0 would either NRE the WHERE or (worse) collide with a legit
    //     spawngroupID = 0 spawnentry once one exists.

    // Add a new spawnentry row. NpcType comes from the sidebar's NPC picker
    // (SearchNpcTypesAsync return shape); we splice it straight into
    // Record.Entries so the cycler shows the variant on the next frame with
    // no round-trip.
    public sealed class SpawnEntryInsertAction : IEditAction
    {
        public int SpawnId { get; }
        public int NpcId { get; }
        public int Chance { get; }
        public string NpcName { get; }
        public DateTime Timestamp { get; }

        // Cloned at ctor time so Revert can restore the exact entry we removed,
        // even if the Npc object mutates in-cache later (it doesn't today, but
        // the safety is cheap).
        readonly SpawnEntryWithNpc _entrySnapshot;

        public string Description => $"Added '{NpcName}' to spawngroup";
        public string TargetKey   => $"spawnentry:{SpawnId}:{NpcId}";

        public SpawnEntryInsertAction(SpawnPoint sp, NpcType npc, int chance)
        {
            SpawnId   = sp.Record.Spawn.Id;
            NpcId     = npc.Id;
            Chance    = chance;
            NpcName   = npc.Name ?? "?";
            Timestamp = DateTime.UtcNow;
            _entrySnapshot = new SpawnEntryWithNpc
            {
                Entry = new SpawnEntry
                {
                    SpawnGroupId = sp.Record.Spawn.SpawnGroupId,
                    NpcId        = npc.Id,
                    Chance       = chance,
                },
                Npc = npc,
            };
        }

        public void Apply(Controller controller)
        {
            var sp = FindSpawn(controller);
            if (sp == null) return;
            if (sp.Record.Entries == null) sp.Record.Entries = new List<SpawnEntryWithNpc>();

            // Idempotent: skip if the entry is already present (Ctrl+Y after Ctrl+Z, or
            // ApplyPendingBuffer replay after session recovery would double-add otherwise).
            if (sp.Record.Entries.Any(e => e?.Entry?.NpcId == NpcId)) return;

            sp.Record.Entries.Add(_entrySnapshot);

            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            // Pending-insert spawngroup: extend SpawnInsert.Entries. spawngroupID isn't
            // resolved until commit, so a buffer.SpawnEntries entry keyed on 0 would be
            // ambiguous — and the commit path for SpawnInserts already writes its
            // Entries list as part of the three-step INSERT chain.
            if (buffer.SpawnInserts.TryGetValue(SpawnId, out var ins))
            {
                if (!ins.Entries.Any(e => e.NpcId == NpcId))
                    ins.Entries.Add(new SpawnInsertEntry { NpcId = NpcId, Chance = Chance });
                controller.MarkBufferDirty();
                return;
            }

            // Persisted spawngroup: SpawnEntryOp with Original=null → INSERT at commit.
            var key = EditBuffer.SpawnEntryKey(sp.Record.Spawn.SpawnGroupId, NpcId);
            buffer.SpawnEntries[key] = new SpawnEntryOp
            {
                SpawnGroupId   = sp.Record.Spawn.SpawnGroupId,
                NpcId          = NpcId,
                Original       = null,
                Current        = new SpawnEntrySnapshot { Chance = Chance },
                NpcName        = NpcName,
                LastModifiedAt = DateTime.UtcNow,
            };
            controller.MarkBufferDirty();
        }

        public void Revert(Controller controller)
        {
            var sp = FindSpawn(controller);
            if (sp == null) return;

            if (sp.Record.Entries != null)
                sp.Record.Entries.RemoveAll(e => e?.Entry?.NpcId == NpcId);

            // Clamp FocusedEntryIndex so the cycler doesn't dangle off the end.
            var count = sp.Record.Entries?.Count ?? 0;
            if (sp.FocusedEntryIndex >= count)
                sp.FocusedEntryIndex = Math.Max(0, count - 1);

            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (buffer.SpawnInserts.TryGetValue(SpawnId, out var ins))
            {
                ins.Entries.RemoveAll(e => e.NpcId == NpcId);
                controller.MarkBufferDirty();
                return;
            }

            var key = EditBuffer.SpawnEntryKey(sp.Record.Spawn.SpawnGroupId, NpcId);
            buffer.SpawnEntries.Remove(key);
            controller.MarkBufferDirty();
        }

        SpawnPoint FindSpawn(Controller controller) =>
            controller.SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);
    }

    // Remove a spawnentry row. Prior buffer state is snapshotted at ctor so
    // Revert can restore an in-progress chance-edit that the delete replaced.
    public sealed class SpawnEntryDeleteAction : IEditAction
    {
        public int SpawnId { get; }
        public int NpcId { get; }
        public string NpcName { get; }
        public DateTime Timestamp { get; }

        readonly SpawnEntryWithNpc _entrySnapshot;
        readonly int _originalIndex;
        readonly SpawnEntryOp _priorBufferOp; // nullable — captured from buffer at ctor time

        public string Description => $"Removed '{NpcName}' from spawngroup";
        public string TargetKey   => $"spawnentry:{SpawnId}:{NpcId}";

        public SpawnEntryDeleteAction(SpawnPoint sp, SpawnEntryWithNpc entry, int originalIndex, SpawnEntryOp priorBufferOp)
        {
            SpawnId        = sp.Record.Spawn.Id;
            NpcId          = entry?.Entry?.NpcId ?? 0;
            NpcName        = entry?.Npc?.Name ?? "?";
            Timestamp      = DateTime.UtcNow;
            _entrySnapshot = CloneEntry(entry);
            _originalIndex = originalIndex;
            _priorBufferOp = CloneOp(priorBufferOp);
        }

        public void Apply(Controller controller)
        {
            var sp = FindSpawn(controller);
            if (sp == null || sp.Record.Entries == null) return;

            var removed = sp.Record.Entries.RemoveAll(e => e?.Entry?.NpcId == NpcId);
            if (removed == 0) return; // already gone — idempotent

            // Cycler clamp so a delete of the focused entry doesn't dangle.
            if (sp.FocusedEntryIndex >= sp.Record.Entries.Count)
                sp.FocusedEntryIndex = Math.Max(0, sp.Record.Entries.Count - 1);

            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (buffer.SpawnInserts.TryGetValue(SpawnId, out var ins))
            {
                ins.Entries.RemoveAll(e => e.NpcId == NpcId);
                controller.MarkBufferDirty();
                return;
            }

            var key = EditBuffer.SpawnEntryKey(sp.Record.Spawn.SpawnGroupId, NpcId);

            // If the prior op was a pending INSERT (Original==null, Current!=null),
            // the "delete" just cancels the pending insert — no DB write. Drop it.
            if (_priorBufferOp != null && _priorBufferOp.Original == null && _priorBufferOp.Current != null)
            {
                buffer.SpawnEntries.Remove(key);
                controller.MarkBufferDirty();
                return;
            }

            // Otherwise write a delete op. Baseline chance comes from an existing
            // Original (if a chance-edit op was already in flight) or from the entry
            // snapshot (first-touch delete).
            var origChance = _priorBufferOp?.Original?.Chance ?? _entrySnapshot?.Entry?.Chance ?? 0;
            buffer.SpawnEntries[key] = new SpawnEntryOp
            {
                SpawnGroupId   = sp.Record.Spawn.SpawnGroupId,
                NpcId          = NpcId,
                Original       = new SpawnEntrySnapshot { Chance = origChance },
                Current        = null,
                NpcName        = NpcName,
                LastModifiedAt = DateTime.UtcNow,
            };
            controller.MarkBufferDirty();
        }

        public void Revert(Controller controller)
        {
            var sp = FindSpawn(controller);
            if (sp == null || _entrySnapshot == null) return;

            if (sp.Record.Entries == null) sp.Record.Entries = new List<SpawnEntryWithNpc>();

            // Idempotent: if the entry is already restored, skip.
            if (sp.Record.Entries.Any(e => e?.Entry?.NpcId == NpcId)) return;

            var idx = Math.Min(Math.Max(0, _originalIndex), sp.Record.Entries.Count);
            sp.Record.Entries.Insert(idx, _entrySnapshot);

            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (buffer.SpawnInserts.TryGetValue(SpawnId, out var ins))
            {
                if (!ins.Entries.Any(e => e.NpcId == NpcId))
                {
                    var insIdx = Math.Min(idx, ins.Entries.Count);
                    ins.Entries.Insert(insIdx, new SpawnInsertEntry
                    {
                        NpcId  = NpcId,
                        Chance = _entrySnapshot.Entry?.Chance ?? 0,
                    });
                }
                controller.MarkBufferDirty();
                return;
            }

            var key = EditBuffer.SpawnEntryKey(sp.Record.Spawn.SpawnGroupId, NpcId);
            if (_priorBufferOp != null)
                buffer.SpawnEntries[key] = CloneOp(_priorBufferOp);
            else
                buffer.SpawnEntries.Remove(key);
            controller.MarkBufferDirty();
        }

        SpawnPoint FindSpawn(Controller controller) =>
            controller.SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);

        static SpawnEntryWithNpc CloneEntry(SpawnEntryWithNpc src)
        {
            if (src == null) return null;
            return new SpawnEntryWithNpc
            {
                Entry = src.Entry == null ? null : new SpawnEntry
                {
                    SpawnGroupId = src.Entry.SpawnGroupId,
                    NpcId        = src.Entry.NpcId,
                    Chance       = src.Entry.Chance,
                },
                Npc = src.Npc, // shared — the NpcType cache entry is read-only for rendering
            };
        }

        static SpawnEntryOp CloneOp(SpawnEntryOp src)
        {
            if (src == null) return null;
            return new SpawnEntryOp
            {
                SpawnGroupId   = src.SpawnGroupId,
                NpcId          = src.NpcId,
                Original       = src.Original == null ? null : new SpawnEntrySnapshot { Chance = src.Original.Chance },
                Current        = src.Current  == null ? null : new SpawnEntrySnapshot { Chance = src.Current.Chance  },
                NpcName        = src.NpcName,
                LastModifiedAt = src.LastModifiedAt,
            };
        }
    }

    // Change the chance (weight) of one spawnentry row. Chance is a relative
    // weight in the server-side pick (spawngroup.cpp: totalchance = sum; roll =
    // random(0, totalchance-1)) — "50/50" and "1/1" produce the same 50/50
    // split. The sidebar surfaces the sum so users can reason about effective %.
    public sealed class SpawnEntryChanceEditAction : IEditAction
    {
        public int SpawnId { get; }
        public int NpcId { get; }
        public int FromChance { get; }
        public int ToChance { get; }
        public string NpcName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Changed '{NpcName}' weight {FromChance}→{ToChance}";
        public string TargetKey   => $"spawnentry:{SpawnId}:{NpcId}";

        public SpawnEntryChanceEditAction(SpawnPoint sp, int npcId, string npcName, int fromChance, int toChance)
        {
            SpawnId    = sp.Record.Spawn.Id;
            NpcId      = npcId;
            FromChance = fromChance;
            ToChance   = toChance;
            NpcName    = npcName ?? "?";
            Timestamp  = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => SetChance(controller, ToChance);
        public void Revert(Controller controller) => SetChance(controller, FromChance);

        void SetChance(Controller controller, int chance)
        {
            var sp = controller.SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);
            if (sp == null || sp.Record.Entries == null) return;

            var entry = sp.Record.Entries.FirstOrDefault(e => e?.Entry?.NpcId == NpcId);
            if (entry?.Entry == null) return;

            entry.Entry.Chance = chance;

            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            // Pending-insert spawngroup: mutate SpawnInsert.Entries in place.
            if (buffer.SpawnInserts.TryGetValue(SpawnId, out var ins))
            {
                var ie = ins.Entries.FirstOrDefault(e => e.NpcId == NpcId);
                if (ie != null) ie.Chance = chance;
                controller.MarkBufferDirty();
                return;
            }

            var key = EditBuffer.SpawnEntryKey(sp.Record.Spawn.SpawnGroupId, NpcId);
            buffer.SpawnEntries.TryGetValue(key, out var prior);

            // If there's an in-progress pending INSERT for this entry, stay in INSERT
            // mode and just update Current. Original stays null → commit still INSERTs
            // the row with the fresh chance.
            if (prior != null && prior.Original == null && prior.Current != null)
            {
                prior.Current = new SpawnEntrySnapshot { Chance = chance };
                prior.LastModifiedAt = DateTime.UtcNow;
                controller.MarkBufferDirty();
                return;
            }

            // Otherwise it's an UPDATE. Preserve the earliest Original across chained
            // edits; fall back to FromChance for first-touch. Walk-back cleanup drops
            // the op entirely if the user ends up back at the baseline.
            var effectiveOriginal = prior?.Original?.Chance ?? FromChance;
            if (chance == effectiveOriginal)
            {
                buffer.SpawnEntries.Remove(key);
            }
            else
            {
                buffer.SpawnEntries[key] = new SpawnEntryOp
                {
                    SpawnGroupId   = sp.Record.Spawn.SpawnGroupId,
                    NpcId          = NpcId,
                    Original       = new SpawnEntrySnapshot { Chance = effectiveOriginal },
                    Current        = new SpawnEntrySnapshot { Chance = chance },
                    NpcName        = NpcName,
                    LastModifiedAt = DateTime.UtcNow,
                };
            }
            controller.MarkBufferDirty();
        }
    }
}
