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
            System.Console.WriteLine(
                $"[SpawnPoint] MarkMoved #{Record.Spawn.Id} pos ({Model.Position.X:F1},{Model.Position.Y:F1},{Model.Position.Z:F1}) " +
                $"→ ({newPos.X:F1},{newPos.Y:F1},{newPos.Z:F1}) heading {CurrentHeading:F0}→{heading:F0}");
            Model.Rotation = SpawnManager.HeadingToRotation(heading);
            Model.Position = newPos;
            CurrentHeading = heading;
            IsDirty = true;
        }

        public void Revert()
        {
            System.Console.WriteLine(
                $"[SpawnPoint] Revert #{Record.Spawn.Id} pos ({Model.Position.X:F1},{Model.Position.Y:F1},{Model.Position.Z:F1}) " +
                $"→ ({OriginalPosition.X:F1},{OriginalPosition.Y:F1},{OriginalPosition.Z:F1}) heading {CurrentHeading:F0}→{OriginalHeading:F0}");
            Model.Rotation = SpawnManager.HeadingToRotation(OriginalHeading);
            Model.Position = OriginalPosition;
            CurrentHeading = OriginalHeading;
            IsDirty = false;
        }
    }
}
