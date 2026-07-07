using System.Collections.Generic;

namespace VisualEQ.EditSystem
{
    // Bounded, session-only undo/redo history. Cleared on zone unload. Not persisted —
    // rationale in the Phase 5 plan: after restart, "state == what's on disk" and the
    // recent-action list doesn't survive. Per-item revert (5.5) covers the "undo an
    // edit from an older session" case.
    //
    // Backed by List<T> so we can drop the oldest entry when we exceed the cap.
    public class UndoStack
    {
        public const int MaxDepth = 500;

        readonly List<IEditAction> _undo = new List<IEditAction>();
        readonly List<IEditAction> _redo = new List<IEditAction>();

        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;
        public IReadOnlyList<IEditAction> RecentUndo => _undo;

        public void Record(IEditAction action)
        {
            _undo.Add(action);
            _redo.Clear(); // New action invalidates any pending redo history.
            while (_undo.Count > MaxDepth)
                _undo.RemoveAt(0); // Drop oldest.
        }

        // Pop the most recent action, Revert it, push onto redo. Returns the action that
        // was reverted so the caller can log or refresh UI.
        public IEditAction Undo(Controller controller)
        {
            if (_undo.Count == 0) return null;
            var action = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            action.Revert(controller);
            _redo.Add(action);
            return action;
        }

        public IEditAction Redo(Controller controller)
        {
            if (_redo.Count == 0) return null;
            var action = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            action.Apply(controller);
            _undo.Add(action);
            return action;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
