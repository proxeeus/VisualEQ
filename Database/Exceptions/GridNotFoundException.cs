using System;

namespace VisualEQ.Database.Exceptions
{
    public class GridNotFoundException : Exception
    {
        public int GridId { get; }

        public GridNotFoundException(int gridId) 
            : base($"Grid with ID {gridId} was not found")
        {
            GridId = gridId;
        }
    }
} 