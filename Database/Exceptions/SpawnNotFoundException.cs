using System;

namespace VisualEQ.Database.Exceptions
{
    public class SpawnNotFoundException : Exception
    {
        public int SpawnId { get; }

        public SpawnNotFoundException(int spawnId) 
            : base($"Spawn with ID {spawnId} was not found")
        {
            SpawnId = spawnId;
        }
    }
} 