using System.Numerics;
using VisualEQ.Database.Models;
using VisualEQ.Engine;

namespace VisualEQ.SpawnSystem
{
    public class SpawnPoint
    {
        public SpawnRecord Record { get; }
        public AniModelInstance Model { get; }
        public bool IsPlaceholder { get; }

        public bool IsDirty { get; private set; }

        public Vector3 OriginalPosition { get; }
        public float OriginalHeading { get; }
        public float CurrentHeading { get; private set; }

        public SpawnPoint(SpawnRecord record, AniModelInstance model, bool isPlaceholder)
        {
            Record = record;
            Model = model;
            IsPlaceholder = isPlaceholder;
            OriginalPosition = model.Position;
            OriginalHeading = record.Spawn.Heading;
            CurrentHeading = record.Spawn.Heading;
        }

        public void MarkMoved(Vector3 newPos, float heading)
        {
            Model.Position = newPos;
            CurrentHeading = heading;
            IsDirty = true;
        }

        public void Revert()
        {
            Model.Position = OriginalPosition;
            CurrentHeading = OriginalHeading;
            IsDirty = false;
        }
    }
}
