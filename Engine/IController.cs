using System.Collections.Generic;
using VisualEQ.Engine;

namespace VisualEQ.Engine
{
    /// <summary>
    /// Interface for the controller class to avoid circular dependencies
    /// </summary>
    public interface IController
    {
        // Common methods and properties needed by the Engine
        object ModelSelector { get; }
        IReadOnlyList<AniModelInstance> GetCharacterModels();

        // Called by EngineCore when the user hits the "return to menu" hotkey (F10).
        // Implementations may prompt the user if there are unsaved changes; use this rather
        // than ClearCurrentZone from the hotkey path so the prompt can appear.
        void RequestClearCurrentZone();

        // Force-clear without any prompt. Callers responsible for confirming with the user first.
        void ClearCurrentZone();

        // Called by EngineCore when the user hits the edit-mode toggle hotkey (E).
        void ToggleEditMode();

        // Undo/redo — called by EngineCore for Ctrl+Z / Ctrl+Y hotkeys. Return true if
        // there was history to consume (for future UI feedback like a toast).
        bool TryUndo();
        bool TryRedo();

        // Wired to Escape — clears any current model + waypoint selection.
        void ClearSelection();

        // Wired to F — camera flies to the currently-selected spawn (wall-aware placement).
        // No-op when nothing is selected.
        void FrameSelection();
    }
}
