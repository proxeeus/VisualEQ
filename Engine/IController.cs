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
    }
}
