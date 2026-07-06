using System.Collections.Generic;
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

        // Sidebar layout persistence. Width in pixels. Section order is a list of section IDs
        // (see SidebarWidget.Section* constants) — empty list means "use default order".
        public float SidebarWidth { get; set; } = 380f;
        public List<string> SidebarSectionOrder { get; set; } = new List<string>();

        // Spawn state markers — vertical colored lines above spawns (see SpawnMarkers).
        public bool ShowPlaceholderMarkers { get; set; } = true;
        public bool ShowDirtyMarkers { get; set; } = true;
        public bool ShowSelectedMarker { get; set; } = true;
    }
}
