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
        void ClearCurrentZone();
    }
}
