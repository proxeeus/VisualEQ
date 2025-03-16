namespace VisualEQ.Database.Models
{
    public class NpcType
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public int Level { get; set; }
        public int Race { get; set; }
        public int Class { get; set; }
        public int BodyType { get; set; }
        public float Size { get; set; }
        public int Gender { get; set; }
        public int Texture { get; set; }
        public int HelmTexture { get; set; }
        public int Face { get; set; }
        public int HairColor { get; set; }
        public int HairStyle { get; set; }
        public int BeardColor { get; set; }
        public int BeardStyle { get; set; }
        public int EyeColor1 { get; set; }
        public int EyeColor2 { get; set; }
    }
} 