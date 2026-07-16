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

        // ─── Drag-to-create pipeline hooks ───────────────────────────────────────────
        // When true, EngineCore intercepts mouse input BEFORE the normal selector chain
        // (zone-point / waypoint / spawn) so left-click-drag can draw a preview + commit
        // a new zone_point on release. False → normal mouse flow.
        bool IsCreationActive { get; }

        // groundHit is the mouse ray's intersection with the ground plane (Z from the
        // collider probe, or a fallback if no hit).
        void OnCreationMouseDown(System.Numerics.Vector3 groundHit);
        void OnCreationMouseMove(System.Numerics.Vector3 groundHit);
        void OnCreationMouseUp();

        // Wired to Escape when creation is active — abandons the drag without commit.
        void CancelCreation();

        // ─── Grid Mode ───────────────────────────────────────────────────────────────
        // Sub-mode of Edit Mode. When active, LMB double-click on a collision surface
        // places a waypoint: appends to the selected grid, or creates a new grid + first
        // waypoint if none is selected. Controller must auto-exit when EditModeEnabled
        // flips false so a stale Grid Mode doesn't survive a mode toggle.
        bool GridModeActive { get; }

        // Called by EngineCore when a valid LMB double-click lands on collision geometry
        // while GridModeActive == true. hitPoint is scene-space (X/Y swapped from DB).
        void OnGridModeDoubleClick(System.Numerics.Vector3 hitPoint);

        // Wired to Escape — exits Grid Mode without placing anything. Runs before the
        // other Escape cancels (creation drag, waypoint drag, etc.).
        void ExitGridMode();
    }
}
