using VisualEQ.Database.Configuration;

namespace VisualEQ.Settings
{
    public class AppSettings
    {
        public DatabaseSettings Database { get; set; } = new DatabaseSettings
        {
            Server = "localhost",
            Port = 3306,
            Database = "",
            Username = "",
            Password = "",
            ConnectionTimeout = 10
        };

        public bool ShowPathGrids { get; set; } = true;
        public bool ShowSpawnList { get; set; } = true;
    }
}
