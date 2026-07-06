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

        // Root of the user's EverQuest install (used by the in-app converter). Replaces eq_config.txt.
        public string EqInstallPath { get; set; } = @"C:\Program Files (x86)\EverQuest";
    }
}
